namespace Test.Shared.Inspection
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using CsvHelper;
    using CsvHelper.Configuration;

    /// <summary>
    /// Inspects CSV and TSV output with CsvHelper directly.
    /// </summary>
    public static class DelimitedInspector
    {
        /// <summary>
        /// Parse and snapshot delimited output.
        /// </summary>
        /// <param name="bytes">UTF-8 CSV or TSV.</param>
        /// <param name="delimiter">Field delimiter.</param>
        /// <returns>Snapshot; TableRows holds every record.</returns>
        public static ContentSnapshot Inspect(byte[] bytes, char delimiter)
        {
            string text = TextInspector.DecodeStrict(bytes);
            CsvConfiguration config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = delimiter.ToString(),
                HasHeaderRecord = false,
                BadDataFound = context => throw new TestAssertionException("Malformed delimited field at row " + context.Context.Parser!.Row + "."),
                IgnoreBlankLines = true
            };

            ContentSnapshot snapshot = new ContentSnapshot();
            StringBuilder all = new StringBuilder();
            using (StringReader reader = new StringReader(text))
            using (CsvParser parser = new CsvParser(reader, config))
            {
                while (parser.Read())
                {
                    string[]? record = parser.Record;
                    if (record == null) continue;
                    List<string> row = new List<string>();
                    foreach (string field in record)
                    {
                        row.Add(ContentSnapshot.Normalize(field));
                        all.Append(' ').Append(field);
                    }

                    snapshot.TableRows.Add(row);
                }
            }

            snapshot.AllText = ContentSnapshot.Normalize(all.ToString());
            return snapshot;
        }
    }
}
