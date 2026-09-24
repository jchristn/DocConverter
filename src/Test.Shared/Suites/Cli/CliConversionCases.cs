#if !DOCCONVERTER_NETSTANDARD_TEST
namespace Test.Shared.Suites.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Cli.Reporting;
    using Touchstone.Core;

    /// <summary>
    /// Conversions: format inference, pipes, binary integrity, and every failure exit code.
    /// </summary>
    public static class CliConversionCases
    {
        /// <summary>
        /// Build the cases.
        /// </summary>
        /// <returns>Cases.</returns>
        public static List<TestCaseDescriptor> Cases()
        {
            return new List<TestCaseDescriptor>
            {
                Case("FormatAliases", "--from accepts md, markdown, .md and MD", async () =>
                {
                    foreach (string alias in new string[] { "md", "markdown", ".md", "MD", "Markdown" })
                    {
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", alias, "--to", "htm" }, CliSamples.Utf8(CliSamples.Markdown)).ConfigureAwait(false);
                        TestSupport.AssertEqual(0, r.ExitCode, "exit code for " + alias + ": " + r);
                        TestSupport.AssertContains(r.StdoutText, "<h1>Title</h1>", "html for " + alias);
                    }
                }),
                Case("UnknownFormatName", "An unknown --from or --to exits 2", async () =>
                {
                    CliRunResult a = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", "nope", "--to", "html" }, CliSamples.Utf8("x")).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, a.ExitCode, "bad --from");
                    CliRunResult b = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", "md", "--to", "nope" }, CliSamples.Utf8("x")).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, b.ExitCode, "bad --to");
                    CliRunResult c = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", "md", "--to", "auto" }, CliSamples.Utf8("x")).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, c.ExitCode, "--to auto");
                }),
                Case("InferFromExtensions", "Without --from and --to both formats come from the file names", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string input = dir.WriteText("notes.md", CliSamples.Markdown);
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", input, "-o", dir.File("notes.html"), "--json" }).ConfigureAwait(false);
                        TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                        CliReport report = CliSamples.LastLineReport(r.StdoutText);
                        TestSupport.AssertEqual("Markdown", report.Input?.Format, "source");
                        TestSupport.AssertEqual(true, report.Input?.Detected, "detected");
                        TestSupport.AssertEqual("Html", report.Output?.Format, "target");
                        TestSupport.AssertContains(dir.ReadText("notes.html"), "<h1>Title</h1>", "html written");
                    }
                }),
                Case("InferFromContentOnStdin", "Stdin input is detected from content", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", dir.File("out.txt"), "--json" }, CliSamples.Utf8(CliSamples.HtmlUnderline)).ConfigureAwait(false);
                        TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                        CliReport report = CliSamples.LastLineReport(r.StdoutText);
                        TestSupport.AssertEqual("Html", report.Input?.Format, "detected html");
                        TestSupport.AssertEqual("stdin", report.Input?.Path, "stdin name");
                        TestSupport.AssertEqual("Text", report.Output?.Format, "txt target");
                        TestSupport.AssertContains(dir.ReadText("out.txt"), "underlined", "text written");
                    }
                }),
                Case("ExplicitAuto", "--from auto detects", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", "auto", "--to", "html" }, CliSamples.Utf8(CliSamples.Markdown)).ConfigureAwait(false);
                    TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                    TestSupport.AssertContains(r.StdoutText, "<h1>Title</h1>", "markdown detected");
                }),
                Case("StdoutNeedsTo", "-o - without --to exits 2", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-" }, CliSamples.Utf8(CliSamples.Markdown)).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                    TestSupport.AssertContains(r.Stderr, "--to is required", "message");
                    TestSupport.AssertEqual(0, r.Stdout.Length, "no output");
                }),
                Case("UninferableTarget", "An output extension that is not a format exits 2", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string input = dir.WriteText("in.md", CliSamples.Markdown);
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", input, "-o", dir.File("out.zzz") }).ConfigureAwait(false);
                        TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                        TestSupport.AssertEqual(1, dir.FileCount, "no output created");
                    }
                }),
                Case("StdinToStdout", "Markdown on stdin becomes HTML on stdout, and stdout holds only the document", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", "md", "--to", "html" }, CliSamples.Utf8(CliSamples.Markdown)).ConfigureAwait(false);
                    TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                    TestSupport.Assert(r.StdoutText.StartsWith("<!DOCTYPE html>", StringComparison.Ordinal), "stdout starts with the document");
                    TestSupport.AssertNotContains(r.StdoutText, "docconv:", "no summary on stdout");
                    TestSupport.AssertContains(r.Stderr, "docconv: stdin (Markdown) -> stdout (Html)", "summary on stderr");
                }),
                Case("BinaryPipeByteExact", "Every byte value passes stdin to stdout unchanged", async () =>
                {
                    byte[] payload = CliSamples.BinaryPayload();
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", "png", "--to", "pdf", "-q" }, payload, CliSamples.EchoFactory()).ConfigureAwait(false);
                    TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                    TestSupport.AssertEqual(payload.Length, r.Stdout.Length, "length");
                    for (int i = 0; i < payload.Length; i++)
                        if (payload[i] != r.Stdout[i]) throw new TestAssertionException("byte " + i + " differs");
                }),
                Case("BinaryFileByteExact", "Every byte value passes file to file unchanged, with formats inferred", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        byte[] payload = CliSamples.BinaryPayload();
                        string input = dir.WriteBytes("in.png", payload);
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", input, "-o", dir.File("out.pdf") }, null, CliSamples.EchoFactory()).ConfigureAwait(false);
                        TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                        byte[] written = File.ReadAllBytes(dir.File("out.pdf"));
                        TestSupport.AssertEqual(Convert.ToBase64String(payload), Convert.ToBase64String(written), "bytes");
                    }
                }),
                Case("UnrecognizedInput", "Random binary input exits 3 with UnsupportedFormat", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", dir.File("out.md"), "--json" }, CliSamples.RandomBinary()).ConfigureAwait(false);
                        TestSupport.AssertEqual(3, r.ExitCode, "exit code: " + r);
                        CliReport report = CliSamples.LastLineReport(r.StdoutText);
                        TestSupport.AssertEqual("UnsupportedFormat", report.Error?.Code, "error code");
                        TestSupport.AssertEqual(0, dir.FileCount, "no output created");
                    }
                }),
                Case("LegacyDoc", "A legacy .doc exits 3 and says what it is", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--to", "md" }, CliSamples.LegacyDoc()).ConfigureAwait(false);
                    TestSupport.AssertEqual(3, r.ExitCode, "exit code");
                    TestSupport.AssertContains(r.Stderr, ".doc", "names the legacy format");
                }),
                Case("PairWithoutWriter", "A target with no writer (rtf) exits 3 with ConversionNotSupported", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", "md", "--to", "rtf", "--json" }, CliSamples.Utf8(CliSamples.Markdown)).ConfigureAwait(false);
                    TestSupport.AssertEqual(3, r.ExitCode, "exit code");
                    CliReport report = CliSamples.LastLineReport(r.Stderr);
                    TestSupport.AssertEqual("ConversionNotSupported", report.Error?.Code, "error code");
                }),
                Case("InputMissing", "A missing input file exits 4", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", dir.File("nope.md"), "-o", dir.File("out.html"), "--json" }).ConfigureAwait(false);
                        TestSupport.AssertEqual(4, r.ExitCode, "exit code");
                        CliReport report = CliSamples.LastLineReport(r.StdoutText);
                        TestSupport.AssertEqual("NotFound", report.Error?.Code, "error code");
                        TestSupport.AssertEqual(false, report.Success, "success");
                        TestSupport.AssertEqual(4, report.ExitCode, "report exit code");
                    }
                }),
                Case("OutputExists", "An existing output without --overwrite exits 4 and is untouched", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string input = dir.WriteText("in.md", CliSamples.Markdown);
                        string output = dir.WriteText("out.html", "original");
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", input, "-o", output }).ConfigureAwait(false);
                        TestSupport.AssertEqual(4, r.ExitCode, "exit code");
                        TestSupport.AssertEqual("original", dir.ReadText("out.html"), "untouched");
                        TestSupport.AssertContains(r.Stderr, "--overwrite", "suggests --overwrite");
                        TestSupport.AssertEqual(2, dir.FileCount, "no temp file left");
                    }
                }),
                Case("Overwrite", "--overwrite replaces an existing output", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string input = dir.WriteText("in.md", CliSamples.Markdown);
                        string output = dir.WriteText("out.html", "original");
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", input, "-o", output, "--overwrite" }).ConfigureAwait(false);
                        TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                        TestSupport.AssertContains(dir.ReadText("out.html"), "<h1>Title</h1>", "replaced");
                        TestSupport.AssertEqual(2, dir.FileCount, "no temp file left");
                    }
                }),
                Case("OutputDirectoryMissing", "An output in a missing directory exits 4", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string input = dir.WriteText("in.md", CliSamples.Markdown);
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", input, "-o", Path.Combine(dir.Path, "missing", "out.html") }).ConfigureAwait(false);
                        TestSupport.AssertEqual(4, r.ExitCode, "exit code");
                    }
                }),
                Case("StrictWithWarnings", "--strict with warnings exits 5 and leaves no output file", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string input = dir.WriteText("in.md", CliSamples.MarkdownWithTable);
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", input, "-o", dir.File("out.csv"), "--strict", "--json" }).ConfigureAwait(false);
                        TestSupport.AssertEqual(5, r.ExitCode, "exit code: " + r);
                        TestSupport.Assert(!File.Exists(dir.File("out.csv")), "output removed");
                        TestSupport.AssertEqual(1, dir.FileCount, "no temp file left");
                        CliReport report = CliSamples.LastLineReport(r.StdoutText);
                        TestSupport.AssertEqual("Warnings", report.Error?.Code, "error code");
                        TestSupport.Assert(report.Warnings.Exists(w => w.Code == "NonTableContentDropped"), "warning listed");
                        TestSupport.AssertEqual(0L, report.Output?.Bytes, "no output bytes kept");
                        TestSupport.AssertContains(r.Stderr, "warning NonTableContentDropped", "warning on stderr");
                    }
                }),
                Case("StrictHtmlUnderline", "--strict turns FormattingLost (underline to Markdown) into exit 5", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", "html", "--to", "md", "--strict" }, CliSamples.Utf8(CliSamples.HtmlUnderline)).ConfigureAwait(false);
                    TestSupport.AssertEqual(5, r.ExitCode, "exit code: " + r);
                    TestSupport.AssertContains(r.Stderr, "FormattingLost", "names the warning");
                }),
                Case("StrictWithoutWarnings", "--strict without warnings succeeds", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", "md", "--to", "html", "--strict" }, CliSamples.Utf8(CliSamples.Markdown)).ConfigureAwait(false);
                    TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                }),
                Case("MaxInputFile", "A file larger than --max-input-mb exits 6", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string big = new string('a', 2 * 1048576);
                        string input = dir.WriteText("big.txt", big);
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", input, "-o", dir.File("out.md"), "--max-input-mb", "1", "--json" }).ConfigureAwait(false);
                        TestSupport.AssertEqual(6, r.ExitCode, "exit code: " + r);
                        TestSupport.AssertEqual("InputTooLarge", CliSamples.LastLineReport(r.StdoutText).Error?.Code, "error code");
                    }
                }),
                Case("MaxInputStdin", "Stdin larger than --max-input-mb exits 6", async () =>
                {
                    byte[] big = CliSamples.Utf8(new string('b', 1048576 + 10));
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--to", "md", "--max-input-mb", "1" }, big).ConfigureAwait(false);
                    TestSupport.AssertEqual(6, r.ExitCode, "exit code: " + r);
                }),
                Case("MaxInputInvalid", "--max-input-mb 0 exits 2", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--to", "md", "--max-input-mb", "0" }, CliSamples.Utf8("x")).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                }),
                Case("Cancelled", "A cancelled token exits 130", async () =>
                {
                    using (CancellationTokenSource cts = new CancellationTokenSource())
                    {
                        cts.Cancel();
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", "md", "--to", "html", "--json" }, CliSamples.Utf8(CliSamples.Markdown), null, cts.Token).ConfigureAwait(false);
                        TestSupport.AssertEqual(130, r.ExitCode, "exit code: " + r);
                        TestSupport.AssertEqual("Cancelled", CliSamples.LastLineReport(r.Stderr).Error?.Code, "error code");
                    }
                }),
                Case("WriterFailure", "A failing writer exits 1 with DocumentWrite and leaves no partial file", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string input = dir.WriteBytes("in.png", CliSamples.BinaryPayload());
                        Func<ConverterSettings, Converter> factory = settings =>
                        {
                            Converter converter = new Converter(settings);
                            converter.RegisterReader(new EchoImageReader());
                            converter.RegisterWriter(new FailingWriter());
                            return converter;
                        };
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", input, "-o", dir.File("out.pdf"), "--json" }, null, factory).ConfigureAwait(false);
                        TestSupport.AssertEqual(1, r.ExitCode, "exit code: " + r);
                        TestSupport.AssertEqual("DocumentWrite", CliSamples.LastLineReport(r.StdoutText).Error?.Code, "error code");
                        TestSupport.AssertEqual(1, dir.FileCount, "no partial output or temp file");
                    }
                }),
                Case("ReaderFailure", "A failing reader exits 1 with DocumentRead", async () =>
                {
                    Func<ConverterSettings, Converter> factory = settings =>
                    {
                        Converter converter = new Converter(settings);
                        converter.RegisterReader(new FailingReader());
                        return converter;
                    };
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", "png", "--to", "md", "--json" }, CliSamples.BinaryPayload(), factory).ConfigureAwait(false);
                    TestSupport.AssertEqual(1, r.ExitCode, "exit code: " + r);
                    TestSupport.AssertEqual("DocumentRead", CliSamples.LastLineReport(r.Stderr).Error?.Code, "error code");
                }),
                Case("EmptyStdin", "Empty Markdown on stdin produces empty output", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", "md", "--to", "txt" }, new byte[0]).ConfigureAwait(false);
                    TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                    TestSupport.AssertEqual(0, r.Stdout.Length, "empty output");
                })
            };
        }

        private static TestCaseDescriptor Case(string id, string name, Func<Task> body)
        {
            return new TestCaseDescriptor("Cli", id, name, executeAsync: async ct => await body().ConfigureAwait(false));
        }
    }
}
#endif
