namespace Test.Shared.Suites
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Results;
    using Test.Shared.Fixtures;
    using Touchstone.Core;

    /// <summary>
    /// Format detection from bytes, streams and strings: signatures, zip and OLE structure, text heuristics and hints.
    /// Fixtures for binary formats that other suites build are covered by the conversion matrix; this suite uses
    /// signatures and structures it can create itself.
    /// </summary>
    public static class DetectionSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            SuiteBuilder s = new SuiteBuilder("Detection", "Format detection");
            Converter converter = new Converter();

            foreach (DocumentFormatEnum f in new[] { DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv, DocumentFormatEnum.Json, DocumentFormatEnum.Xml, DocumentFormatEnum.Text })
            {
                DocumentFormatEnum format = f;
                s.Add("TextFixture_" + format, "The " + format + " reference fixture is detected from content alone", async ct =>
                {
                    DetectionResult r = await converter.DetectFormatAsync(TextFixtures.Reference(format), null, ct).ConfigureAwait(false);
                    TestSupport.AssertEqual<DocumentFormatEnum?>(format, r.Format, "detected format (" + r + ")");
                    TestSupport.Assert(r.IsSupported, "supported");
                });
            }

            s.Add("CanonicalJson", "Canonical JSON is detected as Json", async ct =>
            {
                DetectionResult r = await converter.DetectFormatAsync(TextFixtures.CanonicalJson(), null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Json, r.Format, "format");
                TestSupport.AssertEqual(DetectionConfidenceEnum.Structure, r.Confidence, "confidence");
            });

            s.Add("CanonicalXml", "Canonical XML is detected as Xml", async ct =>
            {
                DetectionResult r = await converter.DetectFormatAsync(TextFixtures.CanonicalXml(), null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Xml, r.Format, "format");
            });

            foreach (string name in new[] { "sample.png", "sample-alpha.png", "sample-palette.png", "sample.jpg", "sample-progressive.jpg", "sample-cmyk.jpg", "sample.gif", "sample.bmp", "sample.tiff", "sample.webp", "sample-lossless.webp" })
            {
                string file = name;
                s.Add("Image_" + file, file + " is detected by signature", async ct =>
                {
                    DetectionResult r = await converter.DetectFormatAsync(TestImages.Sample(file), null, ct).ConfigureAwait(false);
                    TestSupport.Assert(r.Format.HasValue, "supported: " + r);
                    TestSupport.AssertEqual(DetectionConfidenceEnum.Signature, r.Confidence, "confidence");
                    string ext = Path.GetExtension(file).TrimStart('.');
                    TestSupport.AssertEqual<DocumentFormatEnum?>(DocConverter.Detection.DocumentFormatParser.FromExtension(file), r.Format, "format for ." + ext);
                });
            }

            s.Add("Pdf", "%PDF- signature is detected, including after junk bytes within 1 KB", async ct =>
            {
                DetectionResult a = await converter.DetectFormatAsync(Encoding.ASCII.GetBytes("%PDF-1.7\n%...\n"), null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Pdf, a.Format, "plain header");
                byte[] junk = new byte[200];
                byte[] pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n");
                byte[] combined = new byte[junk.Length + pdf.Length];
                Buffer.BlockCopy(pdf, 0, combined, junk.Length, pdf.Length);
                DetectionResult b = await converter.DetectFormatAsync(combined, null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Pdf, b.Format, "header after junk");
            });

            s.Add("Rtf", "{\\rtf signature is detected", async ct =>
            {
                DetectionResult r = await converter.DetectFormatAsync(Encoding.ASCII.GetBytes("{\\rtf1\\ansi Hello}"), null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Rtf, r.Format, "format");
            });

            s.Add("OoxmlByContentType", "DOCX, XLSX and PPTX are told apart by [Content_Types].xml, not folder names", async ct =>
            {
                string[][] cases = new string[][]
                {
                    new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml", "Docx" },
                    new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml", "Xlsx" },
                    new[] { "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml", "Pptx" },
                    new[] { "application/vnd.ms-word.document.macroEnabled.main+xml", "Docx" }
                };

                foreach (string[] c in cases)
                {
                    byte[] zip = Zip("[Content_Types].xml", "<Types><Override PartName=\"/x.xml\" ContentType=\"" + c[0] + "\"/></Types>", "x.xml", "<x/>");
                    DetectionResult r = await converter.DetectFormatAsync(zip, null, ct).ConfigureAwait(false);
                    TestSupport.AssertEqual(c[1], r.Format?.ToString(), "format for content type " + c[0]);
                    TestSupport.AssertEqual(DetectionConfidenceEnum.Structure, r.Confidence, "confidence");
                }
            });

            s.Add("ZipUnsupported", "EPUB, ODF, iWork, plain zip and damaged zip are recognized but unsupported", async ct =>
            {
                await Expect(converter, Zip("mimetype", "application/epub+zip", "content.opf", "x"), "EPUB", ct).ConfigureAwait(false);
                await Expect(converter, Zip("mimetype", "application/vnd.oasis.opendocument.text", "content.xml", "x"), ".odt", ct).ConfigureAwait(false);
                await Expect(converter, Zip("mimetype", "application/vnd.oasis.opendocument.spreadsheet", "content.xml", "x"), ".ods", ct).ConfigureAwait(false);
                await Expect(converter, Zip("mimetype", "application/vnd.oasis.opendocument.presentation", "content.xml", "x"), ".odp", ct).ConfigureAwait(false);
                await Expect(converter, Zip("Index/Document.iwa", "x"), "iWork", ct).ConfigureAwait(false);
                await Expect(converter, Zip("readme.txt", "hello"), "zip archive", ct).ConfigureAwait(false);
                byte[] damaged = Zip("a.txt", "hello world hello world");
                Array.Resize(ref damaged, damaged.Length - 30);
                await Expect(converter, damaged, "damaged", ct).ConfigureAwait(false);
            });

            s.Add("OleLegacy", "Legacy .doc, .xls, .ppt, password protected OOXML and .msg are described precisely", async ct =>
            {
                await Expect(converter, Fixtures.NegativeFixtures.Ole("WordDocument"), ".doc", ct).ConfigureAwait(false);
                await Expect(converter, Fixtures.NegativeFixtures.Ole("Workbook"), ".xls", ct).ConfigureAwait(false);
                await Expect(converter, Fixtures.NegativeFixtures.Ole("PowerPoint Document"), ".ppt", ct).ConfigureAwait(false);
                await Expect(converter, Fixtures.NegativeFixtures.Ole("EncryptedPackage"), "password protected", ct).ConfigureAwait(false);
                await Expect(converter, Fixtures.NegativeFixtures.Ole("__substg1.0_0037001F"), ".msg", ct).ConfigureAwait(false);
            });

            s.Add("OtherBinaries", "Archives, media, databases and executables are unsupported with a description", async ct =>
            {
                await Expect(converter, new byte[] { 0x1F, 0x8B, 8, 0, 0, 0, 0, 0, 0, 3 }, "gzip", ct).ConfigureAwait(false);
                await Expect(converter, new byte[] { (byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C, 0, 4 }, "7-Zip", ct).ConfigureAwait(false);
                await Expect(converter, Encoding.ASCII.GetBytes("Rar!\u001a\u0007\u0000xxxx"), "RAR", ct).ConfigureAwait(false);
                await Expect(converter, Encoding.ASCII.GetBytes("SQLite format 3\u0000xxxxxxxx"), "SQLite", ct).ConfigureAwait(false);
                await Expect(converter, Encoding.ASCII.GetBytes("MZ\u0090\u0000\u0003xxxxxxxxxxxxxxxx"), "executable", ct).ConfigureAwait(false);
                await Expect(converter, new byte[] { 0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'m', (byte)'p', (byte)'4', (byte)'2' }, "MP4", ct).ConfigureAwait(false);
                await Expect(converter, new byte[] { 0, 0, 1, 0, 1, 0, 16, 16, 0, 0 }, "ICO", ct).ConfigureAwait(false);
                byte[] random = new byte[4096];
                new Random(42).NextBytes(random);
                random[0] = 0;
                await Expect(converter, random, "binary", ct).ConfigureAwait(false);
            });

            s.Add("ExtensionHintBreaksTies", "A file name hint decides between ambiguous text formats but never overrides a binary signature", async ct =>
            {
                byte[] plain = Encoding.UTF8.GetBytes("just some words\nand more words\n");
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Text, (await converter.DetectFormatAsync(plain, null, ct).ConfigureAwait(false)).Format, "no hint");
                DetectionResult md = await converter.DetectFormatAsync(plain, "notes.md", ct).ConfigureAwait(false);
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Markdown, md.Format, "md hint");
                TestSupport.AssertEqual(DetectionConfidenceEnum.ExtensionHint, md.Confidence, "hint confidence");
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Csv, (await converter.DetectFormatAsync(plain, "data.csv", ct).ConfigureAwait(false)).Format, "csv hint");
                byte[] png = TestImages.Sample("sample.png");
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Png, (await converter.DetectFormatAsync(png, "picture.md", ct).ConfigureAwait(false)).Format, "signature beats hint");
                byte[] json = Encoding.UTF8.GetBytes("{\"a\": 1}");
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Json, (await converter.DetectFormatAsync(json, "x.txt", ct).ConfigureAwait(false)).Format, "structure beats hint");
            });

            s.Add("XmlVersusHtml", "XHTML is Html; other XML is Xml; an .xml hint keeps XHTML as Xml", async ct =>
            {
                byte[] xhtml = Encoding.UTF8.GetBytes("<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><p>x</p></body></html>");
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Html, (await converter.DetectFormatAsync(xhtml, null, ct).ConfigureAwait(false)).Format, "xhtml");
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Xml, (await converter.DetectFormatAsync(xhtml, "page.xml", ct).ConfigureAwait(false)).Format, "xml hint");
                byte[] svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect/></svg>");
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Xml, (await converter.DetectFormatAsync(svg, null, ct).ConfigureAwait(false)).Format, "svg is xml");
                byte[] fragment = Encoding.UTF8.GetBytes("<p>Hello <b>there</b></p><p>Again</p>");
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Html, (await converter.DetectFormatAsync(fragment, null, ct).ConfigureAwait(false)).Format, "html fragment");
            });

            s.Add("MarkdownHeuristics", "Markdown signals are scored; prose without them is Text", async ct =>
            {
                string[] markdown = new[]
                {
                    "# Title\n\nSome text with a [link](https://x.y).",
                    "Intro\n\n```\ncode\n```\n",
                    "| a | b |\n|---|---|\n| 1 | 2 |\n",
                    "Title\n=====\n\nBody **bold** text\n"
                };
                foreach (string m in markdown)
                    TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Markdown, (await converter.DetectFormatAsync(Encoding.UTF8.GetBytes(m), null, ct).ConfigureAwait(false)).Format, "markdown: " + m.Replace("\n", "\\n"));
                string prose = "Dear team,\nthe meeting is at 3 pm. Bring notes - and coffee.\nThanks\n";
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Text, (await converter.DetectFormatAsync(Encoding.UTF8.GetBytes(prose), null, ct).ConfigureAwait(false)).Format, "prose");
            });

            s.Add("DelimitedHeuristics", "Consistent commas mean CSV, consistent tabs mean TSV, inconsistent means Text", async ct =>
            {
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Csv, (await converter.DetectFormatAsync(Encoding.UTF8.GetBytes("a,b,c\n1,2,3\n4,\"5,5\",6\n"), null, ct).ConfigureAwait(false)).Format, "csv with quoted comma");
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Tsv, (await converter.DetectFormatAsync(Encoding.UTF8.GetBytes("a\tb\n1\t2\n"), null, ct).ConfigureAwait(false)).Format, "tsv");
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Text, (await converter.DetectFormatAsync(Encoding.UTF8.GetBytes("Hello, world.\nThis line, has, three commas, really.\n"), null, ct).ConfigureAwait(false)).Format, "prose with commas");
            });

            s.Add("EncodedText", "UTF-16 LE, UTF-16 BE and UTF-8 BOM text is decoded before classification", async ct =>
            {
                string md = "# Heading\n\n- item one\n- item two\n\n[x](https://x.y)\n";
                foreach (Encoding e in new Encoding[] { new UnicodeEncoding(false, true), new UnicodeEncoding(true, true), new UTF8Encoding(true), new UTF32Encoding(false, true) })
                {
                    byte[] bytes = Combine(e.GetPreamble(), e.GetBytes(md));
                    DetectionResult r = await converter.DetectFormatAsync(bytes, null, ct).ConfigureAwait(false);
                    TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Markdown, r.Format, "markdown in " + e.WebName);
                }
            });

            s.Add("Empty", "Empty input is Text, or the hinted text format", async ct =>
            {
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Text, (await converter.DetectFormatAsync(new byte[0], null, ct).ConfigureAwait(false)).Format, "empty");
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Html, (await converter.DetectFormatAsync(new byte[0], "x.html", ct).ConfigureAwait(false)).Format, "empty html");
            });

            s.Add("StreamPositionPreserved", "Detecting from a seekable stream restores its position", async ct =>
            {
                byte[] bytes = TextFixtures.Reference(DocumentFormatEnum.Html);
                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(new byte[] { 1, 2, 3 }, 0, 3);
                    ms.Write(bytes, 0, bytes.Length);
                    ms.Position = 3;
                    DetectionResult r = await converter.DetectFormatAsync(ms, null, ct).ConfigureAwait(false);
                    TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Html, r.Format, "format from offset");
                    TestSupport.AssertEqual(3L, ms.Position, "position restored");
                }
            });

            s.Add("NonSeekableStream", "Detecting from a non-seekable stream works", async ct =>
            {
                using (NonSeekableStream ns = new NonSeekableStream(TextFixtures.Reference(DocumentFormatEnum.Csv)))
                {
                    DetectionResult r = await converter.DetectFormatAsync(ns, null, ct).ConfigureAwait(false);
                    TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Csv, r.Format, "format");
                }
            });

            s.Add("StringInput", "String input is classified as text; base64 of a binary document is recognized as that document", async ct =>
            {
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Html, (await converter.DetectFormatAsync("<!DOCTYPE html><html><body><p>x</p></body></html>", null, ct).ConfigureAwait(false)).Format, "html string");
                string b64 = Convert.ToBase64String(TestImages.Sample("sample.gif"));
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Gif, (await converter.DetectFormatAsync(b64, null, ct).ConfigureAwait(false)).Format, "base64 gif");
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Text, (await converter.DetectFormatAsync("SGVsbG9Xb3JsZDEyMzQ=", null, ct).ConfigureAwait(false)).Format, "base64 of text stays text");
            });

            s.Add("AutoConversionOfUnsupported", "Converting an unsupported input with Auto throws UnsupportedFormatException naming what it is", async ct =>
            {
                UnsupportedFormatException ex = await TestSupport.ExpectThrowsAsync<UnsupportedFormatException>(
                    () => converter.ConvertToStringAsync(Fixtures.NegativeFixtures.Ole("WordDocument"), DocumentFormatEnum.Auto, DocumentFormatEnum.Markdown, null, ct),
                    "legacy doc").ConfigureAwait(false);
                TestSupport.AssertContains(ex.Message, ".doc", "message names the legacy format");
            });

            s.Add("DetectedSourceReported", "A conversion from Auto reports the detection result", async ct =>
            {
                StringConversionResult r = await converter.ConvertToStringAsync(TextFixtures.Reference(DocumentFormatEnum.Markdown), DocumentFormatEnum.Auto, DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual(DocumentFormatEnum.Markdown, r.SourceFormat, "resolved source");
                TestSupport.Assert(r.DetectedSource != null, "detection reported");
                TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Markdown, r.DetectedSource!.Format, "detected");
            });

            return s.Build();
        }

        private static async Task Expect(Converter converter, byte[] data, string fragment, System.Threading.CancellationToken ct)
        {
            DetectionResult r = await converter.DetectFormatAsync(data, null, ct).ConfigureAwait(false);
            TestSupport.Assert(!r.IsSupported, "expected unsupported, got " + r);
            TestSupport.AssertContains(r.RecognizedAs, fragment, "description");
        }

        private static byte[] Zip(params string[] nameContentPairs)
        {
            return Fixtures.NegativeFixtures.Zip(nameContentPairs);
        }

        private static byte[] Combine(byte[] a, byte[] b)
        {
            byte[] result = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, result, 0, a.Length);
            Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
            return result;
        }
    }
}
