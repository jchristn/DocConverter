namespace Test.Shared.Fixtures.Builders
{
    using PdfSharp.Drawing;

    /// <summary>
    /// A run of text drawn by PdfFixtureBuilder: font, optional link target, and decoration.
    /// </summary>
    internal sealed class PdfFixtureRun
    {
        internal string Text { get; }

        internal XFont Font { get; }

        internal string? Url { get; }

        internal bool Underline { get; }

        internal bool Strike { get; }

        internal PdfFixtureRun(string text, XFont font, string? url, bool underline, bool strike)
        {
            Text = text;
            Font = font;
            Url = url;
            Underline = underline;
            Strike = strike;
        }
    }
}
