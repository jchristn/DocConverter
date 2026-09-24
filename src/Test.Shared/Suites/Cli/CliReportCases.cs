#if !DOCCONVERTER_NETSTANDARD_TEST
namespace Test.Shared.Suites.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using DocConverter.Cli.Reporting;
    using Touchstone.Core;

    /// <summary>
    /// JSON reports, stream routing, summaries, and the detect and formats commands.
    /// </summary>
    public static class CliReportCases
    {
        /// <summary>
        /// Build the cases.
        /// </summary>
        /// <returns>Cases.</returns>
        public static List<TestCaseDescriptor> Cases()
        {
            return new List<TestCaseDescriptor>
            {
                Case("JsonReportSchema", "--json writes a complete one line report on stdout", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string input = dir.WriteText("in.md", CliSamples.Markdown);
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", input, "-o", dir.File("out.html"), "--json" }).ConfigureAwait(false);
                        TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                        string text = r.StdoutText;
                        TestSupport.Assert(text.EndsWith("\n", StringComparison.Ordinal) && text.TrimEnd('\n').IndexOf('\n') < 0, "exactly one line");
                        TestSupport.AssertContains(text, "\"error\":null", "error written as null");
                        TestSupport.AssertContains(text, "\"contractVersion\":1", "camelCase contract version");

                        CliReport report = CliSamples.LastLineReport(text);
                        TestSupport.AssertEqual(1, report.ContractVersion, "contract version");
                        TestSupport.AssertEqual(true, report.Success, "success");
                        TestSupport.AssertEqual("convert", report.Command, "command");
                        TestSupport.AssertEqual(input, report.Input?.Path, "input path");
                        TestSupport.AssertEqual("Markdown", report.Input?.Format, "input format");
                        TestSupport.AssertEqual(new FileInfo(input).Length, report.Input?.Bytes ?? -1, "input bytes");
                        TestSupport.AssertEqual(true, report.Input?.Detected, "detected");
                        TestSupport.AssertEqual(dir.File("out.html"), report.Output?.Path, "output path");
                        TestSupport.AssertEqual("Html", report.Output?.Format, "output format");
                        TestSupport.AssertEqual(new FileInfo(dir.File("out.html")).Length, report.Output?.Bytes ?? -1, "output bytes match file");
                        TestSupport.Assert(report.DurationMs >= 0, "duration");
                        TestSupport.AssertEqual(0, report.Warnings.Count, "no warnings");
                        TestSupport.Assert(report.Statistics != null, "statistics present");
                        TestSupport.AssertEqual(1, report.Statistics!.Headings, "headings");
                        TestSupport.AssertEqual(1, report.Statistics.Lists, "lists");
                        TestSupport.AssertEqual(1, report.Statistics.Links, "links");
                        TestSupport.Assert(report.Statistics.Paragraphs >= 1, "paragraphs");
                        TestSupport.AssertEqual(0, report.Statistics.Tables, "tables");
                        TestSupport.Assert(report.Error == null, "no error");
                        TestSupport.AssertEqual(0, report.ExitCode, "exit code in report");
                    }
                }),
                Case("JsonReportWarnings", "Warnings appear in the report with code, message and count", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", dir.File("out.md"), "--from", "html", "--json" }, CliSamples.Utf8(CliSamples.HtmlUnderline)).ConfigureAwait(false);
                        TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                        CliReport report = CliSamples.LastLineReport(r.StdoutText);
                        CliReportWarning? w = report.Warnings.Find(x => x.Code == "FormattingLost");
                        TestSupport.Assert(w != null, "FormattingLost present");
                        TestSupport.Assert(w!.Count >= 1 && w.Message.Length > 0, "count and message");
                        TestSupport.AssertContains(r.Stderr, "1 warning", "summary counts the warning");
                        TestSupport.AssertContains(r.Stderr, "  warning FormattingLost: ", "warning line on stderr");
                    }
                }),
                Case("JsonReportToStderr", "With -o - the document owns stdout and the report goes to stderr", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", "md", "--to", "html", "--json" }, CliSamples.Utf8(CliSamples.Markdown)).ConfigureAwait(false);
                    TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                    TestSupport.Assert(r.StdoutText.StartsWith("<!DOCTYPE html>", StringComparison.Ordinal), "stdout is the document");
                    TestSupport.AssertNotContains(r.StdoutText, "contractVersion", "no report on stdout");
                    CliReport report = CliSamples.LastLineReport(r.Stderr);
                    TestSupport.AssertEqual("stdout", report.Output?.Path, "stdout name");
                    TestSupport.AssertEqual((long)r.Stdout.Length, report.Output?.Bytes ?? -1, "bytes match stdout");
                }),
                Case("QuietSuppressesSummary", "--quiet writes nothing to stderr on success", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", "html", "--to", "md", "-q" }, CliSamples.Utf8(CliSamples.HtmlUnderline)).ConfigureAwait(false);
                    TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                    TestSupport.AssertEqual("", r.Stderr, "stderr empty");
                }),
                Case("QuietKeepsErrors", "--quiet still reports errors on stderr", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "missing-file.md", "-o", "-", "--to", "html", "-q" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(4, r.ExitCode, "exit code");
                    TestSupport.AssertContains(r.Stderr, "docconv: error:", "error line");
                }),
                Case("SummaryLine", "The summary names both ends, sizes, duration and warnings", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string input = dir.WriteText("report.md", CliSamples.Markdown);
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", input, "-o", dir.File("report.html") }).ConfigureAwait(false);
                        TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                        TestSupport.AssertContains(r.Stderr, "docconv: " + input + " (Markdown) -> " + dir.File("report.html") + " (Html), ", "names");
                        TestSupport.AssertContains(r.Stderr, " B -> ", "sizes");
                        TestSupport.AssertContains(r.Stderr, " ms, 0 warnings", "duration and warnings");
                        TestSupport.AssertEqual(0, r.Stdout.Length, "stdout empty without --json");
                    }
                }),
                Case("DetectText", "detect prints the format in text", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string input = dir.WriteText("notes.md", CliSamples.Markdown);
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "detect", "-i", input }).ConfigureAwait(false);
                        TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                        TestSupport.AssertContains(r.StdoutText, "Format:        Markdown", "format line");
                        TestSupport.AssertContains(r.StdoutText, "Media type:    text/markdown", "media type");
                    }
                }),
                Case("DetectJson", "detect --json reports the detection", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "detect", "-i", "-", "--json" }, CliSamples.Utf8("{\"a\": [1, 2]}")).ConfigureAwait(false);
                    TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                    CliDetectReport? report = CliReportWriter.Deserialize<CliDetectReport>(r.StdoutText.Trim());
                    TestSupport.Assert(report != null, "report");
                    TestSupport.AssertEqual(1, report!.ContractVersion, "contract");
                    TestSupport.AssertEqual("detect", report.Command, "command");
                    TestSupport.AssertEqual(true, report.Success, "success");
                    TestSupport.AssertEqual("stdin", report.Input?.Path, "path");
                    TestSupport.AssertEqual(13L, report.Input?.Bytes ?? -1, "bytes");
                    TestSupport.AssertEqual("Json", report.Detection?.Format, "format");
                    TestSupport.AssertEqual("application/json", report.Detection?.MediaType, "media type");
                    TestSupport.AssertEqual("json", report.Detection?.Extension, "extension");
                    TestSupport.AssertEqual("Structure", report.Detection?.Confidence, "confidence");
                    TestSupport.AssertEqual(true, report.Detection?.Supported, "supported");
                    TestSupport.Assert(report.Error == null, "no error");
                }),
                Case("DetectUnsupported", "detect of a legacy .doc exits 3 and describes it", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "detect", "-i", "-", "--json" }, CliSamples.LegacyDoc()).ConfigureAwait(false);
                    TestSupport.AssertEqual(3, r.ExitCode, "exit code");
                    CliDetectReport? report = CliReportWriter.Deserialize<CliDetectReport>(r.StdoutText.Trim());
                    TestSupport.Assert(report != null && report.Detection != null, "report");
                    TestSupport.Assert(report!.Detection!.Format == null, "format null");
                    TestSupport.AssertEqual(false, report.Detection.Supported, "unsupported");
                    TestSupport.AssertContains(report.Detection.RecognizedAs, ".doc", "described");
                    TestSupport.AssertEqual("UnsupportedFormat", report.Error?.Code, "error code");
                }),
                Case("DetectMissingInput", "detect without -i exits 2; with a missing file exits 4", async () =>
                {
                    CliRunResult a = await CliTestRunner.RunAsync(new string[] { "detect" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, a.ExitCode, "no -i");
                    CliRunResult b = await CliTestRunner.RunAsync(new string[] { "detect", "-i", "does-not-exist.md" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(4, b.ExitCode, "missing file");
                }),
                Case("FormatsText", "formats prints the formats and the matrix", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "formats" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(0, r.ExitCode, "exit code");
                    TestSupport.AssertContains(r.StdoutText, "Input formats:", "inputs");
                    TestSupport.AssertContains(r.StdoutText, "Output formats:", "outputs");
                    TestSupport.AssertContains(r.StdoutText, "From \\ To", "grid header");
                    TestSupport.AssertContains(r.StdoutText, "F = full", "legend");
                }),
                Case("FormatsJson", "formats --json lists 18 inputs, 11 outputs and 198 conversions", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "formats", "--json" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(0, r.ExitCode, "exit code");
                    CliFormatsReport? report = CliReportWriter.Deserialize<CliFormatsReport>(r.StdoutText.Trim());
                    TestSupport.Assert(report != null, "report");
                    TestSupport.AssertEqual(1, report!.ContractVersion, "contract");
                    TestSupport.AssertEqual(18, report.Inputs.Count, "inputs");
                    TestSupport.AssertEqual(11, report.Outputs.Count, "outputs");
                    TestSupport.AssertEqual(198, report.Conversions.Count, "conversions");
                    CliFormatsConversion? csv = report.Conversions.Find(c => c.From == "Markdown" && c.To == "Csv");
                    TestSupport.Assert(csv != null && csv.Fidelity == "Projection" && csv.Notes.Length > 0, "projection pair with notes");
                    CliFormatsConversion? html = report.Conversions.Find(c => c.From == "Markdown" && c.To == "Html");
                    TestSupport.Assert(html != null && html.Fidelity == "Full", "full pair");
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
