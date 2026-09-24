namespace DocConverter.Options
{
    using DocConverter.Enums;
    using DocConverter.Exceptions;

    /// <summary>
    /// Options for reading and writing PPTX.
    /// </summary>
    public class PptxOptions
    {
        private int _SlideSplitHeadingLevel = 2;
        private int _MaxBlocksPerSlide = 12;
        private int _MaxTableRowsPerSlide = 15;

        /// <summary>
        /// When reading, include speaker notes. Default false.
        /// </summary>
        public bool IncludeNotes { get; set; } = false;

        /// <summary>
        /// When writing, start a new slide at each heading of this level or lower. Default 2. Allowed: 1 through 6.
        /// </summary>
        /// <exception cref="InvalidConversionOptionsException">Thrown when set outside the allowed range.</exception>
        public int SlideSplitHeadingLevel
        {
            get => _SlideSplitHeadingLevel;
            set => _SlideSplitHeadingLevel = value >= 1 && value <= 6
                ? value
                : throw new InvalidConversionOptionsException("SlideSplitHeadingLevel must be 1 through 6; got " + value + ".");
        }

        /// <summary>
        /// When writing, start a continuation slide after this many blocks. Default 12. Allowed: 1 through 100.
        /// </summary>
        /// <exception cref="InvalidConversionOptionsException">Thrown when set outside the allowed range.</exception>
        public int MaxBlocksPerSlide
        {
            get => _MaxBlocksPerSlide;
            set => _MaxBlocksPerSlide = value >= 1 && value <= 100
                ? value
                : throw new InvalidConversionOptionsException("MaxBlocksPerSlide must be 1 through 100; got " + value + ".");
        }

        /// <summary>
        /// When writing, split tables into continuation slides after this many rows. Default 15. Allowed: 2 through 100.
        /// </summary>
        /// <exception cref="InvalidConversionOptionsException">Thrown when set outside the allowed range.</exception>
        public int MaxTableRowsPerSlide
        {
            get => _MaxTableRowsPerSlide;
            set => _MaxTableRowsPerSlide = value >= 2 && value <= 100
                ? value
                : throw new InvalidConversionOptionsException("MaxTableRowsPerSlide must be 2 through 100; got " + value + ".");
        }
    }
}
