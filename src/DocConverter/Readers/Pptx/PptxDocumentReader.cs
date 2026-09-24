namespace DocConverter.Readers.Pptx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocConverter.Readers.Xlsx;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using A = DocumentFormat.OpenXml.Drawing;
    using P = DocumentFormat.OpenXml.Presentation;

    /// <summary>
    /// Reads PPTX presentations into the document model: one slide section per slide holding the title (level 1 heading),
    /// subtitle (level 2 heading), then text, lists, tables and pictures ordered top to bottom and left to right.
    /// Speaker notes are included on request. Stateless and thread safe.
    /// </summary>
    public sealed class PptxDocumentReader : IDocumentReader
    {
        #region Public-Members

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        #endregion

        #region Private-Members

        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Pptx };

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (context == null) throw new ArgumentNullException(nameof(context));
            token.ThrowIfCancellationRequested();

            OfficeReadHelper.EnsureReadable(input, "PPTX", context);
            DocumentModel document = new DocumentModel();
            PresentationDocument presentation;
            try
            {
                presentation = PresentationDocument.Open(input, false);
            }
            catch (Exception ex) when (!(ex is DocConverterException) && !(ex is OperationCanceledException))
            {
                throw OfficeReadHelper.Wrap(ex, "PPTX");
            }

            using (presentation)
            {
                PresentationPart part = presentation.PresentationPart
                    ?? throw new DocumentReadException("The PPTX file has no presentation part.");
                if (part.Presentation == null) throw new DocumentReadException("The PPTX presentation part is empty.");
                OfficeReadHelper.ReadMetadata(presentation, document);

                Dictionary<string, string> seenImages = new Dictionary<string, string>(StringComparer.Ordinal);
                P.SlideIdList? slideIds = part.Presentation.SlideIdList;
                if (slideIds == null) return Task.FromResult(document);

                int slideNumber = 0;
                bool notesWarned = false;
                foreach (P.SlideId slideId in slideIds.Elements<P.SlideId>())
                {
                    token.ThrowIfCancellationRequested();
                    string? relId = slideId.RelationshipId?.Value;
                    if (string.IsNullOrEmpty(relId)) continue;
                    SlidePart? slidePart;
                    try
                    {
                        slidePart = part.GetPartById(relId!) as SlidePart;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        slidePart = null;
                    }

                    if (slidePart?.Slide == null) continue;
                    slideNumber++;
                    SectionBlock section = ReadSlide(slidePart, slideNumber, document, context, seenImages);

                    if (options.Pptx.IncludeNotes)
                    {
                        SectionBlock? notes = ReadNotes(slidePart, context.MaxNestingDepth);
                        if (notes != null)
                        {
                            section.Blocks.Add(notes);
                            if (!notesWarned)
                            {
                                context.AddWarning(WarningCodeEnum.NotesIncluded, "Speaker notes were included as a Notes section inside each slide.");
                                notesWarned = true;
                            }
                        }
                    }

                    document.Blocks.Add(section);
                }
            }

            return Task.FromResult(document);
        }

        #endregion

        #region Private-Methods

        private static SectionBlock ReadSlide(SlidePart slidePart, int slideNumber, DocumentModel document, ConversionContext context, Dictionary<string, string> seenImages)
        {
            SectionBlock section = new SectionBlock(SectionKindEnum.Slide, null);
            section.SourceSlide = slideNumber;
            P.ShapeTree? tree = slidePart.Slide?.CommonSlideData?.ShapeTree;
            if (tree == null) return section;

            PptxPlaceholderPositions positions = new PptxPlaceholderPositions(slidePart);
            List<PptxShapeItem> items = new List<PptxShapeItem>();
            Collect(tree, positions, items);

            string? title = null;
            string? subtitle = null;
            List<PptxShapeItem> content = new List<PptxShapeItem>();
            foreach (PptxShapeItem item in items)
            {
                if (item.Element is P.Shape shape)
                {
                    P.PlaceholderShape? ph = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;
                    string type = ph != null ? PptxPlaceholderPositions.TypeName(ph) : "";
                    if (ph != null && type == "title" && title == null && shape.TextBody != null)
                    {
                        title = PptxTextReader.PlainText(shape.TextBody);
                        continue;
                    }

                    if (ph != null && type == "subTitle" && subtitle == null && shape.TextBody != null)
                    {
                        subtitle = PptxTextReader.PlainText(shape.TextBody);
                        continue;
                    }
                }

                content.Add(item);
            }

            content.Sort(CompareItems);
            if (!string.IsNullOrEmpty(title))
            {
                section.Title = title;
                HeadingBlock heading = new HeadingBlock(1, title);
                heading.SourceSlide = slideNumber;
                section.Blocks.Add(heading);
            }

            if (!string.IsNullOrEmpty(subtitle))
            {
                HeadingBlock sub = new HeadingBlock(2, subtitle);
                sub.SourceSlide = slideNumber;
                section.Blocks.Add(sub);
            }

            foreach (PptxShapeItem item in content)
            {
                List<Block> blocks = new List<Block>();
                if (item.Element is P.Shape shape && shape.TextBody != null)
                {
                    P.PlaceholderShape? ph = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;
                    bool defaultBullets = ph != null && (PptxPlaceholderPositions.TypeName(ph) == "body" || PptxPlaceholderPositions.TypeName(ph) == "Object");
                    blocks.AddRange(PptxTextReader.Blocks(shape.TextBody, slidePart, defaultBullets, context.MaxNestingDepth));
                }
                else if (item.Element is P.GraphicFrame frame)
                {
                    A.Table? table = frame.Descendants<A.Table>().FirstOrDefault();
                    if (table != null) blocks.Add(ReadTable(table, slidePart, context));
                }
                else if (item.Element is P.Picture picture)
                {
                    ImageBlock? image = ReadPicture(picture, slidePart, document, seenImages);
                    if (image != null) blocks.Add(image);
                }

                foreach (Block block in blocks)
                {
                    block.SourceSlide = slideNumber;
                    section.Blocks.Add(block);
                }
            }

            return section;
        }

        private static void Collect(OpenXmlElement container, PptxPlaceholderPositions positions, List<PptxShapeItem> items)
        {
            foreach (OpenXmlElement child in container.ChildElements)
            {
                if (child is P.GroupShape group)
                {
                    Collect(group, positions, items);
                    continue;
                }

                A.Offset? offset = null;
                if (child is P.Shape shape)
                {
                    offset = shape.ShapeProperties?.Transform2D?.Offset;
                    if (offset == null) offset = positions.Find(shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape);
                }
                else if (child is P.GraphicFrame frame)
                {
                    offset = frame.Transform?.Offset;
                }
                else if (child is P.Picture picture)
                {
                    offset = picture.ShapeProperties?.Transform2D?.Offset;
                    if (offset == null) offset = positions.Find(picture.NonVisualPictureProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape);
                }
                else
                {
                    continue;
                }

                long x = offset?.X != null && offset.X.HasValue ? offset.X.Value : 0;
                long y = offset?.Y != null && offset.Y.HasValue ? offset.Y.Value : 0;
                items.Add(new PptxShapeItem(child, x, y, items.Count));
            }
        }

        private static int CompareItems(PptxShapeItem a, PptxShapeItem b)
        {
            const long rowTolerance = 91440;
            long dy = a.Y - b.Y;
            if (Math.Abs(dy) > rowTolerance) return dy < 0 ? -1 : 1;
            if (a.X != b.X) return a.X < b.X ? -1 : 1;
            return a.Order.CompareTo(b.Order);
        }

        private static TableBlock ReadTable(A.Table table, SlidePart slidePart, ConversionContext context)
        {
            TableBlock block = new TableBlock();
            A.TableProperties? props = table.TableProperties;
            bool firstRow = props?.FirstRow != null && props.FirstRow.HasValue && props.FirstRow.Value;
            int rowIndex = 0;
            foreach (A.TableRow row in table.Elements<A.TableRow>())
            {
                TableRow modelRow = new TableRow();
                foreach (A.TableCell cell in row.Elements<A.TableCell>())
                {
                    bool hMerge = cell.HorizontalMerge != null && cell.HorizontalMerge.HasValue && cell.HorizontalMerge.Value;
                    bool vMerge = cell.VerticalMerge != null && cell.VerticalMerge.HasValue && cell.VerticalMerge.Value;
                    if (hMerge || vMerge) continue;

                    TableCell modelCell = new TableCell();
                    if (cell.TextBody != null) modelCell.Blocks.AddRange(PptxTextReader.Blocks(cell.TextBody, slidePart, false, context.MaxNestingDepth));
                    if (cell.GridSpan != null && cell.GridSpan.HasValue) modelCell.ColumnSpan = cell.GridSpan.Value;
                    if (cell.RowSpan != null && cell.RowSpan.HasValue) modelCell.RowSpan = cell.RowSpan.Value;
                    modelCell.IsHeader = firstRow && rowIndex == 0;
                    modelRow.Cells.Add(modelCell);
                }

                block.Rows.Add(modelRow);
                rowIndex++;
            }

            block.HeaderRowCount = firstRow && block.Rows.Count > 0 ? 1 : 0;
            return block;
        }

        private static ImageBlock? ReadPicture(P.Picture picture, SlidePart slidePart, DocumentModel document, Dictionary<string, string> seen)
        {
            string? embed = picture.BlipFill?.Blip?.Embed?.Value;
            if (string.IsNullOrEmpty(embed)) return null;
            OpenXmlPart imagePart;
            try
            {
                imagePart = slidePart.GetPartById(embed!);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }

            string? id = OfficeReadHelper.AddImage(document, imagePart, seen);
            if (id == null) return null;
            P.NonVisualDrawingProperties? nv = picture.NonVisualPictureProperties?.NonVisualDrawingProperties;
            string? alt = nv?.Description?.Value;
            if (string.IsNullOrEmpty(alt)) alt = nv?.Name?.Value;
            return new ImageBlock(id, alt);
        }

        private static SectionBlock? ReadNotes(SlidePart slidePart, int maxDepth)
        {
            P.NotesSlide? notes = slidePart.NotesSlidePart?.NotesSlide;
            P.ShapeTree? tree = notes?.CommonSlideData?.ShapeTree;
            if (tree == null) return null;

            SectionBlock section = new SectionBlock(SectionKindEnum.Generic, "Notes");
            foreach (P.Shape shape in tree.Descendants<P.Shape>())
            {
                P.PlaceholderShape? ph = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;
                if (ph == null || shape.TextBody == null) continue;
                if (ph.Type != null && ph.Type.HasValue && ph.Type.Value != P.PlaceholderValues.Body) continue;
                section.Blocks.AddRange(PptxTextReader.Blocks(shape.TextBody, slidePart.NotesSlidePart!, false, maxDepth));
            }

            return section.Blocks.Count > 0 ? section : null;
        }

        #endregion
    }
}
