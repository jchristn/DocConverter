namespace DocConverter.Model
{
    using System.Collections.Generic;

    /// <summary>
    /// A heading of level 1 through 6.
    /// </summary>
    public class HeadingBlock : Block
    {
        private int _Level = 1;
        private List<Inline> _Inlines = new List<Inline>();

        /// <summary>
        /// Heading level. Default 1. Values below 1 are stored as 1 and values above 6 as 6.
        /// </summary>
        public int Level
        {
            get => _Level;
            set => _Level = value < 1 ? 1 : (value > 6 ? 6 : value);
        }

        /// <summary>
        /// Heading content. Never null.
        /// </summary>
        public List<Inline> Inlines
        {
            get => _Inlines;
            set => _Inlines = value ?? new List<Inline>();
        }

        /// <summary>
        /// Instantiate an empty level 1 heading.
        /// </summary>
        public HeadingBlock()
        {
        }

        /// <summary>
        /// Instantiate a heading with plain text.
        /// </summary>
        /// <param name="level">Level, clamped to 1 through 6.</param>
        /// <param name="text">Heading text. Null is treated as empty.</param>
        public HeadingBlock(int level, string? text)
        {
            Level = level;
            if (!string.IsNullOrEmpty(text)) _Inlines.Add(new TextInline(text!));
        }
    }
}
