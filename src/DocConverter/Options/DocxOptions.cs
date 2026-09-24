namespace DocConverter.Options
{
    using DocConverter.Enums;
    using DocConverter.Exceptions;

    /// <summary>
    /// Options for reading and writing DOCX.
    /// </summary>
    public class DocxOptions
    {
        private double _MarginPoints = 72.0;

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
        /// When reading, append footnotes and endnotes as trailing sections. Default true.
        /// </summary>
        public bool IncludeFootnotes { get; set; } = true;
    }
}
