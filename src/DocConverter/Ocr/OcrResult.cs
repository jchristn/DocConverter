namespace DocConverter.Ocr
{
    using System.Collections.Generic;
    using DocConverter.Model;

    /// <summary>
    /// Content recognized by an OCR provider. Reserved: OCR is not implemented in this release.
    /// </summary>
    public class OcrResult
    {
        private List<Block> _Blocks = new List<Block>();
        private double _Confidence = 0.0;

        /// <summary>
        /// Recognized content as document model blocks (paragraphs, tables, lists). Never null.
        /// </summary>
        public List<Block> Blocks
        {
            get => _Blocks;
            set => _Blocks = value ?? new List<Block>();
        }

        /// <summary>
        /// Overall confidence. Values are clamped to 0.0 through 1.0. Default 0.0.
        /// </summary>
        public double Confidence
        {
            get => _Confidence;
            set => _Confidence = value < 0.0 ? 0.0 : (value > 1.0 ? 1.0 : value);
        }

        /// <summary>
        /// Detected language. Null when unknown.
        /// </summary>
        public string? Language { get; set; } = null;
    }
}
