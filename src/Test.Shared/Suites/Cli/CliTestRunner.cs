#if !DOCCONVERTER_NETSTANDARD_TEST
namespace Test.Shared.Suites.Cli
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Cli;

    /// <summary>
    /// Runs docconv in-process with memory streams.
    /// </summary>
    public static class CliTestRunner
    {
        /// <summary>
        /// Run docconv.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <param name="stdin">Bytes for stdin, or null for empty.</param>
        /// <param name="factory">Converter factory, or null for the default.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public static async Task<CliRunResult> RunAsync(string[] args, byte[]? stdin = null, Func<ConverterSettings, Converter>? factory = null, CancellationToken token = default)
        {
            CliApplication app = factory == null ? new CliApplication() : new CliApplication(factory);
            using (MemoryStream input = new MemoryStream(stdin ?? new byte[0]))
            using (MemoryStream output = new MemoryStream())
            using (StringWriter error = new StringWriter())
            {
                int exit = await app.RunAsync(args, input, output, error, token).ConfigureAwait(false);
                return new CliRunResult { ExitCode = exit, Stdout = output.ToArray(), Stderr = error.ToString() };
            }
        }
    }
}
#endif
