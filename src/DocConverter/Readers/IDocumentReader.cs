namespace DocConverter.Readers
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Options;

    /// <summary>
    /// Reads a source format into the document model. Implementations must be stateless between calls and thread safe.
    /// </summary>
    public interface IDocumentReader
    {
        /// <summary>
        /// Formats this reader handles.
        /// </summary>
        IReadOnlyList<DocumentFormatEnum> Formats { get; }

        /// <summary>
        /// Read the input into a document model.
        /// </summary>
        /// <param name="input">Seekable input stream positioned at the start of the document. Not closed by the reader.</param>
        /// <param name="format">Format being read.</param>
        /// <param name="options">Conversion options.</param>
        /// <param name="context">Conversion context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The document model.</returns>
        Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default);
    }
}
