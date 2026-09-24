namespace DocConverter.Writers.Pptx
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocConverter.Writers.Xlsx;
    using DocumentFormat.OpenXml.Packaging;
    using P = DocumentFormat.OpenXml.Presentation;

    /// <summary>
    /// Writes the document model as a PPTX presentation built entirely in code (theme, master and layouts included).
    /// A new slide starts at each slide section and at each heading at or below PptxOptions.SlideSplitHeadingLevel;
    /// long content continues on "(continued)" slides and long tables repeat their header rows. Stateless and thread safe.
    /// </summary>
    public sealed class PptxDocumentWriter : IDocumentWriter
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Pptx };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <inheritdoc />
        public Task WriteAsync(DocumentModel document, Stream output, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (context == null) throw new ArgumentNullException(nameof(context));
            token.ThrowIfCancellationRequested();

            List<PptxSlidePage> pages = new List<PptxSlidePage>();
            foreach (PptxSlidePlan plan in PptxSlidePlanner.Plan(document, options))
            {
                token.ThrowIfCancellationRequested();
                pages.AddRange(PptxPaginator.Paginate(plan, document, options, context));
            }

            using (MemoryStream package = new MemoryStream())
            {
                using (PresentationDocument presentation = PresentationDocument.Create(package, DocumentFormat.OpenXml.PresentationDocumentType.Presentation))
                {
                    PresentationScaffold scaffold = PresentationScaffold.Create(presentation);
                    P.SlideIdList slideIds = scaffold.Presentation.Presentation!.SlideIdList!;
                    uint slideId = 256;
                    int number = 1;
                    foreach (PptxSlidePage page in pages)
                    {
                        token.ThrowIfCancellationRequested();
                        bool titleSlide = page.Units.Count == 0 && !string.IsNullOrEmpty(page.Title);
                        string relId = "rIdSlide" + number.ToString(CultureInfo.InvariantCulture);
                        SlidePart slidePart = scaffold.Presentation.AddNewPart<SlidePart>(relId);
                        slidePart.AddPart(titleSlide ? scaffold.TitleLayout : scaffold.ContentLayout, "rIdLayout1");
                        PptxSlideDrawer drawer = new PptxSlideDrawer(slidePart, document, context);
                        slidePart.Slide = drawer.Draw(page, titleSlide);
                        slideIds.Append(new P.SlideId { Id = slideId++, RelationshipId = relId });
                        number++;
                    }

                    OfficeWriteHelper.WriteCoreProperties(presentation, document.Metadata, options);
                }

                OfficeWriteHelper.CopyPackage(package, output, options.Deterministic);
            }

            return Task.CompletedTask;
        }
    }
}
