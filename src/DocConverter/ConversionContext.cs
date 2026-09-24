namespace DocConverter
{
    using System;
    using System.Collections.Generic;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Results;

    /// <summary>
    /// Per-conversion state shared by the reader and the writer: warnings, limits, logging and side resources.
    /// One context is created per conversion, so readers and writers never keep state between calls.
    /// Not thread safe: a context belongs to one conversion.
    /// </summary>
    public class ConversionContext
    {
        private readonly List<ConversionWarning> _Warnings = new List<ConversionWarning>();
        private readonly List<BinaryResource> _OutputResources = new List<BinaryResource>();
        private readonly Action<SeverityEnum, string>? _Logger;

        /// <summary>
        /// Source format being read. Never Auto once reading starts.
        /// </summary>
        public DocumentFormatEnum SourceFormat { get; internal set; }

        /// <summary>
        /// Target format being written.
        /// </summary>
        public DocumentFormatEnum TargetFormat { get; internal set; }

        /// <summary>
        /// Largest accepted input in bytes.
        /// </summary>
        public long MaxInputBytes { get; }

        /// <summary>
        /// Largest total decompressed size of zip parts.
        /// </summary>
        public long MaxDecompressedBytes { get; }

        /// <summary>
        /// Deepest nesting readers follow.
        /// </summary>
        public int MaxNestingDepth { get; }

        /// <summary>
        /// Warnings raised so far, one entry per code.
        /// </summary>
        public IReadOnlyList<ConversionWarning> Warnings
        {
            get => _Warnings;
        }

        /// <summary>
        /// Side files a writer produced for the caller (for example images with ImageMode External).
        /// </summary>
        public IReadOnlyList<BinaryResource> OutputResources
        {
            get => _OutputResources;
        }

        /// <summary>
        /// Decoded source text when the caller passed a string for a text based format. Readers of text formats use it
        /// instead of decoding the stream. Null otherwise.
        /// </summary>
        public string? SourceText { get; internal set; } = null;

        /// <summary>
        /// File name hint for the source, when known. Null otherwise.
        /// </summary>
        public string? SourceFileName { get; internal set; } = null;

        /// <summary>
        /// Instantiate a context.
        /// </summary>
        /// <param name="settings">Converter settings supplying limits and the logger.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        public ConversionContext(ConverterSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            MaxInputBytes = settings.MaxInputBytes;
            MaxDecompressedBytes = settings.MaxDecompressedBytes;
            MaxNestingDepth = settings.MaxNestingDepth;
            _Logger = settings.Logger;
        }

        /// <summary>
        /// Raise a warning. Repeated codes increment the count of the existing warning and keep its first message.
        /// </summary>
        /// <param name="code">Warning code.</param>
        /// <param name="message">Message describing the first occurrence.</param>
        public void AddWarning(WarningCodeEnum code, string message)
        {
            foreach (ConversionWarning existing in _Warnings)
            {
                if (existing.Code == code)
                {
                    existing.Count++;
                    return;
                }
            }

            _Warnings.Add(new ConversionWarning(code, message));
            Log(SeverityEnum.Warn, code + ": " + message);
        }

        /// <summary>
        /// Add a side resource for the caller.
        /// </summary>
        /// <param name="resource">Resource.</param>
        /// <exception cref="ArgumentNullException">Thrown when resource is null.</exception>
        public void AddOutputResource(BinaryResource resource)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            _OutputResources.Add(resource);
        }

        /// <summary>
        /// Send a message to the configured logger. Logger exceptions are swallowed.
        /// </summary>
        /// <param name="severity">Severity.</param>
        /// <param name="message">Message.</param>
        public void Log(SeverityEnum severity, string message)
        {
            if (_Logger == null) return;
            try
            {
                _Logger(severity, "[DocConverter] " + message);
            }
            catch (Exception)
            {
                // A faulty logger must never break a conversion.
            }
        }
    }
}
