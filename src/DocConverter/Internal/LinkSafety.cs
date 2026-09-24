namespace DocConverter.Internal
{
    using System;

    /// <summary>
    /// Decides whether a link URL may be written. Allowed: http, https, mailto, fragment (#...) and relative URLs.
    /// Anything else, including javascript:, vbscript: and data: links, is refused.
    /// </summary>
    internal static class LinkSafety
    {
        internal static bool IsSafe(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            string trimmed = url!.Trim();

            // Strip characters browsers ignore inside schemes ("java\tscript:").
            System.Text.StringBuilder sb = new System.Text.StringBuilder(trimmed.Length);
            foreach (char c in trimmed)
                if (!char.IsControl(c) && !char.IsWhiteSpace(c)) sb.Append(c);
            string compact = sb.ToString();

            int colon = compact.IndexOf(':');
            if (colon < 0) return true;

            int slash = compact.IndexOfAny(new char[] { '/', '?', '#' });
            if (slash >= 0 && slash < colon) return true;

            string scheme = compact.Substring(0, colon).ToLowerInvariant();
            return scheme == "http" || scheme == "https" || scheme == "mailto";
        }
    }
}
