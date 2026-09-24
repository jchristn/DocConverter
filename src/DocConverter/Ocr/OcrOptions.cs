namespace DocConverter.Ocr
{
    using System.Collections.Generic;
    using DocConverter.Exceptions;

    /// <summary>
    /// Options passed to an OCR provider. Reserved: OCR is not implemented in this release.
    /// </summary>
    public class OcrOptions
    {
        private List<string> _Languages = new List<string> { "eng" };
        private double _MinimumConfidence = 0.6;

        /// <summary>
        /// Languages to recognize, as provider specific codes. Default ["eng"]. Never null.
        /// </summary>
        public List<string> Languages
        {
            get => _Languages;
            set => _Languages = value ?? new List<string>();
        }

        /// <summary>
        /// Results below this confidence are discarded. Default 0.6. Allowed: 0.0 through 1.0.
        /// </summary>
        /// <exception cref="InvalidConversionOptionsException">Thrown when set outside the allowed range.</exception>
        public double MinimumConfidence
        {
            get => _MinimumConfidence;
            set => _MinimumConfidence = value >= 0.0 && value <= 1.0
                ? value
                : throw new InvalidConversionOptionsException("MinimumConfidence must be 0.0 through 1.0.");
        }

        /// <summary>
        /// When true, the provider should report tables. Default true.
        /// </summary>
        public bool DetectTables { get; set; } = true;

        /// <summary>
        /// When true, the provider should report lists. Default true.
        /// </summary>
        public bool DetectLists { get; set; } = true;
    }
}
