namespace DocConverter.Readers.Xlsx
{
    /// <summary>
    /// A merged cell range with zero based, inclusive bounds.
    /// </summary>
    internal sealed class MergeRange
    {
        internal int Top { get; }

        internal int Left { get; }

        internal int Bottom { get; }

        internal int Right { get; }

        internal MergeRange(int top, int left, int bottom, int right)
        {
            Top = top;
            Left = left;
            Bottom = bottom;
            Right = right;
        }
    }
}
