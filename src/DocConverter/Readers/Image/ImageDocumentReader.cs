namespace DocConverter.Readers.Image
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Detection;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Options;

    /// <summary>
    /// Reads an image (PNG, JPEG, GIF, BMP, TIFF, WebP) into a document holding that image. Pixel dimensions come from
    /// the image header; nothing is decoded and no text is recognized (no OCR). Stateless and thread safe.
    /// </summary>
    public sealed class ImageDocumentReader : IDocumentReader
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[]
        {
            DocumentFormatEnum.Png, DocumentFormatEnum.Jpeg, DocumentFormatEnum.Gif, DocumentFormatEnum.Bmp, DocumentFormatEnum.Tiff, DocumentFormatEnum.WebP
        };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <summary>
        /// Instantiate the reader.
        /// </summary>
        public ImageDocumentReader()
        {
        }

        /// <inheritdoc />
        public async Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (context == null) throw new ArgumentNullException(nameof(context));
            token.ThrowIfCancellationRequested();

            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            {
                await input.CopyToAsync(ms, 81920, token).ConfigureAwait(false);
                data = ms.ToArray();
            }

            ImageInfo? info = ImageHeaderReader.Read(data);
            if (info == null)
                throw new DocumentReadException("The input is not a readable " + format + " image: the image header is missing or truncated.");
            if (info.Format != format)
                throw new DocumentReadException("The input was declared as " + format + " but its header identifies it as " + info.Format + ".");
            if (!info.Width.HasValue || !info.Height.HasValue || info.Width.Value <= 0 || info.Height.Value <= 0)
                throw new DocumentReadException("The " + format + " image header is truncated: its pixel dimensions could not be read.");

            string extension = ImageHeaderReader.ExtensionFor(format);
            string fileName = !string.IsNullOrEmpty(context.SourceFileName) ? Path.GetFileName(context.SourceFileName!) : "image." + extension;
            string alt = !string.IsNullOrEmpty(context.SourceFileName) ? Path.GetFileNameWithoutExtension(context.SourceFileName!) : "Image";

            DocumentModel model = new DocumentModel();
            BinaryResource resource = new BinaryResource
            {
                MediaType = DocumentFormatParser.GetMediaType(format),
                Data = data,
                FileName = fileName,
                PixelWidth = info.Width,
                PixelHeight = info.Height
            };

            string id = model.AddResource(resource);
            model.Blocks.Add(new ImageBlock(id, alt));
            return model;
        }
    }
}
