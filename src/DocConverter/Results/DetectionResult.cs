namespace DocConverter.Results
{
    using DocConverter.Enums;

    /// <summary>
    /// Result of format detection.
    /// </summary>
    public class DetectionResult
    {
        /// <summary>
        /// Detected format. Null when the content is unrecognized or recognized but unsupported (see RecognizedAs).
        /// </summary>
        public DocumentFormatEnum? Format { get; internal set; } = null;

        /// <summary>
        /// Media type of the detected content, for example "application/pdf". Never null.
        /// </summary>
        public string MediaType { get; internal set; } = "application/octet-stream";

        /// <summary>
        /// Usual file extension without the dot, for example "pdf". Never null.
        /// </summary>
        public string Extension { get; internal set; } = "";

        /// <summary>
        /// Human readable description, including recognized but unsupported formats such as
        /// "legacy Word 97-2003 document (.doc)". Never null.
        /// </summary>
        public string RecognizedAs { get; internal set; } = "unknown";

        /// <summary>
        /// How the format was determined.
        /// </summary>
        public DetectionConfidenceEnum Confidence { get; internal set; } = DetectionConfidenceEnum.Heuristic;

        /// <summary>
        /// True when Format is set, meaning DocConverter can read the input.
        /// </summary>
        public bool IsSupported
        {
            get => Format.HasValue;
        }

        /// <summary>
        /// Instantiate an empty result.
        /// </summary>
        public DetectionResult()
        {
        }

        /// <summary>
        /// Format, confidence and description, for logs.
        /// </summary>
        /// <returns>String form.</returns>
        public override string ToString()
        {
            return (Format.HasValue ? Format.Value.ToString() : "Unsupported") + " (" + RecognizedAs + ", " + Confidence + ")";
        }
    }
}
