using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Mono.Unix;
using Native = Mono.Unix.Native;

namespace AspNetEphemeralHttpServerPoC.Extensions;

internal static class BinaryReaderExtensions
{
    public static PosixAclXattrEntry ReadPosixAclXattrEntry(this BinaryReader reader)
    {
        return new PosixAclXattrEntry(
            (AclTag)reader.ReadUInt16(),
            (AclPermissions)reader.ReadUInt16(),
            reader.ReadUInt32()
        );
    }

    public static PosixAclXattrHeader ReadPosixAclXattrHeader(this BinaryReader reader)
    {
        return new PosixAclXattrHeader(reader.ReadUInt32());
    }
}

internal static class BinaryWriterExtensions
{
    public static void Write(this BinaryWriter writer, PosixAclXattrEntry value)
    {
        writer.Write((UInt16)value.EntryTag);
        writer.Write((UInt16)value.EntryPermissions);
        writer.Write((UInt32)value.EntryId);
    }

    public static void Write(this BinaryWriter writer, PosixAclXattrHeader value)
    {
        writer.Write((UInt32)value.AclVersion);
    }
}

public static class AclPermissionsExtensions
{
    public static string ShortSummary(this AclPermissions permissions)
    {
        var readChar = permissions.HasFlag(AclPermissions.Read) ? 'r' : '-';
        var writeChar = permissions.HasFlag(AclPermissions.Write) ? 'w' : '-';
        var executeChar = permissions.HasFlag(AclPermissions.Execute) ? 'x' : '-';
        return $"{readChar}{writeChar}{executeChar}";
    }
}

[Flags]
public enum AclTag
{
    UserObject = 1 << 0,
    User = 1 << 1,
    GroupObject = 1 << 2,
    Group = 1 << 3,
    Mask = 1 << 4,
    Other = 1 << 5,
}

[Flags]
public enum AclPermissions
{
    Execute = 1 << 0,  // 0b001
    Write = 1 << 1,  // 0b010
    Read = 1 << 2,  // 0b100
    All =  Execute | Write | Read,
}

public readonly struct PosixAclXattrEntry(AclTag pEntryTag, AclPermissions pEntryPermissions, UInt32 pEntryId)
{
    public const UInt32 AclUndefinedId = UInt32.MaxValue;

    public AclTag EntryTag => pEntryTag;
    public AclPermissions EntryPermissions => pEntryPermissions;
    public UInt32 EntryId => pEntryId;

    public override string ToString()
    {
        switch (EntryTag) {
            case AclTag.UserObject:
                Debug.Assert(EntryId == AclUndefinedId);
                return $"user::{EntryPermissions.ShortSummary()}";
            case AclTag.User:
                return $"user:{new UnixUserInfo(EntryId).UserName}:{EntryPermissions.ShortSummary()}";
            case AclTag.GroupObject:
                Debug.Assert(EntryId == AclUndefinedId);
                return $"group::{EntryPermissions.ShortSummary()}";
            case AclTag.Group:
                return $"group:{new UnixGroupInfo(EntryId).GroupName}:{EntryPermissions.ShortSummary()}";
            case AclTag.Mask:
                Debug.Assert(EntryId == AclUndefinedId);
                return $"mask::{EntryPermissions.ShortSummary()}";
            case AclTag.Other:
                Debug.Assert(EntryId == AclUndefinedId);
                return $"other::{EntryPermissions.ShortSummary()}";
            default:
                throw new ArgumentOutOfRangeException(nameof(EntryTag));
        }
    }
}

readonly struct PosixAclXattrHeader(UInt32 pAclVersion)
{
    public UInt32 AclVersion => pAclVersion;
}

public class PosixAcl
{
    private const UInt32 PosixAclXattrVersion = 2;

    private readonly UnixFileSystemInfo _node;
    private readonly string _xattrName;

    private PosixAclXattrHeader _header;
    private readonly List<PosixAclXattrEntry> _entries;
    public IList<PosixAclXattrEntry> Entries { get; }

    internal PosixAcl(UnixFileSystemInfo node, string xattrName)
    {
        _node = node;
        _xattrName = xattrName;
        _entries = new List<PosixAclXattrEntry>();
        Entries = new ReadOnlyCollection<PosixAclXattrEntry>(_entries);
    }

    public void Refresh()
    {
        _entries.Clear();
        var aclByteBufferLength = Native.Syscall.getxattr(_node.FullName, _xattrName, out var aclByteBuffer);
        if (aclByteBufferLength < 0) return;
        using var aclByteStream = new MemoryStream(aclByteBuffer, false);
        using var aclByteReader = new BinaryReader(aclByteStream);
        _header = aclByteReader.ReadPosixAclXattrHeader();
        Debug.Assert(_header.AclVersion == PosixAclXattrVersion);
        while (aclByteReader.PeekChar() > 0) {
            _entries.Add(aclByteReader.ReadPosixAclXattrEntry());
        }
    }

    public byte[] ToXattrBytes()
    {
        using var aclByteStream = new MemoryStream();
        using var aclByteWriter = new BinaryWriter(aclByteStream);
        aclByteWriter.Write(_header);
        foreach (var entry in _entries)
            aclByteWriter.Write(entry);
        return aclByteStream.ToArray();
    }

    public void Flush()
    {
        var result = Native.Syscall.setxattr(_node.FullName, _xattrName, ToXattrBytes());
        if (result != 0) throw new Exception($"setxattr returned exit code {result}");
    }

    public void ModifyOwnerUser(AclPermissions permissions)
    {
        var maskedPermissions = _node.FileAccessPermissions & (FileAccessPermissions.AllPermissions ^ FileAccessPermissions.UserReadWriteExecute);
        _node.FileAccessPermissions = maskedPermissions | (FileAccessPermissions)((int)permissions << 6);
        Refresh();
    }

    public void ModifyUser(UnixUserInfo user, AclPermissions permissions)
    {
        if (_entries.Count == 0) _entries.AddRange(GetImplicitAcl());

        int matchingEntryIndex = -1;
        int cursor;
        for (cursor = 0; cursor < _entries.Count; ++cursor) {
            var entry = _entries[cursor];
            if (entry.EntryTag < AclTag.User) continue;
            if (entry.EntryTag > AclTag.User) break;
            if (entry.EntryId != user.UserId) continue;
            matchingEntryIndex = cursor;
            break;
        }

        if (matchingEntryIndex == -1) {
            // insert a new ACL entry
            _entries.Insert(cursor, new PosixAclXattrEntry(AclTag.User, permissions, (UInt32)user.UserId));
            Flush();
            return;
        }

        // replace existing ACL entry
        _entries[matchingEntryIndex] = new PosixAclXattrEntry(AclTag.User, permissions, (UInt32)user.UserId);
        Flush();
    }

    public void ModifyOwnerGroup(AclPermissions permissions)
    {
        var maskedPermissions = _node.FileAccessPermissions & (FileAccessPermissions.AllPermissions ^ FileAccessPermissions.GroupReadWriteExecute);
        _node.FileAccessPermissions = maskedPermissions | (FileAccessPermissions)((int)permissions << 3);
        Refresh();
    }

    public void ModifyGroup(UnixGroupInfo group, AclPermissions permissions)
    {
        if (_entries.Count == 0) _entries.AddRange(GetImplicitAcl());

        int matchingEntryIndex = -1;
        int cursor;
        for (cursor = 0; cursor < _entries.Count; ++cursor) {
            var entry = _entries[cursor];
            if (entry.EntryTag < AclTag.Group) continue;
            if (entry.EntryTag > AclTag.Group) break;
            if (entry.EntryId != group.GroupId) continue;
            matchingEntryIndex = cursor;
            break;
        }

        if (matchingEntryIndex == -1) {
            // insert a new ACL entry
            _entries.Insert(cursor, new PosixAclXattrEntry(AclTag.User, permissions, (UInt32)group.GroupId));
            Flush();
            return;
        }

        // replace existing ACL entry
        _entries[matchingEntryIndex] = new PosixAclXattrEntry(AclTag.User, permissions, (UInt32)group.GroupId);
        Flush();
    }

    public void ModifyOther(AclPermissions permissions)
    {
        var maskedPermissions = _node.FileAccessPermissions & (FileAccessPermissions.AllPermissions ^ FileAccessPermissions.OtherReadWriteExecute);
        _node.FileAccessPermissions = maskedPermissions | (FileAccessPermissions)permissions;
        Refresh();
    }

    private IList<PosixAclXattrEntry> GetImplicitAcl()
    {
        var userOwnerPermissions = (AclPermissions)((int)(_node.FileAccessPermissions & FileAccessPermissions.UserReadWriteExecute) >> 6);
        var groupOwnerPermissions = (AclPermissions)((int)(_node.FileAccessPermissions & FileAccessPermissions.GroupReadWriteExecute) >> 3);
        var otherPermissions = (AclPermissions)((int)(_node.FileAccessPermissions & FileAccessPermissions.OtherReadWriteExecute) >> 0);

        return [
            new PosixAclXattrEntry(AclTag.UserObject, userOwnerPermissions, PosixAclXattrEntry.AclUndefinedId),
            new PosixAclXattrEntry(AclTag.GroupObject, AclPermissions.All, PosixAclXattrEntry.AclUndefinedId),
            new PosixAclXattrEntry(AclTag.Mask, groupOwnerPermissions, PosixAclXattrEntry.AclUndefinedId),
            new PosixAclXattrEntry(AclTag.Other, otherPermissions, PosixAclXattrEntry.AclUndefinedId),
        ];
    }
}

public class UnixFileAccessControl
{
    private const string PosixAccessAclXattrName = "system.posix_acl_access";
    private const string PosixDefaultAclXattrName = "system.posix_acl_default";

    public PosixAcl AccessAcl { get; }
    public PosixAcl DefaultAcl { get; }

    public UnixFileAccessControl(UnixFileSystemInfo fileSystemInfo)
    {
        AccessAcl = new PosixAcl(fileSystemInfo, PosixAccessAclXattrName);
        DefaultAcl = new PosixAcl(fileSystemInfo, PosixDefaultAclXattrName);
    }

    public override string ToString()
    {
        return $"access: [{String.Join(", ", AccessAcl)}], default: [{String.Join(", ", DefaultAcl)}]";
    }
}

public static class UnixFileSystemInfoExtensions
{
    public static UnixFileAccessControl GetFileAccessControl(this UnixFileSystemInfo fileSystemInfo)
    {
        return new UnixFileAccessControl(fileSystemInfo);
    }
}
