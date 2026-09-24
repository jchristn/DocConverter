namespace DocConverter.Readers.Pdf
{
    using System;
    using UglyToad.PdfPig.Content;

    /// <summary>
    /// Infers bold, italic and monospace from PDF font names such as "ABCDEF+LiberationSans-BoldItalic" or
    /// "Liberation Sans,Bold".
    /// </summary>
    internal static class PdfFontClassifier
    {
        internal static bool IsBold(Letter letter)
        {
            string name = Name(letter);
            return name.IndexOf("bold", StringComparison.Ordinal) >= 0
                || name.IndexOf("black", StringComparison.Ordinal) >= 0
                || name.IndexOf("heavy", StringComparison.Ordinal) >= 0
                || name.IndexOf("semibold", StringComparison.Ordinal) >= 0;
        }

        internal static bool IsItalic(Letter letter)
        {
            string name = Name(letter);
            return name.IndexOf("italic", StringComparison.Ordinal) >= 0 || name.IndexOf("oblique", StringComparison.Ordinal) >= 0;
        }

        internal static bool IsMono(Letter letter)
        {
            string name = Name(letter);
            return name.IndexOf("mono", StringComparison.Ordinal) >= 0
                || name.IndexOf("courier", StringComparison.Ordinal) >= 0
                || name.IndexOf("consolas", StringComparison.Ordinal) >= 0
                || name.IndexOf("menlo", StringComparison.Ordinal) >= 0
                || name.IndexOf("monaco", StringComparison.Ordinal) >= 0;
        }

        internal static bool IsBold(Word word)
        {
            foreach (Letter letter in word.Letters)
                if (!string.IsNullOrWhiteSpace(letter.Value)) return IsBold(letter);
            return false;
        }

        internal static bool IsItalic(Word word)
        {
            foreach (Letter letter in word.Letters)
                if (!string.IsNullOrWhiteSpace(letter.Value)) return IsItalic(letter);
            return false;
        }

        internal static bool IsMono(Word word)
        {
            foreach (Letter letter in word.Letters)
                if (!string.IsNullOrWhiteSpace(letter.Value)) return IsMono(letter);
            return false;
        }

        private static string Name(Letter letter)
        {
            string? name = letter.FontName;
            return string.IsNullOrEmpty(name) ? "" : name!.ToLowerInvariant();
        }
    }
}
