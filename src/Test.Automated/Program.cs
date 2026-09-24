namespace Test.Automated
{
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Cli;

    /// <summary>
    /// Console runner for every DocConverter suite. Exit code 0 when all tests pass, 1 otherwise.
    /// Arguments: --results &lt;path&gt; writes JSON results; --update-golden rewrites golden files under the given
    /// repository fixtures directory instead of comparing against them.
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
                else if (args[i] == "--update-golden" && i + 1 < args.Length) GoldenSettings.UpdateDirectory = args[i + 1];
            }

            return await ConsoleRunner.RunAsync(DocConverterSuites.All, resultsPath: resultsPath).ConfigureAwait(false);
        }
    }
}
