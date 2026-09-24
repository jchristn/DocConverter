namespace Test.Shared.Fixtures.Builders
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Edge case and negative DOCX fixtures, built with the raw OpenXml SDK or by hand.
    /// </summary>
    public static class DocxEdgeFixtures
    {
        /// <summary>
        /// A table whose first row has a cell spanning two columns and a cell merged down two rows.
        /// Expected model: row 0 [A (colspan 2), B (rowspan 2)], row 1 [C, D], row 2 [E, F, G].
        /// </summary>
        /// <returns>DOCX bytes.</returns>
        public static byte[] SpanTable()
        {
            return DocxFixtureBuilder.Build((main, body) =>
            {
                W.Table table = new W.Table(new W.TableProperties(new W.TableStyle { Val = "TableGrid" }));
                table.Append(new W.TableGrid(new W.GridColumn { Width = "2000" }, new W.GridColumn { Width = "2000" }, new W.GridColumn { Width = "2000" }));
                table.Append(new W.TableRow(
                    Cell("A", new W.GridSpan { Val = 2 }, null),
                    Cell("B", null, new W.VerticalMerge { Val = W.MergedCellValues.Restart })));
                table.Append(new W.TableRow(
                    Cell("C", null, null),
                    Cell("D", null, null),
                    Cell("", null, new W.VerticalMerge())));
                table.Append(new W.TableRow(Cell("E", null, null), Cell("F", null, null), Cell("G", null, null)));
                body.Append(table);
                body.Append(new W.Paragraph(DocxFixtureBuilder.Run("After table", null)));
            });
        }

        /// <summary>
        /// A body paragraph referencing footnote 1 ("Footnote content here.") and endnote 1 ("Endnote content here.").
        /// </summary>
        /// <returns>DOCX bytes.</returns>
        public static byte[] WithNotes()
        {
            return DocxFixtureBuilder.Build((main, body) =>
            {
                FootnotesPart footnotes = main.AddNewPart<FootnotesPart>("rIdFootnotes");
                footnotes.Footnotes = new W.Footnotes(
                    new W.Footnote(new W.Paragraph(new W.Run(new W.SeparatorMark()))) { Type = W.FootnoteEndnoteValues.Separator, Id = -1 },
                    new W.Footnote(new W.Paragraph(new W.Run(new W.ContinuationSeparatorMark()))) { Type = W.FootnoteEndnoteValues.ContinuationSeparator, Id = 0 },
                    new W.Footnote(new W.Paragraph(DocxFixtureBuilder.Run("Footnote content here.", null))) { Id = 1 });
                EndnotesPart endnotes = main.AddNewPart<EndnotesPart>("rIdEndnotes");
                endnotes.Endnotes = new W.Endnotes(
                    new W.Endnote(new W.Paragraph(new W.Run(new W.SeparatorMark()))) { Type = W.FootnoteEndnoteValues.Separator, Id = -1 },
                    new W.Endnote(new W.Paragraph(new W.Run(new W.ContinuationSeparatorMark()))) { Type = W.FootnoteEndnoteValues.ContinuationSeparator, Id = 0 },
                    new W.Endnote(new W.Paragraph(DocxFixtureBuilder.Run("Endnote content here.", null))) { Id = 1 });

                body.Append(new W.Paragraph(
                    DocxFixtureBuilder.Run("Body text", null),
                    new W.Run(new W.RunProperties(new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Superscript }), new W.FootnoteReference { Id = 1 }),
                    DocxFixtureBuilder.Run(" continues", null),
                    new W.Run(new W.RunProperties(new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Superscript }), new W.EndnoteReference { Id = 1 }),
                    DocxFixtureBuilder.Run(".", null)));
            });
        }

        /// <summary>
        /// A paragraph carrying a VML text box whose content is "Boxed text".
        /// </summary>
        /// <returns>DOCX bytes.</returns>
        public static byte[] WithTextBox()
        {
            return DocxFixtureBuilder.Build((main, body) =>
            {
                body.Append(new W.Paragraph(DocxFixtureBuilder.Run("Before box", null)));
                string pict =
                    "<w:pict xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:v=\"urn:schemas-microsoft-com:vml\">"
                    + "<v:shape id=\"TextBox1\" style=\"width:200pt;height:50pt\"><v:textbox><w:txbxContent>"
                    + "<w:p><w:r><w:t>Boxed text</w:t></w:r></w:p>"
                    + "</w:txbxContent></v:textbox></v:shape></w:pict>";
                body.Append(new W.Paragraph(new W.Run(new W.Picture(pict))));
                body.Append(new W.Paragraph(DocxFixtureBuilder.Run("After box", null)));
            });
        }

        /// <summary>
        /// Headings declared three ways: direct outline level 1 (level 2), a localized style named "heading 3" with id
        /// "Titre3" (level 3), and Title style (level 1, also the title fallback).
        /// </summary>
        /// <returns>DOCX bytes.</returns>
        public static byte[] OutlineHeadings()
        {
            return DocxFixtureBuilder.Build((main, body) =>
            {
                W.Styles styles = main.StyleDefinitionsPart!.Styles!;
                W.Style titre = new W.Style { Type = W.StyleValues.Paragraph, StyleId = "Titre3" };
                titre.StyleName = new W.StyleName { Val = "heading 3" };
                styles.Append(titre);
                W.Style title = new W.Style { Type = W.StyleValues.Paragraph, StyleId = "Title" };
                title.StyleName = new W.StyleName { Val = "Title" };
                styles.Append(title);

                body.Append(DocxFixtureBuilder.StyledParagraph("Title", "Document Title Text"));
                body.Append(new W.Paragraph(new W.ParagraphProperties(new W.OutlineLevel { Val = 1 }), DocxFixtureBuilder.Run("Outline heading", null)));
                body.Append(DocxFixtureBuilder.StyledParagraph("Titre3", "Localized heading"));
                body.Append(new W.Paragraph(DocxFixtureBuilder.Run("Body", null)));
            });
        }

        /// <summary>
        /// "Before image", a paragraph with one PNG, then "After image".
        /// </summary>
        /// <returns>DOCX bytes.</returns>
        public static byte[] ImageInMiddle()
        {
            return DocxFixtureBuilder.Build((main, body) =>
            {
                body.Append(new W.Paragraph(DocxFixtureBuilder.Run("Before image", null)));
                body.Append(DocxFixtureBuilder.ImageParagraph(main, ReferenceContent.ImagePng(), ImagePartType.Png, "Middle image", "rIdImg9", 9));
                body.Append(new W.Paragraph(DocxFixtureBuilder.Run("After image", null)));
            });
        }

        /// <summary>
        /// A complex field HYPERLINK to https://example.org/field with result text "field link".
        /// </summary>
        /// <returns>DOCX bytes.</returns>
        public static byte[] FieldHyperlink()
        {
            return DocxFixtureBuilder.Build((main, body) =>
            {
                body.Append(new W.Paragraph(
                    DocxFixtureBuilder.Run("See the ", null),
                    new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }),
                    new W.Run(new W.FieldCode(" HYPERLINK \"https://example.org/field\" ") { Space = SpaceProcessingModeValues.Preserve }),
                    new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }),
                    DocxFixtureBuilder.Run("field link", null),
                    new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }),
                    DocxFixtureBuilder.Run(" now.", null)));
            });
        }

        /// <summary>
        /// One paragraph "Page one" followed by a page break run and "Page two".
        /// </summary>
        /// <returns>DOCX bytes.</returns>
        public static byte[] PageBreakInParagraph()
        {
            return DocxFixtureBuilder.Build((main, body) =>
            {
                body.Append(new W.Paragraph(
                    DocxFixtureBuilder.Run("Page one", null),
                    new W.Run(new W.Break { Type = W.BreakValues.Page }),
                    DocxFixtureBuilder.Run("Page two", null)));
            });
        }

        /// <summary>
        /// A zip package with content types but no word/document.xml.
        /// </summary>
        /// <returns>Bytes.</returns>
        public static byte[] MissingMainPart()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    AddEntry(zip, "[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/></Types>");
                    AddEntry(zip, "_rels/.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"></Relationships>");
                    AddEntry(zip, "docProps/core.xml", "<?xml version=\"1.0\"?><x/>");
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// The first half of the reference DOCX, which leaves the zip central directory missing.
        /// </summary>
        /// <returns>Bytes.</returns>
        public static byte[] Truncated()
        {
            byte[] full = DocxFixtureBuilder.BuildReference();
            byte[] half = new byte[full.Length / 2];
            Buffer.BlockCopy(full, 0, half, 0, half.Length);
            return half;
        }

        /// <summary>
        /// An OLE2 compound file header followed by the UTF-16LE stream names of an encrypted Office Open XML package.
        /// </summary>
        /// <returns>Bytes.</returns>
        public static byte[] PasswordProtected()
        {
            return Ole("EncryptionInfo", "EncryptedPackage");
        }

        /// <summary>
        /// An OLE2 compound file header followed by the UTF-16LE stream name of a legacy Word document.
        /// </summary>
        /// <returns>Bytes.</returns>
        public static byte[] LegacyDoc()
        {
            return Ole("WordDocument", "1Table");
        }

        private static byte[] Ole(params string[] names)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }, 0, 8);
                ms.Write(new byte[504], 0, 504);
                foreach (string name in names)
                {
                    byte[] bytes = Encoding.Unicode.GetBytes(name);
                    ms.Write(bytes, 0, bytes.Length);
                    ms.Write(new byte[128 - (bytes.Length % 128)], 0, 128 - (bytes.Length % 128));
                }

                return ms.ToArray();
            }
        }

        private static void AddEntry(ZipArchive zip, string name, string content)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name);
            using (Stream s = entry.Open())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                s.Write(bytes, 0, bytes.Length);
            }
        }

        private static W.TableCell Cell(string text, W.GridSpan? span, W.VerticalMerge? merge)
        {
            W.TableCellProperties tcPr = new W.TableCellProperties();
            if (span != null) tcPr.GridSpan = span;
            if (merge != null) tcPr.VerticalMerge = merge;
            return new W.TableCell(tcPr, new W.Paragraph(DocxFixtureBuilder.Run(text, null)));
        }
    }
}
