namespace DocConverter.Cli
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Entry point for the docconv tool. Wires the real console streams and Ctrl+C into CliApplication.
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
            try
            {
                Console.OutputEncoding = new UTF8Encoding(false);
            }
            catch (IOException)
            {
                // Some hosts do not allow changing the console encoding; output still works.
            }
            catch (PlatformNotSupportedException)
            {
                // Same as above.
            }

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                ConsoleCancelEventHandler handler = (sender, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                Console.CancelKeyPress += handler;
                try
                {
                    using (Stream stdin = Console.OpenStandardInput())
                    using (Stream stdout = Console.OpenStandardOutput())
                    {
                        CliApplication app = new CliApplication();
                        return await app.RunAsync(args, stdin, stdout, Console.Error, cts.Token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    Console.CancelKeyPress -= handler;
                }
            }
        }
    }
}
