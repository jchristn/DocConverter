namespace Test.Shared.Inspection
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using DocumentFormat.OpenXml.Validation;
    using A = DocumentFormat.OpenXml.Drawing;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Inspects DOCX output with the OpenXml SDK only (never DocConverter's reader): validates it against the Office 2019
    /// schema and extracts a content snapshot.
    /// </summary>
    public static class DocxInspector
    {
        /// <summary>
        /// Validate and inspect a DOCX.
        /// </summary>
        /// <param name="docx">DOCX bytes.</param>
        /// <returns>Content snapshot.</returns>
        /// <exception cref="TestAssertionException">Thrown when the package fails schema validation or has no body.</exception>
        public static ContentSnapshot Inspect(byte[] docx)
        {
            List<string> errors = Validate(docx);
            if (errors.Count > 0) throw new TestAssertionException("DOCX failed OpenXml validation (" + errors.Count + " errors): " + string.Join(" | ", errors.Take(5)));

            ContentSnapshot snapshot = new ContentSnapshot();
            using (MemoryStream ms = new MemoryStream(docx, false))
            using (WordprocessingDocument doc = WordprocessingDocument.Open(ms, false))
            {
                MainDocumentPart main = doc.MainDocumentPart ?? throw new TestAssertionException("DOCX has no main document part.");
                W.Body body = main.Document?.Body ?? throw new TestAssertionException("DOCX has no body.");

                StringBuilder all = new StringBuilder();
                foreach (W.Paragraph p in body.Descendants<W.Paragraph>())
                {
                    string text = ParagraphText(p);
                    if (text.Length > 0) all.Append(text).Append(' ');
                    string? style = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                    if (style != null && (style.StartsWith("Heading", StringComparison.Ordinal) || style == "Title") && text.Length > 0) snapshot.Headings.Add(text);
                    if (p.ParagraphProperties?.NumberingProperties != null && text.Length > 0) snapshot.ListItems.Add(text);
                }

                snapshot.AllText = ContentSnapshot.Normalize(all.ToString());

                foreach (W.Table table in body.Descendants<W.Table>())
                {
                    foreach (W.TableRow row in table.Elements<W.TableRow>())
                    {
                        List<string> cells = new List<string>();
                        foreach (W.TableCell cell in row.Elements<W.TableCell>())
                        {
                            StringBuilder sb = new StringBuilder();
                            foreach (W.Paragraph p in cell.Elements<W.Paragraph>()) sb.Append(ParagraphText(p)).Append(' ');
                            cells.Add(ContentSnapshot.Normalize(sb.ToString()));
                        }

                        snapshot.TableRows.Add(cells);
                    }
                }

                snapshot.ImageCount = body.Descendants<A.Blip>().Count(b => !string.IsNullOrEmpty(b.Embed?.Value));
                Dictionary<string, string> rels = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (HyperlinkRelationship rel in main.HyperlinkRelationships) rels[rel.Id] = rel.Uri.OriginalString;
                foreach (W.Hyperlink link in body.Descendants<W.Hyperlink>())
                {
                    if (link.Id?.Value != null && rels.TryGetValue(link.Id.Value, out string? url)) snapshot.LinkUrls.Add(url);
                    else if (link.Anchor?.Value != null) snapshot.LinkUrls.Add("#" + link.Anchor.Value);
                }

                CoreFilePropertiesPart? core = doc.CoreFilePropertiesPart;
                if (core != null)
                {
                    using (Stream s = core.GetStream(FileMode.Open, FileAccess.Read))
                    {
                        XDocument xml = XDocument.Load(s);
                        XElement? title = xml.Root?.Element(XName.Get("title", "http://purl.org/dc/elements/1.1/"));
                        snapshot.Title = title?.Value;
                    }
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Validation errors against the Office 2019 schema. Empty when valid.
        /// </summary>
        /// <param name="docx">DOCX bytes.</param>
        /// <returns>Error descriptions.</returns>
        public static List<string> Validate(byte[] docx)
        {
            List<string> result = new List<string>();
            using (MemoryStream ms = new MemoryStream(docx, false))
            using (WordprocessingDocument doc = WordprocessingDocument.Open(ms, false))
            {
                OpenXmlValidator validator = new OpenXmlValidator(FileFormatVersions.Office2019);
                foreach (ValidationErrorInfo error in validator.Validate(doc))
                    result.Add((error.Part?.Uri.ToString() ?? "") + " " + (error.Path?.XPath ?? "") + ": " + error.Description);
            }

            return result;
        }

        /// <summary>
        /// Read a raw XML part of a DOCX, for example "word/document.xml".
        /// </summary>
        /// <param name="docx">DOCX bytes.</param>
        /// <param name="entryName">Zip entry name.</param>
        /// <returns>Part XML text.</returns>
        public static string PartXml(byte[] docx, string entryName)
        {
            using (MemoryStream ms = new MemoryStream(docx, false))
            using (System.IO.Compression.ZipArchive zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read))
            {
                System.IO.Compression.ZipArchiveEntry? entry = zip.GetEntry(entryName) ?? throw new TestAssertionException("DOCX has no entry '" + entryName + "'.");
                using (StreamReader reader = new StreamReader(entry.Open(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static string ParagraphText(W.Paragraph p)
        {
            StringBuilder sb = new StringBuilder();
            foreach (OpenXmlElement e in p.Descendants())
            {
                if (e is W.Text t) sb.Append(t.Text);
                else if (e is W.TabChar) sb.Append('\t');
                else if (e is W.Break) sb.Append(' ');
            }

            return ContentSnapshot.Normalize(sb.ToString());
        }
    }
}
