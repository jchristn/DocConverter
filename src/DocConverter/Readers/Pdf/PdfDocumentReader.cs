namespace DocConverter.Readers.Pdf
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Model;
    using DocConverter.Options;
    using UglyToad.PdfPig;
    using UglyToad.PdfPig.Content;
    using UglyToad.PdfPig.Exceptions;

    /// <summary>
    /// Reads PDF into the document model with PdfPig and Tabula. PDF has no semantic structure, so structure is inferred:
    /// headings from font size relative to the document's body size (PdfOptions.HeadingSizeRatio), lists from markers,
    /// code from monospace fonts, ruled tables with Tabula (unruled tables arrive as paragraphs), links from link
    /// annotations. Raises HeadingsInferred when headings were inferred and NoTextLayer for pages that are only images.
    /// Encrypted PDFs throw DocumentReadException. Stateless and thread safe.
    /// </summary>
    public sealed class PdfDocumentReader : IDocumentReader
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Pdf };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <summary>
        /// Instantiate the reader.
        /// </summary>
        public PdfDocumentReader()
        {
        }

        /// <inheritdoc />
        public async Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (context == null) throw new ArgumentNullException(nameof(context));
            token.ThrowIfCancellationRequested();

            byte[] bytes;
            using (MemoryStream ms = new MemoryStream())
            {
                await input.CopyToAsync(ms, 81920, token).ConfigureAwait(false);
                bytes = ms.ToArray();
            }

            if (bytes.Length == 0) throw new DocumentReadException("The PDF input is empty.");

            PdfDocument document;
            try
            {
                document = PdfDocument.Open(bytes, new ParsingOptions { ClipPaths = true, UseLenientParsing = true, SkipMissingFonts = true });
            }
            catch (PdfDocumentEncryptedException ex)
            {
                throw new DocumentReadException("The PDF is encrypted or password protected and cannot be read without the password.", ex);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new DocumentReadException("The PDF could not be opened; it is corrupt, truncated or not a PDF: " + ex.Message, ex);
            }

            using (document)
            {
                try
                {
                    return Read(document, options, context, token);
                }
                catch (PdfDocumentEncryptedException ex)
                {
                    throw new DocumentReadException("The PDF is encrypted or password protected and cannot be read without the password.", ex);
                }
            }
        }

        private static DocumentModel Read(PdfDocument document, ConversionOptions options, ConversionContext context, CancellationToken token)
        {
            DocumentModel model = new DocumentModel();
            ReadMetadata(document, model);

            double bodySize = BodySize(document, token);
            bool headings = false;
            for (int pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
            {
                token.ThrowIfCancellationRequested();
                Page page = document.GetPage(pageNumber);
                PdfPageReader reader = new PdfPageReader(document, page, model, options, context, bodySize);
                List<PdfPageElement> elements = reader.Read();
                if (reader.HeadingsInferred) headings = true;

                if (options.Pdf.PreservePages)
                {
                    SectionBlock section = new SectionBlock(SectionKindEnum.Page, null);
                    section.SourcePage = pageNumber;
                    foreach (PdfPageElement element in elements) section.Blocks.Add(element.Block);
                    model.Blocks.Add(section);
                }
                else
                {
                    foreach (PdfPageElement element in elements) model.Blocks.Add(element.Block);
                }
            }

            if (headings)
                context.AddWarning(WarningCodeEnum.HeadingsInferred, "Headings were inferred from font size because PDF does not declare document structure.");
            return model;
        }

        private static double BodySize(PdfDocument document, CancellationToken token)
        {
            Dictionary<double, int> counts = new Dictionary<double, int>();
            for (int pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
            {
                token.ThrowIfCancellationRequested();
                Page page = document.GetPage(pageNumber);
                foreach (Letter letter in page.Letters)
                {
                    if (string.IsNullOrWhiteSpace(letter.Value)) continue;
                    double size = Math.Round(letter.PointSize * 2) / 2;
                    if (size <= 0) continue;
                    counts.TryGetValue(size, out int n);
                    counts[size] = n + 1;
                }
            }

            double best = 11;
            int bestCount = 0;
            foreach (KeyValuePair<double, int> pair in counts)
            {
                if (pair.Value > bestCount || (pair.Value == bestCount && pair.Key < best))
                {
                    best = pair.Key;
                    bestCount = pair.Value;
                }
            }

            return best;
        }

        private static void ReadMetadata(PdfDocument document, DocumentModel model)
        {
            DocumentInformation info = document.Information;
            model.Metadata.Title = Clean(info.Title);
            model.Metadata.Author = Clean(info.Author);
            model.Metadata.Subject = Clean(info.Subject);
            model.Metadata.Keywords = Clean(info.Keywords);
            try
            {
                DateTimeOffset? created = info.GetCreatedDateTimeOffset();
                if (created.HasValue) model.Metadata.CreatedUtc = created.Value.UtcDateTime;
                DateTimeOffset? modified = info.GetModifiedDateTimeOffset();
                if (modified.HasValue) model.Metadata.ModifiedUtc = modified.Value.UtcDateTime;
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
            {
                model.Metadata.CreatedUtc = null;
            }
        }

        private static string? Clean(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return value!.Trim();
        }
    }
}
