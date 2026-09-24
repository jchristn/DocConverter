namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Model;
    using DocConverter.Results;
    using Test.Shared.Fixtures;
    using Test.Shared.Inspection;
    using Touchstone.Core;

    /// <summary>
    /// Hostile input: script injection, unsafe link schemes, XML external entities and entity expansion, zip bombs,
    /// remote resources that must never be fetched, and no file system writes during conversion.
    /// </summary>
    public static class SecuritySuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            SuiteBuilder s = new SuiteBuilder("Security", "Hostile input");
            Converter c = new Converter();

            s.Add("HtmlScriptStripped", "Scripts, event handlers, iframes and javascript links in HTML input never reach HTML output", async ct =>
            {
                string hostile = "<html><body><h1 onclick=\"alert(1)\">Title</h1><script>alert('x')</script>"
                    + "<p>Text <a href=\"javascript:alert(1)\">bad link</a> and <a href=\"  JaVa\tScRiPt:alert(2)\">obfuscated</a> "
                    + "<a href=\"vbscript:msgbox\">vb</a> <a href=\"data:text/html,<script>alert(3)</script>\">data</a> "
                    + "<a href=\"https://ok.example/\">good</a></p><iframe src=\"https://evil.example\"></iframe>"
                    + "<img src=\"x\" onerror=\"alert(4)\"><svg onload=\"alert(5)\"><script>alert(6)</script></svg></body></html>";
                StringConversionResult r = await c.ConvertToStringAsync(hostile, DocumentFormatEnum.Html, DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                HtmlInspector.Inspect(Encoding.UTF8.GetBytes(r.Output));
                TestSupport.AssertNotContains(r.Output, "alert", "no script payload survives");
                TestSupport.AssertContains(r.Output, "href=\"https://ok.example/\"", "safe link kept");
                TestSupport.AssertContains(r.Output, "bad link", "unsafe link text kept as text");
                TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.LinkRemovedUnsafe), "LinkRemovedUnsafe");
            });

            s.Add("MarkdownRawHtml", "Raw HTML in Markdown is not passed through as markup", async ct =>
            {
                string md = "Hello <script>alert(1)</script> <img src=x onerror=alert(2)>\n\n<div onclick=\"alert(3)\">block</div>\n\n[x](javascript:alert(4))";
                StringConversionResult r = await c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                HtmlInspector.Inspect(Encoding.UTF8.GetBytes(r.Output));
                TestSupport.AssertNotContains(r.Output, "<script", "no script element");
                TestSupport.AssertNotContains(r.Output, "onerror", "no handler");
                TestSupport.AssertNotContains(r.Output, "href=\"javascript", "no javascript href");
            });

            s.Add("UnsafeLinksEverywhere", "Unsafe link schemes are dropped by the Markdown, HTML and DOCX writers", async ct =>
            {
                DocumentModel m = new DocumentModel();
                ParagraphBlock p = new ParagraphBlock();
                p.Inlines.Add(new LinkInline("javascript:alert(1)", "click"));
                p.Inlines.Add(new TextInline(" "));
                p.Inlines.Add(new LinkInline("mailto:a@b.example", "mail"));
                m.Blocks.Add(p);
                foreach (DocumentFormatEnum f in new[] { DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, DocumentFormatEnum.Docx })
                {
                    BytesConversionResult r = await c.WriteToBytesAsync(m, f, null, ct).ConfigureAwait(false);
                    TestSupport.Assert(r.Warnings.Any(w => w.Code == WarningCodeEnum.LinkRemovedUnsafe), f + " warns");
                    ContentSnapshot snap = Matrix.OutputInspector.Inspect(f, r.Output);
                    TestSupport.Assert(!snap.LinkUrls.Any(u => u.StartsWith("javascript", StringComparison.OrdinalIgnoreCase)), f + " has no javascript link");
                    TestSupport.Assert(snap.LinkUrls.Contains("mailto:a@b.example"), f + " keeps mailto");
                    TestSupport.Assert(snap.ContainsText("click"), f + " keeps the link text");
                }
            });

            s.Add("XxeNotResolved", "XML external entities are refused; the target file is never read into the output", async ct =>
            {
                string secretPath = Path.Combine(Path.GetTempPath(), "docconverter-xxe-" + Guid.NewGuid().ToString("N") + ".txt");
                File.WriteAllText(secretPath, "TOP-SECRET-CONTENT");
                try
                {
                    string xxe = NegativeFixtures.XxeXml(secretPath);
                    await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => c.ReadAsync(xxe, DocumentFormatEnum.Xml, null, ct), "xxe").ConfigureAwait(false);
                    DetectionResult d = await c.DetectFormatAsync(xxe, null, ct).ConfigureAwait(false);
                    TestSupport.AssertEqual<DocumentFormatEnum?>(DocumentFormatEnum.Xml, d.Format, "still detected as XML without resolving");
                }
                finally
                {
                    File.Delete(secretPath);
                }
            });

            s.Add("BillionLaughs", "Entity expansion bombs are refused quickly", async ct =>
            {
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => c.ReadAsync(NegativeFixtures.BillionLaughsXml(), DocumentFormatEnum.Xml, null, ct), "billion laughs").ConfigureAwait(false);
                TestSupport.Assert(sw.ElapsedMilliseconds < 5000, "refused quickly");
            });

            s.Add("HtmlDoctypeHarmless", "An HTML document with a DOCTYPE declaring entities converts without resolving them", async ct =>
            {
                string html = "<!DOCTYPE html [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><html><body><p>&x; ok</p></body></html>";
                StringConversionResult r = await c.ConvertToStringAsync(html, DocumentFormatEnum.Html, DocumentFormatEnum.Text, null, ct).ConfigureAwait(false);
                TestSupport.AssertNotContains(r.Output, "root:", "no file content");
            });

            s.Add("ZipBombGuard", "Office parts that decompress past MaxDecompressedBytes are refused before parsing", async ct =>
            {
                byte[] docx = (await c.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Docx, null, ct).ConfigureAwait(false)).Output;
                Converter tight = new Converter(new ConverterSettings { MaxDecompressedBytes = 2048 });
                await TestSupport.ExpectThrowsAsync<InputTooLargeException>(() => tight.ReadAsync(docx, DocumentFormatEnum.Docx, null, ct), "docx over the limit").ConfigureAwait(false);
                byte[] bomb = NegativeFixtures.Zip("[Content_Types].xml", "<Types/>", "word/document.xml", new string('A', 5 * 1024 * 1024));
                await TestSupport.ExpectThrowsAsync<InputTooLargeException>(() => new Converter(new ConverterSettings { MaxDecompressedBytes = 1024 * 1024 }).ReadAsync(bomb, DocumentFormatEnum.Docx, null, ct), "highly compressible part").ConfigureAwait(false);
            });

            s.Add("RemoteResourcesNeverFetched", "Remote image URLs in Markdown and HTML are never requested (a local listener sees zero connections)", async ct =>
            {
                // The URL path carries a token unique to this run, and only requests for it count. On shared CI runners
                // (and under test runners that execute suites concurrently) unrelated loopback clients can open a
                // connection to any listening port; counting raw connections made this test flaky.
                string token = Guid.NewGuid().ToString("N");
                TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                int fetches = 0;
                StringBuilder requests = new StringBuilder();
                List<Task> handlers = new List<Task>();
                using (CancellationTokenSource stop = new CancellationTokenSource())
                {
                    Task accept = Task.Run(async () =>
                    {
                        try
                        {
                            while (!stop.IsCancellationRequested)
                            {
                                Task<TcpClient> pending = listener.AcceptTcpClientAsync();
                                Task done = await Task.WhenAny(pending, Task.Delay(Timeout.Infinite, stop.Token)).ConfigureAwait(false);
                                if (done != pending) break;
                                TcpClient client = pending.Result;
                                Task handler = Task.Run(async () =>
                                {
                                    using (client)
                                    {
                                        string request = await ReadRequestAsync(client).ConfigureAwait(false);
                                        if (request.IndexOf(token, StringComparison.Ordinal) >= 0)
                                        {
                                            Interlocked.Increment(ref fetches);
                                            lock (requests) requests.Append(request.Split('\n')[0].Trim()).Append("; ");
                                        }
                                    }
                                });
                                lock (handlers) handlers.Add(handler);
                            }
                        }
                        catch (Exception)
                        {
                            // Listener stopped.
                        }
                    });

                    string url = "http://127.0.0.1:" + port + "/" + token + "/image.png";
                    string md = "![remote](" + url + ")\n\n[link](" + url + ")";
                    string html = "<p><img src=\"" + url + "\"><link rel=\"stylesheet\" href=\"" + url + "\"></p>";
                    foreach (DocumentFormatEnum target in new[] { DocumentFormatEnum.Html, DocumentFormatEnum.Docx, DocumentFormatEnum.Pdf, DocumentFormatEnum.Pptx })
                    {
                        await c.ConvertToBytesAsync(md, DocumentFormatEnum.Markdown, target, null, ct).ConfigureAwait(false);
                        await c.ConvertToBytesAsync(html, DocumentFormatEnum.Html, target, null, ct).ConfigureAwait(false);
                    }

                    await Task.Delay(200, ct).ConfigureAwait(false);
                    stop.Cancel();
                    listener.Stop();
                    await accept.ConfigureAwait(false);
                    Task[] all;
                    lock (handlers) all = handlers.ToArray();
                    await Task.WhenAll(all).ConfigureAwait(false);
                }

                TestSupport.AssertEqual(0, fetches, "requests for the remote URL (" + requests + ")");
            });

            s.Add("NoFileSystemWrites", "Conversions write nothing to the temp directory or the working directory", async ct =>
            {
                string temp = Path.GetTempPath();
                string cwd = Directory.GetCurrentDirectory();
                string[] tempBefore = SafeList(temp);
                string[] cwdBefore = SafeList(cwd);
                foreach (DocumentFormatEnum target in new[] { DocumentFormatEnum.Docx, DocumentFormatEnum.Xlsx, DocumentFormatEnum.Pptx, DocumentFormatEnum.Pdf, DocumentFormatEnum.Html })
                {
                    BytesConversionResult r = await c.WriteToBytesAsync(ReferenceContent.ToModel(), target, null, ct).ConfigureAwait(false);
                    await c.ReadAsync(r.Output, target == DocumentFormatEnum.Html ? DocumentFormatEnum.Html : target, null, ct).ConfigureAwait(false);
                }

                string[] tempNew = SafeList(temp).Except(tempBefore).Where(p => p.IndexOf("docconverter", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("docconv", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                string[] cwdNew = SafeList(cwd).Except(cwdBefore).ToArray();
                TestSupport.AssertEqual(0, cwdNew.Length, "new files in the working directory: " + string.Join(", ", cwdNew));
                TestSupport.AssertEqual(0, tempNew.Length, "new DocConverter files in temp: " + string.Join(", ", tempNew));
            });

            s.Add("RandomBytesEveryReader", "Random bytes given to every reader fail with DocumentReadException, never a crash or hang", async ct =>
            {
                byte[] junk = NegativeFixtures.Random(64 * 1024);
                foreach (DocumentFormatEnum f in new[] { DocumentFormatEnum.Docx, DocumentFormatEnum.Xlsx, DocumentFormatEnum.Pptx, DocumentFormatEnum.Pdf, DocumentFormatEnum.Png, DocumentFormatEnum.Jpeg, DocumentFormatEnum.Gif, DocumentFormatEnum.Bmp, DocumentFormatEnum.Tiff, DocumentFormatEnum.WebP, DocumentFormatEnum.Json, DocumentFormatEnum.Xml, DocumentFormatEnum.Rtf })
                    await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => c.ReadAsync(junk, f, null, ct), f + " with random bytes").ConfigureAwait(false);

                foreach (DocumentFormatEnum f in new[] { DocumentFormatEnum.Text, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv })
                    await c.ReadAsync(junk, f, null, ct).ConfigureAwait(false);
            });

            return s.Build();
        }

        private static async Task<string> ReadRequestAsync(TcpClient client)
        {
            try
            {
                using (NetworkStream stream = client.GetStream())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                {
                    byte[] buffer = new byte[4096];
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false);
                    return read > 0 ? Encoding.ASCII.GetString(buffer, 0, read) : "";
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static string[] SafeList(string directory)
        {
            try
            {
                return Directory.GetFileSystemEntries(directory);
            }
            catch (Exception)
            {
                return new string[0];
            }
        }
    }
}
