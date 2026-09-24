namespace DocConverter
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Detection;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Model.Serialization;
    using DocConverter.Observability;
    using DocConverter.Ocr;
    using DocConverter.Options;
    using DocConverter.Readers;
    using DocConverter.Registry;
    using DocConverter.Results;
    using DocConverter.Writers;

    /// <summary>
    /// Converts documents between formats, entirely in memory. Input may be a string, byte array or stream; output may be
    /// written into a caller-owned stream or returned as a string or byte array.
    /// Thread safe: one instance can serve concurrent conversions. Registering readers or writers while conversions run
    /// is also safe.
    /// String input: for text based source formats (Text, Markdown, Html, Json, Xml, Csv, Tsv, Rtf) the string is the
    /// document; for binary formats (Docx, Xlsx, Pptx, Pdf, images) it must be base64.
    /// Streams passed in are never closed. A caller-owned output stream is written from its current position and left
    /// positioned after the written data.
    /// </summary>
    public class Converter : IConverter, IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Instance settings. Never null.
        /// </summary>
        public ConverterSettings Settings
        {
            get => _Settings;
        }

        #endregion

        #region Private-Members

        private readonly ConverterSettings _Settings;
        private readonly FormatRegistry _Registry;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a converter with default settings.
        /// </summary>
        public Converter()
            : this(null)
        {
        }

        /// <summary>
        /// Instantiate a converter.
        /// </summary>
        /// <param name="settings">Settings. Null uses defaults.</param>
        public Converter(ConverterSettings? settings)
        {
            _Settings = settings ?? new ConverterSettings();
            _Registry = FormatRegistry.CreateDefault();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Convert string input, writing the result into a caller-owned stream.
        /// </summary>
        /// <param name="input">Document text for text based formats, or base64 for binary formats.</param>
        /// <param name="from">Source format, or Auto to detect.</param>
        /// <param name="to">Target format. Must not be Auto.</param>
        /// <param name="output">Writable stream. Not closed.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversion result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input or output is null.</exception>
        /// <exception cref="ArgumentException">Thrown when to is Auto or output is not writable.</exception>
        /// <exception cref="UnsupportedFormatException">Thrown when detection fails or finds an unsupported format.</exception>
        /// <exception cref="ConversionNotSupportedException">Thrown when no registered reader or writer covers the pair.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input exceeds Settings.MaxInputBytes.</exception>
        /// <exception cref="DocumentReadException">Thrown when the input cannot be read.</exception>
        /// <exception cref="DocumentWriteException">Thrown when the output cannot be produced.</exception>
        /// <exception cref="ConversionWarningException">Thrown after writing when TreatWarningsAsErrors is set and warnings were raised.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public async Task<ConversionResult> ConvertAsync(string input, DocumentFormatEnum from, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            ValidateOutputStream(output);
            ValidateTarget(to);
            ConversionOptions opts = options ?? _Settings.DefaultOptions;
            InputBuffer buffer = PrepareString(input, ref from);
            PipelineOutput result = await RunAsync(buffer, from, to, opts, null, token).ConfigureAwait(false);
            return await DeliverToStreamAsync(result, output, opts, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Convert byte array input, writing the result into a caller-owned stream.
        /// </summary>
        /// <param name="input">Document bytes.</param>
        /// <param name="from">Source format, or Auto to detect.</param>
        /// <param name="to">Target format. Must not be Auto.</param>
        /// <param name="output">Writable stream. Not closed.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversion result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input or output is null.</exception>
        /// <exception cref="ArgumentException">Thrown when to is Auto or output is not writable.</exception>
        /// <exception cref="UnsupportedFormatException">Thrown when detection fails or finds an unsupported format.</exception>
        /// <exception cref="ConversionNotSupportedException">Thrown when no registered reader or writer covers the pair.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input exceeds Settings.MaxInputBytes.</exception>
        /// <exception cref="DocumentReadException">Thrown when the input cannot be read.</exception>
        /// <exception cref="DocumentWriteException">Thrown when the output cannot be produced.</exception>
        /// <exception cref="ConversionWarningException">Thrown after writing when TreatWarningsAsErrors is set and warnings were raised.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public async Task<ConversionResult> ConvertAsync(byte[] input, DocumentFormatEnum from, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            ValidateOutputStream(output);
            ValidateTarget(to);
            ConversionOptions opts = options ?? _Settings.DefaultOptions;
            InputBuffer buffer = InputBuffer.FromBytes(input, _Settings.MaxInputBytes);
            PipelineOutput result = await RunAsync(buffer, from, to, opts, null, token).ConfigureAwait(false);
            return await DeliverToStreamAsync(result, output, opts, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Convert stream input, writing the result into a caller-owned stream. The input is read from its current
        /// position to its end and is not closed.
        /// </summary>
        /// <param name="input">Readable stream. Non-seekable streams are supported.</param>
        /// <param name="from">Source format, or Auto to detect.</param>
        /// <param name="to">Target format. Must not be Auto.</param>
        /// <param name="output">Writable stream. Not closed.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversion result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input or output is null.</exception>
        /// <exception cref="ArgumentException">Thrown when to is Auto, input is not readable, or output is not writable.</exception>
        /// <exception cref="UnsupportedFormatException">Thrown when detection fails or finds an unsupported format.</exception>
        /// <exception cref="ConversionNotSupportedException">Thrown when no registered reader or writer covers the pair.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input exceeds Settings.MaxInputBytes.</exception>
        /// <exception cref="DocumentReadException">Thrown when the input cannot be read.</exception>
        /// <exception cref="DocumentWriteException">Thrown when the output cannot be produced.</exception>
        /// <exception cref="ConversionWarningException">Thrown after writing when TreatWarningsAsErrors is set and warnings were raised.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public async Task<ConversionResult> ConvertAsync(Stream input, DocumentFormatEnum from, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            ValidateInputStream(input);
            ValidateOutputStream(output);
            ValidateTarget(to);
            ConversionOptions opts = options ?? _Settings.DefaultOptions;
            InputBuffer buffer = await InputBuffer.FromStreamAsync(input, _Settings.MaxInputBytes, token).ConfigureAwait(false);
            PipelineOutput result = await RunAsync(buffer, from, to, opts, null, token).ConfigureAwait(false);
            return await DeliverToStreamAsync(result, output, opts, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Convert string input, returning the output as a string. Binary targets are returned as base64 with IsBase64 set.
        /// </summary>
        /// <param name="input">Document text for text based formats, or base64 for binary formats.</param>
        /// <param name="from">Source format, or Auto to detect.</param>
        /// <param name="to">Target format. Must not be Auto.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversion result carrying the output.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        /// <exception cref="ArgumentException">Thrown when to is Auto.</exception>
        /// <exception cref="UnsupportedFormatException">Thrown when detection fails or finds an unsupported format.</exception>
        /// <exception cref="ConversionNotSupportedException">Thrown when no registered reader or writer covers the pair.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input exceeds Settings.MaxInputBytes.</exception>
        /// <exception cref="DocumentReadException">Thrown when the input cannot be read.</exception>
        /// <exception cref="DocumentWriteException">Thrown when the output cannot be produced.</exception>
        /// <exception cref="ConversionWarningException">Thrown when TreatWarningsAsErrors is set and warnings were raised.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public async Task<StringConversionResult> ConvertToStringAsync(string input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            ValidateTarget(to);
            ConversionOptions opts = options ?? _Settings.DefaultOptions;
            InputBuffer buffer = PrepareString(input, ref from);
            PipelineOutput result = await RunAsync(buffer, from, to, opts, null, token).ConfigureAwait(false);
            return DeliverAsString(result, opts);
        }

        /// <summary>
        /// Convert byte array input, returning the output as a string. Binary targets are returned as base64 with IsBase64 set.
        /// </summary>
        /// <param name="input">Document bytes.</param>
        /// <param name="from">Source format, or Auto to detect.</param>
        /// <param name="to">Target format. Must not be Auto.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversion result carrying the output.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        /// <exception cref="ArgumentException">Thrown when to is Auto.</exception>
        /// <exception cref="UnsupportedFormatException">Thrown when detection fails or finds an unsupported format.</exception>
        /// <exception cref="ConversionNotSupportedException">Thrown when no registered reader or writer covers the pair.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input exceeds Settings.MaxInputBytes.</exception>
        /// <exception cref="DocumentReadException">Thrown when the input cannot be read.</exception>
        /// <exception cref="DocumentWriteException">Thrown when the output cannot be produced.</exception>
        /// <exception cref="ConversionWarningException">Thrown when TreatWarningsAsErrors is set and warnings were raised.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public async Task<StringConversionResult> ConvertToStringAsync(byte[] input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            ValidateTarget(to);
            ConversionOptions opts = options ?? _Settings.DefaultOptions;
            InputBuffer buffer = InputBuffer.FromBytes(input, _Settings.MaxInputBytes);
            PipelineOutput result = await RunAsync(buffer, from, to, opts, null, token).ConfigureAwait(false);
            return DeliverAsString(result, opts);
        }

        /// <summary>
        /// Convert stream input, returning the output as a string. Binary targets are returned as base64 with IsBase64 set.
        /// </summary>
        /// <param name="input">Readable stream, read from its current position. Not closed.</param>
        /// <param name="from">Source format, or Auto to detect.</param>
        /// <param name="to">Target format. Must not be Auto.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversion result carrying the output.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        /// <exception cref="ArgumentException">Thrown when to is Auto or input is not readable.</exception>
        /// <exception cref="UnsupportedFormatException">Thrown when detection fails or finds an unsupported format.</exception>
        /// <exception cref="ConversionNotSupportedException">Thrown when no registered reader or writer covers the pair.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input exceeds Settings.MaxInputBytes.</exception>
        /// <exception cref="DocumentReadException">Thrown when the input cannot be read.</exception>
        /// <exception cref="DocumentWriteException">Thrown when the output cannot be produced.</exception>
        /// <exception cref="ConversionWarningException">Thrown when TreatWarningsAsErrors is set and warnings were raised.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public async Task<StringConversionResult> ConvertToStringAsync(Stream input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            ValidateInputStream(input);
            ValidateTarget(to);
            ConversionOptions opts = options ?? _Settings.DefaultOptions;
            InputBuffer buffer = await InputBuffer.FromStreamAsync(input, _Settings.MaxInputBytes, token).ConfigureAwait(false);
            PipelineOutput result = await RunAsync(buffer, from, to, opts, null, token).ConfigureAwait(false);
            return DeliverAsString(result, opts);
        }

        /// <summary>
        /// Convert string input, returning the output as bytes. Text targets are encoded with OutputEncoding.
        /// </summary>
        /// <param name="input">Document text for text based formats, or base64 for binary formats.</param>
        /// <param name="from">Source format, or Auto to detect.</param>
        /// <param name="to">Target format. Must not be Auto.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversion result carrying the output.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        /// <exception cref="ArgumentException">Thrown when to is Auto.</exception>
        /// <exception cref="UnsupportedFormatException">Thrown when detection fails or finds an unsupported format.</exception>
        /// <exception cref="ConversionNotSupportedException">Thrown when no registered reader or writer covers the pair.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input exceeds Settings.MaxInputBytes.</exception>
        /// <exception cref="DocumentReadException">Thrown when the input cannot be read.</exception>
        /// <exception cref="DocumentWriteException">Thrown when the output cannot be produced.</exception>
        /// <exception cref="ConversionWarningException">Thrown when TreatWarningsAsErrors is set and warnings were raised.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public async Task<BytesConversionResult> ConvertToBytesAsync(string input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            ValidateTarget(to);
            ConversionOptions opts = options ?? _Settings.DefaultOptions;
            InputBuffer buffer = PrepareString(input, ref from);
            PipelineOutput result = await RunAsync(buffer, from, to, opts, null, token).ConfigureAwait(false);
            return DeliverAsBytes(result, opts);
        }

        /// <summary>
        /// Convert byte array input, returning the output as bytes. Text targets are encoded with OutputEncoding.
        /// </summary>
        /// <param name="input">Document bytes.</param>
        /// <param name="from">Source format, or Auto to detect.</param>
        /// <param name="to">Target format. Must not be Auto.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversion result carrying the output.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        /// <exception cref="ArgumentException">Thrown when to is Auto.</exception>
        /// <exception cref="UnsupportedFormatException">Thrown when detection fails or finds an unsupported format.</exception>
        /// <exception cref="ConversionNotSupportedException">Thrown when no registered reader or writer covers the pair.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input exceeds Settings.MaxInputBytes.</exception>
        /// <exception cref="DocumentReadException">Thrown when the input cannot be read.</exception>
        /// <exception cref="DocumentWriteException">Thrown when the output cannot be produced.</exception>
        /// <exception cref="ConversionWarningException">Thrown when TreatWarningsAsErrors is set and warnings were raised.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public async Task<BytesConversionResult> ConvertToBytesAsync(byte[] input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            ValidateTarget(to);
            ConversionOptions opts = options ?? _Settings.DefaultOptions;
            InputBuffer buffer = InputBuffer.FromBytes(input, _Settings.MaxInputBytes);
            PipelineOutput result = await RunAsync(buffer, from, to, opts, null, token).ConfigureAwait(false);
            return DeliverAsBytes(result, opts);
        }

        /// <summary>
        /// Convert stream input, returning the output as bytes. Text targets are encoded with OutputEncoding.
        /// </summary>
        /// <param name="input">Readable stream, read from its current position. Not closed.</param>
        /// <param name="from">Source format, or Auto to detect.</param>
        /// <param name="to">Target format. Must not be Auto.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversion result carrying the output.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        /// <exception cref="ArgumentException">Thrown when to is Auto or input is not readable.</exception>
        /// <exception cref="UnsupportedFormatException">Thrown when detection fails or finds an unsupported format.</exception>
        /// <exception cref="ConversionNotSupportedException">Thrown when no registered reader or writer covers the pair.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input exceeds Settings.MaxInputBytes.</exception>
        /// <exception cref="DocumentReadException">Thrown when the input cannot be read.</exception>
        /// <exception cref="DocumentWriteException">Thrown when the output cannot be produced.</exception>
        /// <exception cref="ConversionWarningException">Thrown when TreatWarningsAsErrors is set and warnings were raised.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public async Task<BytesConversionResult> ConvertToBytesAsync(Stream input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            ValidateInputStream(input);
            ValidateTarget(to);
            ConversionOptions opts = options ?? _Settings.DefaultOptions;
            InputBuffer buffer = await InputBuffer.FromStreamAsync(input, _Settings.MaxInputBytes, token).ConfigureAwait(false);
            PipelineOutput result = await RunAsync(buffer, from, to, opts, null, token).ConfigureAwait(false);
            return DeliverAsBytes(result, opts);
        }

        /// <summary>
        /// Convert a file to another file. With Auto, the source format is detected from the content (using the input
        /// extension as a hint) and the target format is taken from the output extension. The output file is written only
        /// after the conversion succeeds.
        /// </summary>
        /// <param name="inputPath">Input file path.</param>
        /// <param name="outputPath">Output file path.</param>
        /// <param name="from">Source format, or Auto.</param>
        /// <param name="to">Target format, or Auto to use the output extension.</param>
        /// <param name="overwrite">When false, an existing output file is an error.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversion result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a path is null.</exception>
        /// <exception cref="ArgumentException">Thrown when to is Auto and the output extension is not a known format.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the input file does not exist.</exception>
        /// <exception cref="IOException">Thrown when the output exists and overwrite is false, or on other I/O failures.</exception>
        /// <exception cref="UnsupportedFormatException">Thrown when detection fails or finds an unsupported format.</exception>
        /// <exception cref="DocumentReadException">Thrown when the input cannot be read.</exception>
        /// <exception cref="DocumentWriteException">Thrown when the output cannot be produced.</exception>
        /// <exception cref="ConversionWarningException">Thrown after writing when TreatWarningsAsErrors is set and warnings were raised.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public async Task<ConversionResult> ConvertFileAsync(string inputPath, string outputPath, DocumentFormatEnum from = DocumentFormatEnum.Auto, DocumentFormatEnum to = DocumentFormatEnum.Auto, bool overwrite = false, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (inputPath == null) throw new ArgumentNullException(nameof(inputPath));
            if (outputPath == null) throw new ArgumentNullException(nameof(outputPath));
            if (to == DocumentFormatEnum.Auto)
            {
                DocumentFormatEnum? fromExtension = DocumentFormatParser.FromExtension(outputPath);
                if (!fromExtension.HasValue)
                    throw new ArgumentException("The target format is Auto and the output file extension of '" + outputPath + "' is not a known format.", nameof(to));
                to = fromExtension.Value;
            }

            if (!File.Exists(inputPath)) throw new FileNotFoundException("The input file was not found.", inputPath);
            if (File.Exists(outputPath) && !overwrite) throw new IOException("The output file '" + outputPath + "' already exists. Pass overwrite to replace it.");

            ConversionOptions opts = options ?? _Settings.DefaultOptions;
            InputBuffer buffer;
            using (FileStream fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            {
                buffer = await InputBuffer.FromStreamAsync(fs, _Settings.MaxInputBytes, token).ConfigureAwait(false);
            }

            PipelineOutput result = await RunAsync(buffer, from, to, opts, inputPath, token).ConfigureAwait(false);
            using (FileStream outStream = new FileStream(outputPath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                return await DeliverToStreamAsync(result, outStream, opts, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Read string input into a document model without writing it anywhere.
        /// </summary>
        /// <param name="input">Document text for text based formats, or base64 for binary formats.</param>
        /// <param name="from">Source format, or Auto to detect.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The document model.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        /// <exception cref="UnsupportedFormatException">Thrown when detection fails or finds an unsupported format.</exception>
        /// <exception cref="ConversionNotSupportedException">Thrown when no registered reader covers the format.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input exceeds Settings.MaxInputBytes.</exception>
        /// <exception cref="DocumentReadException">Thrown when the input cannot be read.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public Task<DocumentModel> ReadAsync(string input, DocumentFormatEnum from, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            InputBuffer buffer = PrepareString(input, ref from);
            return ReadOnlyAsync(buffer, from, options ?? _Settings.DefaultOptions, token);
        }

        /// <summary>
        /// Read byte array input into a document model without writing it anywhere.
        /// </summary>
        /// <param name="input">Document bytes.</param>
        /// <param name="from">Source format, or Auto to detect.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The document model.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        /// <exception cref="UnsupportedFormatException">Thrown when detection fails or finds an unsupported format.</exception>
        /// <exception cref="ConversionNotSupportedException">Thrown when no registered reader covers the format.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input exceeds Settings.MaxInputBytes.</exception>
        /// <exception cref="DocumentReadException">Thrown when the input cannot be read.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public Task<DocumentModel> ReadAsync(byte[] input, DocumentFormatEnum from, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            InputBuffer buffer = InputBuffer.FromBytes(input, _Settings.MaxInputBytes);
            return ReadOnlyAsync(buffer, from, options ?? _Settings.DefaultOptions, token);
        }

        /// <summary>
        /// Read stream input into a document model without writing it anywhere. The stream is not closed.
        /// </summary>
        /// <param name="input">Readable stream, read from its current position.</param>
        /// <param name="from">Source format, or Auto to detect.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The document model.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        /// <exception cref="ArgumentException">Thrown when input is not readable.</exception>
        /// <exception cref="UnsupportedFormatException">Thrown when detection fails or finds an unsupported format.</exception>
        /// <exception cref="ConversionNotSupportedException">Thrown when no registered reader covers the format.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input exceeds Settings.MaxInputBytes.</exception>
        /// <exception cref="DocumentReadException">Thrown when the input cannot be read.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public async Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum from, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            ValidateInputStream(input);
            InputBuffer buffer = await InputBuffer.FromStreamAsync(input, _Settings.MaxInputBytes, token).ConfigureAwait(false);
            return await ReadOnlyAsync(buffer, from, options ?? _Settings.DefaultOptions, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a document model into a caller-owned stream. The document is not modified; option driven changes are made
        /// on a copy. The result's SourceFormat is Auto because no source was read.
        /// </summary>
        /// <param name="document">Document to write.</param>
        /// <param name="to">Target format. Must not be Auto.</param>
        /// <param name="output">Writable stream. Not closed.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversion result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when document or output is null.</exception>
        /// <exception cref="ArgumentException">Thrown when to is Auto or output is not writable.</exception>
        /// <exception cref="ConversionNotSupportedException">Thrown when no registered writer covers the format.</exception>
        /// <exception cref="DocumentWriteException">Thrown when the output cannot be produced.</exception>
        /// <exception cref="ConversionWarningException">Thrown after writing when TreatWarningsAsErrors is set and warnings were raised.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public async Task<ConversionResult> WriteAsync(DocumentModel document, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            ValidateOutputStream(output);
            ValidateTarget(to);
            ConversionOptions opts = options ?? _Settings.DefaultOptions;
            PipelineOutput result = await WriteOnlyAsync(document, to, opts, token).ConfigureAwait(false);
            return await DeliverToStreamAsync(result, output, opts, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a document model, returning a string (base64 for binary targets). The document is not modified.
        /// </summary>
        /// <param name="document">Document to write.</param>
        /// <param name="to">Target format. Must not be Auto.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversion result carrying the output.</returns>
        /// <exception cref="ArgumentNullException">Thrown when document is null.</exception>
        /// <exception cref="ArgumentException">Thrown when to is Auto.</exception>
        /// <exception cref="ConversionNotSupportedException">Thrown when no registered writer covers the format.</exception>
        /// <exception cref="DocumentWriteException">Thrown when the output cannot be produced.</exception>
        /// <exception cref="ConversionWarningException">Thrown when TreatWarningsAsErrors is set and warnings were raised.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public async Task<StringConversionResult> WriteToStringAsync(DocumentModel document, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            ValidateTarget(to);
            ConversionOptions opts = options ?? _Settings.DefaultOptions;
            PipelineOutput result = await WriteOnlyAsync(document, to, opts, token).ConfigureAwait(false);
            return DeliverAsString(result, opts);
        }

        /// <summary>
        /// Write a document model, returning bytes. The document is not modified.
        /// </summary>
        /// <param name="document">Document to write.</param>
        /// <param name="to">Target format. Must not be Auto.</param>
        /// <param name="options">Options. Null uses Settings.DefaultOptions.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversion result carrying the output.</returns>
        /// <exception cref="ArgumentNullException">Thrown when document is null.</exception>
        /// <exception cref="ArgumentException">Thrown when to is Auto.</exception>
        /// <exception cref="ConversionNotSupportedException">Thrown when no registered writer covers the format.</exception>
        /// <exception cref="DocumentWriteException">Thrown when the output cannot be produced.</exception>
        /// <exception cref="ConversionWarningException">Thrown when TreatWarningsAsErrors is set and warnings were raised.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public async Task<BytesConversionResult> WriteToBytesAsync(DocumentModel document, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            ValidateTarget(to);
            ConversionOptions opts = options ?? _Settings.DefaultOptions;
            PipelineOutput result = await WriteOnlyAsync(document, to, opts, token).ConfigureAwait(false);
            return DeliverAsBytes(result, opts);
        }

        /// <summary>
        /// Detect the format of byte array content.
        /// </summary>
        /// <param name="input">Content.</param>
        /// <param name="fileNameHint">Optional file name whose extension breaks ties between text formats.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Detection result. Format is null when unsupported; RecognizedAs says what it is.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input exceeds Settings.MaxInputBytes.</exception>
        public Task<DetectionResult> DetectFormatAsync(byte[] input, string? fileNameHint = null, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            token.ThrowIfCancellationRequested();
            InputBuffer buffer = InputBuffer.FromBytes(input, _Settings.MaxInputBytes);
            return Task.FromResult(FormatDetector.Detect(buffer.Data, buffer.Offset, buffer.Length, fileNameHint, _Settings.DetectionBufferBytes));
        }

        /// <summary>
        /// Detect the format of stream content. A seekable stream is returned to its original position; a non-seekable
        /// stream is consumed.
        /// </summary>
        /// <param name="input">Readable stream.</param>
        /// <param name="fileNameHint">Optional file name whose extension breaks ties between text formats.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Detection result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        /// <exception cref="ArgumentException">Thrown when input is not readable.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input exceeds Settings.MaxInputBytes.</exception>
        public async Task<DetectionResult> DetectFormatAsync(Stream input, string? fileNameHint = null, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            ValidateInputStream(input);
            long position = input.CanSeek ? input.Position : 0;
            try
            {
                InputBuffer buffer = await InputBuffer.FromStreamAsync(input, _Settings.MaxInputBytes, token).ConfigureAwait(false);
                return FormatDetector.Detect(buffer.Data, buffer.Offset, buffer.Length, fileNameHint, _Settings.DetectionBufferBytes);
            }
            finally
            {
                if (input.CanSeek) input.Position = position;
            }
        }

        /// <summary>
        /// Detect the format of string content. A string that is valid base64 of a binary document is detected as that
        /// document; otherwise the text itself is classified.
        /// </summary>
        /// <param name="input">Content.</param>
        /// <param name="fileNameHint">Optional file name whose extension breaks ties between text formats.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Detection result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input exceeds Settings.MaxInputBytes.</exception>
        public Task<DetectionResult> DetectFormatAsync(string input, string? fileNameHint = null, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            token.ThrowIfCancellationRequested();
            DocumentFormatEnum from = DocumentFormatEnum.Auto;
            InputBuffer buffer = PrepareString(input, ref from);
            return Task.FromResult(FormatDetector.Detect(buffer.Data, buffer.Offset, buffer.Length, fileNameHint, _Settings.DetectionBufferBytes));
        }

        /// <summary>
        /// True when a registered reader covers from and a registered writer covers to. Auto as the source is true when any
        /// reader is registered. Auto as the target is always false.
        /// </summary>
        /// <param name="from">Source format.</param>
        /// <param name="to">Target format.</param>
        /// <returns>True when convertible.</returns>
        public bool CanConvert(DocumentFormatEnum from, DocumentFormatEnum to)
        {
            if (to == DocumentFormatEnum.Auto) return false;
            if (_Registry.GetWriter(to) == null) return false;
            if (from == DocumentFormatEnum.Auto) return _Registry.GetReaderFormats().Count > 0;
            return _Registry.GetReader(from) != null;
        }

        /// <summary>
        /// Every supported pair (registered reader times registered writer) with its fidelity. Pairs involving
        /// caller-registered formats report Full fidelity with a note, since their behavior is the caller's.
        /// </summary>
        /// <returns>Supported pairs.</returns>
        public IReadOnlyList<SupportedConversion> GetSupportedConversions()
        {
            List<SupportedConversion> pairs = new List<SupportedConversion>();
            foreach (DocumentFormatEnum from in _Registry.GetReaderFormats())
            {
                foreach (DocumentFormatEnum to in _Registry.GetWriterFormats())
                {
                    if (CapabilityMatrix.TryGet(from, to, out FidelityEnum fidelity, out string notes))
                        pairs.Add(new SupportedConversion(from, to, fidelity, notes));
                    else
                        pairs.Add(new SupportedConversion(from, to, FidelityEnum.Full, "Provided by a caller-registered reader or writer."));
                }
            }

            return pairs;
        }

        /// <summary>
        /// Formats that can be read.
        /// </summary>
        /// <returns>Input formats.</returns>
        public IReadOnlyList<DocumentFormatEnum> GetInputFormats()
        {
            return _Registry.GetReaderFormats();
        }

        /// <summary>
        /// Formats that can be written.
        /// </summary>
        /// <returns>Output formats.</returns>
        public IReadOnlyList<DocumentFormatEnum> GetOutputFormats()
        {
            return _Registry.GetWriterFormats();
        }

        /// <summary>
        /// Register a reader, replacing any reader registered for the same formats.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <exception cref="ArgumentNullException">Thrown when reader is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the reader declares no formats or declares Auto.</exception>
        public void RegisterReader(IDocumentReader reader)
        {
            _Registry.RegisterReader(reader);
        }

        /// <summary>
        /// Register a writer, replacing any writer registered for the same formats.
        /// </summary>
        /// <param name="writer">Writer.</param>
        /// <exception cref="ArgumentNullException">Thrown when writer is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the writer declares no formats or declares Auto.</exception>
        public void RegisterWriter(IDocumentWriter writer)
        {
            _Registry.RegisterWriter(writer);
        }

        /// <summary>
        /// Release resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Release resources.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing) _Registry.Dispose();
            _Disposed = true;
        }

        private static void ValidateTarget(DocumentFormatEnum to)
        {
            if (to == DocumentFormatEnum.Auto) throw new ArgumentException("The target format cannot be Auto.", nameof(to));
        }

        private static void ValidateOutputStream(Stream output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (!output.CanWrite) throw new ArgumentException("The output stream is not writable.", nameof(output));
        }

        private static void ValidateInputStream(Stream input)
        {
            if (!input.CanRead) throw new ArgumentException("The input stream is not readable.", nameof(input));
        }

        private InputBuffer PrepareString(string input, ref DocumentFormatEnum from)
        {
            if (from != DocumentFormatEnum.Auto && !DocumentFormatParser.IsTextBased(from))
            {
                byte[] decoded = DecodeBase64(input, from);
                return InputBuffer.FromBytes(decoded, _Settings.MaxInputBytes);
            }

            byte[] utf8 = new UTF8Encoding(false).GetBytes(input);
            if (from == DocumentFormatEnum.Auto)
            {
                byte[]? binary = TryDecodeBinaryBase64(input);
                if (binary != null) return InputBuffer.FromBytes(binary, _Settings.MaxInputBytes);
            }

            return InputBuffer.FromText(input, utf8, _Settings.MaxInputBytes);
        }

        private byte[]? TryDecodeBinaryBase64(string input)
        {
            string trimmed = input.Trim();
            if (trimmed.Length < 16 || trimmed.Length % 4 != 0) return null;
            foreach (char c in trimmed)
            {
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '=';
                if (!ok) return null;
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(trimmed);
                DetectionResult detection = FormatDetector.Detect(bytes, 0, bytes.Length, null, _Settings.DetectionBufferBytes);
                if (detection.Format.HasValue && !DocumentFormatParser.IsTextBased(detection.Format.Value)) return bytes;
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static byte[] DecodeBase64(string input, DocumentFormatEnum from)
        {
            string trimmed = input.Trim();
            int comma = trimmed.IndexOf(',');
            if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0 && trimmed.Substring(0, comma).EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(comma + 1);

            try
            {
                return Convert.FromBase64String(trimmed);
            }
            catch (FormatException ex)
            {
                throw new DocumentReadException("String input for the binary format " + from + " must be base64 encoded, and it is not valid base64.", ex);
            }
        }

        private async Task<PipelineOutput> RunAsync(InputBuffer buffer, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions options, string? fileNameHint, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            OcrStage.EnsureSupported(_Settings.OcrProvider, options);

            ConversionResult result = new ConversionResult();
            result.StartedUtc = DateTime.UtcNow;
            Stopwatch total = Stopwatch.StartNew();
            ConversionContext context = new ConversionContext(_Settings);
            context.SourceText = buffer.Text;
            context.SourceFileName = fileNameHint;

            string fromTag = from.ToString();
            string toTag = to.ToString();
            using (Activity? activity = DocConverterDiagnostics.StartConvert(fromTag, toTag))
            {
                try
                {
                    if (from == DocumentFormatEnum.Auto)
                    {
                        DetectionResult detection = FormatDetector.Detect(buffer.Data, buffer.Offset, buffer.Length, fileNameHint, _Settings.DetectionBufferBytes);
                        result.DetectedSource = detection;
                        if (!detection.Format.HasValue)
                            throw new UnsupportedFormatException("The input was recognized as " + detection.RecognizedAs + ", which DocConverter cannot read.");
                        from = detection.Format.Value;
                        fromTag = from.ToString();
                        activity?.SetTag("docconverter.from", fromTag);
                    }

                    IDocumentReader reader = _Registry.GetReader(from)
                        ?? throw new ConversionNotSupportedException("No reader is registered for " + from + ".");
                    IDocumentWriter writer = _Registry.GetWriter(to)
                        ?? throw new ConversionNotSupportedException("No writer is registered for " + to + ".");

                    context.SourceFormat = from;
                    context.TargetFormat = to;
                    if (context.SourceText != null && !DocumentFormatParser.IsTextBased(from)) context.SourceText = null;

                    Stopwatch readWatch = Stopwatch.StartNew();
                    DocumentModel document = await ReadModelAsync(reader, buffer, from, options, context, token).ConfigureAwait(false);
                    readWatch.Stop();

                    ModelTransforms.Apply(document, options, context);

                    Stopwatch writeWatch = Stopwatch.StartNew();
                    MemoryStream output = await WriteModelAsync(writer, document, to, options, context, token).ConfigureAwait(false);
                    writeWatch.Stop();
                    total.Stop();

                    result.SourceFormat = from;
                    result.TargetFormat = to;
                    result.BytesRead = buffer.Length;
                    result.BytesWritten = output.Length;
                    result.ReadMs = readWatch.Elapsed.TotalMilliseconds;
                    result.WriteMs = writeWatch.Elapsed.TotalMilliseconds;
                    result.TotalMs = total.Elapsed.TotalMilliseconds;
                    result.CompletedUtc = DateTime.UtcNow;
                    result.Statistics = ModelStatistics.Compute(document);
                    result.Metadata = document.Metadata;
                    result.SetWarnings(context.Warnings);
                    result.SetResources(context.OutputResources);

                    DocConverterDiagnostics.RecordConversion(fromTag, toTag, "success", result.TotalMs, buffer.Length);
                    foreach (ConversionWarning warning in context.Warnings) DocConverterDiagnostics.RecordWarning(warning.Code.ToString(), warning.Count);
                    context.Log(SeverityEnum.Debug, "converted " + from + " to " + to + " in " + result.TotalMs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " ms");
                    return new PipelineOutput(result, output);
                }
                catch (Exception ex)
                {
                    string outcome = ex is OperationCanceledException ? "cancelled" : "failure";
                    DocConverterDiagnostics.RecordConversion(fromTag, toTag, outcome, total.Elapsed.TotalMilliseconds, buffer.Length);
                    activity?.SetTag("error", true);
                    context.Log(SeverityEnum.Error, "conversion " + fromTag + " to " + toTag + " failed: " + ex.Message);
                    throw;
                }
            }
        }

        private async Task<DocumentModel> ReadOnlyAsync(InputBuffer buffer, DocumentFormatEnum from, ConversionOptions options, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            OcrStage.EnsureSupported(_Settings.OcrProvider, options);
            ConversionContext context = new ConversionContext(_Settings);
            context.SourceText = buffer.Text;

            if (from == DocumentFormatEnum.Auto)
            {
                DetectionResult detection = FormatDetector.Detect(buffer.Data, buffer.Offset, buffer.Length, null, _Settings.DetectionBufferBytes);
                if (!detection.Format.HasValue)
                    throw new UnsupportedFormatException("The input was recognized as " + detection.RecognizedAs + ", which DocConverter cannot read.");
                from = detection.Format.Value;
            }

            IDocumentReader reader = _Registry.GetReader(from)
                ?? throw new ConversionNotSupportedException("No reader is registered for " + from + ".");
            context.SourceFormat = from;
            if (context.SourceText != null && !DocumentFormatParser.IsTextBased(from)) context.SourceText = null;
            return await ReadModelAsync(reader, buffer, from, options, context, token).ConfigureAwait(false);
        }

        private async Task<PipelineOutput> WriteOnlyAsync(DocumentModel document, DocumentFormatEnum to, ConversionOptions options, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            IDocumentWriter writer = _Registry.GetWriter(to)
                ?? throw new ConversionNotSupportedException("No writer is registered for " + to + ".");

            ConversionResult result = new ConversionResult();
            result.StartedUtc = DateTime.UtcNow;
            Stopwatch watch = Stopwatch.StartNew();
            ConversionContext context = new ConversionContext(_Settings);
            context.SourceFormat = DocumentFormatEnum.Auto;
            context.TargetFormat = to;

            DocumentModel copy = CanonicalMapper.Clone(document);
            ModelTransforms.Apply(copy, options, context);
            MemoryStream output = await WriteModelAsync(writer, copy, to, options, context, token).ConfigureAwait(false);
            watch.Stop();

            result.SourceFormat = DocumentFormatEnum.Auto;
            result.TargetFormat = to;
            result.BytesWritten = output.Length;
            result.WriteMs = watch.Elapsed.TotalMilliseconds;
            result.TotalMs = watch.Elapsed.TotalMilliseconds;
            result.CompletedUtc = DateTime.UtcNow;
            result.Statistics = ModelStatistics.Compute(copy);
            result.Metadata = copy.Metadata;
            result.SetWarnings(context.Warnings);
            result.SetResources(context.OutputResources);
            return new PipelineOutput(result, output);
        }

        private static async Task<DocumentModel> ReadModelAsync(IDocumentReader reader, InputBuffer buffer, DocumentFormatEnum from, ConversionOptions options, ConversionContext context, CancellationToken token)
        {
            using (MemoryStream input = buffer.CreateStream())
            {
                try
                {
                    DocumentModel? document = await reader.ReadAsync(input, from, options, context, token).ConfigureAwait(false);
                    if (document == null) throw new DocumentReadException("The " + from + " reader returned no document.");
                    return document;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (DocConverterException)
                {
                    throw;
                }
                catch (NotImplementedException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new DocumentReadException("The " + from + " input could not be read: " + ex.Message, ex);
                }
            }
        }

        private static async Task<MemoryStream> WriteModelAsync(IDocumentWriter writer, DocumentModel document, DocumentFormatEnum to, ConversionOptions options, ConversionContext context, CancellationToken token)
        {
            MemoryStream output = new MemoryStream();
            try
            {
                await writer.WriteAsync(document, output, to, options, context, token).ConfigureAwait(false);
                output.Position = 0;
                return output;
            }
            catch (OperationCanceledException)
            {
                output.Dispose();
                throw;
            }
            catch (DocConverterException)
            {
                output.Dispose();
                throw;
            }
            catch (NotImplementedException)
            {
                output.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                output.Dispose();
                throw new DocumentWriteException("The " + to + " output could not be written: " + ex.Message, ex);
            }
        }

        private static async Task<ConversionResult> DeliverToStreamAsync(PipelineOutput pipeline, Stream output, ConversionOptions options, CancellationToken token)
        {
            using (MemoryStream produced = pipeline.Output)
            {
                produced.Position = 0;
                await produced.CopyToAsync(output, 81920, token).ConfigureAwait(false);
                await output.FlushAsync(token).ConfigureAwait(false);
            }

            ThrowIfWarnings(pipeline.Result, options);
            return pipeline.Result;
        }

        private static StringConversionResult DeliverAsString(PipelineOutput pipeline, ConversionOptions options)
        {
            StringConversionResult result = new StringConversionResult();
            result.CopyFrom(pipeline.Result);
            using (MemoryStream produced = pipeline.Output)
            {
                byte[] bytes = produced.ToArray();
                if (DocumentFormatParser.IsTextBased(pipeline.Result.TargetFormat))
                {
                    result.Output = TextEncodingDetector.Decode(bytes, 0, bytes.Length, options.OutputEncoding);
                    result.IsBase64 = false;
                }
                else
                {
                    result.Output = Convert.ToBase64String(bytes);
                    result.IsBase64 = true;
                }
            }

            ThrowIfWarnings(result, options);
            return result;
        }

        private static BytesConversionResult DeliverAsBytes(PipelineOutput pipeline, ConversionOptions options)
        {
            BytesConversionResult result = new BytesConversionResult();
            result.CopyFrom(pipeline.Result);
            using (MemoryStream produced = pipeline.Output)
            {
                result.Output = produced.ToArray();
            }

            ThrowIfWarnings(result, options);
            return result;
        }

        private static void ThrowIfWarnings(ConversionResult result, ConversionOptions options)
        {
            if (!options.TreatWarningsAsErrors || result.Warnings.Count == 0) return;
            List<string> parts = new List<string>();
            foreach (ConversionWarning w in result.Warnings) parts.Add(w.ToString());
            throw new ConversionWarningException("The conversion raised warnings and TreatWarningsAsErrors is set: " + string.Join("; ", parts), result);
        }

        #endregion
    }
}
