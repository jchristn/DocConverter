namespace DocConverter.Cli
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Cli.Commands;
    using DocConverter.Cli.Reporting;

    /// <summary>
    /// The docconv application. All console access goes through the streams passed to RunAsync, so the whole tool runs
    /// in-process for tests. RunAsync never throws: every failure becomes an exit code (see CliExitCodeEnum).
    /// </summary>
    public class CliApplication
    {
        private readonly Func<ConverterSettings, Converter> _ConverterFactory;

        /// <summary>
        /// Instantiate with the default converter.
        /// </summary>
        public CliApplication()
            : this(settings => new Converter(settings))
        {
        }

        /// <summary>
        /// Instantiate with a converter factory, for example one that registers extra readers or writers.
        /// </summary>
        /// <param name="converterFactory">Creates the converter from settings built from the command line.</param>
        /// <exception cref="ArgumentNullException">Thrown when converterFactory is null.</exception>
        public CliApplication(Func<ConverterSettings, Converter> converterFactory)
        {
            _ConverterFactory = converterFactory ?? throw new ArgumentNullException(nameof(converterFactory));
        }

        /// <summary>
        /// The tool version without build metadata, for example "0.1.0".
        /// </summary>
        public static string Version
        {
            get
            {
                Assembly assembly = typeof(CliApplication).Assembly;
                AssemblyInformationalVersionAttribute? info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                string version = info?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "0.0.0";
                int plus = version.IndexOf('+');
                return plus >= 0 ? version.Substring(0, plus) : version;
            }
        }

        /// <summary>
        /// Run docconv.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <param name="stdin">Standard input (raw bytes).</param>
        /// <param name="stdout">Standard output (raw bytes).</param>
        /// <param name="stderr">Standard error.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Exit code.</returns>
        public async Task<int> RunAsync(string[] args, Stream stdin, Stream stdout, TextWriter stderr, CancellationToken token)
        {
            if (args == null) args = new string[0];
            if (stdin == null) throw new ArgumentNullException(nameof(stdin));
            if (stdout == null) throw new ArgumentNullException(nameof(stdout));
            if (stderr == null) throw new ArgumentNullException(nameof(stderr));

            try
            {
                CliArguments parsed;
                try
                {
                    parsed = CliArgumentParser.Parse(args);
                }
                catch (CliUsageException ex)
                {
                    await CliIO.ErrorLineAsync(stderr, "docconv: error: " + ex.Message).ConfigureAwait(false);
                    await CliIO.ErrorLineAsync(stderr, "Run 'docconv --help' for usage.").ConfigureAwait(false);
                    await EmitUsageReportAsync(args, ex, stdout, stderr, token).ConfigureAwait(false);
                    return (int)CliExitCodeEnum.Usage;
                }

                switch (parsed.Command)
                {
                    case CliCommandEnum.None:
                        await CliIO.ErrorLineAsync(stderr, NormalizedHelp().TrimEnd('\n')).ConfigureAwait(false);
                        return (int)CliExitCodeEnum.Usage;
                    case CliCommandEnum.Help:
                        await CliIO.WriteTextAsync(stdout, NormalizedHelp(), token).ConfigureAwait(false);
                        return (int)CliExitCodeEnum.Success;
                    case CliCommandEnum.Version:
                        await CliIO.WriteTextAsync(stdout, "docconv " + Version + "\n", token).ConfigureAwait(false);
                        return (int)CliExitCodeEnum.Success;
                    case CliCommandEnum.Convert:
                        return await new ConvertCommand(_ConverterFactory).RunAsync(parsed, stdin, stdout, stderr, token).ConfigureAwait(false);
                    case CliCommandEnum.Detect:
                        return await new DetectCommand(_ConverterFactory).RunAsync(parsed, stdin, stdout, stderr, token).ConfigureAwait(false);
                    case CliCommandEnum.Formats:
                        return await new FormatsCommand(_ConverterFactory).RunAsync(parsed, stdout, token).ConfigureAwait(false);
                    default:
                        await CliIO.ErrorLineAsync(stderr, "docconv: error: unknown command.").ConfigureAwait(false);
                        return (int)CliExitCodeEnum.Usage;
                }
            }
            catch (Exception ex)
            {
                await CliIO.ErrorLineAsync(stderr, "docconv: error: " + CliErrors.Message(ex)).ConfigureAwait(false);
                return (int)CliErrors.ExitCode(ex);
            }
        }

        private static string NormalizedHelp()
        {
            return HelpText.Text.Replace("\r\n", "\n");
        }

        private static async Task EmitUsageReportAsync(string[] args, Exception ex, Stream stdout, TextWriter stderr, CancellationToken token)
        {
            bool json = false;
            bool documentOnStdout = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--json" || args[i] == "--json=true") json = true;
                if ((args[i] == "-o" || args[i] == "--output") && i + 1 < args.Length && args[i + 1] == "-") documentOnStdout = true;
                if (args[i] == "--output=-") documentOnStdout = true;
            }

            if (!json) return;
            CliReport report = new CliReport();
            report.Command = args.Length > 0 && !args[0].StartsWith("-", StringComparison.Ordinal) ? args[0] : "";
            report.Error = new CliReportError { Code = "Usage", Message = CliErrors.Message(ex) };
            report.ExitCode = (int)CliExitCodeEnum.Usage;
            string text = CliReportWriter.Serialize(report);
            if (documentOnStdout) await CliIO.ErrorLineAsync(stderr, text).ConfigureAwait(false);
            else await CliIO.WriteTextAsync(stdout, text + "\n", token).ConfigureAwait(false);
        }
    }
}
