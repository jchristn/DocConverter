namespace DocConverter.Writers
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Options;

    /// <summary>
    /// Writes the document model in a target format. Implementations must be stateless between calls and thread safe.
    /// </summary>
    public interface IDocumentWriter
    {
        /// <summary>
        /// Formats this writer produces.
        /// </summary>
        IReadOnlyList<DocumentFormatEnum> Formats { get; }

        /// <summary>
        /// Write the document.
        /// </summary>
        /// <param name="document">Document to write.</param>
        /// <param name="output">Writable, seekable stream to write into. Not closed by the writer.</param>
        /// <param name="format">Format to produce.</param>
        /// <param name="options">Conversion options.</param>
        /// <param name="context">Conversion context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task WriteAsync(DocumentModel document, Stream output, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default);
    }
}
