#if !DOCCONVERTER_NETSTANDARD_TEST
namespace Test.Shared.Suites.Cli
{
    using System;
    using System.Text;

    /// <summary>
    /// Outcome of one in-process docconv run.
    /// </summary>
    public class CliRunResult
    {
        /// <summary>
        /// Exit code.
        /// </summary>
        public int ExitCode { get; set; } = 0;

        /// <summary>
        /// Raw stdout bytes.
        /// </summary>
        public byte[] Stdout { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Everything written to stderr.
        /// </summary>
        public string Stderr { get; set; } = "";

        /// <summary>
        /// Stdout decoded as UTF-8.
        /// </summary>
        public string StdoutText
        {
            get => new UTF8Encoding(false).GetString(Stdout);
        }

        /// <summary>
        /// Summary for failure messages.
        /// </summary>
        /// <returns>Exit code, stdout and stderr, truncated.</returns>
        public override string ToString()
        {
            return "exit=" + ExitCode + " stdout=" + TestSupport.Truncate(StdoutText, 300) + " stderr=" + TestSupport.Truncate(Stderr, 300);
        }
    }
}
#endif
