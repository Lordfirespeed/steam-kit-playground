using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ReAuthenticatePoC.Utils.Ansi;

namespace ReAuthenticatePoC.Utils.Reprint;

public class Reprinter
{
    private static readonly HashSet<TextWriter> StandardWriters = [Console.Out, Console.Error];
    private static readonly Regex AnsiPattern = new Regex(@"\x1b\[[;\d]*[A-Za-z]");

    private static int DisplayLength(string value)
    {
        return AnsiPattern.Replace(value, "").Length;
    }

    private TextWriter _writer;

    public Reprinter(TextWriter? writer = null)
    {
        _writer = writer ?? Console.Out;
        if (StandardWriters.Contains(_writer)) {
            Console.Out.Flush();
            Console.Error.Flush();
        }
    }

    private void Write(string value) => _writer.Write(value);
    private void Flush() => _writer.Flush();

    private void MoveCursorRelatively(int relativePosition)
    {
        string controlSequence;

        if (relativePosition == 0) return;

        if (relativePosition > 5) {
            // use 'cursor down' control sequence to minimise bytes written
            controlSequence = new EscapeBuilder()
                .Argue(relativePosition)
                .Finalise(ControlSequenceTerminator.CursorDown);
            // including content (the `\r`) is necessary, otherwise the cursor won't move onto a blank line
            Write($"{controlSequence}\r");
            return;
        }

        if (relativePosition > 0) {
            // use newlines to move the cursor down
            Write(new string('\n', relativePosition));
            return;
        }

        // relative position is negative; use 'cursor up' control sequence
        controlSequence = new EscapeBuilder()
            .Argue(-relativePosition)
            .Finalise(ControlSequenceTerminator.CursorUp);
        Write(controlSequence);
    }

    public IPrinterSession Open(int lineCount)
    {
        return new PrinterSession(this, lineCount);
    }

    public interface IPrinterSession : IDisposable
    {
        public void Print(string value, int? lineIndex = null);
        public void PrintLines(string[] lines, int offset = 0);
    }

    private class PrinterSession : IPrinterSession
    {
        private readonly Reprinter _reprinter;
        private bool _disposed = false;
        private readonly int _lineCount;
        private int _longestPrintedLength = 0;

        public PrinterSession(Reprinter reprinter, int lineCount)
        {
            _reprinter = reprinter;
            _lineCount = lineCount;
            reprinter.Write(new string('\n', _lineCount));
        }

        private void Overwrite(string value)
        {
            var length = DisplayLength(value);
            var padding = new string(' ', Math.Max(_longestPrintedLength - length, 0));
            _reprinter.Write($"\r{value}{padding}");
            _longestPrintedLength = Math.Max(_longestPrintedLength, length);
        }

        public void Print(string value, int? lineIndex = null)
        {
            if (_disposed)
                throw new InvalidOperationException("Printer session has ended");
            if (lineIndex is null && _lineCount == 1)
                lineIndex = 0;
            if (lineIndex is null)
                throw new ArgumentNullException(nameof(lineIndex), "Line index must be specified");
            if (lineIndex < 0 || _lineCount <= lineIndex)
                throw new ArgumentOutOfRangeException(nameof(lineIndex), "Specified line index is out of range");

            var lineRelativePosition = -_lineCount + lineIndex.Value;
            _reprinter.MoveCursorRelatively(lineRelativePosition);
            Overwrite(value);
            _reprinter.MoveCursorRelatively(-lineRelativePosition);
            _reprinter.Flush();
        }

        public void PrintLines(string[] lines, int offset = 0)
        {
            if (_disposed)
                throw new InvalidOperationException("Printer session has ended");
            if (offset < 0 || _lineCount < lines.Length + offset)
                throw new ArgumentOutOfRangeException(nameof(offset), "Specified offset is out of range and/or too many lines to print");

            var firstLineRelativePosition = -_lineCount + offset;
            _reprinter.MoveCursorRelatively(firstLineRelativePosition);
            foreach (var line in lines)
            {
                Overwrite(line);
                _reprinter.Write("\n");
            }
            var lastLineSuccessorRelativePosition = -_lineCount + lines.Length + offset;
            _reprinter.MoveCursorRelatively(-lastLineSuccessorRelativePosition);
            _reprinter.Flush();
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
