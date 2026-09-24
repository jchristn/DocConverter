namespace Test.Shared.Fixtures.Builders
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using A = DocumentFormat.OpenXml.Drawing;
    using P = DocumentFormat.OpenXml.Presentation;

    /// <summary>
    /// Minimal presentation writer used by the PPTX fixture builders. Raw OpenXml SDK only.
    /// </summary>
    public sealed class PptxDeckBuilder
    {
        private readonly PresentationDocument _Doc;
        private readonly PresentationPart _Presentation;
        private readonly SlideLayoutPart _Layout;
        private readonly P.SlideIdList _SlideIds = new P.SlideIdList();
        private uint _NextSlideId = 256;

        /// <summary>
        /// Instantiate and create the master, layout and theme.
        /// </summary>
        /// <param name="doc">Presentation document.</param>
        public PptxDeckBuilder(PresentationDocument doc)
        {
            _Doc = doc;
            _Presentation = doc.AddPresentationPart();
            SlideMasterPart master = _Presentation.AddNewPart<SlideMasterPart>("rId1");
            _Layout = master.AddNewPart<SlideLayoutPart>("rId1");
            ThemePart theme = master.AddNewPart<ThemePart>("rId2");
            theme.Theme = Theme();
            _Presentation.AddPart(theme, "rId2");

            master.SlideMaster = new P.SlideMaster(
                new P.CommonSlideData(new P.ShapeTree(Group(), new P.GroupShapeProperties(new A.TransformGroup()),
                    Placeholder(2U, P.PlaceholderValues.Title, null, 838200, 365125),
                    Placeholder(3U, P.PlaceholderValues.Body, 1U, 838200, 1825625))),
                new P.ColorMap
                {
                    Background1 = A.ColorSchemeIndexValues.Light1, Text1 = A.ColorSchemeIndexValues.Dark1,
                    Background2 = A.ColorSchemeIndexValues.Light2, Text2 = A.ColorSchemeIndexValues.Dark2,
                    Accent1 = A.ColorSchemeIndexValues.Accent1, Accent2 = A.ColorSchemeIndexValues.Accent2,
                    Accent3 = A.ColorSchemeIndexValues.Accent3, Accent4 = A.ColorSchemeIndexValues.Accent4,
                    Accent5 = A.ColorSchemeIndexValues.Accent5, Accent6 = A.ColorSchemeIndexValues.Accent6,
                    Hyperlink = A.ColorSchemeIndexValues.Hyperlink, FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
                },
                new P.SlideLayoutIdList(new P.SlideLayoutId { Id = 2147483649U, RelationshipId = "rId1" }),
                new P.TextStyles(new P.TitleStyle(), new P.BodyStyle(), new P.OtherStyle()));

            _Layout.SlideLayout = new P.SlideLayout(
                new P.CommonSlideData(new P.ShapeTree(Group(), new P.GroupShapeProperties(new A.TransformGroup()),
                    Placeholder(2U, P.PlaceholderValues.Title, null, 838200, 365125),
                    Placeholder(3U, P.PlaceholderValues.SubTitle, 1U, 1524000, 3602038))),
                new P.ColorMapOverride(new A.MasterColorMapping()));
            _Layout.AddPart(master, "rId1");
        }

        /// <summary>
        /// Add a slide.
        /// </summary>
        /// <param name="shapes">Shapes in document order.</param>
        /// <param name="notes">Speaker notes text, or null.</param>
        /// <param name="image">Image to embed, or null; referenced as relationship id "rIdImg".</param>
        /// <param name="link">Hyperlink target, or null; referenced as relationship id "rIdLink".</param>
        /// <returns>The slide part.</returns>
        public SlidePart AddSlide(List<OpenXmlElement> shapes, string? notes, byte[]? image, string? link)
        {
            string rel = "rIdS" + _NextSlideId;
            SlidePart slide = _Presentation.AddNewPart<SlidePart>(rel);
            slide.AddPart(_Layout, "rIdLayout");
            if (image != null)
            {
                ImagePart part = slide.AddImagePart(ImagePartType.Png, "rIdImg");
                using (MemoryStream ms = new MemoryStream(image)) part.FeedData(ms);
            }

            if (link != null) slide.AddHyperlinkRelationship(new System.Uri(link), true, "rIdLink");

            P.ShapeTree tree = new P.ShapeTree(Group(), new P.GroupShapeProperties(new A.TransformGroup()));
            foreach (OpenXmlElement shape in shapes) tree.Append(shape);
            slide.Slide = new P.Slide(new P.CommonSlideData(tree), new P.ColorMapOverride(new A.MasterColorMapping()));

            if (notes != null)
            {
                NotesSlidePart notesPart = slide.AddNewPart<NotesSlidePart>("rIdNotes");
                notesPart.NotesSlide = new P.NotesSlide(
                    new P.CommonSlideData(new P.ShapeTree(Group(), new P.GroupShapeProperties(new A.TransformGroup()),
                        new P.Shape(
                            new P.NonVisualShapeProperties(
                                new P.NonVisualDrawingProperties { Id = 2U, Name = "Notes Placeholder" },
                                new P.NonVisualShapeDrawingProperties(),
                                new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape { Type = P.PlaceholderValues.Body, Index = 1U })),
                            new P.ShapeProperties(),
                            new P.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text(notes))))))));
                notesPart.AddPart(slide, "rIdSlide");
            }

            _SlideIds.Append(new P.SlideId { Id = _NextSlideId++, RelationshipId = rel });
            return slide;
        }

        /// <summary>
        /// Write the presentation element and core properties.
        /// </summary>
        public void Finish()
        {
            _Presentation.Presentation = new P.Presentation(
                new P.SlideMasterIdList(new P.SlideMasterId { Id = 2147483648U, RelationshipId = "rId1" }),
                _SlideIds,
                new P.SlideSize { Cx = 12192000, Cy = 6858000 },
                new P.NotesSize { Cx = 6858000, Cy = 9144000 },
                new P.DefaultTextStyle());

            CoreFilePropertiesPart core = _Doc.AddCoreFilePropertiesPart();
            string xml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                + "<dc:title>" + ReferenceContent.Title + "</dc:title><dc:creator>" + ReferenceContent.Author + "</dc:creator></cp:coreProperties>";
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(xml))) core.FeedData(ms);
        }

        /// <summary>
        /// Group shape properties every shape tree starts with.
        /// </summary>
        /// <returns>Element.</returns>
        public static P.NonVisualGroupShapeProperties Group()
        {
            return new P.NonVisualGroupShapeProperties(new P.NonVisualDrawingProperties { Id = 1U, Name = "" }, new P.NonVisualGroupShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties());
        }

        private static P.Shape Placeholder(uint id, P.PlaceholderValues type, uint? index, long x, long y)
        {
            P.PlaceholderShape ph = new P.PlaceholderShape { Type = type };
            if (index.HasValue) ph.Index = index.Value;
            return new P.Shape(
                new P.NonVisualShapeProperties(new P.NonVisualDrawingProperties { Id = id, Name = "Placeholder " + id }, new P.NonVisualShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties(ph)),
                new P.ShapeProperties(new A.Transform2D(new A.Offset { X = x, Y = y }, new A.Extents { Cx = 10515600, Cy = 1325563 })),
                new P.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph()));
        }

        private static A.Theme Theme()
        {
            A.SolidFill fill() => new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor });
            return new A.Theme(new A.ThemeElements(
                new A.ColorScheme(
                    new A.Dark1Color(new A.RgbColorModelHex { Val = "000000" }), new A.Light1Color(new A.RgbColorModelHex { Val = "FFFFFF" }),
                    new A.Dark2Color(new A.RgbColorModelHex { Val = "1F497D" }), new A.Light2Color(new A.RgbColorModelHex { Val = "EEECE1" }),
                    new A.Accent1Color(new A.RgbColorModelHex { Val = "4F81BD" }), new A.Accent2Color(new A.RgbColorModelHex { Val = "C0504D" }),
                    new A.Accent3Color(new A.RgbColorModelHex { Val = "9BBB59" }), new A.Accent4Color(new A.RgbColorModelHex { Val = "8064A2" }),
                    new A.Accent5Color(new A.RgbColorModelHex { Val = "4BACC6" }), new A.Accent6Color(new A.RgbColorModelHex { Val = "F79646" }),
                    new A.Hyperlink(new A.RgbColorModelHex { Val = "0000FF" }), new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = "800080" }))
                { Name = "Fixture" },
                new A.FontScheme(
                    new A.MajorFont(new A.LatinFont { Typeface = "Arial" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" }),
                    new A.MinorFont(new A.LatinFont { Typeface = "Arial" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" }))
                { Name = "Fixture" },
                new A.FormatScheme(
                    new A.FillStyleList(fill(), fill(), fill()),
                    new A.LineStyleList(new A.Outline(fill()), new A.Outline(fill()), new A.Outline(fill())),
                    new A.EffectStyleList(new A.EffectStyle(new A.EffectList()), new A.EffectStyle(new A.EffectList()), new A.EffectStyle(new A.EffectList())),
                    new A.BackgroundFillStyleList(fill(), fill(), fill()))
                { Name = "Fixture" }))
            { Name = "Fixture Theme" };
        }
    }
}
