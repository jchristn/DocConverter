namespace DocConverter.Readers.Xlsx
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// Scores the first row of a block of rows to decide whether it is a header row. Ported from DocumentAtom's
    /// HeaderRowDetector: the same patterns, weights and threshold, reshaped to work on plain cell strings.
    /// </summary>
    internal static class XlsxHeaderRowDetector
    {
        private const double _Threshold = 3.0;
        private const double _WeightSequentialFirstColumn = 2.0;
        private const double _WeightHigherTextRatio = 1.0;
        private const double _WeightDistinctFormat = 1.0;
        private const double _WeightConsistentDataColumns = 1.0;
        private const double _WeightHeaderTerms = 1.5;
        private const double _WeightColumnHeaders = 3.0;
        private const double _WeightRow1Differs = 2.0;
        private const double _WeightAllRowsSimilar = -3.0;
        private const double _WeightColumnNumbers = 2.0;
        private const double _WeightNumericSequence = -3.0;
        private const double _WeightPureNumericHeader = 5.0;
        private const int _MaxRowsToAnalyze = 10;

        private static readonly HashSet<string> _HeaderTerms = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "identifier", "key", "code", "ref", "reference", "number", "num", "no",
            "name", "first", "last", "middle", "full", "prefix", "suffix", "title", "username",
            "firstname", "lastname", "fullname", "surname", "nickname",
            "age", "gender", "sex", "dob", "birth", "birthday", "birthdate", "born",
            "date", "time", "day", "month", "year", "years", "quarter", "period", "duration", "term",
            "created", "updated", "modified", "start", "end", "begin", "expiry", "timestamp",
            "address", "street", "city", "state", "country", "province", "zip", "zipcode",
            "postal", "location", "place", "region", "area", "territory", "continent",
            "email", "phone", "fax", "mobile", "cell", "contact", "website", "url", "web",
            "homepage", "domain", "twitter", "facebook", "social",
            "type", "category", "class", "classification", "group", "department", "division",
            "status", "condition", "level", "tier", "grade", "rank", "rating",
            "description", "desc", "details", "info", "information", "note", "notes", "comment",
            "comments", "summary", "overview", "remark", "remarks", "about", "text", "content",
            "amount", "quantity", "count", "total", "sum", "value", "score", "size", "height",
            "width", "length", "weight", "volume", "depth", "balance", "percent",
            "percentage", "ratio", "rate", "frequency",
            "price", "cost", "fee", "charge", "tax", "discount", "currency", "payment", "paid",
            "debit", "credit", "invoice", "salary", "wage", "budget", "expense",
            "revenue", "income", "profit", "loss", "margin", "interest", "commission",
            "sku", "upc", "product", "item", "inventory", "stock", "model", "brand", "make",
            "unit", "part", "component", "material", "color", "version", "edition",
            "company", "organization", "org", "business", "corp", "corporation", "enterprise",
            "agency", "institution", "branch", "team", "position",
            "role", "job", "occupation", "employer", "employee", "customer", "client",
            "vendor", "supplier", "partner",
            "file", "filename", "path", "extension", "format", "author",
            "creator", "owner", "publisher", "source",
            "ip", "mac", "port", "host", "server", "user", "login", "password",
            "hash", "encrypt", "ssl", "http", "uri", "api", "token", "session",
            "active", "inactive", "enabled", "disabled",
            "complete", "incomplete", "approved", "rejected", "pending", "processed",
            "measurement", "measure", "metric", "dimension", "distance", "speed", "velocity", "acceleration",
            "force", "energy", "power", "voltage", "current", "resistance", "temperature",
            "degree", "pressure", "flow", "capacity",
            "average", "avg", "mean", "median", "mode", "max", "min", "maximum", "minimum",
            "variance", "deviation", "std", "quartile", "percentile", "q1", "q2", "q3", "q4"
        };

        internal static bool IsHeaderRow(List<List<string>> rows)
        {
            if (rows == null || rows.Count <= 1) return false;

            List<Dictionary<int, string>> rowData = Prepare(rows);
            if (rowData.Count <= 1) return false;
            if (IsSimpleRowNumbering(rowData) && rowData.Count >= 5) return false;

            double total = 0;
            if (DetectAllRowsSimilar(rowData)) total += _WeightAllRowsSimilar;
            if (DetectSequentialFirstColumn(rowData)) total += _WeightSequentialFirstColumn;
            if (DetectHigherTextRatio(rowData)) total += _WeightHigherTextRatio;
            if (DetectDistinctFormat(rowData)) total += _WeightDistinctFormat;
            if (DetectConsistentDataColumns(rowData)) total += _WeightConsistentDataColumns;
            if (DetectHeaderTerms(rowData)) total += _WeightHeaderTerms;

            bool numericSequence = false;
            bool columnNumbers = false;
            DetectNumericSequence(rowData, ref numericSequence, ref columnNumbers);
            bool pureNumeric = IsPureNumericHeader(rowData);
            if (numericSequence && !columnNumbers && !pureNumeric) total += _WeightNumericSequence;
            if (columnNumbers) total += _WeightColumnNumbers;
            if (rowData[0].Count >= 2 && HasNamedColumnHeaders(rowData[0].Values)) total += _WeightColumnHeaders;
            if (pureNumeric) total += _WeightPureNumericHeader;
            if (DetectRow1Differs(rowData)) total += _WeightRow1Differs;

            return total >= _Threshold;
        }

        private static List<Dictionary<int, string>> Prepare(List<List<string>> rows)
        {
            List<Dictionary<int, string>> data = new List<Dictionary<int, string>>();
            int maxColumns = 0;
            int take = Math.Min(rows.Count, _MaxRowsToAnalyze + 1);
            for (int r = 0; r < take; r++)
            {
                Dictionary<int, string> row = new Dictionary<int, string>();
                for (int c = 0; c < rows[r].Count; c++) row[c] = rows[r][c] ?? "";
                if (rows[r].Count > maxColumns) maxColumns = rows[r].Count;
                data.Add(row);
            }

            foreach (Dictionary<int, string> row in data)
                for (int c = 0; c < maxColumns; c++)
                    if (!row.ContainsKey(c)) row[c] = "";

            return data;
        }

        private static bool DetectAllRowsSimilar(List<Dictionary<int, string>> rowData)
        {
            List<int> counts = rowData.Select(row => row.Values.Count(v => !string.IsNullOrWhiteSpace(v))).ToList();
            double avg = counts.Average();
            double stdDev = Math.Sqrt(counts.Select(x => Math.Pow(x - avg, 2)).Average());
            double relative = avg > 0 ? stdDev / avg : 0;
            if (relative >= 0.1) return false;

            List<List<int>> patterns = rowData.Select(row => row.OrderBy(p => p.Key).Select(p => p.Value.Length).ToList()).ToList();
            for (int i = 1; i < patterns.Count; i++)
                if (PatternSimilarity(patterns[0], patterns[i]) < 0.8) return false;
            return true;
        }

        private static bool DetectSequentialFirstColumn(List<Dictionary<int, string>> rowData)
        {
            if (rowData.Count <= 2) return false;
            if (int.TryParse(rowData[1][0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int second) && second == 1) return true;

            int expected = 1;
            for (int r = 1; r < rowData.Count; r++)
            {
                if (!int.TryParse(rowData[r][0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int actual) || actual != expected) return false;
                expected++;
            }

            return true;
        }

        private static bool DetectHigherTextRatio(List<Dictionary<int, string>> rowData)
        {
            int firstText = 0, firstNumeric = 0, dataText = 0, dataNumeric = 0;
            foreach (string value in rowData[0].Values)
            {
                if (IsNumeric(value)) firstNumeric++;
                else if (!string.IsNullOrWhiteSpace(value)) firstText++;
            }

            for (int r = 1; r < rowData.Count; r++)
            {
                foreach (string value in rowData[r].Values)
                {
                    if (IsNumeric(value)) dataNumeric++;
                    else if (!string.IsNullOrWhiteSpace(value)) dataText++;
                }
            }

            double firstRatio = firstText + firstNumeric > 0 ? (double)firstText / (firstText + firstNumeric) : 0;
            double dataRatio = dataText + dataNumeric > 0 ? (double)dataText / (dataText + dataNumeric) : 0;
            return firstRatio > dataRatio && firstRatio > 0.5;
        }

        private static bool DetectDistinctFormat(List<Dictionary<int, string>> rowData)
        {
            int first = 0, second = 0;
            foreach (int col in rowData[0].Keys)
            {
                if (!rowData[1].ContainsKey(col)) continue;
                if (IsTitleCase(rowData[0][col]) || IsAllCaps(rowData[0][col])) first++;
                if (IsTitleCase(rowData[1][col]) || IsAllCaps(rowData[1][col])) second++;
            }

            return first > second;
        }

        private static bool DetectConsistentDataColumns(List<Dictionary<int, string>> rowData)
        {
            if (rowData.Count <= 2) return false;
            int maxCol = rowData.SelectMany(r => r.Keys).Max();
            Dictionary<int, List<string>> types = new Dictionary<int, List<string>>();
            for (int c = 0; c <= maxCol; c++)
            {
                types[c] = new List<string>();
                for (int r = 1; r < rowData.Count; r++)
                    if (rowData[r].TryGetValue(c, out string? value) && !string.IsNullOrWhiteSpace(value)) types[c].Add(DataType(value));
            }

            int consistent = 0;
            int firstDiffers = 0;
            foreach (KeyValuePair<int, List<string>> column in types)
            {
                if (column.Value.Count == 0 || column.Value.Distinct().Count() != 1) continue;
                consistent++;
                if (rowData[0].TryGetValue(column.Key, out string? firstValue) && !string.IsNullOrWhiteSpace(firstValue) && DataType(firstValue) != column.Value[0])
                    firstDiffers++;
            }

            return consistent > 0 && (double)consistent / Math.Max(1, types.Count) > 0.5 && firstDiffers > 0;
        }

        private static bool DetectHeaderTerms(List<Dictionary<int, string>> rowData)
        {
            int exact = 0, fragments = 0;
            foreach (string value in rowData[0].Values)
            {
                if (string.IsNullOrEmpty(value)) continue;
                string lower = value.Trim().ToLowerInvariant();
                if (_HeaderTerms.Contains(lower)) exact++;
                else if (_HeaderTerms.Any(term => term.Length > 2 && lower.IndexOf(term, StringComparison.Ordinal) >= 0)) fragments++;
            }

            return exact > 0 || fragments >= 2;
        }

        private static void DetectNumericSequence(List<Dictionary<int, string>> rowData, ref bool numericSequence, ref bool columnNumbers)
        {
            if (rowData[0].Count < 2) return;
            List<int> numbers = new List<int>();
            foreach (KeyValuePair<int, string> pair in rowData[0].OrderBy(p => p.Key))
                if (int.TryParse(pair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) numbers.Add(n);

            if (numbers.Count < 2) return;
            for (int i = 1; i < numbers.Count; i++)
                if (numbers[i] != numbers[i - 1] + 1) return;
            if (numbers.Count < rowData[0].Count / 2) return;

            numericSequence = true;
            if (numbers[0] != 1) return;
            bool matchesPosition = true;
            for (int i = 0; i < Math.Min(numbers.Count, 5); i++)
            {
                if (Math.Abs(numbers[i] - (i + 1)) > 2)
                {
                    matchesPosition = false;
                    break;
                }
            }

            if (matchesPosition || numbers.Count >= rowData[0].Count * 0.75)
            {
                columnNumbers = true;
                numericSequence = false;
            }
        }

        private static bool HasNamedColumnHeaders(IEnumerable<string> values)
        {
            Dictionary<string, List<int>> groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                int digit = -1;
                for (int i = 0; i < value.Length; i++)
                {
                    if (char.IsDigit(value[i]))
                    {
                        digit = i;
                        break;
                    }
                }

                if (digit <= 0) continue;
                string prefix = value.Substring(0, digit);
                if (!int.TryParse(value.Substring(digit), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) continue;
                if (!groups.ContainsKey(prefix)) groups[prefix] = new List<int>();
                groups[prefix].Add(number);
            }

            foreach (KeyValuePair<string, List<int>> group in groups)
            {
                if (group.Value.Count < 2) continue;
                List<int> sorted = group.Value.OrderBy(n => n).ToList();
                bool sequential = true;
                for (int i = 1; i < sorted.Count; i++)
                {
                    if (sorted[i] != sorted[i - 1] + 1)
                    {
                        sequential = false;
                        break;
                    }
                }

                if (sequential) return true;
            }

            return false;
        }

        private static bool DetectRow1Differs(List<Dictionary<int, string>> rowData)
        {
            if (rowData.Count < 3) return false;
            int maxCol = rowData.SelectMany(r => r.Keys).Max();
            Dictionary<int, HashSet<string>> values = new Dictionary<int, HashSet<string>>();
            for (int c = 0; c <= maxCol; c++) values[c] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int r = 1; r < rowData.Count; r++)
                foreach (KeyValuePair<int, string> pair in rowData[r])
                    if (!string.IsNullOrWhiteSpace(pair.Value)) values[pair.Key].Add(pair.Value);

            int consistent = 0, differs = 0;
            foreach (int col in rowData[0].Keys)
            {
                if (!values.ContainsKey(col) || values[col].Count > 1) continue;
                consistent++;
                string first = rowData[0][col];
                if (!string.IsNullOrWhiteSpace(first) && (values[col].Count == 0 || !values[col].Contains(first))) differs++;
            }

            return consistent > 0 && differs > 0 && (double)differs / consistent >= 0.5;
        }

        private static bool IsSimpleRowNumbering(List<Dictionary<int, string>> rowData)
        {
            if (rowData.Count < 3) return false;
            for (int i = 0; i < rowData.Count; i++)
            {
                if (!rowData[i].TryGetValue(0, out string? value)
                    || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
                    || n != i + 1)
                    return false;
            }

            return true;
        }

        private static bool IsPureNumericHeader(List<Dictionary<int, string>> rowData)
        {
            List<int> values = new List<int>();
            foreach (string v in rowData[0].Values)
                if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) values.Add(n);
            values.Sort();
            if (values.Count == 0 || values.Count < rowData[0].Count * 0.8 || values[0] > 3) return false;
            for (int i = 1; i < values.Count; i++)
                if (values[i] - values[i - 1] > 3) return false;
            return true;
        }

        private static double PatternSimilarity(List<int> a, List<int> b)
        {
            int min = Math.Min(a.Count, b.Count);
            if (min == 0) return 0;
            int matches = 0;
            for (int i = 0; i < min; i++)
            {
                if (a[i] == 0 && b[i] == 0) matches++;
                else if (a[i] > 0 && b[i] > 0)
                {
                    double ratio = (double)Math.Max(a[i], b[i]) / Math.Max(1, Math.Min(a[i], b[i]));
                    if (ratio <= 1.2) matches++;
                }
            }

            return (double)matches / min;
        }

        private static bool IsNumeric(string value)
        {
            return !string.IsNullOrEmpty(value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double _);
        }

        private static bool IsTitleCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 2) return false;
            string[] words = value.Split(' ');
            return words.Length > 0 && words.All(w => w.Length > 1 && char.IsUpper(w[0]) && w.Skip(1).All(c => !char.IsUpper(c) || !char.IsLetter(c)));
        }

        private static bool IsAllCaps(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length > 1 && value.Any(char.IsLetter) && value.All(c => !char.IsLetter(c) || char.IsUpper(c));
        }

        private static string DataType(string value)
        {
            if (string.IsNullOrEmpty(value)) return "empty";
            if (IsNumeric(value)) return "numeric";
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime _)) return "date";
            return "text";
        }
    }
}
