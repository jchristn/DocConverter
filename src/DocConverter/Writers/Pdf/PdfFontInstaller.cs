namespace DocConverter.Writers.Pdf
{
    using System;
    using PdfSharp.Fonts;

    /// <summary>
    /// Installs the embedded font resolver into PDFsharp's process-global font settings, once, under a lock.
    /// When the host application has already installed its own resolver, it is left in place and DocConverter's resolver
    /// is registered as the fallback (only if no fallback is set), so the Liberation families still resolve.
    /// </summary>
    internal static class PdfFontInstaller
    {
        private static readonly object _Lock = new object();
        private static volatile bool _Installed = false;

        internal static void EnsureInstalled()
        {
            if (_Installed) return;
            lock (_Lock)
            {
                if (_Installed) return;
                try
                {
                    if (GlobalFontSettings.FontResolver == null)
                    {
                        GlobalFontSettings.FontResolver = new DocConverterFontResolver();
                    }
                    else if (!(GlobalFontSettings.FontResolver is DocConverterFontResolver) && GlobalFontSettings.FallbackFontResolver == null)
                    {
                        GlobalFontSettings.FallbackFontResolver = new DocConverterFontResolver();
                    }
                }
                catch (InvalidOperationException)
                {
                    // PDFsharp refuses resolver changes after fonts were first used. The host's configuration stands.
                }

                _Installed = true;
            }
        }

        internal static bool EmbeddedResolverActive
        {
            get
            {
                return GlobalFontSettings.FontResolver is DocConverterFontResolver
                    || GlobalFontSettings.FallbackFontResolver is DocConverterFontResolver;
            }
        }
    }
}
