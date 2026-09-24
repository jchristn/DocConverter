namespace DocConverter.Readers.Image
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
    /// Reads Png, Jpeg, Gif, Bmp, Tiff, WebP into the document model. Stateless and thread safe.
    /// </summary>
    public sealed class ImageDocumentReader : IDocumentReader
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Png, DocumentFormatEnum.Jpeg, DocumentFormatEnum.Gif, DocumentFormatEnum.Bmp, DocumentFormatEnum.Tiff, DocumentFormatEnum.WebP };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <inheritdoc />
        public Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            throw new NotImplementedException("ImageDocumentReader is not implemented yet.");
        }
    }
}
