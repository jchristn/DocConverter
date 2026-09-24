namespace DocConverter.Readers.Pptx
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
    /// Reads Pptx into the document model. Stateless and thread safe.
    /// </summary>
    public sealed class PptxDocumentReader : IDocumentReader
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Pptx };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <inheritdoc />
        public Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            throw new NotImplementedException("PptxDocumentReader is not implemented yet.");
        }
    }
}
