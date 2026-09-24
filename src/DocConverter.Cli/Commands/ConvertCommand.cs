namespace DocConverter.Cli.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Cli.Reporting;
    using DocConverter.Detection;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocConverter.Results;

    /// <summary>
    /// Implements "docconv convert". The output file is written to a temporary file in the same directory and moved into
    /// place only after the conversion succeeds, so a failed or strict-mode conversion never leaves a partial file.
    /// </summary>
    public class ConvertCommand
    {
        private readonly Func<ConverterSettings, Converter> _ConverterFactory;

        /// <summary>
        /// Instantiate the command.
        /// </summary>
        /// <param name="converterFactory">Creates the converter from settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when converterFactory is null.</exception>
        public ConvertCommand(Func<ConverterSettings, Converter> converterFactory)
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
            Stopwatch watch = Stopwatch.StartNew();
            bool documentOnStdout = args.Output == "-";
            CliReport report = new CliReport();
            report.Input = new CliReportInput { Path = CliIO.DisplayName(args.Input, true) };
            report.Output = new CliReportOutput { Path = CliIO.DisplayName(args.Output, false) };
            ConversionResult? result = null;
            string? tempPath = null;
            List<string> sideFiles = new List<string>();
            int exitCode;

            try
            {
                if (string.IsNullOrEmpty(args.Input)) throw new CliUsageException("The convert command requires -i <path|->.");
                if (string.IsNullOrEmpty(args.Output)) throw new CliUsageException("The convert command requires -o <path|->.");

                CliOptionsFile? file = args.Get("--options") != null ? CliOptionsBuilder.ReadFile(args.Get("--options")!) : null;
                ConverterSettings settings = CliOptionsBuilder.BuildSettings(args, file);
                ConversionOptions options = CliOptionsBuilder.BuildOptions(args, file);
                if (documentOnStdout && CliOptionsBuilder.UsesExternalImages(options))
                    throw new CliUsageException("--images external writes image files next to the output file, so it cannot be used with -o -.");

                DocumentFormatEnum from = DocumentFormatEnum.Auto;
                string? fromText = args.Get("--from");
                if (fromText != null && !DocumentFormatParser.TryParse(fromText, out from))
                    throw new CliUsageException("--from '" + fromText + "' is not a known format. Run docconv formats for the list.");

                DocumentFormatEnum to;
                string? toText = args.Get("--to");
                if (toText != null)
                {
                    if (!DocumentFormatParser.TryParse(toText, out to))
                        throw new CliUsageException("--to '" + toText + "' is not a known format. Run docconv formats for the list.");
                }
                else if (documentOnStdout)
                {
                    throw new CliUsageException("--to is required when -o is - (the target cannot be inferred from a file extension).");
                }
                else
                {
                    DocumentFormatEnum? inferred = DocumentFormatParser.FromExtension(args.Output);
                    if (!inferred.HasValue)
                        throw new CliUsageException("Cannot infer the target format from '" + args.Output + "'. Pass --to.");
                    to = inferred.Value;
                }

                if (to == DocumentFormatEnum.Auto) throw new CliUsageException("--to cannot be auto.");
                report.Output.Format = to.ToString();

                using (Converter converter = _ConverterFactory(settings))
                {
                    byte[] input = await CliIO.ReadInputAsync(args.Input!, settings.MaxInputBytes, stdin, token).ConfigureAwait(false);
                    report.Input.Bytes = input.LongLength;

                    if (from == DocumentFormatEnum.Auto)
                    {
                        string? hint = args.Input == "-" ? null : args.Input;
                        DetectionResult detection = await converter.DetectFormatAsync(input, hint, token).ConfigureAwait(false);
                        if (!detection.Format.HasValue)
                            throw new UnsupportedFormatException("The input was recognized as " + detection.RecognizedAs + ", which DocConverter cannot read.");
                        from = detection.Format.Value;
                        report.Input.Detected = true;
                    }

                    report.Input.Format = from.ToString();

                    if (documentOnStdout)
                    {
                        result = await ConvertCapturingAsync(converter, input, from, to, stdout, options, token).ConfigureAwait(false);
                    }
                    else
                    {
                        string fullPath = Path.GetFullPath(args.Output!);
                        if (Directory.Exists(fullPath)) throw new CliOutputExistsException("The output path is a directory: " + args.Output);
                        if (File.Exists(fullPath) && !args.Has("--overwrite"))
                            throw new CliOutputExistsException("The output file '" + args.Output + "' already exists. Pass --overwrite to replace it.");

                        string? directory = Path.GetDirectoryName(fullPath);
                        if (string.IsNullOrEmpty(directory)) directory = Directory.GetCurrentDirectory();
                        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("The output directory does not exist: " + directory);

                        tempPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
                        using (FileStream fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                        {
                            result = await ConvertCapturingAsync(converter, input, from, to, fs, options, token).ConfigureAwait(false);
                        }

                        if (CliOptionsBuilder.UsesExternalImages(options))
                        {
                            foreach (BinaryResource resource in result.Resources)
                            {
                                string name = Path.GetFileName(string.IsNullOrEmpty(resource.FileName) ? resource.Id : resource.FileName!);
                                string sidePath = Path.Combine(directory, name);
                                if (File.Exists(sidePath) && !args.Has("--overwrite"))
                                    throw new CliOutputExistsException("The image file '" + sidePath + "' already exists. Pass --overwrite to replace it.");
                                File.WriteAllBytes(sidePath, resource.Data);
                                sideFiles.Add(sidePath);
                            }
                        }

                        File.Move(tempPath, fullPath, true);
                        tempPath = null;
                    }

                    FillFromResult(report, result, true);
                    report.Success = true;
                    exitCode = (int)CliExitCodeEnum.Success;
                }
            }
            catch (ConversionWarningException ex)
            {
                exitCode = (int)CliExitCodeEnum.Warnings;
                if (ex.Result != null) FillFromResult(report, ex.Result, documentOnStdout);
                report.Error = new CliReportError { Code = CliErrors.ErrorCode(ex), Message = CliErrors.Message(ex) };
                foreach (string side in sideFiles) TryDelete(side);
            }
            catch (Exception ex)
            {
                exitCode = (int)CliErrors.ExitCode(ex);
                report.Error = new CliReportError { Code = CliErrors.ErrorCode(ex), Message = CliErrors.Message(ex) };
                if (result != null) FillFromResult(report, result, false);
                foreach (string side in sideFiles) TryDelete(side);
            }
            finally
            {
                if (tempPath != null) TryDelete(tempPath);
            }

            watch.Stop();
            report.DurationMs = (long)Math.Round(watch.Elapsed.TotalMilliseconds);
            report.ExitCode = exitCode;
            await EmitAsync(args, report, documentOnStdout, stdout, stderr, token).ConfigureAwait(false);
            return exitCode;
        }

        private static async Task<ConversionResult> ConvertCapturingAsync(Converter converter, byte[] input, DocumentFormatEnum from, DocumentFormatEnum to, Stream output, ConversionOptions options, CancellationToken token)
        {
            return await converter.ConvertAsync(input, from, to, output, options, token).ConfigureAwait(false);
        }

        private static void FillFromResult(CliReport report, ConversionResult result, bool outputKept)
        {
            if (report.Output != null) report.Output.Bytes = outputKept ? result.BytesWritten : 0;
            report.Warnings.Clear();
            foreach (ConversionWarning warning in result.Warnings)
                report.Warnings.Add(new CliReportWarning { Code = warning.Code.ToString(), Message = warning.Message, Count = warning.Count });

            ConversionStatistics s = result.Statistics;
            report.Statistics = new CliReportStatistics
            {
                Pages = s.Pages,
                Slides = s.Slides,
                Sheets = s.Sheets,
                Headings = s.Headings,
                Paragraphs = s.Paragraphs,
                Lists = s.Lists,
                Tables = s.Tables,
                Images = s.Images,
                Links = s.Links
            };
        }

        private static async Task EmitAsync(CliArguments args, CliReport report, bool documentOnStdout, Stream stdout, TextWriter stderr, CancellationToken token)
        {
            if (report.Error != null) await CliIO.ErrorLineAsync(stderr, "docconv: error: " + report.Error.Message).ConfigureAwait(false);

            if (!args.Quiet && report.Success)
            {
                StringBuilder line = new StringBuilder();
                line.Append("docconv: ")
                    .Append(report.Input?.Path).Append(" (").Append(report.Input?.Format).Append(") -> ")
                    .Append(report.Output?.Path).Append(" (").Append(report.Output?.Format).Append("), ")
                    .Append(CliIO.FormatSize(report.Input?.Bytes ?? 0)).Append(" -> ").Append(CliIO.FormatSize(report.Output?.Bytes ?? 0)).Append(", ")
                    .Append(report.DurationMs.ToString(CultureInfo.InvariantCulture)).Append(" ms, ")
                    .Append(report.Warnings.Count.ToString(CultureInfo.InvariantCulture)).Append(report.Warnings.Count == 1 ? " warning" : " warnings");
                await CliIO.ErrorLineAsync(stderr, line.ToString()).ConfigureAwait(false);
            }

            if (!args.Quiet || report.Error != null)
            {
                foreach (CliReportWarning warning in report.Warnings)
                {
                    string count = warning.Count > 1 ? " (x" + warning.Count.ToString(CultureInfo.InvariantCulture) + ")" : "";
                    await CliIO.ErrorLineAsync(stderr, "  warning " + warning.Code + count + ": " + warning.Message).ConfigureAwait(false);
                }
            }

            if (args.Json)
            {
                string json = CliReportWriter.Serialize(report);
                if (documentOnStdout) await CliIO.ErrorLineAsync(stderr, json).ConfigureAwait(false);
                else await CliIO.WriteTextAsync(stdout, json + "\n", token).ConfigureAwait(false);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // Best effort cleanup.
            }
            catch (UnauthorizedAccessException)
            {
                // Best effort cleanup.
            }
        }
    }
}
