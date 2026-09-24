#if !DOCCONVERTER_NETSTANDARD_TEST
namespace Test.Shared.Suites.Cli
{
    using System;
    using System.Text;
    using DocConverter;
    using DocConverter.Cli.Reporting;
    using Test.Shared.Fixtures;

    /// <summary>
    /// Inline sample documents for CLI tests. No embedded fixtures are used.
    /// </summary>
    public static class CliSamples
    {
        /// <summary>
        /// A small Markdown document.
        /// </summary>
        public const string Markdown = "# Title\n\nHello **world** and a [link](https://example.com).\n\n- one\n- two\n";

        /// <summary>
        /// Markdown with a paragraph and a table, so CSV output drops content (NonTableContentDropped).
        /// </summary>
        public const string MarkdownWithTable = "# Data\n\nIntro paragraph.\n\n| Name | Value |\n| --- | --- |\n| a | 1 |\n| b | 2 |\n";

        /// <summary>
        /// Markdown with two tables.
        /// </summary>
        public const string MarkdownTwoTables = "| A | B |\n| --- | --- |\n| 1 | 2 |\n\nBetween.\n\n| C | D |\n| --- | --- |\n| 3 | 4 |\n";

        /// <summary>
        /// Markdown with YAML front matter.
        /// </summary>
        public const string MarkdownFrontMatter = "---\ntitle: Front Title\n---\n\n# Heading One\n\nBody.\n";

        /// <summary>
        /// A long paragraph for wrapping tests.
        /// </summary>
        public const string LongParagraph = "The quick brown fox jumps over the lazy dog and keeps running through the forest until the sun goes down over the distant hills.\n";

        /// <summary>
        /// HTML with an underline, which Markdown cannot express (FormattingLost).
        /// </summary>
        public const string HtmlUnderline = "<html><body><h1>Head</h1><p>Some <u>underlined</u> text.</p></body></html>";

        /// <summary>
        /// A 4x4 PNG as a data URI image in Markdown.
        /// </summary>
        public static string MarkdownWithImage()
        {
            byte[] png = TestImages.SolidPng(4, 4, 200, 10, 10);
            return "# Pic\n\n![red square](data:image/png;base64," + Convert.ToBase64String(png) + ")\n\nAfter the image.\n";
        }

        /// <summary>
        /// Bytes starting with a PNG signature followed by every byte value, for byte exact pipe tests.
        /// </summary>
        public static byte[] BinaryPayload()
        {
            byte[] header = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            byte[] data = new byte[header.Length + 512];
            Buffer.BlockCopy(header, 0, data, 0, header.Length);
            for (int i = 0; i < 512; i++) data[header.Length + i] = (byte)(i % 256);
            return data;
        }

        /// <summary>
        /// An OLE2 compound file header with the UTF-16LE "WordDocument" stream name, like a legacy .doc file.
        /// </summary>
        public static byte[] LegacyDoc()
        {
            byte[] header = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
            byte[] name = Encoding.Unicode.GetBytes("WordDocument");
            byte[] data = new byte[1024];
            Buffer.BlockCopy(header, 0, data, 0, header.Length);
            Buffer.BlockCopy(name, 0, data, 600, name.Length);
            return data;
        }

        /// <summary>
        /// Pseudo random bytes with nulls and control characters, recognized as nothing.
        /// </summary>
        public static byte[] RandomBinary()
        {
            byte[] data = new byte[4096];
            Random random = new Random(1234);
            random.NextBytes(data);
            data[0] = 0x00;
            data[1] = 0x01;
            data[2] = 0x02;
            data[3] = 0x03;
            return data;
        }

        /// <summary>
        /// UTF-8 bytes of text without a byte order mark.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>Bytes.</returns>
        public static byte[] Utf8(string text)
        {
            return new UTF8Encoding(false).GetBytes(text);
        }

        /// <summary>
        /// Parse the last non-empty line of text as a convert report.
        /// </summary>
        /// <param name="text">Text holding the report.</param>
        /// <returns>Report.</returns>
        public static CliReport LastLineReport(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            CliReport? report = CliReportWriter.Deserialize<CliReport>(lines[lines.Length - 1]);
            if (report == null) throw new TestAssertionException("No report found in: " + text);
            return report;
        }

        /// <summary>
        /// A converter factory that registers the echo reader and writer (PNG in, PDF out).
        /// </summary>
        /// <returns>Factory.</returns>
        public static Func<ConverterSettings, Converter> EchoFactory()
        {
            return settings =>
            {
                Converter converter = new Converter(settings);
                converter.RegisterReader(new EchoImageReader());
                converter.RegisterWriter(new EchoBytesWriter());
                return converter;
            };
        }
    }
}
#endif
