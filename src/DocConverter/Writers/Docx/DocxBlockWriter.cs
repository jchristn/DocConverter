namespace DocConverter.Writers.Docx
{
    using System.Collections.Generic;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocumentFormat.OpenXml;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Writes model blocks as Word body content: styled headings and paragraphs, numbered lists, tables, code, quotes,
    /// images, breaks and sections.
    /// </summary>
    internal sealed class DocxBlockWriter
    {
        private readonly DocxWriteSession _Session;
        private readonly DocxInlineWriter _Inlines;
        private readonly DocxTableWriter _Tables;
        private bool _WrotePageSection = false;

        internal DocxBlockWriter(DocxWriteSession session)
        {
            _Session = session;
            _Inlines = new DocxInlineWriter(session);
            _Tables = new DocxTableWriter(session, this);
        }

        internal void WriteBlocks(IEnumerable<Block> blocks, OpenXmlCompositeElement parent)
        {
            foreach (Block block in blocks) WriteBlock(block, parent, null);
        }

        private void WriteBlock(Block block, OpenXmlCompositeElement parent, string? paragraphStyle)
        {
            _Session.Token.ThrowIfCancellationRequested();
            switch (block)
            {
                case HeadingBlock heading:
                    parent.Append(Paragraph(heading.Inlines, "Heading" + heading.Level, TextAlignmentEnum.Default));
                    break;
                case ParagraphBlock paragraph:
                    parent.Append(Paragraph(paragraph.Inlines, paragraphStyle, paragraph.Alignment));
                    break;
                case ListBlock list:
                    WriteList(list, parent, 0);
                    break;
                case ListItemBlock looseItem:
                    ListBlock wrapper = new ListBlock(ListKindEnum.Unordered);
                    wrapper.Items.Add(looseItem);
                    WriteList(wrapper, parent, 0);
                    break;
                case TableBlock table:
                    parent.Append(_Tables.Write(table));
                    if (!string.IsNullOrEmpty(table.Caption)) parent.Append(Paragraph(new List<Inline> { new TextInline(table.Caption) }, "Caption", TextAlignmentEnum.Default));
                    break;
                case CodeBlock code:
                    parent.Append(CodeParagraph(code.Text));
                    break;
                case QuoteBlock quote:
                    foreach (Block child in quote.Blocks) WriteBlock(child, parent, "Quote");
                    break;
                case ImageBlock image:
                    W.Paragraph imageParagraph = new W.Paragraph();
                    if (paragraphStyle != null) imageParagraph.Append(new W.ParagraphProperties(new W.ParagraphStyleId { Val = paragraphStyle }));
                    _Inlines.WriteImage(imageParagraph, image.ResourceId, image.AltText, image.Width, image.Height);
                    parent.Append(imageParagraph);
                    if (!string.IsNullOrEmpty(image.Caption)) parent.Append(Paragraph(new List<Inline> { new TextInline(image.Caption) }, "Caption", TextAlignmentEnum.Default));
                    break;
                case ThematicBreakBlock _:
                    W.ParagraphProperties rulePr = new W.ParagraphProperties();
                    rulePr.ParagraphBorders = new W.ParagraphBorders(new W.BottomBorder { Val = W.BorderValues.Single, Size = 6, Space = 1, Color = "auto" });
                    parent.Append(new W.Paragraph(rulePr));
                    break;
                case PageBreakBlock _:
                    parent.Append(new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page })));
                    break;
                case SectionBlock section:
                    WriteSection(section, parent);
                    break;
            }
        }

        private void WriteSection(SectionBlock section, OpenXmlCompositeElement parent)
        {
            if (section.Kind == SectionKindEnum.Page)
            {
                if (_WrotePageSection) parent.Append(new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page })));
                _WrotePageSection = true;
            }

            if (!string.IsNullOrEmpty(section.Title))
                parent.Append(Paragraph(new List<Inline> { new TextInline(section.Title) }, "Heading2", TextAlignmentEnum.Default));
            foreach (Block child in section.Blocks) WriteBlock(child, parent, null);
        }

        private void WriteList(ListBlock list, OpenXmlCompositeElement parent, int depth)
        {
            int level = depth;
            if (level > DocxNumberingBuilder.MaxLevel)
            {
                level = DocxNumberingBuilder.MaxLevel;
                _Session.Context.AddWarning(WarningCodeEnum.NestedDepthLimited, "A list nested deeper than Word's nine levels was written at the ninth level.");
            }

            int numId = _Session.Numbering.AddList(list.Kind, level, list.Start);
            foreach (ListItemBlock item in list.Items)
            {
                bool numbered = false;
                foreach (Block child in item.Blocks)
                {
                    if (!numbered && child is ParagraphBlock paragraph)
                    {
                        List<Inline> inlines = new List<Inline>();
                        if (list.Kind == ListKindEnum.Task) inlines.Add(new TextInline(item.Checked == true ? "[x] " : "[ ] "));
                        inlines.AddRange(paragraph.Inlines);
                        parent.Append(ListParagraph(inlines, numId, level));
                        numbered = true;
                        continue;
                    }

                    if (child is ListBlock nested)
                    {
                        if (!numbered)
                        {
                            parent.Append(ListParagraph(new List<Inline>(), numId, level));
                            numbered = true;
                        }

                        WriteList(nested, parent, depth + 1);
                        continue;
                    }

                    if (child is ParagraphBlock continuation)
                    {
                        W.Paragraph p = Paragraph(continuation.Inlines, "ListParagraph", continuation.Alignment);
                        W.ParagraphProperties? pPr = p.ParagraphProperties;
                        if (pPr != null) pPr.Indentation = new W.Indentation { Left = ((level + 1) * 720).ToString() };
                        parent.Append(p);
                        continue;
                    }

                    if (!numbered)
                    {
                        parent.Append(ListParagraph(new List<Inline>(), numId, level));
                        numbered = true;
                    }

                    WriteBlock(child, parent, null);
                }

                if (!numbered) parent.Append(ListParagraph(new List<Inline>(), numId, level));
            }
        }

        private W.Paragraph ListParagraph(List<Inline> inlines, int numId, int level)
        {
            W.Paragraph p = new W.Paragraph();
            W.ParagraphProperties pPr = new W.ParagraphProperties();
            pPr.ParagraphStyleId = new W.ParagraphStyleId { Val = "ListParagraph" };
            pPr.NumberingProperties = new W.NumberingProperties(
                new W.NumberingLevelReference { Val = level },
                new W.NumberingId { Val = numId });
            p.Append(pPr);
            _Inlines.Write(inlines, p, InlineStyleEnum.None, false);
            return p;
        }

        private W.Paragraph Paragraph(List<Inline> inlines, string? style, TextAlignmentEnum alignment)
        {
            W.Paragraph p = new W.Paragraph();
            W.JustificationValues? justification = Justification(alignment);
            if (style != null || justification != null)
            {
                W.ParagraphProperties pPr = new W.ParagraphProperties();
                if (style != null) pPr.ParagraphStyleId = new W.ParagraphStyleId { Val = style };
                if (justification != null) pPr.Justification = new W.Justification { Val = justification.Value };
                p.Append(pPr);
            }

            _Inlines.Write(inlines, p, InlineStyleEnum.None, false);
            return p;
        }

        private static W.Paragraph CodeParagraph(string text)
        {
            W.Paragraph p = new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = "Code" }));
            DocxInlineWriter.AppendText(p, text, InlineStyleEnum.None, false);
            return p;
        }

        private static W.JustificationValues? Justification(TextAlignmentEnum alignment)
        {
            switch (alignment)
            {
                case TextAlignmentEnum.Left: return W.JustificationValues.Left;
                case TextAlignmentEnum.Center: return W.JustificationValues.Center;
                case TextAlignmentEnum.Right: return W.JustificationValues.Right;
                case TextAlignmentEnum.Justify: return W.JustificationValues.Both;
                default: return null;
            }
        }
    }
}
