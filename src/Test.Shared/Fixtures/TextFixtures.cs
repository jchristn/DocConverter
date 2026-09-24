namespace Test.Shared.Fixtures
{
    using System;
    using DocConverter.Enums;

    /// <summary>
    /// Hand-written reference fixtures for the text based formats, embedded from Fixtures/Text.
    /// </summary>
    public static class TextFixtures
    {
        /// <summary>
        /// The reference fixture for a text based format. Json and Xml return arbitrary (non-canonical) data documents;
        /// use CanonicalJson and CanonicalXml for the canonical forms.
        /// </summary>
        /// <param name="format">Text based format.</param>
        /// <returns>Fixture bytes.</returns>
        /// <exception cref="ArgumentException">Thrown for formats without a text fixture.</exception>
        public static byte[] Reference(DocumentFormatEnum format)
        {
            switch (format)
            {
                case DocumentFormatEnum.Markdown: return TestSupport.Fixture("Text/reference.md");
                case DocumentFormatEnum.Html: return TestSupport.Fixture("Text/reference.html");
                case DocumentFormatEnum.Text: return TestSupport.Fixture("Text/reference.txt");
                case DocumentFormatEnum.Csv: return TestSupport.Fixture("Text/reference.csv");
                case DocumentFormatEnum.Tsv: return TestSupport.Fixture("Text/reference.tsv");
                case DocumentFormatEnum.Json: return TestSupport.Fixture("Text/reference.json");
                case DocumentFormatEnum.Xml: return TestSupport.Fixture("Text/reference.xml");
                default: throw new ArgumentException("No text fixture for " + format + ".", nameof(format));
            }
        }

        /// <summary>
        /// The reference document in canonical JSON form (checked in, produced once and reviewed).
        /// </summary>
        /// <returns>Fixture bytes.</returns>
        public static byte[] CanonicalJson()
        {
            return TestSupport.Fixture("Text/reference.canonical.json");
        }

        /// <summary>
        /// The reference document in canonical XML form (checked in, produced once and reviewed).
        /// </summary>
        /// <returns>Fixture bytes.</returns>
        public static byte[] CanonicalXml()
        {
            return TestSupport.Fixture("Text/reference.canonical.xml");
        }
    }
}
