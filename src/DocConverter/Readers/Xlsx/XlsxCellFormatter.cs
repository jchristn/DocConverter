namespace DocConverter.Readers.Xlsx
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using DocumentFormat.OpenXml.Spreadsheet;

    /// <summary>
    /// Turns spreadsheet cells into display text: shared and inline strings (rich runs flattened, phonetic runs skipped),
    /// booleans as TRUE or FALSE, numbers in invariant culture, dates as ISO 8601, and formulas as their cached value.
    /// </summary>
    internal sealed class XlsxCellFormatter
    {
        private readonly List<string> _SharedStrings = new List<string>();
        private readonly List<uint> _StyleNumberFormats = new List<uint>();
        private readonly Dictionary<uint, string> _CustomFormats = new Dictionary<uint, string>();

        internal XlsxCellFormatter(WorkbookPart workbook)
        {
            SharedStringTablePart? sst = workbook.SharedStringTablePart;
            if (sst != null && sst.SharedStringTable != null)
            {
                foreach (SharedStringItem item in sst.SharedStringTable.Elements<SharedStringItem>())
                    _SharedStrings.Add(RichText(item));
            }

            Stylesheet? styles = workbook.WorkbookStylesPart?.Stylesheet;
            if (styles != null)
            {
                if (styles.NumberingFormats != null)
                {
                    foreach (NumberingFormat nf in styles.NumberingFormats.Elements<NumberingFormat>())
                    {
                        if (nf.NumberFormatId != null) _CustomFormats[nf.NumberFormatId.Value] = nf.FormatCode?.Value ?? "";
                    }
                }

                if (styles.CellFormats != null)
                {
                    foreach (CellFormat cf in styles.CellFormats.Elements<CellFormat>())
                        _StyleNumberFormats.Add(cf.NumberFormatId?.Value ?? 0);
                }
            }
        }

        internal string Text(Cell cell)
        {
            CellValues type = cell.DataType != null && cell.DataType.HasValue ? cell.DataType.Value : CellValues.Number;
            if (type == CellValues.InlineString)
            {
                if (cell.InlineString != null) return RichText(cell.InlineString);
                return cell.CellValue?.Text ?? "";
            }

            string raw = cell.CellValue?.Text ?? "";
            if (type == CellValues.SharedString)
            {
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) && index >= 0 && index < _SharedStrings.Count)
                    return _SharedStrings[index];
                return "";
            }

            if (type == CellValues.Boolean) return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) ? "TRUE" : "FALSE";
            if (type == CellValues.Error || type == CellValues.String) return raw;
            if (raw.Length == 0) return "";

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) return raw;
            if (type == CellValues.Date) return raw;
            if (IsDateStyle(cell.StyleIndex)) return XlsxDateFormats.FromSerial(number);
            return number.ToString("G15", CultureInfo.InvariantCulture);
        }

        private bool IsDateStyle(UInt32Value? styleIndex)
        {
            if (styleIndex == null || !styleIndex.HasValue) return false;
            int index = (int)styleIndex.Value;
            if (index < 0 || index >= _StyleNumberFormats.Count) return false;
            uint formatId = _StyleNumberFormats[index];
            if (XlsxDateFormats.IsBuiltInDate(formatId)) return true;
            if (_CustomFormats.TryGetValue(formatId, out string? code)) return XlsxDateFormats.IsDateFormatCode(code);
            return false;
        }

        private static string RichText(OpenXmlElement element)
        {
            StringBuilder sb = new StringBuilder();
            foreach (OpenXmlElement child in element.ChildElements)
            {
                if (child is Text text) sb.Append(text.Text);
                else if (child is Run run)
                {
                    foreach (Text t in run.Elements<Text>()) sb.Append(t.Text);
                }
            }

            return sb.ToString();
        }
    }
}
