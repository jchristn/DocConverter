namespace Test.Shared.Fixtures.Builders
{
    using System;
    using System.IO;
    using System.Text;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using A = DocumentFormat.OpenXml.Drawing;
    using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
    using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Builds DOCX fixtures with the raw OpenXml SDK, independently of DocConverter's DOCX writer.
    /// </summary>
    public static class DocxFixtureBuilder
    {
        /// <summary>
        /// Numbering id of the bulleted list definition in fixtures built with styles.
        /// </summary>
        public const int BulletNumId = 1;

        /// <summary>
        /// Numbering id of the decimal list definition in fixtures built with styles.
        /// </summary>
        public const int DecimalNumId = 2;

        /// <summary>
        /// The full reference content (ReferenceContent) as a DOCX.
        /// </summary>
        /// <returns>DOCX bytes.</returns>
        public static byte[] BuildReference()
        {
            return Build((main, body) =>
            {
                body.Append(StyledParagraph("Heading1", ReferenceContent.Heading1));

                W.Paragraph styled = new W.Paragraph();
                styled.Append(Run(ReferenceContent.StyledLead, null));
                styled.Append(Run(ReferenceContent.BoldText, new W.RunProperties(new W.Bold())));
                styled.Append(Run(", ", null));
                styled.Append(Run(ReferenceContent.ItalicText, new W.RunProperties(new W.Italic())));
                styled.Append(Run(", ", null));
                styled.Append(Run(ReferenceContent.UnderlineText, new W.RunProperties(new W.Underline { Val = W.UnderlineValues.Single })));
                styled.Append(Run(", ", null));
                styled.Append(Run(ReferenceContent.StrikeText, new W.RunProperties(new W.Strike())));
                styled.Append(Run(", ", null));
                styled.Append(Run(ReferenceContent.InlineCode, new W.RunProperties(new W.RunFonts { Ascii = "Courier New", HighAnsi = "Courier New" })));
                styled.Append(Run(" and a ", null));
                main.AddHyperlinkRelationship(new Uri(ReferenceContent.LinkUrl, UriKind.Absolute), true, "rIdLink1");
                styled.Append(new W.Hyperlink(Run(ReferenceContent.LinkText, new W.RunProperties(new W.RunStyle { Val = "Hyperlink" }))) { Id = "rIdLink1" });
                styled.Append(Run(".", null));
                body.Append(styled);

                body.Append(StyledParagraph("Heading2", ReferenceContent.HeadingLists));
                body.Append(ListParagraph(ReferenceContent.Bullets[0], BulletNumId, 0));
                body.Append(ListParagraph(ReferenceContent.Bullets[1], BulletNumId, 0));
                body.Append(ListParagraph(ReferenceContent.NestedBullet, BulletNumId, 1));
                body.Append(ListParagraph(ReferenceContent.DeepBullet, BulletNumId, 2));
                body.Append(ListParagraph(ReferenceContent.Bullets[2], BulletNumId, 0));
                foreach (string step in ReferenceContent.Steps) body.Append(ListParagraph(step, DecimalNumId, 0));

                body.Append(StyledParagraph("Heading2", ReferenceContent.HeadingTable));
                body.Append(Table(ReferenceContent.TableRows, true));

                body.Append(StyledParagraph("Heading3", ReferenceContent.HeadingCode));
                foreach (string line in ReferenceContent.CodeText.Split('\n'))
                    body.Append(StyledParagraph("HTMLPreformatted", line));

                body.Append(StyledParagraph("Quote", ReferenceContent.QuoteText));
                body.Append(ImageParagraph(main, ReferenceContent.ImagePng(), ImagePartType.Png, ReferenceContent.ImageAlt, "rIdImage1", 1));
                body.Append(new W.Paragraph(Run(ReferenceContent.International, null)));
                body.Append(new W.Paragraph(Run(ReferenceContent.Special, null)));
                body.Append(new W.Paragraph(Run(ReferenceContent.Closing, null)));
            }, ReferenceContent.Title, ReferenceContent.Author, ReferenceContent.Subject);
        }

        /// <summary>
        /// Build a DOCX with the fixture styles and numbering, letting the caller fill the body.
        /// </summary>
        /// <param name="fill">Callback that fills the body.</param>
        /// <param name="title">Core title, or null.</param>
        /// <param name="author">Core creator, or null.</param>
        /// <param name="subject">Core subject, or null.</param>
        /// <returns>DOCX bytes.</returns>
        public static byte[] Build(Action<MainDocumentPart, W.Body> fill, string? title = null, string? author = null, string? subject = null)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (WordprocessingDocument doc = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
                {
                    MainDocumentPart main = doc.AddMainDocumentPart();
                    main.Document = new W.Document(new W.Body());
                    StyleDefinitionsPart styles = main.AddNewPart<StyleDefinitionsPart>("rIdStyles");
                    styles.Styles = Styles();
                    NumberingDefinitionsPart numbering = main.AddNewPart<NumberingDefinitionsPart>("rIdNumbering");
                    numbering.Numbering = Numbering();
                    fill(main, main.Document.Body!);
                    CoreFilePropertiesPart core = doc.AddCoreFilePropertiesPart();
                    using (Stream s = core.GetStream(FileMode.Create, FileAccess.Write))
                    {
                        byte[] xml = Encoding.UTF8.GetBytes(CoreXml(title, author, subject));
                        s.Write(xml, 0, xml.Length);
                    }

                    main.Document.Save();
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// A paragraph with a paragraph style and one plain run.
        /// </summary>
        /// <param name="styleId">Paragraph style id.</param>
        /// <param name="text">Text.</param>
        /// <returns>Paragraph.</returns>
        public static W.Paragraph StyledParagraph(string styleId, string text)
        {
            return new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = styleId }), Run(text, null));
        }

        /// <summary>
        /// A numbered paragraph.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="numId">Numbering id.</param>
        /// <param name="level">List level.</param>
        /// <returns>Paragraph.</returns>
        public static W.Paragraph ListParagraph(string text, int numId, int level)
        {
            return new W.Paragraph(
                new W.ParagraphProperties(
                    new W.ParagraphStyleId { Val = "ListParagraph" },
                    new W.NumberingProperties(new W.NumberingLevelReference { Val = level }, new W.NumberingId { Val = numId })),
                Run(text, null));
        }

        /// <summary>
        /// A run with preserved text.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="properties">Run properties, or null.</param>
        /// <returns>Run.</returns>
        public static W.Run Run(string text, W.RunProperties? properties)
        {
            W.Run run = new W.Run();
            if (properties != null) run.Append(properties);
            run.Append(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
            return run;
        }

        /// <summary>
        /// A bordered table of plain text cells.
        /// </summary>
        /// <param name="rows">Rows of cell texts.</param>
        /// <param name="headerRow">When true the first row is marked as a repeating header row.</param>
        /// <returns>Table.</returns>
        public static W.Table Table(string[][] rows, bool headerRow)
        {
            W.Table table = new W.Table(new W.TableProperties(
                new W.TableStyle { Val = "TableGrid" },
                new W.TableWidth { Width = "0", Type = W.TableWidthUnitValues.Auto },
                new W.TableLook { Val = "0000", FirstRow = false, LastRow = false, FirstColumn = false, LastColumn = false, NoHorizontalBand = true, NoVerticalBand = true }));
            W.TableGrid grid = new W.TableGrid();
            for (int c = 0; c < rows[0].Length; c++) grid.Append(new W.GridColumn { Width = "2000" });
            table.Append(grid);
            for (int r = 0; r < rows.Length; r++)
            {
                W.TableRow tr = new W.TableRow();
                if (r == 0 && headerRow) tr.Append(new W.TableRowProperties(new W.TableHeader()));
                foreach (string cell in rows[r])
                    tr.Append(new W.TableCell(new W.TableCellProperties(new W.TableCellWidth { Width = "2000", Type = W.TableWidthUnitValues.Dxa }), new W.Paragraph(Run(cell, null))));
                table.Append(tr);
            }

            return table;
        }

        /// <summary>
        /// A paragraph holding one inline picture.
        /// </summary>
        /// <param name="main">Main document part.</param>
        /// <param name="data">Image bytes.</param>
        /// <param name="type">Image part type.</param>
        /// <param name="alt">Alternative text.</param>
        /// <param name="relId">Relationship id to assign.</param>
        /// <param name="drawingId">Unique drawing id.</param>
        /// <returns>Paragraph.</returns>
        public static W.Paragraph ImageParagraph(MainDocumentPart main, byte[] data, PartTypeInfo type, string alt, string relId, uint drawingId)
        {
            ImagePart part = main.AddImagePart(type, relId);
            using (MemoryStream ms = new MemoryStream(data))
            {
                part.FeedData(ms);
            }

            long cx = 16L * 9525, cy = 16L * 9525;
            W.Drawing drawing = new W.Drawing(new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = drawingId, Name = "Picture " + drawingId, Description = alt },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties { Id = 0U, Name = "image" + drawingId }, new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(new A.Blip { Embed = relId }, new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });
            return new W.Paragraph(new W.Run(drawing));
        }

        private static W.Styles Styles()
        {
            W.Styles styles = new W.Styles();
            styles.Append(ParagraphStyle("Normal", "Normal", null, null));
            for (int level = 1; level <= 3; level++)
                styles.Append(ParagraphStyle("Heading" + level, "heading " + level, "Normal", new W.StyleParagraphProperties(new W.KeepNext(), new W.OutlineLevel { Val = level - 1 })));
            W.Style pre = ParagraphStyle("HTMLPreformatted", "HTML Preformatted", "Normal", null);
            pre.StyleRunProperties = new W.StyleRunProperties(new W.RunFonts { Ascii = "Courier New", HighAnsi = "Courier New" });
            styles.Append(pre);
            styles.Append(ParagraphStyle("Quote", "Quote", "Normal", new W.StyleParagraphProperties(new W.Indentation { Left = "720" })));
            styles.Append(ParagraphStyle("ListParagraph", "List Paragraph", "Normal", new W.StyleParagraphProperties(new W.Indentation { Left = "720" })));
            W.Style hyperlink = new W.Style { Type = W.StyleValues.Character, StyleId = "Hyperlink" };
            hyperlink.StyleName = new W.StyleName { Val = "Hyperlink" };
            hyperlink.StyleRunProperties = new W.StyleRunProperties(new W.Color { Val = "0563C1" }, new W.Underline { Val = W.UnderlineValues.Single });
            styles.Append(hyperlink);
            W.Style grid = new W.Style { Type = W.StyleValues.Table, StyleId = "TableGrid" };
            grid.StyleName = new W.StyleName { Val = "Table Grid" };
            styles.Append(grid);
            return styles;
        }

        private static W.Style ParagraphStyle(string id, string name, string? basedOn, W.StyleParagraphProperties? pPr)
        {
            W.Style style = new W.Style { Type = W.StyleValues.Paragraph, StyleId = id };
            style.StyleName = new W.StyleName { Val = name };
            if (basedOn != null) style.BasedOn = new W.BasedOn { Val = basedOn };
            if (pPr != null) style.StyleParagraphProperties = pPr;
            return style;
        }

        private static W.Numbering Numbering()
        {
            W.Numbering numbering = new W.Numbering();
            numbering.Append(Abstract(10, W.NumberFormatValues.Bullet, "•"));
            numbering.Append(Abstract(20, W.NumberFormatValues.Decimal, null));
            W.NumberingInstance bullets = new W.NumberingInstance(new W.AbstractNumId { Val = 10 });
            bullets.NumberID = BulletNumId;
            numbering.Append(bullets);
            W.NumberingInstance steps = new W.NumberingInstance(new W.AbstractNumId { Val = 20 });
            steps.NumberID = DecimalNumId;
            numbering.Append(steps);
            return numbering;
        }

        private static W.AbstractNum Abstract(int id, W.NumberFormatValues format, string? bullet)
        {
            W.AbstractNum abs = new W.AbstractNum();
            abs.AbstractNumberId = id;
            for (int level = 0; level < 9; level++)
            {
                W.Level lvl = new W.Level(
                    new W.StartNumberingValue { Val = 1 },
                    new W.NumberingFormat { Val = format },
                    new W.LevelText { Val = bullet ?? "%" + (level + 1) + "." },
                    new W.LevelJustification { Val = W.LevelJustificationValues.Left });
                lvl.LevelIndex = level;
                abs.Append(lvl);
            }

            return abs;
        }

        private static string CoreXml(string? title, string? author, string? subject)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
            if (title != null) sb.Append("<dc:title>").Append(System.Security.SecurityElement.Escape(title)).Append("</dc:title>");
            if (subject != null) sb.Append("<dc:subject>").Append(System.Security.SecurityElement.Escape(subject)).Append("</dc:subject>");
            if (author != null) sb.Append("<dc:creator>").Append(System.Security.SecurityElement.Escape(author)).Append("</dc:creator>");
            sb.Append("<dcterms:created xsi:type=\"dcterms:W3CDTF\">2024-05-01T10:00:00Z</dcterms:created>");
            sb.Append("<dcterms:modified xsi:type=\"dcterms:W3CDTF\">2024-05-02T11:30:00Z</dcterms:modified>");
            sb.Append("</cp:coreProperties>");
            return sb.ToString();
        }
    }
}
