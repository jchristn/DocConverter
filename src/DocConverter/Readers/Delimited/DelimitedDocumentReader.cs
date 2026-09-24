namespace DocConverter.Readers.Delimited
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using CsvHelper;
    using CsvHelper.Configuration;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Options;

    /// <summary>
    /// Reads CSV and TSV into a single table. Quoted fields, embedded delimiters and embedded newlines follow RFC 4180.
    /// Ragged rows are padded to the widest row. Stateless and thread safe.
    /// </summary>
    public sealed class DelimitedDocumentReader : IDocumentReader
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <inheritdoc />
        public async Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            string text = await TextIO.ReadAllTextAsync(input, options, context, token).ConfigureAwait(false);
            char delimiter = options.Csv.Delimiter ?? (format == DocumentFormatEnum.Tsv ? '\t' : ',');

            CsvConfiguration config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = delimiter.ToString(),
                HasHeaderRecord = false,
                BadDataFound = null,
                MissingFieldFound = null,
                DetectDelimiter = false,
                TrimOptions = TrimOptions.None,
                IgnoreBlankLines = true
            };

            List<string[]> records = new List<string[]>();
            int width = 0;
            using (StringReader sr = new StringReader(text.TrimStart('﻿')))
            using (CsvParser parser = new CsvParser(sr, config))
            {
                while (parser.Read())
                {
                    if ((records.Count & 1023) == 0) token.ThrowIfCancellationRequested();
                    string[]? record = parser.Record;
                    if (record == null) continue;
                    records.Add(record);
                    if (record.Length > width) width = record.Length;
                }
            }

            DocumentModel document = new DocumentModel();
            if (records.Count == 0) return document;

            TableBlock table = new TableBlock();
            table.HeaderRowCount = options.Csv.HasHeaderRow ? 1 : 0;
            for (int r = 0; r < records.Count; r++)
            {
                TableRow row = new TableRow();
                for (int c = 0; c < width; c++)
                {
                    string value = c < records[r].Length ? records[r][c] : "";
                    TableCell cell = new TableCell(value);
                    if (r == 0 && options.Csv.HasHeaderRow) cell.IsHeader = true;
                    row.Cells.Add(cell);
                }

                table.Rows.Add(row);
            }

            document.Blocks.Add(table);
            return document;
        }
    }
}
