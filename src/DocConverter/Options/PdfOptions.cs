namespace DocConverter.Options
{
    using DocConverter.Enums;
    using DocConverter.Exceptions;

    /// <summary>
    /// Options for reading and writing PDF.
    /// </summary>
    public class PdfOptions
    {
        private double _MarginPoints = 72.0;
        private double _BaseFontSize = 11.0;
        private double _HeadingSizeRatio = 1.2;

        /// <summary>
        /// Page size of written documents. Default Letter.
        /// </summary>
        public PdfPageSizeEnum PageSize { get; set; } = PdfPageSizeEnum.Letter;

        /// <summary>
        /// Page margin in points (72 points per inch). Default 72.0. Allowed: 0.0 through 216.0.
        /// </summary>
        /// <exception cref="InvalidConversionOptionsException">Thrown when set outside the allowed range or not a number.</exception>
        public double MarginPoints
        {
            get => _MarginPoints;
            set => _MarginPoints = value >= 0.0 && value <= 216.0
                ? value
                : throw new InvalidConversionOptionsException("MarginPoints must be 0.0 through 216.0; got " + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        }

        /// <summary>
        /// Body font size in points for written documents. Default 11.0. Allowed: 6.0 through 36.0.
        /// </summary>
        /// <exception cref="InvalidConversionOptionsException">Thrown when set outside the allowed range or not a number.</exception>
        public double BaseFontSize
        {
            get => _BaseFontSize;
            set => _BaseFontSize = value >= 6.0 && value <= 36.0
                ? value
                : throw new InvalidConversionOptionsException("BaseFontSize must be 6.0 through 36.0; got " + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        }

        /// <summary>
        /// When reading, text at least this many times the page's median body size is treated as a heading. Default 1.2. Allowed: 1.05 through 3.0.
        /// </summary>
        /// <exception cref="InvalidConversionOptionsException">Thrown when set outside the allowed range or not a number.</exception>
        public double HeadingSizeRatio
        {
            get => _HeadingSizeRatio;
            set => _HeadingSizeRatio = value >= 1.05 && value <= 3.0
                ? value
                : throw new InvalidConversionOptionsException("HeadingSizeRatio must be 1.05 through 3.0; got " + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        }

        /// <summary>
        /// When reading, extract ruled tables. Default true.
        /// </summary>
        public bool DetectTables { get; set; } = true;

        /// <summary>
        /// When reading, wrap each page in a page section. Default false (pages are flattened).
        /// </summary>
        public bool PreservePages { get; set; } = false;
    }
}
