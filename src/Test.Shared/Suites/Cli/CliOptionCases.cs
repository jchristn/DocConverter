#if !DOCCONVERTER_NETSTANDARD_TEST
namespace Test.Shared.Suites.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;

    /// <summary>
    /// Every conversion option flag reaches the output; invalid values exit 2.
    /// </summary>
    public static class CliOptionCases
    {
        /// <summary>
        /// Build the cases.
        /// </summary>
        /// <returns>Cases.</returns>
        public static List<TestCaseDescriptor> Cases()
        {
            return new List<TestCaseDescriptor>
            {
                Case("HtmlFragment", "--html-fragment omits the document wrapper", async () =>
                {
                    string html = await PipeAsync(CliSamples.Markdown, "md", "html", "--html-fragment").ConfigureAwait(false);
                    TestSupport.AssertNotContains(html, "<!DOCTYPE", "no doctype");
                    TestSupport.AssertContains(html, "<h1>Title</h1>", "content");
                }),
                Case("HtmlNoCss", "--html-no-css omits the stylesheet", async () =>
                {
                    string with = await PipeAsync(CliSamples.Markdown, "md", "html").ConfigureAwait(false);
                    string without = await PipeAsync(CliSamples.Markdown, "md", "html", "--html-no-css").ConfigureAwait(false);
                    TestSupport.AssertContains(with, "<style>", "default has css");
                    TestSupport.AssertNotContains(without, "<style>", "no css");
                }),
                Case("Title", "--title sets the HTML title", async () =>
                {
                    string html = await PipeAsync(CliSamples.Markdown, "md", "html", "--title", "My Custom Title").ConfigureAwait(false);
                    TestSupport.AssertContains(html, "<title>My Custom Title</title>", "title");
                }),
                Case("NoMetadata", "--no-metadata drops front matter metadata", async () =>
                {
                    string with = await PipeAsync(CliSamples.MarkdownFrontMatter, "md", "html").ConfigureAwait(false);
                    string without = await PipeAsync(CliSamples.MarkdownFrontMatter, "md", "html", "--no-metadata").ConfigureAwait(false);
                    TestSupport.AssertContains(with, "<title>Front Title</title>", "metadata title by default");
                    TestSupport.AssertNotContains(without, "Front Title", "metadata dropped");
                }),
                Case("JsonCompact", "--json-compact writes JSON on one line", async () =>
                {
                    string indented = await PipeAsync(CliSamples.Markdown, "md", "json").ConfigureAwait(false);
                    string compact = await PipeAsync(CliSamples.Markdown, "md", "json", "--json-compact").ConfigureAwait(false);
                    TestSupport.Assert(indented.Trim().IndexOf('\n') > 0, "default indented");
                    TestSupport.Assert(compact.IndexOf('\n') < 0, "compact has no newlines");
                    TestSupport.AssertContains(compact, "\"docconverter\":\"1\"", "canonical marker");
                }),
                Case("JsonNoBinary", "--json-no-binary leaves image bytes out", async () =>
                {
                    string with = await PipeAsync(CliSamples.MarkdownWithImage(), "md", "json", "--json-compact").ConfigureAwait(false);
                    string without = await PipeAsync(CliSamples.MarkdownWithImage(), "md", "json", "--json-compact", "--json-no-binary").ConfigureAwait(false);
                    TestSupport.AssertContains(with, "\"data\":\"", "data by default");
                    TestSupport.AssertNotContains(without, "\"data\":", "data omitted");
                    TestSupport.AssertContains(without, "\"size\":", "size kept");
                }),
                Case("CsvDelimiter", "--csv-delimiter changes the output delimiter", async () =>
                {
                    string csv = await PipeAsync(CliSamples.MarkdownWithTable, "md", "csv", "--csv-delimiter", ";").ConfigureAwait(false);
                    TestSupport.AssertContains(csv, "Name;Value", "semicolons");
                    string tab = await PipeAsync(CliSamples.MarkdownWithTable, "md", "csv", "--csv-delimiter", "tab").ConfigureAwait(false);
                    TestSupport.AssertContains(tab, "Name\tValue", "tabs");
                }),
                Case("CsvDelimiterInvalid", "A multi character delimiter exits 2", async () =>
                {
                    CliRunResult r = await RunPipeAsync(CliSamples.MarkdownWithTable, "md", "csv", "--csv-delimiter", ";;").ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                }),
                Case("TableSelection", "--table first, all and an index choose tables", async () =>
                {
                    string first = await PipeAsync(CliSamples.MarkdownTwoTables, "md", "csv").ConfigureAwait(false);
                    TestSupport.AssertContains(first, "A,B", "first table");
                    TestSupport.AssertNotContains(first, "C,D", "only first by default");
                    string all = await PipeAsync(CliSamples.MarkdownTwoTables, "md", "csv", "--table", "all").ConfigureAwait(false);
                    TestSupport.Assert(all.Contains("A,B") && all.Contains("C,D"), "both tables");
                    string second = await PipeAsync(CliSamples.MarkdownTwoTables, "md", "csv", "--table", "1").ConfigureAwait(false);
                    TestSupport.Assert(second.Contains("C,D") && !second.Contains("A,B"), "second table only");
                    CliRunResult bad = await RunPipeAsync(CliSamples.MarkdownTwoTables, "md", "csv", "--table", "many").ConfigureAwait(false);
                    TestSupport.AssertEqual(2, bad.ExitCode, "invalid --table");
                }),
                Case("NoHeader", "--no-header reads CSV without a header row", async () =>
                {
                    string with = await PipeAsync("a,b\n1,2\n", "csv", "json", "--json-compact").ConfigureAwait(false);
                    string without = await PipeAsync("a,b\n1,2\n", "csv", "json", "--json-compact", "--no-header").ConfigureAwait(false);
                    TestSupport.AssertContains(with, "\"headerRowCount\":1", "header by default");
                    TestSupport.AssertContains(without, "\"headerRowCount\":0", "no header");
                }),
                Case("TextWrap", "--text-wrap wraps plain text", async () =>
                {
                    string text = await PipeAsync(CliSamples.LongParagraph, "txt", "txt", "--text-wrap", "30").ConfigureAwait(false);
                    foreach (string line in text.Split('\n')) TestSupport.Assert(line.Length <= 30, "line too long: " + line);
                    TestSupport.Assert(text.Split('\n').Length > 3, "wrapped into several lines");
                }),
                Case("TextWrapInvalid", "--text-wrap 5 exits 2", async () =>
                {
                    CliRunResult r = await RunPipeAsync(CliSamples.LongParagraph, "txt", "txt", "--text-wrap", "5").ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                    CliRunResult r2 = await RunPipeAsync(CliSamples.LongParagraph, "txt", "txt", "--text-wrap", "wide").ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r2.ExitCode, "not a number");
                }),
                Case("LineEndingCrlf", "--line-ending crlf writes CRLF only", async () =>
                {
                    string text = await PipeAsync(CliSamples.Markdown, "md", "md", "--line-ending", "crlf").ConfigureAwait(false);
                    TestSupport.AssertContains(text, "\r\n", "crlf present");
                    TestSupport.AssertEqual(-1, text.Replace("\r\n", "").IndexOf('\n'), "no bare lf");
                }),
                Case("LineEndingInvalid", "--line-ending cr exits 2", async () =>
                {
                    CliRunResult r = await RunPipeAsync(CliSamples.Markdown, "md", "md", "--line-ending", "cr").ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                }),
                Case("OutputEncoding", "--output-encoding utf-16 writes a UTF-16 byte order mark", async () =>
                {
                    CliRunResult r = await RunPipeAsync(CliSamples.Markdown, "md", "txt", "--output-encoding", "utf-16").ConfigureAwait(false);
                    TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                    TestSupport.Assert(r.Stdout.Length > 2 && r.Stdout[0] == 0xFF && r.Stdout[1] == 0xFE, "utf-16 le bom");
                    TestSupport.AssertContains(Encoding.Unicode.GetString(r.Stdout, 2, r.Stdout.Length - 2), "Title", "content");
                }),
                Case("InputEncoding", "--input-encoding latin1 decodes single byte text", async () =>
                {
                    byte[] latin = new byte[] { (byte)'C', (byte)'a', (byte)'f', 0xE9 };
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "-", "-o", "-", "--from", "txt", "--to", "md", "--input-encoding", "iso-8859-1" }, latin).ConfigureAwait(false);
                    TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                    TestSupport.AssertContains(r.StdoutText, "Café", "decoded");
                }),
                Case("EncodingInvalid", "An unknown encoding exits 2", async () =>
                {
                    CliRunResult r = await RunPipeAsync(CliSamples.Markdown, "md", "txt", "--output-encoding", "klingon-8").ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                }),
                Case("NoImages", "--no-images drops images with the ImagesOmitted warning", async () =>
                {
                    CliRunResult r = await RunPipeAsync(CliSamples.MarkdownWithImage(), "md", "html", "--no-images").ConfigureAwait(false);
                    TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                    TestSupport.AssertNotContains(r.StdoutText, "<img", "no image");
                    TestSupport.AssertContains(r.Stderr, "ImagesOmitted", "warning");
                }),
                Case("ImagesPlaceholder", "--images placeholder writes a text placeholder", async () =>
                {
                    string html = await PipeAsync(CliSamples.MarkdownWithImage(), "md", "html", "--images", "placeholder").ConfigureAwait(false);
                    TestSupport.AssertContains(html, "[Image: red square, PNG 4x4]", "placeholder");
                    TestSupport.AssertNotContains(html, "<img", "no img tag");
                }),
                Case("ImagesOmit", "--images omit drops images from Markdown", async () =>
                {
                    string md = await PipeAsync(CliSamples.MarkdownWithImage(), "md", "md", "--images", "omit").ConfigureAwait(false);
                    TestSupport.AssertNotContains(md, "data:image", "no image");
                    TestSupport.AssertContains(md, "After the image.", "text kept");
                }),
                Case("ImagesExternal", "--images external writes side files next to the output", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string input = dir.WriteText("in.md", CliSamples.MarkdownWithImage());
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", input, "-o", dir.File("out.html"), "--images", "external" }).ConfigureAwait(false);
                        TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                        string html = dir.ReadText("out.html");
                        TestSupport.AssertNotContains(html, "data:image", "no data uri");
                        string[] pngs = Directory.GetFiles(dir.Path, "*.png");
                        TestSupport.AssertEqual(1, pngs.Length, "one side file");
                        TestSupport.AssertContains(html, "src=\"" + Path.GetFileName(pngs[0]) + "\"", "html references the side file");
                        byte[] png = File.ReadAllBytes(pngs[0]);
                        TestSupport.Assert(png.Length > 8 && png[1] == (byte)'P' && png[2] == (byte)'N', "valid png written");
                    }
                }),
                Case("ImagesExternalStdout", "--images external with -o - exits 2", async () =>
                {
                    CliRunResult r = await RunPipeAsync(CliSamples.MarkdownWithImage(), "md", "html", "--images", "external").ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                }),
                Case("ImagesInvalid", "--images sideways exits 2", async () =>
                {
                    CliRunResult r = await RunPipeAsync(CliSamples.Markdown, "md", "html", "--images", "sideways").ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                }),
                Case("OptionsFile", "--options applies a JSON file and flags override it", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string file = dir.WriteText("opts.json", "{ \"htmlMode\": \"fragment\", \"lineEnding\": \"crlf\", \"jsonIndented\": false, // comment\n }");
                        CliRunResult a = await RunPipeAsync(CliSamples.Markdown, "md", "html", "--options", file).ConfigureAwait(false);
                        TestSupport.AssertEqual(0, a.ExitCode, "exit code: " + a);
                        TestSupport.AssertNotContains(a.StdoutText, "<!DOCTYPE", "fragment from file");
                        TestSupport.AssertContains(a.StdoutText, "\r\n", "crlf from file");
                        CliRunResult b = await RunPipeAsync(CliSamples.Markdown, "md", "html", "--options", file, "--line-ending", "lf").ConfigureAwait(false);
                        TestSupport.AssertEqual(0, b.ExitCode, "exit code: " + b);
                        TestSupport.AssertNotContains(b.StdoutText, "\r", "flag overrides file");
                    }
                }),
                Case("OptionsFileInvalid", "An options file with an unknown property exits 2; a missing one exits 4", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string file = dir.WriteText("opts.json", "{ \"htmlModee\": \"fragment\" }");
                        CliRunResult a = await RunPipeAsync(CliSamples.Markdown, "md", "html", "--options", file).ConfigureAwait(false);
                        TestSupport.AssertEqual(2, a.ExitCode, "unknown property");
                        CliRunResult b = await RunPipeAsync(CliSamples.Markdown, "md", "html", "--options", dir.File("none.json")).ConfigureAwait(false);
                        TestSupport.AssertEqual(4, b.ExitCode, "missing file");
                        string bad = dir.WriteText("bad.json", "{ not json");
                        CliRunResult c = await RunPipeAsync(CliSamples.Markdown, "md", "html", "--options", bad).ConfigureAwait(false);
                        TestSupport.AssertEqual(2, c.ExitCode, "malformed json");
                    }
                }),
                Case("PageAndMarginValidation", "Invalid --page-size, --margin and --slide-split exit 2; valid ones are accepted", async () =>
                {
                    TestSupport.AssertEqual(2, (await RunPipeAsync(CliSamples.Markdown, "md", "html", "--page-size", "huge").ConfigureAwait(false)).ExitCode, "page size");
                    TestSupport.AssertEqual(2, (await RunPipeAsync(CliSamples.Markdown, "md", "html", "--margin", "500").ConfigureAwait(false)).ExitCode, "margin range");
                    TestSupport.AssertEqual(2, (await RunPipeAsync(CliSamples.Markdown, "md", "html", "--margin", "wide").ConfigureAwait(false)).ExitCode, "margin number");
                    TestSupport.AssertEqual(2, (await RunPipeAsync(CliSamples.Markdown, "md", "html", "--slide-split", "9").ConfigureAwait(false)).ExitCode, "slide split");
                    CliRunResult ok = await RunPipeAsync(CliSamples.Markdown, "md", "html", "--page-size", "A4", "--margin", "36", "--slide-split", "1").ConfigureAwait(false);
                    TestSupport.AssertEqual(0, ok.ExitCode, "valid values: " + ok);
                }),
                Case("ReaderFlagsAccepted", "--include-notes, --include-hidden-sheets, --preserve-pages and --deterministic are accepted", async () =>
                {
                    CliRunResult r = await RunPipeAsync(CliSamples.Markdown, "md", "json", "--include-notes", "--include-hidden-sheets", "--preserve-pages", "--deterministic").ConfigureAwait(false);
                    TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                }),
                Case("DeterministicRepeatable", "Two --deterministic runs produce identical bytes", async () =>
                {
                    CliRunResult a = await RunPipeAsync(CliSamples.MarkdownWithImage(), "md", "json", "--deterministic").ConfigureAwait(false);
                    CliRunResult b = await RunPipeAsync(CliSamples.MarkdownWithImage(), "md", "json", "--deterministic").ConfigureAwait(false);
                    TestSupport.AssertEqual(Convert.ToBase64String(a.Stdout), Convert.ToBase64String(b.Stdout), "identical");
                })
            };
        }

        private static async Task<CliRunResult> RunPipeAsync(string input, string from, string to, params string[] extra)
        {
            List<string> args = new List<string> { "convert", "-i", "-", "-o", "-", "--from", from, "--to", to };
            args.AddRange(extra);
            return await CliTestRunner.RunAsync(args.ToArray(), CliSamples.Utf8(input)).ConfigureAwait(false);
        }

        private static async Task<string> PipeAsync(string input, string from, string to, params string[] extra)
        {
            CliRunResult r = await RunPipeAsync(input, from, to, extra).ConfigureAwait(false);
            if (r.ExitCode != 0) throw new TestAssertionException("conversion failed: " + r);
            return r.StdoutText;
        }

        private static TestCaseDescriptor Case(string id, string name, Func<Task> body)
        {
            return new TestCaseDescriptor("Cli", id, name, executeAsync: async ct => await body().ConfigureAwait(false));
        }
    }
}
#endif
