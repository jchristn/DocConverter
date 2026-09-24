namespace Test.Shared.Suites.Pdf
{
    using System;
    using Test.Shared.Fixtures.Builders;

    /// <summary>
    /// Lazily built PDF fixtures shared by the PDF cases.
    /// </summary>
    public static class PdfFixtures
    {
        private static readonly Lazy<byte[]> _Reference = new Lazy<byte[]>(PdfFixtureBuilder.BuildReference);
        private static readonly Lazy<byte[]> _ImageOnly = new Lazy<byte[]>(PdfFixtureBuilder.BuildImageOnly);
        private static readonly Lazy<byte[]> _Encrypted = new Lazy<byte[]>(PdfFixtureBuilder.BuildEncrypted);
        private static readonly Lazy<byte[]> _Corrupt = new Lazy<byte[]>(PdfFixtureBuilder.BuildCorrupt);

        /// <summary>The reference PDF.</summary>
        public static byte[] Reference => _Reference.Value;

        /// <summary>An image-only PDF.</summary>
        public static byte[] ImageOnly => _ImageOnly.Value;

        /// <summary>A password protected PDF.</summary>
        public static byte[] Encrypted => _Encrypted.Value;

        /// <summary>A truncated PDF.</summary>
        public static byte[] Corrupt => _Corrupt.Value;
    }
}
