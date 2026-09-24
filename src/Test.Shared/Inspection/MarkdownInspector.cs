namespace Test.Shared.Inspection
{
    using System.Collections.Generic;
    using System.Text;
    using Markdig;
    using Markdig.Extensions.Tables;
    using Markdig.Syntax;
    using Markdig.Syntax.Inlines;

    /// <summary>
    /// Inspects Markdown output by parsing it with Markdig directly (not through DocConverter's reader).
    /// </summary>
    public static class MarkdownInspector
    {
        private static readonly MarkdownPipeline _Pipeline = new MarkdownPipelineBuilder().UsePipeTables().UseTaskLists().UseEmphasisExtras().UseAutoLinks().UseYamlFrontMatter().Build();

        /// <summary>
        /// Parse and snapshot Markdown output.
        /// </summary>
        /// <param name="bytes">UTF-8 Markdown.</param>
        /// <returns>Snapshot.</returns>
        public static ContentSnapshot Inspect(byte[] bytes)
        {
            string text = TextInspector.DecodeStrict(bytes);
            MarkdownDocument doc = Markdown.Parse(text, _Pipeline);
            ContentSnapshot snapshot = new ContentSnapshot();
            StringBuilder all = new StringBuilder();
            foreach (Block block in doc.Descendants<Block>())
            {
                if (block is HeadingBlock heading) snapshot.Headings.Add(ContentSnapshot.Normalize(InlineText(heading.Inline, snapshot, false)));
                else if (block is ListItemBlock item)
                {
                    foreach (Block child in item)
                    {
                        if (child is ParagraphBlock p)
                        {
                            snapshot.ListItems.Add(ContentSnapshot.Normalize(InlineText(p.Inline, snapshot, false)));
                            break;
                        }
                    }
                }
                else if (block is Table table)
                {
                    foreach (Block rowBlock in table)
                    {
                        if (!(rowBlock is TableRow row)) continue;
                        List<string> cells = new List<string>();
                        foreach (Block cellBlock in row)
                        {
                            StringBuilder cellText = new StringBuilder();
                            foreach (Block inner in ((TableCell)cellBlock).Descendants<Block>())
                                if (inner is LeafBlock leaf && leaf.Inline != null) cellText.Append(InlineText(leaf.Inline, snapshot, false));
                            cells.Add(ContentSnapshot.Normalize(cellText.ToString()));
                        }

                        snapshot.TableRows.Add(cells);
                    }
                }

                if (block is LeafBlock leafBlock)
                {
                    if (leafBlock is CodeBlock code) all.Append(' ').Append(code.Lines.ToString());
                    else if (leafBlock.Inline != null) all.Append(' ').Append(InlineText(leafBlock.Inline, snapshot, true));
                }
            }

            snapshot.AllText = ContentSnapshot.Normalize(all.ToString());
            return snapshot;
        }

        private static string InlineText(ContainerInline? container, ContentSnapshot snapshot, bool collect)
        {
            StringBuilder sb = new StringBuilder();
            if (container == null) return "";
            foreach (Inline inline in container.Descendants<Inline>())
            {
                if (inline is LiteralInline literal) sb.Append(literal.Content.ToString());
                else if (inline is CodeInline code) sb.Append(code.Content);
                else if (inline is LineBreakInline) sb.Append(' ');
                else if (inline is HtmlEntityInline entity) sb.Append(entity.Transcoded.ToString());
                else if (inline is LinkInline link && collect)
                {
                    if (link.IsImage)
                    {
                        if ((link.Url ?? "").StartsWith("data:image/", System.StringComparison.Ordinal)) snapshot.ImageCount++;
                    }
                    else snapshot.LinkUrls.Add(link.Url ?? "");
                }
            }

            return sb.ToString();
        }
    }
}
