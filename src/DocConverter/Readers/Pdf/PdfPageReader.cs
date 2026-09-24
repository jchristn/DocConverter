namespace DocConverter.Readers.Pdf
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Options;
    using Tabula;
    using UglyToad.PdfPig;
    using UglyToad.PdfPig.Content;
    using UglyToad.PdfPig.Core;

    /// <summary>
    /// Recovers document structure from one PDF page: ruled tables (Tabula), then text lines grouped into headings
    /// (by font size relative to the body size), lists (by markers), code (by monospace fonts) and paragraphs, then
    /// images. Elements are returned in top to bottom order. Multi-column layouts are read line by line across columns.
    /// </summary>
    internal sealed class PdfPageReader
    {
        #region Private-Members

        private readonly PdfDocument _Document;
        private readonly Page _Page;
        private readonly DocumentModel _Model;
        private readonly ConversionOptions _Options;
        private readonly ConversionContext _Context;
        private readonly double _BodySize;
        private readonly List<Hyperlink> _Links = new List<Hyperlink>();

        #endregion

        #region Constructors-and-Factories

        internal PdfPageReader(PdfDocument document, Page page, DocumentModel model, ConversionOptions options, ConversionContext context, double bodySize)
        {
            _Document = document;
            _Page = page;
            _Model = model;
            _Options = options;
            _Context = context;
            _BodySize = bodySize > 0 ? bodySize : 11;
        }

        #endregion

        #region Public-Methods

        internal bool HeadingsInferred { get; private set; } = false;

        internal List<PdfPageElement> Read()
        {
            List<PdfPageElement> elements = new List<PdfPageElement>();
            int pageNumber = _Page.Number;

            try
            {
                _Links.AddRange(_Page.GetHyperlinks());
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _Context.Log(SeverityEnum.Debug, "hyperlinks unavailable on page " + pageNumber + ": " + ex.Message);
            }

            List<Word> words = new List<Word>();
            foreach (Word word in _Page.GetWords())
                if (!string.IsNullOrWhiteSpace(word.Text)) words.Add(word);

            List<PdfRectangle> tableBoxes = new List<PdfRectangle>();
            if (_Options.Pdf.DetectTables && words.Count > 0)
            {
                foreach (Table table in PdfTableExtractor.Extract(_Document, pageNumber, _Context))
                {
                    TableBlock block = ToTableBlock(table, words);
                    block.SourcePage = pageNumber;
                    tableBoxes.Add(table.BoundingBox);
                    elements.Add(new PdfPageElement(table.BoundingBox.Top, block));
                }
            }

            List<Word> textWords = new List<Word>();
            foreach (Word word in words)
                if (!InsideAny(word.BoundingBox, tableBoxes)) textWords.Add(word);

            List<PdfTextLine> lines = BuildLines(textWords);
            elements.AddRange(BuildTextElements(lines, pageNumber));

            int imageCount = 0;
            foreach (IPdfImage image in _Page.GetImages())
            {
                if (image.IsImageMask) continue;
                ImageBlock? imageBlock = ToImageBlock(image, pageNumber);
                if (imageBlock == null) continue;
                imageCount++;
                elements.Add(new PdfPageElement(image.BoundingBox.Top, imageBlock));
            }

            if (_Page.Letters.Count == 0 && imageCount > 0)
            {
                _Context.AddWarning(
                    WarningCodeEnum.NoTextLayer,
                    "Page " + pageNumber + " has no text layer (it appears to be a scanned image). Optical character recognition would be required to recover its text.");
            }

            List<PdfPageElement> ordered = elements
                .Select((e, i) => new KeyValuePair<int, PdfPageElement>(i, e))
                .OrderByDescending(p => Math.Round(p.Value.Top, 1))
                .ThenBy(p => p.Key)
                .Select(p => p.Value)
                .ToList();
            return ordered;
        }

        #endregion

        #region Private-Methods

        private static bool InsideAny(PdfRectangle box, List<PdfRectangle> regions)
        {
            double cx = (box.Left + box.Right) / 2;
            double cy = (box.Bottom + box.Top) / 2;
            foreach (PdfRectangle r in regions)
            {
                if (cx >= r.Left - 1 && cx <= r.Right + 1 && cy >= r.Bottom - 1 && cy <= r.Top + 1) return true;
            }

            return false;
        }

        private TableBlock ToTableBlock(Table table, List<Word> words)
        {
            TableBlock block = new TableBlock();
            bool firstRowBold = true;
            bool firstRowHasText = false;
            for (int r = 0; r < table.Rows.Count; r++)
            {
                TableRow row = new TableRow();
                foreach (Cell cell in table.Rows[r])
                {
                    string text = cell == null ? "" : NormalizeSpace(cell.GetText());
                    row.Cells.Add(new TableCell(text));
                    if (r == 0 && cell != null && text.Length > 0)
                    {
                        firstRowHasText = true;
                        foreach (Word word in words)
                        {
                            if (InsideAny(word.BoundingBox, new List<PdfRectangle> { cell.BoundingBox }) && !PdfFontClassifier.IsBold(word))
                            {
                                firstRowBold = false;
                                break;
                            }
                        }
                    }
                }

                block.Rows.Add(row);
            }

            if (firstRowHasText && firstRowBold && block.Rows.Count > 1)
            {
                block.HeaderRowCount = 1;
                foreach (TableCell cell in block.Rows[0].Cells) cell.IsHeader = true;
            }

            return block;
        }

        private static string NormalizeSpace(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder sb = new StringBuilder();
            bool space = false;
            foreach (char c in text!)
            {
                if (char.IsWhiteSpace(c))
                {
                    space = true;
                    continue;
                }

                if (space && sb.Length > 0) sb.Append(' ');
                space = false;
                sb.Append(c);
            }

            return sb.ToString();
        }

        private List<PdfTextLine> BuildLines(List<Word> words)
        {
            List<Word> sorted = words
                .OrderByDescending(w => Baseline(w))
                .ThenBy(w => w.BoundingBox.Left)
                .ToList();

            List<PdfTextLine> lines = new List<PdfTextLine>();
            PdfTextLine? current = null;
            foreach (Word word in sorted)
            {
                double baseline = Baseline(word);
                double size = WordSize(word);
                double tolerance = Math.Max(2.0, size * 0.35);
                if (current == null || Math.Abs(current.Baseline - baseline) > tolerance)
                {
                    current = new PdfTextLine { Baseline = baseline };
                    lines.Add(current);
                }

                current.Words.Add(word);
            }

            foreach (PdfTextLine line in lines)
            {
                line.Words.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));
                line.Left = line.Words.Min(w => w.BoundingBox.Left);
                line.Top = line.Words.Max(w => w.BoundingBox.Top);
                line.Bottom = line.Words.Min(w => w.BoundingBox.Bottom);
                List<double> sizes = new List<double>();
                int bold = 0;
                int mono = 0;
                int letters = 0;
                double widthSum = 0;
                foreach (Word word in line.Words)
                {
                    foreach (Letter letter in word.Letters)
                    {
                        if (string.IsNullOrWhiteSpace(letter.Value)) continue;
                        letters++;
                        sizes.Add(letter.PointSize);
                        widthSum += letter.Width;
                        if (PdfFontClassifier.IsBold(letter)) bold++;
                        if (PdfFontClassifier.IsMono(letter)) mono++;
                    }
                }

                sizes.Sort();
                line.FontSize = sizes.Count > 0 ? sizes[sizes.Count / 2] : _BodySize;
                line.Bold = letters > 0 && bold * 2 > letters;
                line.Mono = letters > 0 && mono == letters;
                line.AverageCharWidth = letters > 0 ? widthSum / letters : line.FontSize * 0.6;
            }

            return lines;
        }

        private static double Baseline(Word word)
        {
            if (word.Letters.Count > 0) return word.Letters[0].StartBaseLine.Y;
            return word.BoundingBox.Bottom;
        }

        private static double WordSize(Word word)
        {
            foreach (Letter letter in word.Letters)
                if (!string.IsNullOrWhiteSpace(letter.Value)) return letter.PointSize;
            return word.BoundingBox.Height;
        }

        private int HeadingLevel(PdfTextLine line)
        {
            double ratio = line.FontSize / _BodySize;
            if (ratio < _Options.Pdf.HeadingSizeRatio) return 0;
            if (ratio >= 1.8) return 1;
            if (ratio >= 1.4) return 2;
            return 3;
        }

        private List<PdfPageElement> BuildTextElements(List<PdfTextLine> lines, int pageNumber)
        {
            List<PdfPageElement> elements = new List<PdfPageElement>();
            int i = 0;
            while (i < lines.Count)
            {
                PdfTextLine line = lines[i];
                int level = HeadingLevel(line);
                if (level > 0 && !line.Mono)
                {
                    List<PdfTextLine> group = new List<PdfTextLine> { line };
                    int j = i + 1;
                    while (j < lines.Count && Math.Abs(lines[j].FontSize - line.FontSize) < 0.6 && Gap(lines[j - 1], lines[j]) <= line.FontSize * 0.8)
                    {
                        group.Add(lines[j]);
                        j++;
                    }

                    HeadingBlock heading = new HeadingBlock();
                    heading.Level = level;
                    heading.Inlines = BuildInlines(group);
                    heading.SourcePage = pageNumber;
                    elements.Add(new PdfPageElement(line.Top, heading));
                    HeadingsInferred = true;
                    i = j;
                    continue;
                }

                if (line.Mono)
                {
                    List<PdfTextLine> group = new List<PdfTextLine> { line };
                    int j = i + 1;
                    while (j < lines.Count && lines[j].Mono && Gap(lines[j - 1], lines[j]) <= lines[j].FontSize * 1.2)
                    {
                        group.Add(lines[j]);
                        j++;
                    }

                    CodeBlock code = new CodeBlock(CodeText(group), null);
                    code.SourcePage = pageNumber;
                    elements.Add(new PdfPageElement(line.Top, code));
                    i = j;
                    continue;
                }

                if (line.Words.Count > 1 && PdfListMarker.TryParse(line.Words[0].Text, out bool unusedOrdered, out int unusedNumber))
                {
                    int j = i;
                    ListBlock list = BuildList(lines, ref j, pageNumber);
                    elements.Add(new PdfPageElement(line.Top, list));
                    i = j;
                    continue;
                }

                List<PdfTextLine> paragraphLines = new List<PdfTextLine> { line };
                int k = i + 1;
                while (k < lines.Count && ContinuesParagraph(lines[k - 1], lines[k]))
                {
                    paragraphLines.Add(lines[k]);
                    k++;
                }

                ParagraphBlock paragraph = new ParagraphBlock();
                paragraph.Inlines = BuildInlines(paragraphLines);
                paragraph.SourcePage = pageNumber;
                elements.Add(new PdfPageElement(line.Top, paragraph));
                i = k;
            }

            return elements;
        }

        private bool ContinuesParagraph(PdfTextLine previous, PdfTextLine next)
        {
            if (next.Mono || previous.Mono) return false;
            if (HeadingLevel(next) > 0) return false;
            if (Math.Abs(next.FontSize - previous.FontSize) >= 0.6) return false;
            if (next.Words.Count > 1 && PdfListMarker.TryParse(next.Words[0].Text, out bool nextOrdered, out int nextNumber)) return false;
            return Gap(previous, next) <= previous.FontSize * 0.8;
        }

        private static double Gap(PdfTextLine upper, PdfTextLine lower)
        {
            return upper.Bottom - lower.Top;
        }

        private ListBlock BuildList(List<PdfTextLine> lines, ref int index, int pageNumber)
        {
            PdfTextLine first = lines[index];
            PdfListMarker.TryParse(first.Words[0].Text, out bool firstOrdered, out int firstNumber);
            double baseLeft = first.Left;

            ListBlock root = new ListBlock(firstOrdered ? ListKindEnum.Ordered : ListKindEnum.Unordered);
            root.Start = firstOrdered ? firstNumber : 1;
            root.SourcePage = pageNumber;

            List<ListBlock> stack = new List<ListBlock> { root };
            List<double> stackLefts = new List<double> { baseLeft };
            ListItemBlock? lastItem = null;
            List<PdfTextLine> itemLines = new List<PdfTextLine>();
            double lastItemTextLeft = 0;

            while (index < lines.Count)
            {
                PdfTextLine line = lines[index];
                bool isMarker = line.Words.Count > 1 && PdfListMarker.TryParse(line.Words[0].Text, out bool lineOrdered, out int lineNumber);
                if (!isMarker)
                {
                    bool continuation = lastItem != null && !line.Mono && HeadingLevel(line) == 0
                        && index > 0 && Gap(lines[index - 1], line) <= line.FontSize * 0.8
                        && line.Left >= lastItemTextLeft - 2;
                    if (!continuation) break;
                    itemLines.Add(line);
                    index++;
                    continue;
                }

                if (index > 0 && lastItem != null && Gap(lines[index - 1], line) > line.FontSize * 1.5) break;

                PdfListMarker.TryParse(line.Words[0].Text, out bool markerOrdered, out int markerNumber);
                FlushItem(lastItem, itemLines);
                itemLines = new List<PdfTextLine>();

                double left = line.Left;
                while (stack.Count > 1 && left < stackLefts[stackLefts.Count - 1] - 4)
                {
                    stack.RemoveAt(stack.Count - 1);
                    stackLefts.RemoveAt(stackLefts.Count - 1);
                }

                ListBlock target = stack[stack.Count - 1];
                if (left > stackLefts[stackLefts.Count - 1] + 6 && lastItem != null)
                {
                    ListBlock nested = new ListBlock(markerOrdered ? ListKindEnum.Ordered : ListKindEnum.Unordered);
                    nested.Start = markerOrdered ? markerNumber : 1;
                    lastItem.Blocks.Add(nested);
                    stack.Add(nested);
                    stackLefts.Add(left);
                    target = nested;
                }
                else if (stack.Count == 1 && (markerOrdered ? ListKindEnum.Ordered : ListKindEnum.Unordered) != root.Kind && root.Items.Count > 0)
                {
                    break;
                }

                lastItem = new ListItemBlock();
                target.Items.Add(lastItem);
                PdfTextLine textPart = new PdfTextLine { Baseline = line.Baseline, Left = line.Left, Top = line.Top, Bottom = line.Bottom, FontSize = line.FontSize };
                for (int w = 1; w < line.Words.Count; w++) textPart.Words.Add(line.Words[w]);
                itemLines.Add(textPart);
                lastItemTextLeft = line.Words[1].BoundingBox.Left;
                index++;
            }

            FlushItem(lastItem, itemLines);
            return root;
        }

        private void FlushItem(ListItemBlock? item, List<PdfTextLine> itemLines)
        {
            if (item == null || itemLines.Count == 0) return;
            ParagraphBlock paragraph = new ParagraphBlock();
            paragraph.Inlines = BuildInlines(itemLines);
            item.Blocks.Insert(0, paragraph);
        }

        private static string CodeText(List<PdfTextLine> group)
        {
            double minLeft = group.Min(l => l.Left);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < group.Count; i++)
            {
                PdfTextLine line = group[i];
                if (i > 0) sb.Append('\n');
                double charWidth = line.AverageCharWidth > 0 ? line.AverageCharWidth : line.FontSize * 0.6;
                int spaces = (int)Math.Round((line.Left - minLeft) / charWidth);
                if (spaces > 0) sb.Append(' ', spaces);
                double cursor = -1;
                foreach (Word word in line.Words)
                {
                    if (cursor >= 0)
                    {
                        int gap = (int)Math.Round((word.BoundingBox.Left - cursor) / charWidth);
                        sb.Append(' ', gap < 1 ? 1 : gap);
                    }

                    sb.Append(word.Text);
                    cursor = word.BoundingBox.Right;
                }
            }

            return sb.ToString();
        }

        private List<Inline> BuildInlines(List<PdfTextLine> lines)
        {
            List<string> texts = new List<string>();
            List<InlineStyleEnum> styles = new List<InlineStyleEnum>();
            List<Hyperlink?> links = new List<Hyperlink?>();
            List<bool> glue = new List<bool>();
            foreach (PdfTextLine line in lines)
            {
                foreach (Word word in line.Words)
                {
                    InlineStyleEnum style = InlineStyleEnum.None;
                    if (PdfFontClassifier.IsBold(word)) style |= InlineStyleEnum.Bold;
                    if (PdfFontClassifier.IsItalic(word)) style |= InlineStyleEnum.Italic;
                    if (PdfFontClassifier.IsMono(word)) style |= InlineStyleEnum.Code;
                    AddWordTokens(word, style, texts, styles, links, glue);
                }
            }

            List<Inline> inlines = new List<Inline>();
            LinkInline? openLink = null;
            Hyperlink? openLinkSource = null;
            int i = 0;
            while (i < texts.Count)
            {
                int j = i + 1;
                while (j < texts.Count && styles[j] == styles[i] && SameLink(links[j], links[i])) j++;
                StringBuilder segmentBuilder = new StringBuilder();
                for (int k = i; k < j; k++)
                {
                    if (k > i && !glue[k]) segmentBuilder.Append(' ');
                    segmentBuilder.Append(texts[k]);
                }

                string segment = segmentBuilder.ToString();
                Hyperlink? link = links[i];

                if (i > 0 && !glue[i])
                {
                    if (link != null && SameLink(link, openLinkSource) && openLink != null) openLink.Inlines.Add(new TextInline(" "));
                    else AppendPlain(inlines, " ");
                }

                if (link != null)
                {
                    if (openLink == null || !SameLink(link, openLinkSource))
                    {
                        openLink = new LinkInline();
                        openLink.Url = link.Uri ?? "";
                        openLinkSource = link;
                        inlines.Add(openLink);
                    }

                    openLink.Inlines.Add(new TextInline(segment, styles[i]));
                }
                else
                {
                    openLink = null;
                    openLinkSource = null;
                    if (styles[i] == InlineStyleEnum.None) AppendPlain(inlines, segment);
                    else inlines.Add(new TextInline(segment, styles[i]));
                }

                i = j;
            }

            return inlines;
        }

        private void AddWordTokens(Word word, InlineStyleEnum style, List<string> texts, List<InlineStyleEnum> styles, List<Hyperlink?> links, List<bool> glue)
        {
            if (_Links.Count == 0 || word.Letters.Count == 0)
            {
                texts.Add(word.Text);
                styles.Add(style);
                links.Add(LinkFor(word));
                glue.Add(false);
                return;
            }

            StringBuilder piece = new StringBuilder();
            Hyperlink? pieceLink = null;
            bool first = true;
            foreach (Letter letter in word.Letters)
            {
                Hyperlink? letterLink = LinkAt((letter.BoundingBox.Left + letter.BoundingBox.Right) / 2, (letter.BoundingBox.Bottom + letter.BoundingBox.Top) / 2);
                if (piece.Length > 0 && !SameLink(letterLink, pieceLink))
                {
                    texts.Add(piece.ToString());
                    styles.Add(style);
                    links.Add(pieceLink);
                    glue.Add(!first);
                    first = false;
                    piece.Clear();
                }

                if (piece.Length == 0) pieceLink = letterLink;
                piece.Append(letter.Value);
            }

            if (piece.Length > 0)
            {
                texts.Add(piece.ToString());
                styles.Add(style);
                links.Add(pieceLink);
                glue.Add(!first);
            }
        }

        private Hyperlink? LinkAt(double x, double y)
        {
            foreach (Hyperlink link in _Links)
            {
                if (string.IsNullOrEmpty(link.Uri)) continue;
                PdfRectangle b = link.Bounds;
                if (x >= b.Left - 0.5 && x <= b.Right + 0.5 && y >= b.Bottom - 1 && y <= b.Top + 1) return link;
            }

            return null;
        }

        private static bool SameLink(Hyperlink? a, Hyperlink? b)
        {
            if (a == null || b == null) return a == null && b == null;
            return string.Equals(a.Uri, b.Uri, StringComparison.Ordinal);
        }

        private static void AppendPlain(List<Inline> inlines, string text)
        {
            if (inlines.Count > 0 && inlines[inlines.Count - 1] is TextInline last && last.Style == InlineStyleEnum.None)
            {
                last.Text += text;
                return;
            }

            inlines.Add(new TextInline(text));
        }

        private Hyperlink? LinkFor(Word word)
        {
            if (_Links.Count == 0) return null;
            double cx = (word.BoundingBox.Left + word.BoundingBox.Right) / 2;
            double cy = (word.BoundingBox.Bottom + word.BoundingBox.Top) / 2;
            foreach (Hyperlink link in _Links)
            {
                if (string.IsNullOrEmpty(link.Uri)) continue;
                PdfRectangle b = link.Bounds;
                if (cx >= b.Left - 1 && cx <= b.Right + 1 && cy >= b.Bottom - 1 && cy <= b.Top + 1) return link;
            }

            return null;
        }

        private ImageBlock? ToImageBlock(IPdfImage image, int pageNumber)
        {
            byte[] raw;
            try
            {
                raw = image.RawMemory.ToArray();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _Context.AddWarning(WarningCodeEnum.UnknownElementSkipped, "An image on page " + pageNumber + " could not be read and was skipped: " + ex.Message);
                return null;
            }

            byte[]? data = null;
            string mediaType = "image/png";
            if (raw.Length > 3 && raw[0] == 0xFF && raw[1] == 0xD8 && raw[2] == 0xFF)
            {
                data = raw;
                mediaType = "image/jpeg";
            }
            else
            {
                try
                {
                    if (image.TryGetPng(out byte[]? png) && png != null) data = png;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _Context.Log(SeverityEnum.Debug, "PNG conversion failed on page " + pageNumber + ": " + ex.Message);
                }
            }

            if (data == null)
            {
                _Context.AddWarning(WarningCodeEnum.UnknownElementSkipped, "An image on page " + pageNumber + " uses an encoding DocConverter cannot extract and was skipped.");
                return null;
            }

            ImageInfo? info = ImageHeaderReader.Read(data);
            BinaryResource resource = new BinaryResource
            {
                MediaType = mediaType,
                Data = data,
                PixelWidth = info?.Width ?? image.WidthInSamples,
                PixelHeight = info?.Height ?? image.HeightInSamples,
                FileName = "page" + pageNumber + "-image" + (_Model.Resources.Count + 1) + (mediaType == "image/jpeg" ? ".jpg" : ".png")
            };

            string id = _Model.AddResource(resource);
            ImageBlock block = new ImageBlock(id, null);
            block.Width = image.BoundingBox.Width;
            block.Height = image.BoundingBox.Height;
            block.SourcePage = pageNumber;
            return block;
        }

        #endregion
    }
}
