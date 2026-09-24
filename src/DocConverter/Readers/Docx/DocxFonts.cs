namespace DocConverter.Readers.Docx
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Font facts used to recognize code in Word documents.
    /// </summary>
    internal static class DocxFonts
    {
        internal const string CodeFont = "Consolas";

        private static readonly HashSet<string> _Monospace = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Consolas", "Courier", "Courier New", "Menlo", "Monaco", "Liberation Mono", "Source Code Pro",
            "Lucida Console", "Lucida Sans Typewriter", "DejaVu Sans Mono", "Cascadia Code", "Cascadia Mono", "Fira Code",
            "Fira Mono", "Inconsolata", "Roboto Mono", "SF Mono", "Ubuntu Mono", "Noto Sans Mono", "JetBrains Mono",
            "Andale Mono", "Monospace", "Courier Prime", "IBM Plex Mono", "Hack", "Anonymous Pro", "PT Mono",
            "Droid Sans Mono", "OCR A Extended", "MS Gothic", "Nimbus Mono L", "Nimbus Mono PS"
        };

        internal static bool IsMonospace(string? font)
        {
            if (string.IsNullOrWhiteSpace(font)) return false;
            return _Monospace.Contains(font!.Trim());
        }
    }
}
