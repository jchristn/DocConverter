namespace DocConverter.Writers.Pdf
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
    /// Writes the document model as PDF using MigraDoc and PDFsharp, with the embedded Liberation fonts so output is the
    /// same on Windows, macOS and Linux. PNG, BMP and RGB or grayscale JPEG images are embedded; GIF, TIFF, WebP and
    /// CMYK JPEG become placeholders (ImageFormatUnsupported). Characters outside the fonts' coverage render blank
    /// (GlyphsUnavailable). Strikethrough is not drawn (FormattingLost). Stateless and thread safe. PDFsharp keeps
    /// process-wide rendering state that races under concurrency, so PDF rendering is serialized across the process;
    /// concurrent PDF conversions are safe but run one render at a time.
    /// </summary>
    public sealed class PdfDocumentWriter : IDocumentWriter
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Pdf };
        private static readonly object _RenderLock = new object();

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <summary>
        /// Instantiate the writer.
        /// </summary>
        public PdfDocumentWriter()
        {
        }

        /// <inheritdoc />
        public async Task WriteAsync(DocumentModel document, Stream output, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (context == null) throw new ArgumentNullException(nameof(context));
            token.ThrowIfCancellationRequested();

            PdfFontInstaller.EnsureInstalled();
            PdfModelRenderer renderer = new PdfModelRenderer(document, options, context, token);
            byte[] bytes;
            lock (_RenderLock)
            {
                bytes = renderer.Render();
            }

            token.ThrowIfCancellationRequested();
            await output.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
        }
    }
}
