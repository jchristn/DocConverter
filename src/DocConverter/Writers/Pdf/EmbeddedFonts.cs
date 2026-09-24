namespace DocConverter.Writers.Pdf
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Loads the Liberation Sans and Liberation Mono faces (SIL OFL 1.1) embedded in the DocConverter assembly.
    /// Face names are the file names without extension, for example "LiberationSans-Bold". Thread safe.
    /// </summary>
    internal static class EmbeddedFonts
    {
        internal const string SansFamily = "Liberation Sans";
        internal const string MonoFamily = "Liberation Mono";

        private static readonly object _Lock = new object();
        private static readonly Dictionary<string, byte[]> _Cache = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        internal static readonly string[] FaceNames = new string[]
        {
            "LiberationSans-Regular", "LiberationSans-Bold", "LiberationSans-Italic", "LiberationSans-BoldItalic",
            "LiberationMono-Regular", "LiberationMono-Bold", "LiberationMono-Italic", "LiberationMono-BoldItalic"
        };

        internal static byte[] Load(string faceName)
        {
            lock (_Lock)
            {
                if (_Cache.TryGetValue(faceName, out byte[]? cached)) return cached;

                string resource = "DocConverter.Fonts." + faceName + ".ttf";
                Assembly assembly = typeof(EmbeddedFonts).Assembly;
                using (Stream? stream = assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null) throw new InvalidOperationException("The embedded font resource '" + resource + "' was not found in the DocConverter assembly.");
                    using (MemoryStream ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        byte[] bytes = ms.ToArray();
                        _Cache[faceName] = bytes;
                        return bytes;
                    }
                }
            }
        }

        internal static string FaceFor(string? familyName, bool bold, bool italic)
        {
            string family = IsMonospaceFamily(familyName) ? "LiberationMono" : "LiberationSans";
            string style = bold && italic ? "BoldItalic" : bold ? "Bold" : italic ? "Italic" : "Regular";
            return family + "-" + style;
        }

        internal static bool IsMonospaceFamily(string? familyName)
        {
            if (string.IsNullOrEmpty(familyName)) return false;
            string lower = familyName!.ToLowerInvariant();
            return lower.IndexOf("mono", StringComparison.Ordinal) >= 0
                || lower.IndexOf("courier", StringComparison.Ordinal) >= 0
                || lower.IndexOf("consolas", StringComparison.Ordinal) >= 0
                || lower.IndexOf("menlo", StringComparison.Ordinal) >= 0
                || lower.IndexOf("monaco", StringComparison.Ordinal) >= 0
                || lower.IndexOf("lucida console", StringComparison.Ordinal) >= 0
                || lower.IndexOf("fixed", StringComparison.Ordinal) >= 0;
        }
    }
}
