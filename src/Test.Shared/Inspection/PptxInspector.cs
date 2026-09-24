namespace Test.Shared.Inspection
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using DocumentFormat.OpenXml.Packaging;
    using A = DocumentFormat.OpenXml.Drawing;
    using P = DocumentFormat.OpenXml.Presentation;

    /// <summary>
    /// Inspects PPTX output with the OpenXml SDK (never DocConverter): validates it, then reports slide titles as
    /// headings, bulleted and numbered paragraphs as list items, tables, pictures, links and all slide text.
    /// </summary>
    public static class PptxInspector
    {
        /// <summary>
        /// Inspect PPTX bytes.
        /// </summary>
        /// <param name="bytes">PPTX bytes.</param>
        /// <returns>Snapshot.</returns>
        /// <exception cref="TestAssertionException">Thrown when the package fails validation.</exception>
        public static ContentSnapshot Inspect(byte[] bytes)
        {
            ContentSnapshot snapshot = new ContentSnapshot();
            using (MemoryStream ms = new MemoryStream(bytes))
            using (PresentationDocument doc = PresentationDocument.Open(ms, false))
            {
                OpenXmlInspection.Validate(doc, "PPTX");
                snapshot.Title = OpenXmlInspection.Title(doc);
                PresentationPart presentation = doc.PresentationPart ?? throw new TestAssertionException("PPTX output has no presentation part.");
                foreach (SlidePart slide in SlideParts(presentation))
                {
                    foreach (P.Shape shape in slide.Slide!.Descendants<P.Shape>())
                    {
                        P.PlaceholderShape? ph = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;
                        bool isTitle = ph?.Type != null && ph.Type.HasValue && (ph.Type.Value == P.PlaceholderValues.Title || ph.Type.Value == P.PlaceholderValues.CenteredTitle);
                        if (shape.TextBody == null) continue;
                        foreach (A.Paragraph paragraph in shape.TextBody.Elements<A.Paragraph>())
                        {
                            string text = Text(paragraph);
                            OpenXmlInspection.AppendText(snapshot, text);
                            if (isTitle && text.Trim().Length > 0) snapshot.Headings.Add(text.Trim());
                            A.ParagraphProperties? props = paragraph.ParagraphProperties;
                            bool bullet = props != null && (props.GetFirstChild<A.CharacterBullet>() != null || props.GetFirstChild<A.AutoNumberedBullet>() != null);
                            if (bullet && text.Trim().Length > 0) snapshot.ListItems.Add(text.Trim());
                        }
                    }

                    foreach (A.Table table in slide.Slide.Descendants<A.Table>())
                    {
                        foreach (A.TableRow row in table.Elements<A.TableRow>())
                        {
                            List<string> cells = new List<string>();
                            foreach (A.TableCell cell in row.Elements<A.TableCell>())
                            {
                                string text = cell.TextBody == null ? "" : string.Join(" ", cell.TextBody.Elements<A.Paragraph>().Select(Text));
                                cells.Add(text.Trim());
                                OpenXmlInspection.AppendText(snapshot, text);
                            }

                            snapshot.TableRows.Add(cells);
                        }
                    }

                    snapshot.ImageCount += slide.Slide.Descendants<P.Picture>().Count();
                    foreach (HyperlinkRelationship link in slide.HyperlinkRelationships) snapshot.LinkUrls.Add(link.Uri.OriginalString);
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Number of slides.
        /// </summary>
        /// <param name="bytes">PPTX bytes.</param>
        /// <returns>Slide count.</returns>
        public static int SlideCount(byte[] bytes)
        {
            using (MemoryStream ms = new MemoryStream(bytes))
            using (PresentationDocument doc = PresentationDocument.Open(ms, false))
            {
                return SlideParts(doc.PresentationPart!).Count;
            }
        }

        private static List<SlidePart> SlideParts(PresentationPart presentation)
        {
            List<SlidePart> slides = new List<SlidePart>();
            P.SlideIdList? ids = presentation.Presentation?.SlideIdList;
            if (ids == null) return slides;
            foreach (P.SlideId id in ids.Elements<P.SlideId>())
                slides.Add((SlidePart)presentation.GetPartById(id.RelationshipId!.Value!));
            return slides;
        }

        private static string Text(A.Paragraph paragraph)
        {
            StringBuilder sb = new StringBuilder();
            foreach (A.Run run in paragraph.Elements<A.Run>()) sb.Append(run.Text?.Text ?? "");
            return sb.ToString();
        }
    }
}
