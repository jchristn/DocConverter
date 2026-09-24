namespace DocConverter.Writers.Pdf
{
    using PdfSharp.Fonts;

    /// <summary>
    /// PDFsharp font resolver serving the Liberation faces embedded in DocConverter. Every requested family resolves to
    /// Liberation Sans, except monospace families (Courier New, Consolas, Menlo, anything containing "Mono") which
    /// resolve to Liberation Mono. MigraDoc asks for "Courier New" as its internal error font, so that mapping is
    /// required for rendering to work at all on macOS and Linux. Thread safe.
    /// </summary>
    public sealed class DocConverterFontResolver : IFontResolver
    {
        /// <summary>
        /// Instantiate the resolver.
        /// </summary>
        public DocConverterFontResolver()
        {
        }

        /// <summary>
        /// Resolve a family and style to an embedded face.
        /// </summary>
        /// <param name="familyName">Requested family.</param>
        /// <param name="bold">Bold requested.</param>
        /// <param name="italic">Italic requested.</param>
        /// <returns>The face to use. Never null.</returns>
        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
        {
            return new FontResolverInfo(EmbeddedFonts.FaceFor(familyName, bold, italic));
        }

        /// <summary>
        /// Font file bytes for a face returned by ResolveTypeface.
        /// </summary>
        /// <param name="faceName">Face name.</param>
        /// <returns>TrueType bytes.</returns>
        public byte[]? GetFont(string faceName)
        {
            return EmbeddedFonts.Load(faceName);
        }
    }
}
