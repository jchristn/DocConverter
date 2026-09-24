namespace DocConverter
{
    using System;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Ocr;
    using DocConverter.Options;

    /// <summary>
    /// Instance-level settings shared by every conversion a Converter performs. Configure before use; changing
    /// settings while conversions run is not thread safe.
    /// </summary>
    public class ConverterSettings
    {
        private ConversionOptions _DefaultOptions = new ConversionOptions();
        private long _MaxInputBytes = 268435456;
        private long _MaxDecompressedBytes = 1073741824;
        private int _MaxNestingDepth = 64;
        private int _DetectionBufferBytes = 65536;

        /// <summary>
        /// Options used when a call passes null. Default a new ConversionOptions. Never null.
        /// </summary>
        public ConversionOptions DefaultOptions
        {
            get => _DefaultOptions;
            set => _DefaultOptions = value ?? new ConversionOptions();
        }

        /// <summary>
        /// Largest accepted input in bytes. Larger input throws InputTooLargeException. Default 268,435,456 (256 MB).
        /// Allowed: 1 through 2,147,483,647 (the largest byte array .NET supports).
        /// </summary>
        /// <exception cref="InvalidConversionOptionsException">Thrown when set outside the allowed range.</exception>
        public long MaxInputBytes
        {
            get => _MaxInputBytes;
            set => _MaxInputBytes = value >= 1 && value <= int.MaxValue
                ? value
                : throw new InvalidConversionOptionsException("MaxInputBytes must be 1 through " + int.MaxValue + "; got " + value + ".");
        }

        /// <summary>
        /// Largest total decompressed size of the parts read from a zip based document (DOCX, XLSX, PPTX). Guards against
        /// zip bombs. Default 1,073,741,824 (1 GB). Allowed: 1 through long.MaxValue.
        /// </summary>
        /// <exception cref="InvalidConversionOptionsException">Thrown when set below 1.</exception>
        public long MaxDecompressedBytes
        {
            get => _MaxDecompressedBytes;
            set => _MaxDecompressedBytes = value >= 1
                ? value
                : throw new InvalidConversionOptionsException("MaxDecompressedBytes must be at least 1; got " + value + ".");
        }

        /// <summary>
        /// Deepest nesting of lists, sections, JSON and XML that readers follow. Deeper content is flattened with the
        /// NestedDepthLimited warning. Default 64. Allowed: 1 through 1024.
        /// </summary>
        /// <exception cref="InvalidConversionOptionsException">Thrown when set outside the allowed range.</exception>
        public int MaxNestingDepth
        {
            get => _MaxNestingDepth;
            set => _MaxNestingDepth = value >= 1 && value <= 1024
                ? value
                : throw new InvalidConversionOptionsException("MaxNestingDepth must be 1 through 1024; got " + value + ".");
        }

        /// <summary>
        /// Bytes examined by text heuristics during format detection. Binary signatures and zip structure are always
        /// examined in full. Default 65,536. Allowed: 512 through 16,777,216.
        /// </summary>
        /// <exception cref="InvalidConversionOptionsException">Thrown when set outside the allowed range.</exception>
        public int DetectionBufferBytes
        {
            get => _DetectionBufferBytes;
            set => _DetectionBufferBytes = value >= 512 && value <= 16777216
                ? value
                : throw new InvalidConversionOptionsException("DetectionBufferBytes must be 512 through 16777216; got " + value + ".");
        }

        /// <summary>
        /// Optional logger. DocConverter never writes to the console; messages go here. Exceptions thrown by the logger
        /// are swallowed. Default null.
        /// </summary>
        public Action<SeverityEnum, string>? Logger { get; set; } = null;

        /// <summary>
        /// Reserved for optical character recognition. Default null. In this release, a non-null provider combined with a
        /// ConversionOptions.OcrMode other than Off throws NotImplementedException.
        /// </summary>
        public IOcrProvider? OcrProvider { get; set; } = null;

        /// <summary>
        /// Instantiate default settings.
        /// </summary>
        public ConverterSettings()
        {
        }
    }
}
