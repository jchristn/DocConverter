namespace DocConverter.Writers.Docx
{
    using DocConverter.Readers.Docx;
    using DocumentFormat.OpenXml;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// The style definitions every written DOCX carries: Normal, Title, Heading1 to Heading6, Code, InlineCode, Quote,
    /// ListParagraph, Caption, Hyperlink, TableNormal and TableGrid.
    /// </summary>
    internal static class DocxStyleSheet
    {
        internal const string BodyFont = "Calibri";

        private static readonly string[] _HeadingSizes = new string[] { "32", "28", "26", "24", "22", "22" };

        internal static W.Styles Build()
        {
            W.Styles styles = new W.Styles();
            styles.Append(new W.DocDefaults(
                new W.RunPropertiesDefault(new W.RunPropertiesBaseStyle(
                    new W.RunFonts { Ascii = BodyFont, HighAnsi = BodyFont, EastAsia = BodyFont, ComplexScript = BodyFont },
                    new W.FontSize { Val = "22" },
                    new W.FontSizeComplexScript { Val = "22" },
                    new W.Languages { Val = "en-US" })),
                new W.ParagraphPropertiesDefault(new W.ParagraphPropertiesBaseStyle(
                    new W.SpacingBetweenLines { After = "160", Line = "259", LineRule = W.LineSpacingRuleValues.Auto }))));

            W.Style normal = ParagraphStyle("Normal", "Normal", null);
            normal.Default = OnOffValue.FromBoolean(true);
            styles.Append(normal);

            W.Style title = ParagraphStyle("Title", "Title", "Normal");
            title.StyleParagraphProperties = new W.StyleParagraphProperties(new W.SpacingBetweenLines { After = "240" });
            title.StyleRunProperties = new W.StyleRunProperties(new W.FontSize { Val = "56" });
            styles.Append(title);

            for (int level = 1; level <= 6; level++)
            {
                W.Style heading = ParagraphStyle("Heading" + level, "heading " + level, "Normal");
                heading.StyleParagraphProperties = new W.StyleParagraphProperties(
                    new W.KeepNext(),
                    new W.SpacingBetweenLines { Before = "240", After = "80" },
                    new W.OutlineLevel { Val = level - 1 });
                heading.StyleRunProperties = new W.StyleRunProperties(
                    new W.Bold(),
                    new W.FontSize { Val = _HeadingSizes[level - 1] });
                styles.Append(heading);
            }

            W.Style code = ParagraphStyle("Code", "Code", "Normal");
            code.StyleParagraphProperties = new W.StyleParagraphProperties(
                new W.Shading { Val = W.ShadingPatternValues.Clear, Color = "auto", Fill = "F2F2F2" },
                new W.SpacingBetweenLines { After = "120", Line = "240", LineRule = W.LineSpacingRuleValues.Auto });
            code.StyleRunProperties = new W.StyleRunProperties(
                new W.RunFonts { Ascii = DocxFonts.CodeFont, HighAnsi = DocxFonts.CodeFont, ComplexScript = DocxFonts.CodeFont },
                new W.FontSize { Val = "20" });
            styles.Append(code);

            W.Style inlineCode = new W.Style { Type = W.StyleValues.Character, StyleId = "InlineCode" };
            inlineCode.StyleName = new W.StyleName { Val = "Inline Code" };
            inlineCode.StyleRunProperties = new W.StyleRunProperties(
                new W.RunFonts { Ascii = DocxFonts.CodeFont, HighAnsi = DocxFonts.CodeFont, ComplexScript = DocxFonts.CodeFont });
            styles.Append(inlineCode);

            W.Style quote = ParagraphStyle("Quote", "Quote", "Normal");
            quote.StyleParagraphProperties = new W.StyleParagraphProperties(new W.Indentation { Left = "720", Right = "720" });
            quote.StyleRunProperties = new W.StyleRunProperties(new W.Italic(), new W.Color { Val = "404040" });
            styles.Append(quote);

            W.Style listParagraph = ParagraphStyle("ListParagraph", "List Paragraph", "Normal");
            listParagraph.StyleParagraphProperties = new W.StyleParagraphProperties(
                new W.Indentation { Left = "720" },
                new W.ContextualSpacing());
            styles.Append(listParagraph);

            W.Style caption = ParagraphStyle("Caption", "caption", "Normal");
            caption.StyleRunProperties = new W.StyleRunProperties(new W.Italic(), new W.FontSize { Val = "18" });
            styles.Append(caption);

            W.Style hyperlink = new W.Style { Type = W.StyleValues.Character, StyleId = "Hyperlink" };
            hyperlink.StyleName = new W.StyleName { Val = "Hyperlink" };
            hyperlink.StyleRunProperties = new W.StyleRunProperties(
                new W.Color { Val = "0563C1" },
                new W.Underline { Val = W.UnderlineValues.Single });
            styles.Append(hyperlink);

            W.Style tableNormal = new W.Style { Type = W.StyleValues.Table, StyleId = "TableNormal" };
            tableNormal.Default = OnOffValue.FromBoolean(true);
            tableNormal.StyleName = new W.StyleName { Val = "Normal Table" };
            styles.Append(tableNormal);

            W.Style grid = new W.Style { Type = W.StyleValues.Table, StyleId = "TableGrid" };
            grid.StyleName = new W.StyleName { Val = "Table Grid" };
            grid.BasedOn = new W.BasedOn { Val = "TableNormal" };
            grid.StyleParagraphProperties = new W.StyleParagraphProperties(new W.SpacingBetweenLines { After = "0", Line = "240", LineRule = W.LineSpacingRuleValues.Auto });
            grid.StyleTableProperties = new W.StyleTableProperties(new W.TableBorders(
                new W.TopBorder { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                new W.LeftBorder { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                new W.BottomBorder { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                new W.RightBorder { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" }));
            styles.Append(grid);
            return styles;
        }

        private static W.Style ParagraphStyle(string id, string name, string? basedOn)
        {
            W.Style style = new W.Style { Type = W.StyleValues.Paragraph, StyleId = id };
            style.StyleName = new W.StyleName { Val = name };
            if (basedOn != null) style.BasedOn = new W.BasedOn { Val = basedOn };
            if (id != "Normal") style.NextParagraphStyle = new W.NextParagraphStyle { Val = "Normal" };
            style.PrimaryStyle = new W.PrimaryStyle();
            return style;
        }
    }
}
