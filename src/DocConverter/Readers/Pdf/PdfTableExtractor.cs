namespace DocConverter.Readers.Pdf
{
    using System;
    using System.Collections.Generic;
    using Tabula;
    using Tabula.Extractors;
    using UglyToad.PdfPig;

    /// <summary>
    /// Finds ruled tables on a page with Tabula's lattice (spreadsheet) algorithm. Unruled tables are not detected; they
    /// arrive as paragraphs (a documented limitation).
    /// </summary>
    internal static class PdfTableExtractor
    {
        internal static List<Table> Extract(PdfDocument document, int pageNumber, ConversionContext context)
        {
            List<Table> result = new List<Table>();
            try
            {
                PageArea area = ObjectExtractor.Extract(document, pageNumber);
                IReadOnlyList<Table> tables = new SpreadsheetExtractionAlgorithm().Extract(area);
                foreach (Table table in tables)
                    if (IsTable(table)) result.Add(table);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                context.Log(Enums.SeverityEnum.Warn, "table extraction failed on page " + pageNumber + ": " + ex.Message);
            }

            return result;
        }

        private static bool IsTable(Table table)
        {
            if (table == null || table.Rows == null || table.Rows.Count < 2) return false;
            int maxColumns = 0;
            int cells = 0;
            int nonEmpty = 0;
            foreach (IReadOnlyList<Cell> row in table.Rows)
            {
                if (row.Count > maxColumns) maxColumns = row.Count;
                foreach (Cell cell in row)
                {
                    cells++;
                    if (cell != null && !string.IsNullOrWhiteSpace(cell.GetText())) nonEmpty++;
                }
            }

            if (maxColumns < 2 || cells == 0) return false;
            return (double)nonEmpty / cells >= 0.3;
        }
    }
}
