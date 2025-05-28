namespace ReAuthenticatePoC.Utils.Ansi;

public static class AnsiConstants
{
    /// <summary>
    /// all escape sequences are prefixed with `ESC`.
    /// </summary>
    public const byte Escape = 0x1B;

    /// <summary>
    /// most useful escape sequences begin with `ESC [`, the 'CSI', and are terminated by a byte in the range 0x40-0x7E.
    /// </summary>
    public static readonly byte[] ControlSequenceIntroducer = [ 0x1B, (byte)'[' ];

    /// <summary>
    /// escape sequence arguments are semicolon-delimited.
    /// </summary>
    public const byte Delimiter = (byte)';';
}
