namespace DocConverter.Readers.Pptx
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using A = DocumentFormat.OpenXml.Drawing;

    /// <summary>
    /// Converts DrawingML text bodies into model blocks: bulleted and numbered paragraphs become nested lists, other
    /// paragraphs become paragraphs with styled runs and hyperlinks.
    /// </summary>
    internal static class PptxTextReader
    {
        private static readonly HashSet<string> _MonospaceFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Courier", "Courier New", "Consolas", "Liberation Mono", "Menlo", "Monaco", "Lucida Console", "Cascadia Code",
            "Cascadia Mono", "DejaVu Sans Mono", "Source Code Pro", "Fira Code", "Fira Mono", "Roboto Mono", "Andale Mono"
        };

        internal static List<Block> Blocks(OpenXmlElement textBody, OpenXmlPart part, bool defaultBullets, int maxDepth)
        {
            List<Block> blocks = new List<Block>();
            List<ListBlock> stack = new List<ListBlock>();
            foreach (A.Paragraph paragraph in textBody.Elements<A.Paragraph>())
            {
                List<Inline> inlines = Inlines(paragraph, part);
                bool empty = ModelTextIsBlank(inlines);
                A.ParagraphProperties? props = paragraph.ParagraphProperties;
                bool bulleted = defaultBullets;
                bool ordered = false;
                int start = 1;
                if (props != null)
                {
                    if (props.GetFirstChild<A.NoBullet>() != null) bulleted = false;
                    A.AutoNumberedBullet? auto = props.GetFirstChild<A.AutoNumberedBullet>();
                    if (auto != null)
                    {
                        bulleted = true;
                        ordered = true;
                        if (auto.StartAt != null && auto.StartAt.HasValue) start = auto.StartAt.Value;
                    }
                    else if (props.GetFirstChild<A.CharacterBullet>() != null || props.GetFirstChild<A.PictureBullet>() != null)
                    {
                        bulleted = true;
                    }
                }

                if (empty)
                {
                    if (!bulleted) stack.Clear();
                    continue;
                }

                if (!bulleted)
                {
                    stack.Clear();
                    blocks.Add(new ParagraphBlock(inlines));
                    continue;
                }

                int level = props?.Level != null && props.Level.HasValue ? props.Level.Value : 0;
                if (level < 0) level = 0;
                if (level > maxDepth - 1) level = maxDepth - 1;
                ListKindEnum kind = ordered ? ListKindEnum.Ordered : ListKindEnum.Unordered;

                if (stack.Count == 0 || (level == 0 && stack[0].Kind != kind))
                {
                    stack.Clear();
                    ListBlock root = new ListBlock(kind);
                    root.Start = start;
                    blocks.Add(root);
                    stack.Add(root);
                }

                while (stack.Count - 1 > level) stack.RemoveAt(stack.Count - 1);
                while (stack.Count - 1 < level)
                {
                    ListBlock parent = stack[stack.Count - 1];
                    if (parent.Items.Count == 0) parent.Items.Add(new ListItemBlock());
                    ListItemBlock last = parent.Items[parent.Items.Count - 1];
                    ListBlock nested = new ListBlock(kind);
                    nested.Start = start;
                    last.Blocks.Add(nested);
                    stack.Add(nested);
                }

                ListBlock target = stack[stack.Count - 1];
                if (target.Kind != kind && target.Items.Count == 0)
                {
                    target.Kind = kind;
                    target.Start = start;
                }

                ListItemBlock item = new ListItemBlock();
                item.Blocks.Add(new ParagraphBlock(inlines));
                target.Items.Add(item);
            }

            return blocks;
        }

        internal static string PlainText(OpenXmlElement textBody)
        {
            StringBuilder sb = new StringBuilder();
            foreach (A.Paragraph paragraph in textBody.Elements<A.Paragraph>())
            {
                string line = ParagraphText(paragraph);
                if (line.Trim().Length == 0) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(line.Trim());
            }

            return sb.ToString();
        }

        internal static List<Inline> Inlines(A.Paragraph paragraph, OpenXmlPart part)
        {
            List<Inline> inlines = new List<Inline>();
            foreach (OpenXmlElement child in paragraph.ChildElements)
            {
                if (child is A.Run run)
                {
                    string text = run.Text?.Text ?? "";
                    if (text.Length == 0) continue;
                    AddRun(inlines, text, run.RunProperties, part);
                }
                else if (child is A.Field field)
                {
                    string text = field.Text?.Text ?? "";
                    if (text.Length > 0) AddRun(inlines, text, field.RunProperties, part);
                }
                else if (child is A.Break)
                {
                    inlines.Add(new LineBreakInline());
                }
            }

            return inlines;
        }

        private static void AddRun(List<Inline> inlines, string text, A.TextCharacterPropertiesType? props, OpenXmlPart part)
        {
            InlineStyleEnum style = InlineStyleEnum.None;
            string? url = null;
            if (props != null)
            {
                if (props.Bold != null && props.Bold.HasValue && props.Bold.Value) style |= InlineStyleEnum.Bold;
                if (props.Italic != null && props.Italic.HasValue && props.Italic.Value) style |= InlineStyleEnum.Italic;
                if (props.Underline != null && props.Underline.HasValue && props.Underline.Value != A.TextUnderlineValues.None) style |= InlineStyleEnum.Underline;
                if (props.Strike != null && props.Strike.HasValue && props.Strike.Value != A.TextStrikeValues.NoStrike) style |= InlineStyleEnum.Strikethrough;
                if (props.Baseline != null && props.Baseline.HasValue)
                {
                    if (props.Baseline.Value > 0) style |= InlineStyleEnum.Superscript;
                    else if (props.Baseline.Value < 0) style |= InlineStyleEnum.Subscript;
                }

                string? latin = props.GetFirstChild<A.LatinFont>()?.Typeface?.Value;
                if (!string.IsNullOrEmpty(latin) && _MonospaceFonts.Contains(latin!)) style |= InlineStyleEnum.Code;

                A.HyperlinkOnClick? click = props.GetFirstChild<A.HyperlinkOnClick>();
                string? relId = click?.Id?.Value;
                if (!string.IsNullOrEmpty(relId))
                {
                    foreach (HyperlinkRelationship rel in part.HyperlinkRelationships)
                    {
                        if (rel.Id == relId)
                        {
                            url = rel.Uri.OriginalString;
                            break;
                        }
                    }
                }
            }

            TextInline inline = new TextInline(text, style);
            if (url == null)
            {
                inlines.Add(inline);
                return;
            }

            if (inlines.Count > 0 && inlines[inlines.Count - 1] is LinkInline previous && previous.Url == url)
            {
                previous.Inlines.Add(inline);
                return;
            }

            LinkInline link = new LinkInline();
            link.Url = url;
            link.Inlines.Add(inline);
            inlines.Add(link);
        }

        private static string ParagraphText(A.Paragraph paragraph)
        {
            StringBuilder sb = new StringBuilder();
            foreach (OpenXmlElement child in paragraph.ChildElements)
            {
                if (child is A.Run run) sb.Append(run.Text?.Text ?? "");
                else if (child is A.Field field) sb.Append(field.Text?.Text ?? "");
                else if (child is A.Break) sb.Append(' ');
            }

            return sb.ToString();
        }

        private static bool ModelTextIsBlank(List<Inline> inlines)
        {
            foreach (Inline inline in inlines)
            {
                if (inline is TextInline t && t.Text.Trim().Length > 0) return false;
                if (inline is LinkInline) return false;
            }

            return true;
        }
    }
}
