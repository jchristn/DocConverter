namespace DocConverter.Writers.Pptx
{
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using A = DocumentFormat.OpenXml.Drawing;
    using P = DocumentFormat.OpenXml.Presentation;

    /// <summary>
    /// Builds the fixed parts of a presentation in code: a 16:9 presentation part, an Office style theme, one slide master
    /// and two layouts (title slide, and title plus content). No template binary is shipped.
    /// </summary>
    internal sealed class PresentationScaffold
    {
        internal const long SlideWidth = 12192000L;
        internal const long SlideHeight = 6858000L;
        internal const long ContentLeft = 838200L;
        internal const long ContentWidth = 10515600L;
        internal const long TitleTop = 365125L;
        internal const long TitleHeight = 1325563L;
        internal const long ContentTop = 1825625L;
        internal const long ContentBottom = 6356350L;

        private const uint _MasterId = 2147483648U;

        internal PresentationPart Presentation { get; }

        internal SlideLayoutPart TitleLayout { get; }

        internal SlideLayoutPart ContentLayout { get; }

        private PresentationScaffold(PresentationPart presentation, SlideLayoutPart titleLayout, SlideLayoutPart contentLayout)
        {
            Presentation = presentation;
            TitleLayout = titleLayout;
            ContentLayout = contentLayout;
        }

        internal static PresentationScaffold Create(PresentationDocument document)
        {
            PresentationPart presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation(
                new P.SlideMasterIdList(new P.SlideMasterId { Id = _MasterId, RelationshipId = "rIdMaster1" }),
                new P.SlideIdList(),
                new P.SlideSize { Cx = (int)SlideWidth, Cy = (int)SlideHeight },
                new P.NotesSize { Cx = 6858000, Cy = 9144000 },
                new P.DefaultTextStyle());

            SlideMasterPart master = presentationPart.AddNewPart<SlideMasterPart>("rIdMaster1");
            SlideLayoutPart titleLayout = master.AddNewPart<SlideLayoutPart>("rIdLayout1");
            SlideLayoutPart contentLayout = master.AddNewPart<SlideLayoutPart>("rIdLayout2");
            ThemePart theme = master.AddNewPart<ThemePart>("rIdTheme1");
            theme.Theme = BuildTheme();
            presentationPart.AddPart(theme, "rIdTheme1");

            master.SlideMaster = new P.SlideMaster(
                new P.CommonSlideData(new P.ShapeTree(
                    GroupProperties(),
                    new P.GroupShapeProperties(new A.TransformGroup()),
                    Placeholder(2U, "Title Placeholder 1", P.PlaceholderValues.Title, null, ContentLeft, TitleTop, ContentWidth, TitleHeight),
                    Placeholder(3U, "Text Placeholder 2", P.PlaceholderValues.Body, 1U, ContentLeft, ContentTop, ContentWidth, ContentBottom - ContentTop))),
                new P.ColorMap
                {
                    Background1 = A.ColorSchemeIndexValues.Light1,
                    Text1 = A.ColorSchemeIndexValues.Dark1,
                    Background2 = A.ColorSchemeIndexValues.Light2,
                    Text2 = A.ColorSchemeIndexValues.Dark2,
                    Accent1 = A.ColorSchemeIndexValues.Accent1,
                    Accent2 = A.ColorSchemeIndexValues.Accent2,
                    Accent3 = A.ColorSchemeIndexValues.Accent3,
                    Accent4 = A.ColorSchemeIndexValues.Accent4,
                    Accent5 = A.ColorSchemeIndexValues.Accent5,
                    Accent6 = A.ColorSchemeIndexValues.Accent6,
                    Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
                    FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
                },
                new P.SlideLayoutIdList(
                    new P.SlideLayoutId { Id = 2147483649U, RelationshipId = "rIdLayout1" },
                    new P.SlideLayoutId { Id = 2147483650U, RelationshipId = "rIdLayout2" }),
                new P.TextStyles(new P.TitleStyle(), new P.BodyStyle(), new P.OtherStyle()));

            titleLayout.SlideLayout = new P.SlideLayout(
                new P.CommonSlideData(new P.ShapeTree(
                    GroupProperties(),
                    new P.GroupShapeProperties(new A.TransformGroup()),
                    Placeholder(2U, "Title 1", P.PlaceholderValues.CenteredTitle, null, 1524000L, 1122363L, 9144000L, 2387600L),
                    Placeholder(3U, "Subtitle 2", P.PlaceholderValues.SubTitle, 1U, 1524000L, 3602038L, 9144000L, 1655762L)))
                { Name = "Title Slide" },
                new P.ColorMapOverride(new A.MasterColorMapping()))
            { Type = P.SlideLayoutValues.Title, Preserve = true };
            titleLayout.AddPart(master, "rIdMaster1");

            contentLayout.SlideLayout = new P.SlideLayout(
                new P.CommonSlideData(new P.ShapeTree(
                    GroupProperties(),
                    new P.GroupShapeProperties(new A.TransformGroup()),
                    Placeholder(2U, "Title 1", P.PlaceholderValues.Title, null, ContentLeft, TitleTop, ContentWidth, TitleHeight),
                    Placeholder(3U, "Content Placeholder 2", null, 1U, ContentLeft, ContentTop, ContentWidth, ContentBottom - ContentTop)))
                { Name = "Title and Content" },
                new P.ColorMapOverride(new A.MasterColorMapping()))
            { Type = P.SlideLayoutValues.Object, Preserve = true };
            contentLayout.AddPart(master, "rIdMaster1");

            return new PresentationScaffold(presentationPart, titleLayout, contentLayout);
        }

        internal static P.NonVisualGroupShapeProperties GroupProperties()
        {
            return new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties());
        }

        private static P.Shape Placeholder(uint id, string name, P.PlaceholderValues? type, uint? index, long x, long y, long cx, long cy)
        {
            P.PlaceholderShape placeholder = new P.PlaceholderShape();
            if (type.HasValue) placeholder.Type = type.Value;
            if (index.HasValue) placeholder.Index = index.Value;

            return new P.Shape(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = name },
                    new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                    new P.ApplicationNonVisualDrawingProperties(placeholder)),
                new P.ShapeProperties(new A.Transform2D(new A.Offset { X = x, Y = y }, new A.Extents { Cx = cx, Cy = cy })),
                new P.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.EndParagraphRunProperties { Language = "en-US" })));
        }

        private static A.Theme BuildTheme()
        {
            A.ColorScheme colors = new A.ColorScheme(
                new A.Dark1Color(new A.SystemColor { Val = A.SystemColorValues.WindowText, LastColor = "000000" }),
                new A.Light1Color(new A.SystemColor { Val = A.SystemColorValues.Window, LastColor = "FFFFFF" }),
                new A.Dark2Color(new A.RgbColorModelHex { Val = "1F497D" }),
                new A.Light2Color(new A.RgbColorModelHex { Val = "EEECE1" }),
                new A.Accent1Color(new A.RgbColorModelHex { Val = "4F81BD" }),
                new A.Accent2Color(new A.RgbColorModelHex { Val = "C0504D" }),
                new A.Accent3Color(new A.RgbColorModelHex { Val = "9BBB59" }),
                new A.Accent4Color(new A.RgbColorModelHex { Val = "8064A2" }),
                new A.Accent5Color(new A.RgbColorModelHex { Val = "4BACC6" }),
                new A.Accent6Color(new A.RgbColorModelHex { Val = "F79646" }),
                new A.Hyperlink(new A.RgbColorModelHex { Val = "0000FF" }),
                new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = "800080" }))
            { Name = "Office" };

            A.FontScheme fonts = new A.FontScheme(
                new A.MajorFont(new A.LatinFont { Typeface = "Calibri" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" }),
                new A.MinorFont(new A.LatinFont { Typeface = "Calibri" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" }))
            { Name = "Office" };

            A.FormatScheme format = new A.FormatScheme(
                new A.FillStyleList(Fill(), Fill(), Fill()),
                new A.LineStyleList(
                    new A.Outline(Fill()) { Width = 9525 },
                    new A.Outline(Fill()) { Width = 25400 },
                    new A.Outline(Fill()) { Width = 38100 }),
                new A.EffectStyleList(new A.EffectStyle(new A.EffectList()), new A.EffectStyle(new A.EffectList()), new A.EffectStyle(new A.EffectList())),
                new A.BackgroundFillStyleList(Fill(), Fill(), Fill()))
            { Name = "Office" };

            return new A.Theme(new A.ThemeElements(colors, fonts, format), new A.ObjectDefaults(), new A.ExtraColorSchemeList()) { Name = "Office Theme" };
        }

        private static A.SolidFill Fill()
        {
            return new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor });
        }
    }
}
