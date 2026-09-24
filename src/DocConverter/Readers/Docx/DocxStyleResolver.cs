namespace DocConverter.Readers.Docx
{
    using System;
    using System.Collections.Generic;
    using DocConverter.Enums;
    using DocumentFormat.OpenXml.Packaging;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Answers questions about paragraph and character styles, following basedOn chains.
    /// </summary>
    internal sealed class DocxStyleResolver
    {
        private readonly Dictionary<string, W.Style> _Styles = new Dictionary<string, W.Style>(StringComparer.Ordinal);

        internal DocxStyleResolver(StyleDefinitionsPart? part)
        {
            if (part == null || part.Styles == null) return;
            foreach (W.Style style in part.Styles.Elements<W.Style>())
            {
                string? id = style.StyleId?.Value;
                if (!string.IsNullOrEmpty(id) && !_Styles.ContainsKey(id!)) _Styles[id!] = style;
            }
        }

        internal int? HeadingLevel(string? styleId)
        {
            if (string.IsNullOrEmpty(styleId)) return null;
            foreach (W.Style style in Chain(styleId))
            {
                int? byName = LevelFromName(style.StyleId?.Value) ?? LevelFromName(style.StyleName?.Val?.Value);
                if (byName.HasValue) return byName;
                int? outline = style.StyleParagraphProperties?.OutlineLevel?.Val?.Value;
                if (outline.HasValue && outline.Value >= 0 && outline.Value < 9) return ClampLevel(outline.Value + 1);
            }

            if (!_Styles.ContainsKey(styleId!)) return LevelFromName(styleId);
            return null;
        }

        internal bool IsTitle(string? styleId)
        {
            if (string.IsNullOrEmpty(styleId)) return false;
            foreach (W.Style style in Chain(styleId))
            {
                if (Normalize(style.StyleId?.Value) == "title" || Normalize(style.StyleName?.Val?.Value) == "title") return true;
            }

            return Normalize(styleId) == "title";
        }

        internal bool IsCodeStyle(string? styleId)
        {
            if (string.IsNullOrEmpty(styleId)) return false;
            if (NameSuggestsCode(styleId)) return true;
            foreach (W.Style style in Chain(styleId))
            {
                if (NameSuggestsCode(style.StyleId?.Value) || NameSuggestsCode(style.StyleName?.Val?.Value)) return true;
                W.StyleRunProperties? rPr = style.StyleRunProperties;
                if (rPr != null && rPr.RunFonts != null && DocxFonts.IsMonospace(rPr.RunFonts.Ascii?.Value)) return true;
            }

            return false;
        }

        internal bool IsQuoteStyle(string? styleId)
        {
            if (string.IsNullOrEmpty(styleId)) return false;
            if (Normalize(styleId).IndexOf("quote", StringComparison.Ordinal) >= 0) return true;
            foreach (W.Style style in Chain(styleId))
            {
                if (Normalize(style.StyleId?.Value).IndexOf("quote", StringComparison.Ordinal) >= 0) return true;
                if (Normalize(style.StyleName?.Val?.Value).IndexOf("quote", StringComparison.Ordinal) >= 0) return true;
            }

            return false;
        }

        internal DocxNumberingRef? StyleNumbering(string? styleId)
        {
            if (string.IsNullOrEmpty(styleId)) return null;
            foreach (W.Style style in Chain(styleId))
            {
                W.NumberingProperties? numPr = style.StyleParagraphProperties?.NumberingProperties;
                if (numPr != null && numPr.NumberingId?.Val != null)
                    return new DocxNumberingRef(numPr.NumberingId.Val.Value, numPr.NumberingLevelReference?.Val?.Value ?? 0);
            }

            return null;
        }

        internal InlineStyleEnum CharacterStyle(string? styleId)
        {
            InlineStyleEnum flags = InlineStyleEnum.None;
            if (string.IsNullOrEmpty(styleId)) return flags;
            string norm = Normalize(styleId);
            if (norm == "hyperlink" || norm == "followedhyperlink") return flags;
            if (NameSuggestsCode(styleId)) flags |= InlineStyleEnum.Code;
            if (norm == "strong") flags |= InlineStyleEnum.Bold;
            if (norm == "emphasis") flags |= InlineStyleEnum.Italic;

            foreach (W.Style style in Chain(styleId))
            {
                string name = Normalize(style.StyleName?.Val?.Value);
                if (NameSuggestsCode(style.StyleName?.Val?.Value)) flags |= InlineStyleEnum.Code;
                if (name == "strong") flags |= InlineStyleEnum.Bold;
                if (name == "emphasis") flags |= InlineStyleEnum.Italic;
                W.StyleRunProperties? rPr = style.StyleRunProperties;
                if (rPr == null) continue;
                if (IsOn(rPr.Bold)) flags |= InlineStyleEnum.Bold;
                if (IsOn(rPr.Italic)) flags |= InlineStyleEnum.Italic;
                if (IsOn(rPr.Strike) || IsOn(rPr.DoubleStrike)) flags |= InlineStyleEnum.Strikethrough;
                if (rPr.Underline != null && rPr.Underline.Val != null && rPr.Underline.Val.Value != W.UnderlineValues.None) flags |= InlineStyleEnum.Underline;
                if (rPr.RunFonts != null && DocxFonts.IsMonospace(rPr.RunFonts.Ascii?.Value)) flags |= InlineStyleEnum.Code;
                if (rPr.VerticalTextAlignment?.Val != null)
                {
                    if (rPr.VerticalTextAlignment.Val.Value == W.VerticalPositionValues.Superscript) flags |= InlineStyleEnum.Superscript;
                    else if (rPr.VerticalTextAlignment.Val.Value == W.VerticalPositionValues.Subscript) flags |= InlineStyleEnum.Subscript;
                }
            }

            return flags;
        }

        internal static bool IsOn(W.OnOffType? element)
        {
            if (element == null) return false;
            if (element.Val == null) return true;
            return element.Val.Value;
        }

        private IEnumerable<W.Style> Chain(string? styleId)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            string? current = styleId;
            int guard = 0;
            while (!string.IsNullOrEmpty(current) && guard++ < 32 && seen.Add(current!))
            {
                if (!_Styles.TryGetValue(current!, out W.Style? style)) yield break;
                yield return style;
                current = style.BasedOn?.Val?.Value;
            }
        }

        private static int? LevelFromName(string? name)
        {
            string n = Normalize(name);
            if (n.Length == 0) return null;
            if (n == "title") return 1;
            if (n == "subtitle") return 2;
            if (n.StartsWith("heading", StringComparison.Ordinal))
            {
                string digits = n.Substring("heading".Length);
                if (int.TryParse(digits, out int level) && level >= 1 && level <= 9) return ClampLevel(level);
            }

            return null;
        }

        private static int ClampLevel(int level)
        {
            if (level < 1) return 1;
            if (level > 6) return 6;
            return level;
        }

        private static bool NameSuggestsCode(string? name)
        {
            string n = Normalize(name);
            if (n.Length == 0) return false;
            return n.IndexOf("code", StringComparison.Ordinal) >= 0
                || n.IndexOf("preformatted", StringComparison.Ordinal) >= 0
                || n == "htmlpre" || n == "plaintext" || n == "macrotext";
        }

        private static string Normalize(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value!.Replace(" ", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
        }
    }
}
