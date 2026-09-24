namespace DocConverter.Writers.Delimited
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using CsvHelper;
    using CsvHelper.Configuration;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Options;

    /// <summary>
    /// Writes CSV or TSV. Only tables are written (CsvOptions.TableSelection picks which); other content is dropped with
    /// the NonTableContentDropped warning. A document without tables is written one block per row in a single column
    /// (CsvOptions.NoTableBehavior ParagraphsAsRows, FormattingLost warning) or rejected (Error). Quoting follows RFC 4180.
    /// Stateless and thread safe.
    /// </summary>
    public sealed class DelimitedDocumentWriter : IDocumentWriter
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <inheritdoc />
        public Task WriteAsync(DocumentModel document, Stream output, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (output == null) throw new ArgumentNullException(nameof(output));
            CsvOptions csv = options.Csv;
            char delimiter = csv.Delimiter ?? (format == DocumentFormatEnum.Tsv ? '\t' : ',');

            List<TableBlock> tables = new List<TableBlock>();
            bool otherContent = false;
            Collect(document.Blocks, tables, ref otherContent);

            List<List<string>> records = new List<List<string>>();
            if (tables.Count > 0)
            {
                List<TableBlock> selected = new List<TableBlock>();
                if (csv.TableSelection == TableSelectionEnum.All) selected.AddRange(tables);
                else if (csv.TableSelection == TableSelectionEnum.Index)
                {
                    if (csv.TableIndex >= tables.Count)
                        throw new DocumentWriteException("CsvOptions.TableIndex is " + csv.TableIndex + " but the document has " + tables.Count + " table(s).");
                    selected.Add(tables[csv.TableIndex]);
                }
                else selected.Add(tables[0]);

                if (otherContent)
                    context.AddWarning(WarningCodeEnum.NonTableContentDropped, "Content outside tables was dropped because " + format + " holds tables only.");
                if (selected.Count < tables.Count)
                    context.AddWarning(WarningCodeEnum.NonTableContentDropped, (tables.Count - selected.Count) + " table(s) were not written because CsvOptions.TableSelection is " + csv.TableSelection + ".");

                for (int t = 0; t < selected.Count; t++)
                {
                    token.ThrowIfCancellationRequested();
                    if (t > 0) records.Add(new List<string>());
                    TableGrid grid = TableGrid.Build(selected[t], csv.TableSpanMode, "\n");
                    if (grid.HadSpans)
                        context.AddWarning(WarningCodeEnum.TableSpansFlattened, "Merged table cells were " + (csv.TableSpanMode == TableSpanModeEnum.Repeat ? "repeated" : "emptied") + " because " + format + " cannot merge cells.");
                    records.AddRange(grid.Rows);
                }
            }
            else
            {
                if (csv.NoTableBehavior == NoTableBehaviorEnum.Error)
                    throw new DocumentWriteException("The document contains no tables and CsvOptions.NoTableBehavior is Error.");
                RowsFromBlocks(document.Blocks, records, document, context);
                if (records.Count > 0)
                    context.AddWarning(WarningCodeEnum.FormattingLost, "The document has no tables, so each block was written as one row in a single column.");
            }

            CsvConfiguration config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = delimiter.ToString(),
                NewLine = "\n",
                ShouldQuote = args => NeedsQuote(args.Field, delimiter)
            };

            string text;
            using (StringWriter sw = new StringWriter(CultureInfo.InvariantCulture))
            {
                using (CsvWriter writer = new CsvWriter(sw, config))
                {
                    foreach (List<string> record in records)
                    {
                        foreach (string field in record) writer.WriteField(field);
                        writer.NextRecord();
                    }
                }

                text = sw.ToString();
            }

            return TextIO.WriteAllTextAsync(text, output, options, token);
        }

        private static bool NeedsQuote(string? field, char delimiter)
        {
            if (string.IsNullOrEmpty(field)) return false;
            return field!.IndexOf(delimiter) >= 0 || field.IndexOf('"') >= 0 || field.IndexOf('\n') >= 0 || field.IndexOf('\r') >= 0
                || field[0] == ' ' || field[field.Length - 1] == ' ';
        }

        private static void Collect(List<Block> blocks, List<TableBlock> tables, ref bool otherContent)
        {
            foreach (Block block in blocks)
            {
                switch (block)
                {
                    case TableBlock table:
                        tables.Add(table);
                        break;
                    case SectionBlock section:
                        Collect(section.Blocks, tables, ref otherContent);
                        break;
                    case QuoteBlock quote:
                        Collect(quote.Blocks, tables, ref otherContent);
                        break;
                    case ListBlock list:
                        otherContent = true;
                        foreach (ListItemBlock item in list.Items) Collect(item.Blocks, tables, ref otherContent);
                        break;
                    case ThematicBreakBlock _:
                    case PageBreakBlock _:
                        break;
                    default:
                        otherContent = true;
                        break;
                }
            }
        }

        private static void RowsFromBlocks(List<Block> blocks, List<List<string>> records, DocumentModel document, ConversionContext context)
        {
            foreach (Block block in blocks)
            {
                switch (block)
                {
                    case SectionBlock section:
                        if (SectionTitles.ShouldRender(section)) records.Add(new List<string> { section.Title! });
                        RowsFromBlocks(section.Blocks, records, document, context);
                        break;
                    case QuoteBlock quote:
                        RowsFromBlocks(quote.Blocks, records, document, context);
                        break;
                    case ListBlock list:
                        foreach (ListItemBlock item in list.Items) records.Add(new List<string> { ModelText.Blocks(item.Blocks, "\n") });
                        break;
                    case ImageBlock image:
                        context.AddWarning(WarningCodeEnum.ImagePlaceholderEmitted, "Images cannot be stored in CSV or TSV and were written as placeholders. No text was extracted from them (no OCR).");
                        records.Add(new List<string> { ImagePlaceholder.Describe(image.AltText, image.ResourceId, document.Resources) });
                        break;
                    case ThematicBreakBlock _:
                    case PageBreakBlock _:
                        break;
                    default:
                        string text = ModelText.Block(block);
                        if (text.Length > 0) records.Add(new List<string> { text });
                        break;
                }
            }
        }
    }
}
