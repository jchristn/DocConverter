namespace Test.Shared.Suites
{
    using System;
    using DocConverter.Detection;
    using DocConverter.Enums;
    using Touchstone.Core;

    /// <summary>
    /// Format names, aliases, extensions, media types and classification helpers.
    /// </summary>
    public static class FormatParserSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            SuiteBuilder s = new SuiteBuilder("FormatParser", "Format names, aliases and extensions");

            string[][] aliases = new string[][]
            {
                new[] { "txt", "Text" }, new[] { "text", "Text" }, new[] { "plain", "Text" }, new[] { "md", "Markdown" },
                new[] { "markdown", "Markdown" }, new[] { "mkd", "Markdown" }, new[] { "htm", "Html" }, new[] { "html", "Html" },
                new[] { "xhtml", "Html" }, new[] { "json", "Json" }, new[] { "xml", "Xml" }, new[] { "csv", "Csv" },
                new[] { "tsv", "Tsv" }, new[] { "tab", "Tsv" }, new[] { "rtf", "Rtf" }, new[] { "docx", "Docx" },
                new[] { "word", "Docx" }, new[] { "xlsx", "Xlsx" }, new[] { "excel", "Xlsx" }, new[] { "pptx", "Pptx" },
                new[] { "powerpoint", "Pptx" }, new[] { "pdf", "Pdf" }, new[] { "png", "Png" }, new[] { "jpg", "Jpeg" },
                new[] { "jpeg", "Jpeg" }, new[] { "jpe", "Jpeg" }, new[] { "gif", "Gif" }, new[] { "bmp", "Bmp" },
                new[] { "dib", "Bmp" }, new[] { "tif", "Tiff" }, new[] { "tiff", "Tiff" }, new[] { "webp", "WebP" }, new[] { "auto", "Auto" }
            };

            foreach (string[] pair in aliases)
            {
                string alias = pair[0];
                DocumentFormatEnum expected = (DocumentFormatEnum)Enum.Parse(typeof(DocumentFormatEnum), pair[1]);
                s.AddSync("Alias_" + alias, "'" + alias + "', '." + alias + "' and upper case parse to " + expected, () =>
                {
                    foreach (string variant in new[] { alias, "." + alias, alias.ToUpperInvariant(), "  " + alias + "  " })
                    {
                        TestSupport.Assert(DocumentFormatParser.TryParse(variant, out DocumentFormatEnum parsed), "TryParse failed for '" + variant + "'");
                        TestSupport.AssertEqual(expected, parsed, "parsed format for '" + variant + "'");
                    }
                });
            }

            s.AddSync("InvalidNames", "Null, empty, whitespace and unknown names fail", () =>
            {
                foreach (string? bad in new string?[] { null, "", "   ", "doc", "xls", "ppt", "odt", "exe", "mdx", "..", "pdfx" })
                    TestSupport.Assert(!DocumentFormatParser.TryParse(bad, out DocumentFormatEnum _), "TryParse accepted '" + bad + "'");
            });

            s.AddSync("ParseThrows", "Parse throws ArgumentException naming the known formats", () =>
            {
                ArgumentException ex = TestSupport.ExpectThrows<ArgumentException>(() => DocumentFormatParser.Parse("banana"), "Parse of unknown name");
                TestSupport.AssertContains(ex.Message, "banana", "message names the value");
                TestSupport.AssertContains(ex.Message, "markdown", "message lists known names");
            });

            s.AddSync("FromExtension", "FromExtension reads file names and paths on every platform's separators", () =>
            {
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Docx, DocumentFormatParser.FromExtension("report.docx"), "plain name");
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Markdown, DocumentFormatParser.FromExtension("/home/user/notes.MD"), "unix path, upper case");
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Pdf, DocumentFormatParser.FromExtension("C:\\docs\\a.b.pdf"), "windows path, dotted name");
                TestSupport.AssertEqual<DocumentFormatEnum?>(null, DocumentFormatParser.FromExtension("README"), "no extension");
                TestSupport.AssertEqual<DocumentFormatEnum?>(null, DocumentFormatParser.FromExtension("file."), "trailing dot");
                TestSupport.AssertEqual<DocumentFormatEnum?>(null, DocumentFormatParser.FromExtension("archive.zip"), "unknown extension");
                TestSupport.AssertEqual<DocumentFormatEnum?>(null, DocumentFormatParser.FromExtension("x.auto"), "auto is never an extension");
                TestSupport.AssertEqual<DocumentFormatEnum?>(null, DocumentFormatParser.FromExtension(null), "null");
                TestSupport.AssertEqual<DocumentFormatEnum?>(null, DocumentFormatParser.FromExtension("folder.md/file"), "extension on a folder, not the file");
            });

            foreach (DocumentFormatEnum format in Enum.GetValues(typeof(DocumentFormatEnum)))
            {
                DocumentFormatEnum f = format;
                s.AddSync("MediaAndExtension_" + f, "Media type and default extension of " + f + " round trip", () =>
                {
                    string media = DocumentFormatParser.GetMediaType(f);
                    string ext = DocumentFormatParser.GetDefaultExtension(f);
                    TestSupport.Assert(media.Contains("/"), "media type looks like type/subtype: " + media);
                    TestSupport.Assert(ext.Length > 0, "extension not empty");
                    if (f == DocumentFormatEnum.Auto)
                    {
                        TestSupport.AssertEqual("application/octet-stream", media, "Auto media type");
                        return;
                    }

                    TestSupport.AssertEqual<DocumentFormatEnum?>(f, DocumentFormatParser.FromExtension("file." + ext), "extension maps back to the format");
                });
            }

            s.AddSync("Classification", "IsTextBased and IsImage classify every format", () =>
            {
                DocumentFormatEnum[] text = { DocumentFormatEnum.Text, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, DocumentFormatEnum.Json, DocumentFormatEnum.Xml, DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv, DocumentFormatEnum.Rtf };
                DocumentFormatEnum[] images = { DocumentFormatEnum.Png, DocumentFormatEnum.Jpeg, DocumentFormatEnum.Gif, DocumentFormatEnum.Bmp, DocumentFormatEnum.Tiff, DocumentFormatEnum.WebP };
                foreach (DocumentFormatEnum f in Enum.GetValues(typeof(DocumentFormatEnum)))
                {
                    TestSupport.AssertEqual(Array.IndexOf(text, f) >= 0, DocumentFormatParser.IsTextBased(f), "IsTextBased(" + f + ")");
                    TestSupport.AssertEqual(Array.IndexOf(images, f) >= 0, DocumentFormatParser.IsImage(f), "IsImage(" + f + ")");
                }
            });

            s.AddSync("AliasesListed", "Aliases are exposed for help text and include every enum name", () =>
            {
                foreach (DocumentFormatEnum f in Enum.GetValues(typeof(DocumentFormatEnum)))
                {
                    bool found = false;
                    foreach (string a in DocumentFormatParser.Aliases) if (a == f.ToString().ToLowerInvariant()) found = true;
                    TestSupport.Assert(found, "alias list contains " + f);
                }
            });

            return s.Build();
        }
    }
}
