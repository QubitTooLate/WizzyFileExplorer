using System;

namespace ConsoleWizzyFileExplorer;

public static class ReadonlySpanExtensions
{
    public static bool TryGetSpanOverLineAndAdvanceIndex(this ReadOnlySpan<char> text, out ReadOnlySpan<char> line, ref int index)
    {
        if (!TryGetSpanToCharAndAdvanceIndex(text, '\r', out line, ref index)) { return false; }

        ++index;
        return true;
    }

    public static bool TryGetSegment(this ReadOnlySpan<char> text, out ReadOnlySpan<char> segment, ref int index) => TryGetSpanToCharAndAdvanceIndex(text, ',', out segment, ref index);

    public static bool TryGetSpanToCharAndAdvanceIndex(this ReadOnlySpan<char> text, char c, out ReadOnlySpan<char> segment, ref int index)
    {
        var endIndex = text[index..].IndexOf(c);
        if (endIndex is -1)
        {
            segment = default;
            return false;
        }

        segment = text[index..(index + endIndex)];
        index += endIndex + 1;
        return true;
    }
}
