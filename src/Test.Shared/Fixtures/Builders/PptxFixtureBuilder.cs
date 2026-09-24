namespace Test.Shared.Fixtures.Builders
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using A = DocumentFormat.OpenXml.Drawing;
    using P = DocumentFormat.OpenXml.Presentation;

    /// <summary>
    /// Builds PPTX fixtures with the raw OpenXml SDK, never with DocConverter's writer.
    /// </summary>
    public static class PptxFixtureBuilder
    {
        /// <summary>Subtitle on the first slide.</summary>
        public const string Subtitle = "Reference content";

        /// <summary>Speaker notes on the first slide.</summary>
        public const string Notes = "Speaker notes for the opening slide.";

        /// <summary>Title of the fourth slide.</summary>
        public const string MediaTitle = "Media";

        /// <summary>
        /// The reference deck: title slide with subtitle and notes; a "Lists" slide with nested bullets and a numbered list;
        /// a "Table" slide; a "Media" slide with the reference picture and text (the closing paragraph's shape is placed
        /// before the others in XML but lowest on the slide, to test position ordering).
        /// </summary>
        /// <returns>PPTX bytes.</returns>
        public static byte[] BuildReference()
        {
            return Build(delegate (PptxDeckBuilder deck)
            {
                deck.AddSlide(new List<OpenXmlElement>
                {
                    TitlePlaceholder(2U, P.PlaceholderValues.CenteredTitle, null, ReferenceContent.Heading1),
                    TitlePlaceholder(3U, P.PlaceholderValues.SubTitle, 1U, Subtitle)
                }, Notes, null, null);

                A.Paragraph[] listParagraphs = new A.Paragraph[]
                {
                    Bullet(ReferenceContent.Bullets[0], 0),
                    Bullet(ReferenceContent.Bullets[1], 0),
                    Bullet(ReferenceContent.NestedBullet, 1),
                    Bullet(ReferenceContent.DeepBullet, 2),
                    Bullet(ReferenceContent.Bullets[2], 0),
                    Numbered(ReferenceContent.Steps[0]),
                    Numbered(ReferenceContent.Steps[1]),
                    Numbered(ReferenceContent.Steps[2])
                };
                deck.AddSlide(new List<OpenXmlElement>
                {
                    TitlePlaceholder(2U, P.PlaceholderValues.Title, null, ReferenceContent.HeadingLists),
                    TextBox(3U, 1825625, listParagraphs)
                }, null, null, null);

                deck.AddSlide(new List<OpenXmlElement>
                {
                    TitlePlaceholder(2U, P.PlaceholderValues.Title, null, ReferenceContent.HeadingTable),
                    Table(3U, ReferenceContent.TableRows)
                }, null, null, null);

                A.Paragraph styled = new A.Paragraph(
                    Run(ReferenceContent.StyledLead, null),
                    Run(ReferenceContent.BoldText, new A.RunProperties { Language = "en-US", Bold = true }),
                    Run(", ", null),
                    Run(ReferenceContent.ItalicText, new A.RunProperties { Language = "en-US", Italic = true }),
                    Run(" and ", null),
                    Run(ReferenceContent.InlineCode, new A.RunProperties(new A.LatinFont { Typeface = "Courier New" }) { Language = "en-US" }),
                    Run(" and a ", null),
                    Run(ReferenceContent.LinkText, new A.RunProperties(new A.HyperlinkOnClick { Id = "rIdLink" }) { Language = "en-US" }),
                    Run(".", null));
                deck.AddSlide(new List<OpenXmlElement>
                {
                    TextBox(5U, 5600000, new A.Paragraph[] { Plain(ReferenceContent.Closing) }),
                    TitlePlaceholder(2U, P.PlaceholderValues.Title, null, MediaTitle),
                    TextBox(3U, 1700000, new A.Paragraph[]
                    {
                        styled,
                        Plain(ReferenceContent.International),
                        Plain(ReferenceContent.Special),
                        new A.Paragraph(Run(ReferenceContent.QuoteText, new A.RunProperties { Language = "en-US", Italic = true }))
                    }),
                    Picture(4U, 3600000, ReferenceContent.ImageAlt)
                }, null, ReferenceContent.ImagePng(), ReferenceContent.LinkUrl);
            });
        }

        /// <summary>
        /// A valid zip that lacks the presentation part.
        /// </summary>
        /// <returns>Zip bytes.</returns>
        public static byte[] BuildMissingPresentation()
        {
            return XlsxFixtureBuilder.BuildMissingWorkbook();
        }

        private static byte[] Build(Action<PptxDeckBuilder> configure)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (PresentationDocument doc = PresentationDocument.Create(ms, DocumentFormat.OpenXml.PresentationDocumentType.Presentation))
                {
                    PptxDeckBuilder deck = new PptxDeckBuilder(doc);
                    configure(deck);
                    deck.Finish();
                }

                return ms.ToArray();
            }
        }

        private static P.Shape TitlePlaceholder(uint id, P.PlaceholderValues type, uint? index, string text)
        {
            P.PlaceholderShape ph = new P.PlaceholderShape { Type = type };
            if (index.HasValue) ph.Index = index.Value;
            return new P.Shape(
                new P.NonVisualShapeProperties(new P.NonVisualDrawingProperties { Id = id, Name = "Title " + id }, new P.NonVisualShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties(ph)),
                new P.ShapeProperties(),
                new P.TextBody(new A.BodyProperties(), new A.ListStyle(), Plain(text)));
        }

        private static P.Shape TextBox(uint id, long y, A.Paragraph[] paragraphs)
        {
            P.TextBody body = new P.TextBody(new A.BodyProperties(), new A.ListStyle());
            foreach (A.Paragraph p in paragraphs) body.Append(p);
            return new P.Shape(
                new P.NonVisualShapeProperties(new P.NonVisualDrawingProperties { Id = id, Name = "TextBox " + id }, new P.NonVisualShapeDrawingProperties { TextBox = true }, new P.ApplicationNonVisualDrawingProperties()),
                new P.ShapeProperties(new A.Transform2D(new A.Offset { X = 838200, Y = y }, new A.Extents { Cx = 10515600, Cy = 1000000 }), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }),
                body);
        }

        private static P.GraphicFrame Table(uint id, string[][] rows)
        {
            A.Table table = new A.Table(new A.TableProperties { FirstRow = true, BandRow = true });
            A.TableGrid grid = new A.TableGrid();
            for (int c = 0; c < rows[0].Length; c++) grid.Append(new A.GridColumn { Width = 3000000 });
            table.Append(grid);
            foreach (string[] row in rows)
            {
                A.TableRow tr = new A.TableRow { Height = 370840 };
                foreach (string cell in row)
                    tr.Append(new A.TableCell(new A.TextBody(new A.BodyProperties(), new A.ListStyle(), Plain(cell)), new A.TableCellProperties()));
                table.Append(tr);
            }

            return new P.GraphicFrame(
                new P.NonVisualGraphicFrameProperties(new P.NonVisualDrawingProperties { Id = id, Name = "Table " + id }, new P.NonVisualGraphicFrameDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                new P.Transform(new A.Offset { X = 838200, Y = 1825625 }, new A.Extents { Cx = 9000000, Cy = 1500000 }),
                new A.Graphic(new A.GraphicData(table) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/table" }));
        }

        private static P.Picture Picture(uint id, long y, string alt)
        {
            return new P.Picture(
                new P.NonVisualPictureProperties(new P.NonVisualDrawingProperties { Id = id, Name = "Picture " + id, Description = alt }, new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }), new P.ApplicationNonVisualDrawingProperties()),
                new P.BlipFill(new A.Blip { Embed = "rIdImg" }, new A.Stretch(new A.FillRectangle())),
                new P.ShapeProperties(new A.Transform2D(new A.Offset { X = 838200, Y = y }, new A.Extents { Cx = 914400, Cy = 914400 }), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        }

        private static A.Paragraph Bullet(string text, int level)
        {
            return new A.Paragraph(
                new A.ParagraphProperties(new A.CharacterBullet { Char = "•" }) { Level = level, LeftMargin = 342900 * (level + 1), Indent = -342900 },
                Run(text, null));
        }

        private static A.Paragraph Numbered(string text)
        {
            return new A.Paragraph(
                new A.ParagraphProperties(new A.AutoNumberedBullet { Type = A.TextAutoNumberSchemeValues.ArabicPeriod }) { LeftMargin = 342900, Indent = -342900 },
                Run(text, null));
        }

        private static A.Paragraph Plain(string text)
        {
            return new A.Paragraph(Run(text, null));
        }

        private static A.Run Run(string text, A.RunProperties? props)
        {
            return new A.Run(props ?? new A.RunProperties { Language = "en-US" }, new A.Text(text));
        }
    }
}
