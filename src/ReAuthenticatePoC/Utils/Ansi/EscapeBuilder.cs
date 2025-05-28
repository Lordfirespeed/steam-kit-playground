using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ReAuthenticatePoC.Utils.Ansi;

public class EscapeBuilder
{
    private MemoryStream _stream;
    private int _argumentCount = 0;

    public EscapeBuilder()
    {
        _stream = new MemoryStream(8);
        AppendRaw(AnsiConstants.ControlSequenceIntroducer);
    }

    private void AppendRaw(byte[] bytes)
    {
        _stream.Write(bytes, 0, bytes.Length);
    }

    private void AppendRaw(byte c)
    {
        _stream.WriteByte(c);
    }

    private byte CharacterAt(int index)
    {
        return _stream.GetBuffer()[index];
    }

    private byte[] Content => _stream.GetBuffer()[..(int)_stream.Position];

    public EscapeBuilder Argue(int ordinal)
    {
        if (ordinal < 0) throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, null);
        if (_argumentCount > 0) AppendRaw(AnsiConstants.Delimiter);
        _argumentCount++;
        if (ordinal == 0)
            // missing arguments are interpreted as zero, so do nothing
            return this;
        AppendRaw(Encoding.ASCII.GetBytes(ordinal.ToString()));
        return this;
    }

    public virtual string Finalise(byte terminator)
    {
        AppendRaw(terminator);
        return Encoding.ASCII.GetString(Content);
    }

    public virtual string Finalise(ControlSequenceTerminator terminator) => Finalise(terminator.Get());

    public static string Quick(int ordinal, byte terminator)
    {
        return new EscapeBuilder().Argue(ordinal).Finalise(terminator);
    }

    public static string Quick(int ordinal, ControlSequenceTerminator terminator) => Quick(ordinal, terminator.Get());
}
