using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

public class UnixFileAccessControl
{
    private const string PosixAccessAclXattrName = "system.posix_acl_access";
    private const string PosixDefaultAclXattrName = "system.posix_acl_default";
    private const UInt32 PosixAclXattrVersion = 2;

    private UnixFileSystemInfo _fileSystemInfo;
    private readonly List<PosixAclXattrEntry> _accessAcl;
    private readonly List<PosixAclXattrEntry> _defaultAcl;
    public IList<PosixAclXattrEntry> AccessAcl { get; }
    public IList<PosixAclXattrEntry> DefaultAcl { get; }

    public UnixFileAccessControl(UnixFileSystemInfo fileSystemInfo)
    {
        _fileSystemInfo = fileSystemInfo;
        _accessAcl = GetFileAccessControlList(fileSystemInfo.FullName, PosixAccessAclXattrName);
        _defaultAcl = GetFileAccessControlList(fileSystemInfo.FullName, PosixDefaultAclXattrName);
        AccessAcl = new ReadOnlyCollection<PosixAclXattrEntry>(_accessAcl);
        DefaultAcl = new ReadOnlyCollection<PosixAclXattrEntry>(_defaultAcl);
    }

    private IList<PosixAclXattrEntry> GetImplicitAcl()
    {
        var userOwnerPermissions = (AclPermissions)(((int)_fileSystemInfo.FileAccessPermissions >> 6) & 0b111);
        var groupOwnerPermissions = (AclPermissions)(((int)_fileSystemInfo.FileAccessPermissions >> 3) & 0b111);
        var otherPermissions = (AclPermissions)(((int)_fileSystemInfo.FileAccessPermissions >> 0) & 0b111);

        return [
            new PosixAclXattrEntry(AclTag.UserObject, userOwnerPermissions, PosixAclXattrEntry.AclUndefinedId),
            new PosixAclXattrEntry(AclTag.GroupObject, AclPermissions.All, PosixAclXattrEntry.AclUndefinedId),
            new PosixAclXattrEntry(AclTag.Mask, groupOwnerPermissions, PosixAclXattrEntry.AclUndefinedId),
            new PosixAclXattrEntry(AclTag.Other, otherPermissions, PosixAclXattrEntry.AclUndefinedId),
        ];
    }

    private void FlushAccessAcl() => SetFileAccessControlList(_fileSystemInfo.FullName, PosixAccessAclXattrName, AccessAcl);
    private void RefreshProtection() => _fileSystemInfo.Protection = _fileSystemInfo.Protection;  // chmod() syscall

    public void ModifyOwnerUserAccess(AclPermissions permissions)
    {
        var maskedPermissions = _fileSystemInfo.FileAccessPermissions & (FileAccessPermissions.AllPermissions ^ FileAccessPermissions.UserReadWriteExecute);
        _fileSystemInfo.FileAccessPermissions = maskedPermissions | (FileAccessPermissions)((int)permissions << 6);
        _accessAcl.Clear();
        _accessAcl.AddRange(GetFileAccessControlList(_fileSystemInfo.FullName, PosixAccessAclXattrName));
    }

    public void ModifyUserAccess(UnixUserInfo user, AclPermissions permissions)
    {
        if (_accessAcl.Count == 0) _accessAcl.AddRange(GetImplicitAcl());

        int matchingEntryIndex = -1;
        int cursor;
        for (cursor = 0; cursor < _accessAcl.Count; ++cursor) {
            var entry = _accessAcl[cursor];
            if (entry.EntryTag < AclTag.User) continue;
            if (entry.EntryTag > AclTag.User) break;
            if (entry.EntryId != user.UserId) continue;
            matchingEntryIndex = cursor;
            break;
        }

        if (matchingEntryIndex == -1) {
            // insert a new ACL entry
            _accessAcl.Insert(cursor, new PosixAclXattrEntry(AclTag.User, permissions, (UInt32)user.UserId));
            FlushAccessAcl();
            RefreshProtection();
            return;
        }

        // replace existing ACL entry
        _accessAcl[matchingEntryIndex] = new PosixAclXattrEntry(AclTag.User, permissions, (UInt32)user.UserId);
        FlushAccessAcl();
        RefreshProtection();
    }

    public void ModifyOwnerGroupAccess(AclPermissions permissions)
    {
        var maskedPermissions = _fileSystemInfo.FileAccessPermissions & (FileAccessPermissions.AllPermissions ^ FileAccessPermissions.GroupReadWriteExecute);
        _fileSystemInfo.FileAccessPermissions = maskedPermissions | (FileAccessPermissions)((int)permissions << 3);
        _accessAcl.Clear();
        _accessAcl.AddRange(GetFileAccessControlList(_fileSystemInfo.FullName, PosixAccessAclXattrName));
    }

    public void ModifyGroupAccess(UnixGroupInfo group, AclPermissions permissions)
    {
        if (_accessAcl.Count == 0) _accessAcl.AddRange(GetImplicitAcl());

        int matchingEntryIndex = -1;
        int cursor;
        for (cursor = 0; cursor < _accessAcl.Count; ++cursor) {
            var entry = _accessAcl[cursor];
            if (entry.EntryTag < AclTag.Group) continue;
            if (entry.EntryTag > AclTag.Group) break;
            if (entry.EntryId != group.GroupId) continue;
            matchingEntryIndex = cursor;
            break;
        }

        if (matchingEntryIndex == -1) {
            // insert a new ACL entry
            _accessAcl.Insert(cursor, new PosixAclXattrEntry(AclTag.User, permissions, (UInt32)group.GroupId));
            FlushAccessAcl();
            RefreshProtection();
            return;
        }

        // replace existing ACL entry
        _accessAcl[matchingEntryIndex] = new PosixAclXattrEntry(AclTag.User, permissions, (UInt32)group.GroupId);
        FlushAccessAcl();
        RefreshProtection();
    }

    public void ModifyOtherAccess(AclPermissions permissions)
    {
        var maskedPermissions = _fileSystemInfo.FileAccessPermissions & (FileAccessPermissions.AllPermissions ^ FileAccessPermissions.OtherReadWriteExecute);
        _fileSystemInfo.FileAccessPermissions = maskedPermissions | (FileAccessPermissions)permissions;
        _accessAcl.Clear();
        _accessAcl.AddRange(GetFileAccessControlList(_fileSystemInfo.FullName, PosixAccessAclXattrName));
    }

    private static List<PosixAclXattrEntry> GetFileAccessControlList(string path, string xattrName)
    {
        var aclByteBufferLength = Native.Syscall.getxattr(path, xattrName, out var aclByteBuffer);
        if (aclByteBufferLength < 0) return [];
        using var aclByteStream = new MemoryStream(aclByteBuffer, false);
        using var aclByteReader = new BinaryReader(aclByteStream);
        var header = aclByteReader.ReadPosixAclXattrHeader();
        Debug.Assert(header.AclVersion == PosixAclXattrVersion);
        var acl = new List<PosixAclXattrEntry>();
        while (aclByteReader.PeekChar() > 0) {
            acl.Add(aclByteReader.ReadPosixAclXattrEntry());
        }
        return acl;
    }

    private static void SetFileAccessControlList(string path, string xattrName, IList<PosixAclXattrEntry> acl)
    {
        using var aclByteStream = new MemoryStream();
        using var aclByteWriter = new BinaryWriter(aclByteStream);
        aclByteWriter.Write(new PosixAclXattrHeader(PosixAclXattrVersion));
        foreach (var entry in acl) {
            aclByteWriter.Write(entry);
        }
        var result = Native.Syscall.setxattr(path, xattrName, aclByteStream.ToArray());
        if (result != 0) throw new Exception($"setxattr returned exit code {result}");
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
