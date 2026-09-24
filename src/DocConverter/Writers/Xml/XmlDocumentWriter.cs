namespace DocConverter.Writers.Xml
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Options;

    /// <summary>
    /// Writes the document model as Xml. Stateless and thread safe.
    /// </summary>
    public sealed class XmlDocumentWriter : IDocumentWriter
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Xml };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <inheritdoc />
        public Task WriteAsync(DocumentModel document, Stream output, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            throw new NotImplementedException("XmlDocumentWriter is not implemented yet.");
        }
    }
}
