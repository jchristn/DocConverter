namespace DocConverter.Readers.Docx
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Turns Word body elements (paragraphs, tables, content controls) into model blocks: headings, paragraphs, nested
    /// lists, code blocks, quotes, tables with spans, images and page breaks.
    /// </summary>
    internal sealed class DocxBodyReader
    {
        private readonly DocxReadSession _Session;
        private readonly DocxInlineReader _Inlines;

        internal DocxBodyReader(DocxReadSession session)
        {
            _Session = session;
            _Inlines = new DocxInlineReader(session);
        }

        internal List<Block> ReadBlocks(IEnumerable<OpenXmlElement> elements, OpenXmlPart owner, int depth)
        {
            DocxBlockState state = new DocxBlockState(depth);
            foreach (OpenXmlElement element in elements) ReadElement(state, element, owner);
            FlushAll(state);
            return state.Output;
        }

        private void ReadElement(DocxBlockState state, OpenXmlElement element, OpenXmlPart owner)
        {
            _Session.Token.ThrowIfCancellationRequested();
            switch (element)
            {
                case W.Paragraph paragraph:
                    ReadParagraph(state, paragraph, owner);
                    break;
                case W.Table table:
                    FlushAll(state);
                    state.Output.Add(ReadTable(table, owner, state.Depth + 1));
                    break;
                case W.SdtBlock sdt:
                    if (sdt.SdtContentBlock != null)
                        foreach (OpenXmlElement child in sdt.SdtContentBlock.ChildElements) ReadElement(state, child, owner);
                    break;
                case W.CustomXmlBlock custom:
                    foreach (OpenXmlElement child in custom.ChildElements) ReadElement(state, child, owner);
                    break;
                case AlternateContent alternate:
                    OpenXmlElement? branch = (OpenXmlElement?)alternate.GetFirstChild<AlternateContentChoice>() ?? alternate.GetFirstChild<AlternateContentFallback>();
                    if (branch != null)
                        foreach (OpenXmlElement child in branch.ChildElements) ReadElement(state, child, owner);
                    break;
            }
        }

        private void ReadParagraph(DocxBlockState state, W.Paragraph paragraph, OpenXmlPart owner)
        {
            W.ParagraphProperties? pPr = paragraph.ParagraphProperties;
            string? styleId = pPr?.ParagraphStyleId?.Val?.Value;

            int? headingLevel = _Session.Styles.HeadingLevel(styleId);
            int? outline = pPr?.OutlineLevel?.Val?.Value;
            if (outline.HasValue && outline.Value >= 0 && outline.Value < 9) headingLevel = Math.Min(outline.Value + 1, 6);

            DocxNumberingRef? numbering = null;
            W.NumberingProperties? numPr = pPr?.NumberingProperties;
            if (numPr != null && numPr.NumberingId?.Val != null)
                numbering = new DocxNumberingRef(numPr.NumberingId.Val.Value, numPr.NumberingLevelReference?.Val?.Value ?? 0);
            else if (numPr != null && numPr.NumberingLevelReference?.Val != null)
            {
                DocxNumberingRef? fromStyle = _Session.Styles.StyleNumbering(styleId);
                if (fromStyle != null) numbering = new DocxNumberingRef(fromStyle.NumId, numPr.NumberingLevelReference.Val.Value);
            }
            else numbering = _Session.Styles.StyleNumbering(styleId);
            if (numbering != null && numbering.NumId == 0) numbering = null;

            bool codeStyle = _Session.Styles.IsCodeStyle(styleId);
            bool quoteStyle = _Session.Styles.IsQuoteStyle(styleId);
            TextAlignmentEnum alignment = Alignment(pPr?.Justification?.Val);

            if (pPr != null && DocxStyleResolver.IsOn(pPr.PageBreakBefore) && HasContentSoFar(state))
            {
                FlushAll(state);
                state.Output.Add(new PageBreakBlock());
            }

            DocxParagraphContent content = _Inlines.Read(paragraph, owner);
            bool isCode = headingLevel == null && numbering == null && (codeStyle || content.IsAllMonospace);

            for (int s = 0; s < content.Segments.Count; s++)
            {
                if (s > 0)
                {
                    FlushAll(state);
                    state.Output.Add(new PageBreakBlock());
                }

                List<Inline> inlines = content.Segments[s];
                bool empty = IsEmpty(inlines);

                if (isCode)
                {
                    if (empty && state.Code == null && !codeStyle) continue;
                    FlushLists(state);
                    FlushQuote(state);
                    if (state.Code == null) state.Code = new StringBuilder();
                    else state.Code.Append('\n');
                    state.Code.Append(ModelText.Inlines(inlines));
                    continue;
                }

                if (empty) continue;

                if (headingLevel.HasValue)
                {
                    FlushAll(state);
                    HeadingBlock heading = new HeadingBlock();
                    heading.Level = headingLevel.Value;
                    heading.Inlines = TrimEdges(inlines);
                    state.Output.Add(heading);
                    if (_Session.TitleFallback == null && _Session.Styles.IsTitle(styleId)) _Session.TitleFallback = ModelText.Inlines(heading.Inlines).Trim();
                    continue;
                }

                if (numbering != null)
                {
                    FlushCode(state);
                    FlushQuote(state);
                    AddListItem(state, numbering, TrimEdges(inlines));
                    continue;
                }

                if (quoteStyle)
                {
                    FlushCode(state);
                    FlushLists(state);
                    if (state.Quote == null) state.Quote = new QuoteBlock();
                    ParagraphBlock quoted = new ParagraphBlock(TrimEdges(inlines));
                    quoted.Alignment = alignment;
                    state.Quote.Blocks.Add(quoted);
                    continue;
                }

                FlushAll(state);
                ImageInline? lone = LoneImage(inlines);
                if (lone != null)
                {
                    ImageBlock image = new ImageBlock(lone.ResourceId, lone.AltText);
                    DocxImageInfo? info = content.FindImage(lone);
                    if (info != null)
                    {
                        image.Width = info.Width;
                        image.Height = info.Height;
                    }

                    state.Output.Add(image);
                    continue;
                }

                ParagraphBlock block = new ParagraphBlock(TrimEdges(inlines));
                block.Alignment = alignment;
                state.Output.Add(block);
            }
        }

        private void AddListItem(DocxBlockState state, DocxNumberingRef numbering, List<Inline> inlines)
        {
            int level = numbering.Level;
            ListKindEnum kind = _Session.Numbering.Kind(numbering.NumId, level);
            List<DocxListFrame> frames = state.Lists;

            while (frames.Count > 0 && frames[frames.Count - 1].Level > level) frames.RemoveAt(frames.Count - 1);
            if (frames.Count > 0 && frames[frames.Count - 1].Level == level && frames[frames.Count - 1].NumId != numbering.NumId)
                frames.RemoveAt(frames.Count - 1);

            if (frames.Count == 0 || frames[frames.Count - 1].Level < level)
            {
                if (frames.Count >= _Session.Context.MaxNestingDepth && frames.Count > 0)
                {
                    _Session.Context.AddWarning(WarningCodeEnum.NestedDepthLimited, "A list nested deeper than MaxNestingDepth was flattened.");
                }
                else
                {
                    ListBlock list = new ListBlock(kind);
                    if (kind == ListKindEnum.Ordered) list.Start = _Session.Numbering.Start(numbering.NumId, level);
                    if (frames.Count == 0)
                    {
                        state.Output.Add(list);
                    }
                    else
                    {
                        ListBlock parent = frames[frames.Count - 1].List;
                        if (parent.Items.Count == 0) parent.Items.Add(new ListItemBlock());
                        parent.Items[parent.Items.Count - 1].Blocks.Add(list);
                    }

                    frames.Add(new DocxListFrame(list, level, numbering.NumId));
                }
            }

            ListBlock target = frames[frames.Count - 1].List;
            ListItemBlock item = new ListItemBlock();
            if (target.Kind != ListKindEnum.Ordered && inlines.Count > 0 && inlines[0] is TextInline first)
            {
                string t = first.Text;
                if (t.StartsWith("[ ] ", StringComparison.Ordinal) || t.StartsWith("[x] ", StringComparison.Ordinal) || t.StartsWith("[X] ", StringComparison.Ordinal))
                {
                    item.Checked = t[1] != ' ';
                    first.Text = t.Substring(4);
                    target.Kind = ListKindEnum.Task;
                }
            }

            item.Blocks.Add(new ParagraphBlock(inlines));
            target.Items.Add(item);
        }

        private Block ReadTable(W.Table table, OpenXmlPart owner, int depth)
        {
            if (depth > _Session.Context.MaxNestingDepth)
            {
                _Session.Context.AddWarning(WarningCodeEnum.NestedDepthLimited, "A table nested deeper than MaxNestingDepth was flattened to text.");
                return new ParagraphBlock(table.InnerText);
            }

            TableBlock result = new TableBlock();
            Dictionary<int, TableCell> origins = new Dictionary<int, TableCell>();
            int headerRows = 0;
            bool leadingHeaders = true;

            foreach (W.TableRow tr in Rows(table))
            {
                _Session.Token.ThrowIfCancellationRequested();
                TableRow row = new TableRow();
                int column = tr.TableRowProperties?.GetFirstChild<W.GridBefore>()?.Val?.Value ?? 0;

                foreach (W.TableCell tc in Cells(tr))
                {
                    W.TableCellProperties? tcPr = tc.TableCellProperties;
                    int span = tcPr?.GridSpan?.Val?.Value ?? 1;
                    if (span < 1) span = 1;
                    W.VerticalMerge? vMerge = tcPr?.VerticalMerge;
                    bool continuation = vMerge != null && (vMerge.Val == null || vMerge.Val.Value == W.MergedCellValues.Continue);
                    if (continuation && origins.TryGetValue(column, out TableCell? origin))
                    {
                        origin.RowSpan = origin.RowSpan + 1;
                        column += span;
                        continue;
                    }

                    TableCell cell = new TableCell();
                    cell.ColumnSpan = span;
                    cell.Blocks = ReadBlocks(tc.ChildElements, owner, depth);
                    for (int c = column; c < column + span; c++) origins.Remove(c);
                    if (vMerge != null && vMerge.Val != null && vMerge.Val.Value == W.MergedCellValues.Restart) origins[column] = cell;
                    row.Cells.Add(cell);
                    column += span;
                }

                W.TableHeader? headerMark = tr.TableRowProperties?.GetFirstChild<W.TableHeader>();
                bool header = headerMark != null && (headerMark.Val == null || headerMark.Val.Value == W.OnOffOnlyValues.On);
                if (leadingHeaders && header) headerRows++;
                else leadingHeaders = false;
                result.Rows.Add(row);
            }

            if (headerRows == 0 && result.Rows.Count > 1 && FirstRowLook(table)) headerRows = 1;
            result.HeaderRowCount = headerRows;
            for (int r = 0; r < headerRows && r < result.Rows.Count; r++)
                foreach (TableCell cell in result.Rows[r].Cells) cell.IsHeader = true;
            return result;
        }

        private static IEnumerable<W.TableRow> Rows(OpenXmlElement container)
        {
            foreach (OpenXmlElement child in container.ChildElements)
            {
                if (child is W.TableRow row) yield return row;
                else if (child is W.SdtRow sdt && sdt.SdtContentRow != null)
                    foreach (W.TableRow inner in Rows(sdt.SdtContentRow)) yield return inner;
                else if (child is W.CustomXmlRow custom)
                    foreach (W.TableRow inner in Rows(custom)) yield return inner;
            }
        }

        private static IEnumerable<W.TableCell> Cells(OpenXmlElement container)
        {
            foreach (OpenXmlElement child in container.ChildElements)
            {
                if (child is W.TableCell cell) yield return cell;
                else if (child is W.SdtCell sdt && sdt.SdtContentCell != null)
                    foreach (W.TableCell inner in Cells(sdt.SdtContentCell)) yield return inner;
                else if (child is W.CustomXmlCell custom)
                    foreach (W.TableCell inner in Cells(custom)) yield return inner;
            }
        }

        private static bool FirstRowLook(W.Table table)
        {
            W.TableLook? look = table.GetFirstChild<W.TableProperties>()?.TableLook;
            if (look == null) return false;
            if (look.FirstRow != null) return look.FirstRow.Value;
            string? hex = look.Val?.Value;
            if (!string.IsNullOrEmpty(hex) && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int bits))
                return (bits & 0x0020) != 0;
            return false;
        }

        private static TextAlignmentEnum Alignment(EnumValue<W.JustificationValues>? value)
        {
            if (value == null || !value.HasValue) return TextAlignmentEnum.Default;
            W.JustificationValues v = value.Value;
            if (v == W.JustificationValues.Center) return TextAlignmentEnum.Center;
            if (v == W.JustificationValues.Right || v == W.JustificationValues.End) return TextAlignmentEnum.Right;
            if (v == W.JustificationValues.Both || v == W.JustificationValues.Distribute) return TextAlignmentEnum.Justify;
            if (v == W.JustificationValues.Left || v == W.JustificationValues.Start) return TextAlignmentEnum.Left;
            return TextAlignmentEnum.Default;
        }

        private static bool HasContentSoFar(DocxBlockState state)
        {
            return state.Output.Count > 0 || state.Lists.Count > 0 || state.Code != null || state.Quote != null;
        }

        private static bool IsEmpty(List<Inline> inlines)
        {
            foreach (Inline inline in inlines)
            {
                if (inline is TextInline text)
                {
                    if (text.Text.Trim().Length > 0) return false;
                }
                else if (inline is LineBreakInline)
                {
                    continue;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        private static ImageInline? LoneImage(List<Inline> inlines)
        {
            ImageInline? found = null;
            foreach (Inline inline in inlines)
            {
                if (inline is ImageInline image)
                {
                    if (found != null) return null;
                    found = image;
                }
                else if (inline is TextInline text)
                {
                    if (text.Text.Trim().Length > 0) return null;
                }
                else if (!(inline is LineBreakInline))
                {
                    return null;
                }
            }

            return found;
        }

        private static List<Inline> TrimEdges(List<Inline> inlines)
        {
            List<Inline> result = new List<Inline>(inlines);
            while (result.Count > 0 && result[0] is LineBreakInline) result.RemoveAt(0);
            while (result.Count > 0 && result[result.Count - 1] is LineBreakInline) result.RemoveAt(result.Count - 1);
            if (result.Count > 0 && result[0] is TextInline first)
            {
                first.Text = first.Text.TrimStart();
                if (first.Text.Length == 0) result.RemoveAt(0);
            }

            if (result.Count > 0 && result[result.Count - 1] is TextInline last)
            {
                last.Text = last.Text.TrimEnd();
                if (last.Text.Length == 0) result.RemoveAt(result.Count - 1);
            }

            return result;
        }

        private void FlushAll(DocxBlockState state)
        {
            FlushCode(state);
            FlushQuote(state);
            FlushLists(state);
        }

        private static void FlushCode(DocxBlockState state)
        {
            if (state.Code == null) return;
            string text = state.Code.ToString().TrimEnd('\n');
            state.Code = null;
            if (text.Trim().Length == 0) return;
            state.Output.Add(new CodeBlock(text, null));
        }

        private static void FlushQuote(DocxBlockState state)
        {
            if (state.Quote == null) return;
            if (state.Quote.Blocks.Count > 0) state.Output.Add(state.Quote);
            state.Quote = null;
        }

        private static void FlushLists(DocxBlockState state)
        {
            state.Lists.Clear();
        }
    }
}
