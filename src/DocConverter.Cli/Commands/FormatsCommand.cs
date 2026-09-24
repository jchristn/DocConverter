namespace DocConverter.Cli.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Cli.Reporting;
    using DocConverter.Detection;
    using DocConverter.Enums;
    using DocConverter.Results;

    /// <summary>
    /// Implements "docconv formats": the readable and writable formats and the fidelity of every pair.
    /// </summary>
    public class FormatsCommand
    {
        private readonly Func<ConverterSettings, Converter> _ConverterFactory;

        /// <summary>
        /// Instantiate the command.
        /// </summary>
        /// <param name="converterFactory">Creates the converter from settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when converterFactory is null.</exception>
        public FormatsCommand(Func<ConverterSettings, Converter> converterFactory)
        {
            _ConverterFactory = converterFactory ?? throw new ArgumentNullException(nameof(converterFactory));
        }

        /// <summary>
        /// Run the command.
        /// </summary>
        /// <param name="args">Parsed arguments.</param>
        /// <param name="stdout">Standard output.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Exit code.</returns>
        public async Task<int> RunAsync(CliArguments args, Stream stdout, CancellationToken token)
        {
            using (Converter converter = _ConverterFactory(new ConverterSettings()))
            {
                IReadOnlyList<DocumentFormatEnum> inputs = converter.GetInputFormats();
                IReadOnlyList<DocumentFormatEnum> outputs = converter.GetOutputFormats();
                IReadOnlyList<SupportedConversion> pairs = converter.GetSupportedConversions();

                if (args.Json)
                {
                    CliFormatsReport report = new CliFormatsReport();
                    foreach (DocumentFormatEnum f in inputs) report.Inputs.Add(f.ToString());
                    foreach (DocumentFormatEnum f in outputs) report.Outputs.Add(f.ToString());
                    foreach (SupportedConversion pair in pairs)
                    {
                        report.Conversions.Add(new CliFormatsConversion
                        {
                            From = pair.From.ToString(),
                            To = pair.To.ToString(),
                            Fidelity = pair.Fidelity.ToString(),
                            Notes = pair.Notes
                        });
                    }

                    await CliIO.WriteTextAsync(stdout, CliReportWriter.Serialize(report) + "\n", token).ConfigureAwait(false);
                    return (int)CliExitCodeEnum.Success;
                }

                Dictionary<string, FidelityEnum> lookup = new Dictionary<string, FidelityEnum>(StringComparer.Ordinal);
                foreach (SupportedConversion pair in pairs) lookup[pair.From + ">" + pair.To] = pair.Fidelity;

                StringBuilder sb = new StringBuilder();
                sb.Append("Input formats:  ").Append(Join(inputs)).Append('\n');
                sb.Append("Output formats: ").Append(Join(outputs)).Append('\n');
                sb.Append('\n');
                sb.Append("Conversion matrix (F = full, P = projection: lossy by design, with a warning; . = not supported)\n\n");

                sb.Append("From \\ To".PadRight(10));
                foreach (DocumentFormatEnum to in outputs) sb.Append(DocumentFormatParser.GetDefaultExtension(to).PadRight(6));
                sb.Append('\n');
                foreach (DocumentFormatEnum from in inputs)
                {
                    sb.Append(DocumentFormatParser.GetDefaultExtension(from).PadRight(10));
                    foreach (DocumentFormatEnum to in outputs)
                    {
                        string cell = ".";
                        if (lookup.TryGetValue(from + ">" + to, out FidelityEnum fidelity)) cell = fidelity == FidelityEnum.Full ? "F" : "P";
                        sb.Append(cell.PadRight(6));
                    }

                    sb.Append('\n');
                }

                sb.Append("\nRun 'docconv formats --json' for per-pair notes on what each projection loses.\n");
                await CliIO.WriteTextAsync(stdout, sb.ToString(), token).ConfigureAwait(false);
                return (int)CliExitCodeEnum.Success;
            }
        }

        private static string Join(IReadOnlyList<DocumentFormatEnum> formats)
        {
            List<string> names = new List<string>();
            foreach (DocumentFormatEnum f in formats) names.Add(f.ToString());
            return string.Join(", ", names);
        }
    }
}
