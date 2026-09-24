namespace DocConverter.Detection
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Xml;

    /// <summary>
    /// Heuristics that tell text formats apart: JSON and XML by parsing, HTML by markers, CSV and TSV by consistent
    /// field counts, and Markdown by scoring its syntax.
    /// </summary>
    internal static class TextFormatHeuristics
    {
        /// <summary>
        /// Minimum Markdown score for text to be classified as Markdown.
        /// </summary>
        internal const int MarkdownThreshold = 3;

        private static readonly Regex _AtxHeading = new Regex(@"^\s{0,3}#{1,6}\s+\S", RegexOptions.Compiled);
        private static readonly Regex _Bullet = new Regex(@"^\s*[-*+]\s+\S", RegexOptions.Compiled);
        private static readonly Regex _Numbered = new Regex(@"^\s*\d{1,9}[.)]\s+\S", RegexOptions.Compiled);
        private static readonly Regex _Fence = new Regex(@"^\s{0,3}(```|~~~)", RegexOptions.Compiled);
        private static readonly Regex _Quote = new Regex(@"^\s{0,3}>\s?", RegexOptions.Compiled);
        private static readonly Regex _TableSeparator = new Regex(@"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$", RegexOptions.Compiled);
        private static readonly Regex _Link = new Regex(@"\[[^\]]+\]\([^)\s]+\)", RegexOptions.Compiled);
        private static readonly Regex _Emphasis = new Regex(@"(\*\*|__)[^*_\s][^*_]*(\*\*|__)", RegexOptions.Compiled);
        private static readonly Regex _Setext = new Regex(@"^\s{0,3}(=+|-+)\s*$", RegexOptions.Compiled);
        private static readonly Regex _HtmlTag = new Regex(@"<(html|head|body|div|p|span|table|ul|ol|li|h[1-6]|a|br|img|section|article)(\s[^>]*)?/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _HtmlElementName = new Regex(@"^(html|head|body|div|p|span|table|ul|ol|li|h[1-6]|a|br|img|section|article|main|header|footer|nav|form|pre|blockquote)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static bool IsJson(byte[] d, int offset, int length)
        {
            try
            {
                Utf8JsonReader reader = new Utf8JsonReader(new ReadOnlySpan<byte>(d, offset, length), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                int tokens = 0;
                while (reader.Read()) tokens++;
                return tokens > 0;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        internal static string? XmlRootName(string text)
        {
            try
            {
                XmlReaderSettings settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Ignore,
                    XmlResolver = null,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true,
                    IgnoreWhitespace = true
                };

                string? root = null;
                using (StringReader sr = new StringReader(text))
                using (XmlReader reader = XmlReader.Create(sr, settings))
                {
                    while (reader.Read())
                    {
                        if (root == null && reader.NodeType == XmlNodeType.Element) root = reader.LocalName;
                    }
                }

                return root;
            }
            catch (XmlException)
            {
                return null;
            }
        }

        internal static bool IsHtmlElementName(string name)
        {
            return _HtmlElementName.IsMatch(name);
        }

        internal static bool LooksLikeHtml(string text)
        {
            string head = text.Length > 4096 ? text.Substring(0, 4096) : text;
            string lower = head.TrimStart().ToLowerInvariant();
            if (lower.StartsWith("<!doctype html", StringComparison.Ordinal)) return true;
            if (lower.StartsWith("<html", StringComparison.Ordinal)) return true;
            if (lower.IndexOf("<body", StringComparison.Ordinal) >= 0 && lower.IndexOf("</", StringComparison.Ordinal) >= 0) return true;
            if (lower.StartsWith("<", StringComparison.Ordinal))
            {
                MatchCollection tags = _HtmlTag.Matches(head);
                return tags.Count >= 2;
            }

            return false;
        }

        internal static int MarkdownScore(string text)
        {
            string[] lines = SplitLines(text, 400);
            int score = 0;
            bool heading = false, bullet = false, numbered = false, fence = false, quote = false, table = false, setext = false;
            string previous = "";
            foreach (string line in lines)
            {
                if (!heading && _AtxHeading.IsMatch(line)) { heading = true; score += 2; }
                if (!bullet && _Bullet.IsMatch(line)) { bullet = true; score += 1; }
                if (!numbered && _Numbered.IsMatch(line)) { numbered = true; score += 1; }
                if (!fence && _Fence.IsMatch(line)) { fence = true; score += 3; }
                if (!quote && _Quote.IsMatch(line) && line.TrimStart().Length > 1) { quote = true; score += 1; }
                if (!table && _TableSeparator.IsMatch(line)) { table = true; score += 3; }
                if (!setext && previous.Trim().Length > 0 && _Setext.IsMatch(line) && line.Trim().Length >= 3) { setext = true; score += 2; }
                previous = line;
            }

            if (_Link.IsMatch(text)) score += 2;
            if (_Emphasis.IsMatch(text)) score += 1;
            return score;
        }

        /// <summary>
        /// True when at least two non-empty lines all split into the same number (at least two) of fields.
        /// </summary>
        internal static bool IsDelimited(string text, char delimiter)
        {
            string[] lines = SplitLines(text, 50);
            int expected = -1;
            int rows = 0;
            foreach (string raw in lines)
            {
                if (raw.Trim().Length == 0) continue;
                int count = CountFields(raw, delimiter);
                if (count < 2) return false;
                if (expected < 0) expected = count;
                else if (count != expected) return false;
                rows++;
            }

            return rows >= 2;
        }

        internal static bool LooksBinary(byte[] d, int offset, int length, int sampleBytes)
        {
            int n = Math.Min(length, sampleBytes);
            if (n == 0) return false;
            int nulls = 0;
            int control = 0;
            for (int i = offset; i < offset + n; i++)
            {
                byte b = d[i];
                if (b == 0) nulls++;
                else if (b < 0x09 || (b > 0x0D && b < 0x20 && b != 0x1B)) control++;
            }

            if (nulls > 0) return true;
            return control > n / 20;
        }

        private static int CountFields(string line, char delimiter)
        {
            int count = 1;
            bool quoted = false;
            foreach (char c in line)
            {
                if (c == '"') quoted = !quoted;
                else if (c == delimiter && !quoted) count++;
            }

            return count;
        }

        private static string[] SplitLines(string text, int max)
        {
            List<string> lines = new List<string>();
            using (StringReader sr = new StringReader(text))
            {
                string? line;
                while (lines.Count < max && (line = sr.ReadLine()) != null) lines.Add(line);
            }

            return lines.ToArray();
        }
    }
}
