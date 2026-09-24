namespace Test.Shared.Inspection
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// What an inspector found in converted output, extracted by parsing the output independently of DocConverter's
    /// readers. Matrix suites compare it against the reference content.
    /// </summary>
    public class ContentSnapshot
    {
        /// <summary>
        /// All text found in the output, in reading order, whitespace normalized to single spaces.
        /// </summary>
        public string AllText { get; set; } = "";

        /// <summary>
        /// Heading texts, when the format has headings.
        /// </summary>
        public List<string> Headings { get; set; } = new List<string>();

        /// <summary>
        /// List item texts, when the format has lists.
        /// </summary>
        public List<string> ListItems { get; set; } = new List<string>();

        /// <summary>
        /// Table rows as cell texts, across all tables.
        /// </summary>
        public List<List<string>> TableRows { get; set; } = new List<List<string>>();

        /// <summary>
        /// Number of embedded images.
        /// </summary>
        public int ImageCount { get; set; } = 0;

        /// <summary>
        /// Link URLs.
        /// </summary>
        public List<string> LinkUrls { get; set; } = new List<string>();

        /// <summary>
        /// Document title from metadata, when present.
        /// </summary>
        public string? Title { get; set; } = null;

        /// <summary>
        /// True when AllText contains the fragment after whitespace normalization.
        /// </summary>
        /// <param name="fragment">Fragment.</param>
        /// <returns>True when present.</returns>
        public bool ContainsText(string fragment)
        {
            return AllText.IndexOf(Normalize(fragment), StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// True when some table row contains all of the given cell texts in order.
        /// </summary>
        /// <param name="cells">Cells.</param>
        /// <returns>True when found.</returns>
        public bool HasTableRow(IReadOnlyList<string> cells)
        {
            foreach (List<string> row in TableRows)
            {
                if (row.Count < cells.Count) continue;
                bool match = true;
                for (int i = 0; i < cells.Count; i++)
                {
                    if (Normalize(row[i]) != Normalize(cells[i]))
                    {
                        match = false;
                        break;
                    }
                }

                if (match) return true;
            }

            return false;
        }

        /// <summary>
        /// Collapse whitespace runs to single spaces and trim.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>Normalized text.</returns>
        public static string Normalize(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder(text!.Length);
            bool space = false;
            foreach (char c in text)
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
    }
}
