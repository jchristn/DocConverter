#if !DOCCONVERTER_NETSTANDARD_TEST
namespace Test.Shared.Suites.Cli
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DocConverter.Cli;
    using Touchstone.Core;

    /// <summary>
    /// Command line parsing, help, version and usage errors.
    /// </summary>
    public static class CliParsingCases
    {
        /// <summary>
        /// Build the cases.
        /// </summary>
        /// <returns>Cases.</returns>
        public static List<TestCaseDescriptor> Cases()
        {
            return new List<TestCaseDescriptor>
            {
                Case("HelpLong", "--help prints usage to stdout and exits 0", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "--help" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(0, r.ExitCode, "exit code");
                    TestSupport.AssertContains(r.StdoutText, "USAGE:", "usage heading");
                    TestSupport.AssertContains(r.StdoutText, "EXIT CODES:", "exit code table");
                    TestSupport.AssertContains(r.StdoutText, "--max-input-mb", "every option listed");
                    TestSupport.AssertNotContains(r.StdoutText, ((char)0x2014).ToString(), "no em-dashes in help");
                    TestSupport.AssertEqual("", r.Stderr, "stderr empty");
                }),
                Case("HelpShortForms", "-h, -?, /?, help and 'convert --help' all print help", async () =>
                {
                    foreach (string[] args in new string[][] { new[] { "-h" }, new[] { "-?" }, new[] { "/?" }, new[] { "help" }, new[] { "convert", "--help" }, new[] { "detect", "-i", "x", "-h" } })
                    {
                        CliRunResult r = await CliTestRunner.RunAsync(args).ConfigureAwait(false);
                        TestSupport.AssertEqual(0, r.ExitCode, "exit code for " + string.Join(" ", args));
                        TestSupport.AssertContains(r.StdoutText, "USAGE:", "help for " + string.Join(" ", args));
                    }
                }),
                Case("Version", "--version, -v and version print 'docconv <version>' matching the library version", async () =>
                {
                    foreach (string[] args in new string[][] { new[] { "--version" }, new[] { "-v" }, new[] { "version" }, new[] { "convert", "--version" } })
                    {
                        CliRunResult r = await CliTestRunner.RunAsync(args).ConfigureAwait(false);
                        TestSupport.AssertEqual(0, r.ExitCode, "exit code for " + string.Join(" ", args));
                        TestSupport.AssertEqual("docconv " + CliApplication.Version + "\n", r.StdoutText, "version text");
                        TestSupport.AssertEqual(DocConverter.Observability.DocConverterDiagnostics.Version, CliApplication.Version, "tool and library versions match");
                        TestSupport.AssertNotContains(r.StdoutText, "+", "build metadata stripped");
                    }
                }),
                Case("NoArguments", "No arguments prints help to stderr and exits 2", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[0]).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                    TestSupport.AssertContains(r.Stderr, "USAGE:", "help on stderr");
                    TestSupport.AssertEqual(0, r.Stdout.Length, "stdout empty");
                }),
                Case("UnknownCommand", "An unknown command exits 2 and names it", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "transmogrify" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                    TestSupport.AssertContains(r.Stderr, "transmogrify", "names the command");
                }),
                Case("OptionBeforeCommand", "An option before the command exits 2", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "-i", "a.md", "convert" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                    TestSupport.AssertContains(r.Stderr, "Missing command", "message");
                }),
                Case("UnknownOption", "An unknown option exits 2 and names it", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "a.md", "-o", "b.html", "--bogus" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                    TestSupport.AssertContains(r.Stderr, "--bogus", "names the option");
                }),
                Case("MissingInput", "convert without -i exits 2", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-o", "b.html" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                    TestSupport.AssertContains(r.Stderr, "-i", "mentions -i");
                }),
                Case("MissingOutput", "convert without -o exits 2", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "a.md" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                    TestSupport.AssertContains(r.Stderr, "-o", "mentions -o");
                }),
                Case("MissingValue", "A valued option at the end exits 2", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-o", "b.html", "-i" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                    TestSupport.AssertContains(r.Stderr, "requires a value", "message");
                }),
                Case("EqualsForm", "--opt=value works for valued options", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string input = dir.WriteText("in.md", CliSamples.Markdown);
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "--input=" + input, "--output=" + dir.File("out.html"), "--to=html", "--quiet=true" }).ConfigureAwait(false);
                        TestSupport.AssertEqual(0, r.ExitCode, "exit code: " + r);
                        TestSupport.AssertContains(dir.ReadText("out.html"), "<h1>Title</h1>", "converted");
                        TestSupport.AssertEqual("", r.Stderr, "quiet=true honored");
                    }
                }),
                Case("BooleanFlagFalse", "--overwrite=false keeps the refusal to overwrite", async () =>
                {
                    using (CliTempDirectory dir = new CliTempDirectory())
                    {
                        string input = dir.WriteText("in.md", CliSamples.Markdown);
                        string output = dir.WriteText("out.html", "keep");
                        CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", input, "-o", output, "--overwrite=false" }).ConfigureAwait(false);
                        TestSupport.AssertEqual(4, r.ExitCode, "exit code");
                        TestSupport.AssertEqual("keep", dir.ReadText("out.html"), "untouched");
                    }
                }),
                Case("BadBooleanValue", "--overwrite=maybe exits 2", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "a", "-o", "b", "--overwrite=maybe" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                }),
                Case("DoubleDash", "-- ends options and anything after it is an unexpected argument", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "a.md", "-o", "b.html", "--", "--quiet" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                    TestSupport.AssertContains(r.Stderr, "Unexpected argument '--quiet'", "treated as positional");
                }),
                Case("PositionalRejected", "A bare path without -i exits 2", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "a.md", "b.html" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                    TestSupport.AssertContains(r.Stderr, "Unexpected argument", "message");
                }),
                Case("OptionNotForDetect", "Convert-only options are rejected by detect", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "detect", "-i", "a.md", "--overwrite" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                    TestSupport.AssertContains(r.Stderr, "does not apply to the detect command", "message");
                }),
                Case("OptionNotForFormats", "Options other than --json are rejected by formats", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "formats", "-i", "a.md" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                }),
                Case("UsageErrorJsonReport", "A usage error with --json writes a Usage report on stdout", async () =>
                {
                    CliRunResult r = await CliTestRunner.RunAsync(new string[] { "convert", "-i", "a.md", "-o", "b.html", "--bogus", "--json" }).ConfigureAwait(false);
                    TestSupport.AssertEqual(2, r.ExitCode, "exit code");
                    DocConverter.Cli.Reporting.CliReport report = CliSamples.LastLineReport(r.StdoutText);
                    TestSupport.AssertEqual(false, report.Success, "success");
                    TestSupport.AssertEqual("Usage", report.Error?.Code, "error code");
                    TestSupport.AssertEqual(2, report.ExitCode, "report exit code");
                    TestSupport.AssertEqual("convert", report.Command, "command");
                }),
                Case("ParserUnit", "CliArgumentParser maps aliases and values", () =>
                {
                    CliArguments a = CliArgumentParser.Parse(new string[] { "convert", "-i", "x", "-o", "y", "--from", "md", "-q", "--json", "--table=all" });
                    TestSupport.AssertEqual(CliCommandEnum.Convert, a.Command, "command");
                    TestSupport.AssertEqual("x", a.Input, "input");
                    TestSupport.AssertEqual("y", a.Output, "output");
                    TestSupport.AssertEqual("md", a.Get("--from"), "from");
                    TestSupport.AssertEqual("all", a.Get("--table"), "table");
                    TestSupport.Assert(a.Quiet && a.Json, "flags");
                    return Task.CompletedTask;
                })
            };
        }

        private static TestCaseDescriptor Case(string id, string name, System.Func<Task> body)
        {
            return new TestCaseDescriptor("Cli", id, name, executeAsync: async ct => await body().ConfigureAwait(false));
        }
    }
}
#endif
