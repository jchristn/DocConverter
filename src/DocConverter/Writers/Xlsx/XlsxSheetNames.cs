namespace DocConverter.Writers.Xlsx
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Produces worksheet names Excel accepts: at most 31 characters, none of []:*?/\, not blank, not starting or ending
    /// with an apostrophe, and unique regardless of case (duplicates get " (2)", " (3)" and so on).
    /// </summary>
    internal static class XlsxSheetNames
    {
        internal const int MaxLength = 31;

        internal static string MakeUnique(string? requested, string fallback, HashSet<string> used)
        {
            string name = Sanitize(requested);
            if (name.Length == 0) name = Sanitize(fallback);
            if (name.Length == 0) name = "Sheet";

            string candidate = name;
            int n = 2;
            while (used.Contains(candidate))
            {
                string suffix = " (" + n + ")";
                string stem = name.Length + suffix.Length > MaxLength ? name.Substring(0, MaxLength - suffix.Length).TrimEnd() : name;
                candidate = stem + suffix;
                n++;
            }

            used.Add(candidate);
            return candidate;
        }

        internal static HashSet<string> NewSet()
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        internal static string Sanitize(string? requested)
        {
            if (string.IsNullOrEmpty(requested)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in requested!)
            {
                if (c == '[' || c == ']' || c == ':' || c == '*' || c == '?' || c == '/' || c == '\\') continue;
                if (char.IsControl(c)) continue;
                sb.Append(c);
            }

            string name = sb.ToString().Trim().Trim('\'').Trim();
            if (name.Length > MaxLength) name = name.Substring(0, MaxLength).TrimEnd();
            if (string.Equals(name, "History", StringComparison.OrdinalIgnoreCase)) name = "History (sheet)";
            return name;
        }
    }
}
