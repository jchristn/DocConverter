namespace DocConverter.Cli.Commands
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Cli.Reporting;
    using DocConverter.Results;

    /// <summary>
    /// Implements "docconv detect". Exit code 0 when the input is a supported format, 3 when it is not.
    /// </summary>
    public class DetectCommand
    {
        private readonly Func<ConverterSettings, Converter> _ConverterFactory;

        /// <summary>
        /// Instantiate the command.
        /// </summary>
        /// <param name="converterFactory">Creates the converter from settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when converterFactory is null.</exception>
        public DetectCommand(Func<ConverterSettings, Converter> converterFactory)
        {
            _ConverterFactory = converterFactory ?? throw new ArgumentNullException(nameof(converterFactory));
        }

        /// <summary>
        /// Run the command. Never throws; failures become exit codes.
        /// </summary>
        /// <param name="args">Parsed arguments.</param>
        /// <param name="stdin">Standard input.</param>
        /// <param name="stdout">Standard output.</param>
        /// <param name="stderr">Standard error.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Exit code.</returns>
        public async Task<int> RunAsync(CliArguments args, Stream stdin, Stream stdout, TextWriter stderr, CancellationToken token)
        {
            CliDetectReport report = new CliDetectReport();
            int exitCode;
            try
            {
                if (string.IsNullOrEmpty(args.Input)) throw new CliUsageException("The detect command requires -i <path|->.");
                ConverterSettings settings = CliOptionsBuilder.BuildSettings(args, null);
                using (Converter converter = _ConverterFactory(settings))
                {
                    byte[] input = await CliIO.ReadInputAsync(args.Input!, settings.MaxInputBytes, stdin, token).ConfigureAwait(false);
                    report.Input = new CliDetectInput { Path = CliIO.DisplayName(args.Input, true), Bytes = input.LongLength };
                    string? hint = args.Input == "-" ? null : args.Input;
                    DetectionResult detection = await converter.DetectFormatAsync(input, hint, token).ConfigureAwait(false);
                    report.Detection = new CliDetection
                    {
                        Format = detection.Format.HasValue ? detection.Format.Value.ToString() : null,
                        MediaType = detection.MediaType,
                        Extension = detection.Extension,
                        RecognizedAs = detection.RecognizedAs,
                        Confidence = detection.Confidence.ToString(),
                        Supported = detection.IsSupported
                    };
                    report.Success = detection.IsSupported;
                    exitCode = detection.IsSupported ? (int)CliExitCodeEnum.Success : (int)CliExitCodeEnum.Unsupported;
                    if (!detection.IsSupported)
                        report.Error = new CliReportError { Code = "UnsupportedFormat", Message = "The input was recognized as " + detection.RecognizedAs + ", which DocConverter cannot read." };
                }
            }
            catch (Exception ex)
            {
                exitCode = (int)CliErrors.ExitCode(ex);
                report.Error = new CliReportError { Code = CliErrors.ErrorCode(ex), Message = CliErrors.Message(ex) };
            }

            report.ExitCode = exitCode;
            if (report.Error != null) await CliIO.ErrorLineAsync(stderr, "docconv: error: " + report.Error.Message).ConfigureAwait(false);

            if (args.Json)
            {
                await CliIO.WriteTextAsync(stdout, CliReportWriter.Serialize(report) + "\n", token).ConfigureAwait(false);
            }
            else if (report.Detection != null)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Format:        ").Append(report.Detection.Format ?? "unsupported").Append('\n');
                sb.Append("Recognized as: ").Append(report.Detection.RecognizedAs).Append('\n');
                sb.Append("Media type:    ").Append(report.Detection.MediaType).Append('\n');
                sb.Append("Extension:     ").Append(report.Detection.Extension).Append('\n');
                sb.Append("Confidence:    ").Append(report.Detection.Confidence).Append('\n');
                sb.Append("Bytes:         ").Append(report.Input?.Bytes ?? 0).Append('\n');
                await CliIO.WriteTextAsync(stdout, sb.ToString(), token).ConfigureAwait(false);
            }

            return exitCode;
        }
    }
}
