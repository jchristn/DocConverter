namespace DocConverter.Writers.Docx
{
    using System;

    /// <summary>
    /// Decides which link targets may be written: http, https and mailto URLs, relative references and in-document
    /// anchors. Anything else (javascript:, file:, data: and so on) is dropped.
    /// </summary>
    internal static class DocxLinkPolicy
    {
        internal static bool IsAnchor(string url)
        {
            return url.StartsWith("#", StringComparison.Ordinal) && url.Length > 1;
        }

        internal static bool IsAllowed(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            string trimmed = url.Trim();
            if (IsAnchor(trimmed)) return true;
            string? scheme = Scheme(trimmed);
            if (scheme == null) return true;
            return scheme == "http" || scheme == "https" || scheme == "mailto";
        }

        private static string? Scheme(string url)
        {
            int colon = url.IndexOf(':');
            if (colon <= 0) return null;
            int slash = url.IndexOfAny(new char[] { '/', '?', '#' });
            if (slash >= 0 && slash < colon) return null;
            for (int i = 0; i < colon; i++)
            {
                char c = url[i];
                bool ok = char.IsLetter(c) || (i > 0 && (char.IsDigit(c) || c == '+' || c == '-' || c == '.'));
                if (!ok) return "invalid";
            }

            return url.Substring(0, colon).ToLowerInvariant();
        }
    }
}
