namespace DocConverter
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocConverter.Readers;
    using DocConverter.Results;
    using DocConverter.Writers;

    /// <summary>
    /// Converts documents between formats. See Converter for the full contract of each member.
    /// </summary>
    public interface IConverter
    {
        /// <summary>
        /// Instance settings.
        /// </summary>
        ConverterSettings Settings { get; }

        /// <summary>
        /// Convert string input, writing into a caller-owned stream.
        /// </summary>
        Task<ConversionResult> ConvertAsync(string input, DocumentFormatEnum from, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Convert byte array input, writing into a caller-owned stream.
        /// </summary>
        Task<ConversionResult> ConvertAsync(byte[] input, DocumentFormatEnum from, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Convert stream input, writing into a caller-owned stream.
        /// </summary>
        Task<ConversionResult> ConvertAsync(Stream input, DocumentFormatEnum from, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Convert string input, returning a string.
        /// </summary>
        Task<StringConversionResult> ConvertToStringAsync(string input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Convert byte array input, returning a string.
        /// </summary>
        Task<StringConversionResult> ConvertToStringAsync(byte[] input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Convert stream input, returning a string.
        /// </summary>
        Task<StringConversionResult> ConvertToStringAsync(Stream input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Convert string input, returning bytes.
        /// </summary>
        Task<BytesConversionResult> ConvertToBytesAsync(string input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Convert byte array input, returning bytes.
        /// </summary>
        Task<BytesConversionResult> ConvertToBytesAsync(byte[] input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Convert stream input, returning bytes.
        /// </summary>
        Task<BytesConversionResult> ConvertToBytesAsync(Stream input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Convert a file to another file.
        /// </summary>
        Task<ConversionResult> ConvertFileAsync(string inputPath, string outputPath, DocumentFormatEnum from = DocumentFormatEnum.Auto, DocumentFormatEnum to = DocumentFormatEnum.Auto, bool overwrite = false, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Read string input into a document model.
        /// </summary>
        Task<DocumentModel> ReadAsync(string input, DocumentFormatEnum from, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Read byte array input into a document model.
        /// </summary>
        Task<DocumentModel> ReadAsync(byte[] input, DocumentFormatEnum from, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Read stream input into a document model.
        /// </summary>
        Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum from, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Write a document model into a caller-owned stream.
        /// </summary>
        Task<ConversionResult> WriteAsync(DocumentModel document, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Write a document model, returning a string.
        /// </summary>
        Task<StringConversionResult> WriteToStringAsync(DocumentModel document, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Write a document model, returning bytes.
        /// </summary>
        Task<BytesConversionResult> WriteToBytesAsync(DocumentModel document, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);

        /// <summary>
        /// Detect the format of byte array content.
        /// </summary>
        Task<DetectionResult> DetectFormatAsync(byte[] input, string? fileNameHint = null, CancellationToken token = default);

        /// <summary>
        /// Detect the format of stream content without moving a seekable stream's position.
        /// </summary>
        Task<DetectionResult> DetectFormatAsync(Stream input, string? fileNameHint = null, CancellationToken token = default);

        /// <summary>
        /// Detect the format of string content.
        /// </summary>
        Task<DetectionResult> DetectFormatAsync(string input, string? fileNameHint = null, CancellationToken token = default);

        /// <summary>
        /// True when a registered reader and writer cover the pair.
        /// </summary>
        bool CanConvert(DocumentFormatEnum from, DocumentFormatEnum to);

        /// <summary>
        /// Every supported pair with its fidelity.
        /// </summary>
        IReadOnlyList<SupportedConversion> GetSupportedConversions();

        /// <summary>
        /// Formats that can be read.
        /// </summary>
        IReadOnlyList<DocumentFormatEnum> GetInputFormats();

        /// <summary>
        /// Formats that can be written.
        /// </summary>
        IReadOnlyList<DocumentFormatEnum> GetOutputFormats();

        /// <summary>
        /// Register or replace a reader.
        /// </summary>
        void RegisterReader(IDocumentReader reader);

        /// <summary>
        /// Register or replace a writer.
        /// </summary>
        void RegisterWriter(IDocumentWriter writer);
    }
}
