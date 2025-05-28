using System;

namespace ReAuthenticatePoC.Utils.Ansi;

public static class ControlSequenceTerminatorChars
{
    #region ANSI sequences
    public const byte CursorUp = (byte)'A';
    public const byte CursorDown = (byte)'B';
    public const byte CursorForward = (byte)'C';
    public const byte CursorBack = (byte)'D';
    public const byte CursorNextLine = (byte)'E';
    public const byte CursorPreviousLine = (byte)'F';
    public const byte CursorHorizontalAbsolute = (byte)'G';
    public const byte CursorPosition = (byte)'H';
    public const byte EraseInDisplay = (byte)'J';
    public const byte EraseInLine = (byte)'K';
    public const byte ScrollUp = (byte)'S';
    public const byte ScrollDown = (byte)'T';
    public const byte HorizontalVerticalPosition = (byte)'f';
    public const byte SelectGraphicRendition = (byte)'m';
    #endregion

    #region popular private sequences
    public const byte SaveCurrentCursorPosition = (byte)'s';
    public const byte RestoreCurrentCursorPosition = (byte)'u';
    #endregion

    public static byte Get(this ControlSequenceTerminator terminator) => terminator switch {
        ControlSequenceTerminator.CursorUp => CursorUp,
        ControlSequenceTerminator.CursorDown => CursorDown,
        ControlSequenceTerminator.CursorForward => CursorForward,
        ControlSequenceTerminator.CursorBack => CursorBack,
        ControlSequenceTerminator.CursorNextLine => CursorNextLine,
        ControlSequenceTerminator.CursorPreviousLine => CursorPreviousLine,
        ControlSequenceTerminator.CursorHorizontalAbsolute => CursorHorizontalAbsolute,
        ControlSequenceTerminator.CursorPosition => CursorPosition,
        ControlSequenceTerminator.EraseInDisplay => EraseInDisplay,
        ControlSequenceTerminator.EraseInLine => EraseInLine,
        ControlSequenceTerminator.ScrollUp => ScrollUp,
        ControlSequenceTerminator.ScrollDown => ScrollDown,
        ControlSequenceTerminator.HorizontalVerticalPosition => HorizontalVerticalPosition,
        ControlSequenceTerminator.SelectGraphicRendition => SelectGraphicRendition,
        ControlSequenceTerminator.SaveCurrentCursorPosition => SaveCurrentCursorPosition,
        ControlSequenceTerminator.RestoreCurrentCursorPosition => RestoreCurrentCursorPosition,
        _ => throw new ArgumentOutOfRangeException(nameof(terminator), terminator, null)
    };
}

public enum ControlSequenceTerminator
{
    CursorUp,
    CursorDown,
    CursorForward,
    CursorBack,
    CursorNextLine,
    CursorPreviousLine,
    CursorHorizontalAbsolute,
    CursorPosition,
    EraseInDisplay,
    EraseInLine,
    ScrollUp,
    ScrollDown,
    HorizontalVerticalPosition,
    SelectGraphicRendition,

    SaveCurrentCursorPosition,
    RestoreCurrentCursorPosition,
}
