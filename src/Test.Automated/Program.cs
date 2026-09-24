namespace Test.Automated
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using DocConverter;
    using Test.Shared;
    using Test.Shared.Docs;
    using Touchstone.Cli;

    /// <summary>
    /// Console runner for every DocConverter suite. Exit code 0 when all tests pass, 1 otherwise.
    /// Arguments:
    /// --results &lt;path&gt; writes JSON results;
    /// --update-docs &lt;repo root&gt; regenerates the capability matrix block in docs/FORMATS.md and exits;
    /// --update-golden &lt;directory&gt; rewrites every golden file into the directory and exits.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            string? resultsPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--results" && i + 1 < args.Length) resultsPath = args[i + 1];
                else if (args[i] == "--update-docs" && i + 1 < args.Length) return UpdateDocs(args[i + 1]);
                else if (args[i] == "--update-golden" && i + 1 < args.Length) return await UpdateGoldenAsync(args[i + 1]).ConfigureAwait(false);
            }

            return await ConsoleRunner.RunAsync(DocConverterSuites.All, resultsPath: resultsPath).ConfigureAwait(false);
        }

        private static async Task<int> UpdateGoldenAsync(string directory)
        {
            Directory.CreateDirectory(directory);
            System.Collections.Generic.Dictionary<string, byte[]> files = await Test.Shared.Suites.GoldenSuite.GenerateAsync(System.Threading.CancellationToken.None).ConfigureAwait(false);
            foreach (System.Collections.Generic.KeyValuePair<string, byte[]> file in files)
                File.WriteAllBytes(Path.Combine(directory, file.Key), file.Value);
            Console.WriteLine("Wrote " + files.Count + " golden files to " + directory);
            return 0;
        }

        private static int UpdateDocs(string repoRoot)
        {
            string path = Path.Combine(repoRoot, "docs", "FORMATS.md");
            if (!File.Exists(path))
            {
                Console.Error.WriteLine("Not found: " + path);
                return 1;
            }

            string text = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n");
            int begin = text.IndexOf("<!-- BEGIN GENERATED MATRIX", StringComparison.Ordinal);
            int end = text.IndexOf(FormatsTable.EndMarker, StringComparison.Ordinal);
            if (begin < 0 || end < begin)
            {
                Console.Error.WriteLine("Generated matrix markers not found in " + path);
                return 1;
            }

            string generated = FormatsTable.Render(new Converter());
            string updated = text.Substring(0, begin) + generated + text.Substring(end + FormatsTable.EndMarker.Length);
            File.WriteAllText(path, updated, new UTF8Encoding(false));
            Console.WriteLine("Updated " + path);
            return 0;
        }
    }
}
