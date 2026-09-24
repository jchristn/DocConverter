namespace DocConverter.Results
{
    using System;
    using System.Collections.Generic;
    using DocConverter.Enums;
    using DocConverter.Model;

    /// <summary>
    /// Outcome of a successful conversion. Failures are reported by exceptions.
    /// </summary>
    public class ConversionResult
    {
        private List<ConversionWarning> _Warnings = new List<ConversionWarning>();
        private List<BinaryResource> _Resources = new List<BinaryResource>();
        private ConversionStatistics _Statistics = new ConversionStatistics();
        private DocumentMetadata _Metadata = new DocumentMetadata();

        /// <summary>
        /// Source format as resolved. Never Auto.
        /// </summary>
        public DocumentFormatEnum SourceFormat { get; internal set; } = DocumentFormatEnum.Auto;

        /// <summary>
        /// Target format.
        /// </summary>
        public DocumentFormatEnum TargetFormat { get; internal set; } = DocumentFormatEnum.Auto;

        /// <summary>
        /// Detection details when the source was Auto. Null otherwise.
        /// </summary>
        public DetectionResult? DetectedSource { get; internal set; } = null;

        /// <summary>
        /// Bytes of input read. For string input, the UTF-8 byte count (or decoded bytes for base64).
        /// </summary>
        public long BytesRead { get; internal set; } = 0;

        /// <summary>
        /// Bytes of output produced.
        /// </summary>
        public long BytesWritten { get; internal set; } = 0;

        /// <summary>
        /// When the conversion started, UTC.
        /// </summary>
        public DateTime StartedUtc { get; internal set; } = DateTime.UtcNow;

        /// <summary>
        /// When the conversion completed, UTC.
        /// </summary>
        public DateTime CompletedUtc { get; internal set; } = DateTime.UtcNow;

        /// <summary>
        /// Total elapsed milliseconds.
        /// </summary>
        public double TotalMs { get; internal set; } = 0;

        /// <summary>
        /// Milliseconds spent reading (parsing the source into the model).
        /// </summary>
        public double ReadMs { get; internal set; } = 0;

        /// <summary>
        /// Milliseconds spent writing (rendering the model into the target).
        /// </summary>
        public double WriteMs { get; internal set; } = 0;

        /// <summary>
        /// Warnings, one entry per code. Never null.
        /// </summary>
        public IReadOnlyList<ConversionWarning> Warnings
        {
            get => _Warnings;
        }

        /// <summary>
        /// Content statistics. Never null.
        /// </summary>
        public ConversionStatistics Statistics
        {
            get => _Statistics;
            internal set => _Statistics = value ?? new ConversionStatistics();
        }

        /// <summary>
        /// Document metadata as read from the source. Never null.
        /// </summary>
        public DocumentMetadata Metadata
        {
            get => _Metadata;
            internal set => _Metadata = value ?? new DocumentMetadata();
        }

        /// <summary>
        /// Side files a writer asked the caller to store, for example images written with ImageMode External. Never null.
        /// </summary>
        public IReadOnlyList<BinaryResource> Resources
        {
            get => _Resources;
        }

        /// <summary>
        /// True when at least one warning was raised.
        /// </summary>
        public bool HasWarnings
        {
            get => _Warnings.Count > 0;
        }

        /// <summary>
        /// Instantiate an empty result.
        /// </summary>
        public ConversionResult()
        {
        }

        internal void SetWarnings(IEnumerable<ConversionWarning> warnings)
        {
            _Warnings = new List<ConversionWarning>(warnings);
        }

        internal void SetResources(IEnumerable<BinaryResource> resources)
        {
            _Resources = new List<BinaryResource>(resources);
        }

        internal void CopyFrom(ConversionResult other)
        {
            SourceFormat = other.SourceFormat;
            TargetFormat = other.TargetFormat;
            DetectedSource = other.DetectedSource;
            BytesRead = other.BytesRead;
            BytesWritten = other.BytesWritten;
            StartedUtc = other.StartedUtc;
            CompletedUtc = other.CompletedUtc;
            TotalMs = other.TotalMs;
            ReadMs = other.ReadMs;
            WriteMs = other.WriteMs;
            _Warnings = new List<ConversionWarning>(other._Warnings);
            _Resources = new List<BinaryResource>(other._Resources);
            _Statistics = other._Statistics;
            _Metadata = other._Metadata;
        }
    }
}
