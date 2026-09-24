namespace DocConverter.Cli
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using DocConverter.Enums;
    using DocConverter.Options;

    /// <summary>
    /// Builds ConverterSettings and ConversionOptions from an optional --options file and the command line flags.
    /// Flags always win over the file.
    /// </summary>
    public static class CliOptionsBuilder
    {
        private static readonly JsonSerializerOptions _FileOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        /// <summary>
        /// Read an options file.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>Parsed options file.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="CliUsageException">Thrown when the file is not valid options JSON.</exception>
        public static CliOptionsFile ReadFile(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("The options file was not found: " + path, path);
            string json = File.ReadAllText(path, Encoding.UTF8);
            try
            {
                CliOptionsFile? file = JsonSerializer.Deserialize<CliOptionsFile>(json, _FileOptions);
                if (file == null) throw new CliUsageException("The options file '" + path + "' is empty.");
                return file;
            }
            catch (JsonException ex)
            {
                throw new CliUsageException("The options file '" + path + "' is not valid: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Build converter settings.
        /// </summary>
        /// <param name="args">Parsed arguments.</param>
        /// <param name="file">Options file, or null.</param>
        /// <returns>Settings.</returns>
        /// <exception cref="CliUsageException">Thrown when --max-input-mb is invalid.</exception>
        public static ConverterSettings BuildSettings(CliArguments args, CliOptionsFile? file)
        {
            ConverterSettings settings = new ConverterSettings();
            int? mb = file?.MaxInputMb;
            string? flag = args.Get("--max-input-mb");
            if (flag != null) mb = ParseInt(flag, "--max-input-mb");
            if (mb.HasValue)
            {
                if (mb.Value < 1) throw new CliUsageException("--max-input-mb must be at least 1; got " + mb.Value + ".");
                long bytes = (long)mb.Value * 1048576L;
                settings.MaxInputBytes = Math.Min(bytes, int.MaxValue);
            }

            return settings;
        }

        /// <summary>
        /// Build conversion options.
        /// </summary>
        /// <param name="args">Parsed arguments.</param>
        /// <param name="file">Options file, or null.</param>
        /// <returns>Options.</returns>
        /// <exception cref="CliUsageException">Thrown when a value is not recognized.</exception>
        /// <exception cref="DocConverter.Exceptions.InvalidConversionOptionsException">Thrown when a value is out of range.</exception>
        public static ConversionOptions BuildOptions(CliArguments args, CliOptionsFile? file)
        {
            ConversionOptions o = new ConversionOptions();
            if (file != null) ApplyFile(o, file);

            if (args.Has("--no-images")) o.IncludeImages = false;
            if (args.Has("--no-metadata")) o.IncludeMetadata = false;
            if (args.Get("--title") != null) o.Title = args.Get("--title");
            if (args.Get("--input-encoding") != null) o.InputEncoding = ParseEncoding(args.Get("--input-encoding")!, "--input-encoding");
            if (args.Get("--output-encoding") != null) o.OutputEncoding = ParseEncoding(args.Get("--output-encoding")!, "--output-encoding");
            if (args.Get("--line-ending") != null) o.LineEnding = ParseLineEnding(args.Get("--line-ending")!);
            if (args.Has("--deterministic")) o.Deterministic = true;
            if (args.Has("--strict")) o.TreatWarningsAsErrors = true;
            if (args.Get("--images") != null) ApplyImages(o, args.Get("--images")!);
            if (args.Get("--csv-delimiter") != null) o.Csv.Delimiter = ParseDelimiter(args.Get("--csv-delimiter")!);
            if (args.Has("--no-header")) o.Csv.HasHeaderRow = false;
            if (args.Get("--table") != null) ApplyTable(o, args.Get("--table")!);
            if (args.Has("--html-fragment")) o.Html.Mode = HtmlOutputModeEnum.Fragment;
            if (args.Has("--html-no-css")) o.Html.IncludeStylesheet = false;
            if (args.Get("--text-wrap") != null) o.Text.WrapColumn = ParseInt(args.Get("--text-wrap")!, "--text-wrap");
            if (args.Has("--json-compact")) o.Json.Indented = false;
            if (args.Has("--json-no-binary")) o.Json.IncludeBinary = false;
            if (args.Get("--page-size") != null)
            {
                PdfPageSizeEnum size = ParsePageSize(args.Get("--page-size")!);
                o.Pdf.PageSize = size;
                o.Docx.PageSize = size;
            }

            if (args.Get("--margin") != null)
            {
                double margin = ParseDouble(args.Get("--margin")!, "--margin");
                o.Pdf.MarginPoints = margin;
                o.Docx.MarginPoints = margin;
            }

            if (args.Get("--slide-split") != null) o.Pptx.SlideSplitHeadingLevel = ParseInt(args.Get("--slide-split")!, "--slide-split");
            if (args.Has("--include-notes")) o.Pptx.IncludeNotes = true;
            if (args.Has("--include-hidden-sheets")) o.Xlsx.IncludeHiddenSheets = true;
            if (args.Has("--preserve-pages")) o.Pdf.PreservePages = true;
            return o;
        }

        /// <summary>
        /// True when images are to be written as side files next to the output.
        /// </summary>
        /// <param name="options">Options.</param>
        /// <returns>True for external images.</returns>
        public static bool UsesExternalImages(ConversionOptions options)
        {
            return options.Markdown.ImageMode == ImageModeEnum.External || options.Html.ImageMode == ImageModeEnum.External;
        }

        private static void ApplyFile(ConversionOptions o, CliOptionsFile f)
        {
            if (f.IncludeImages.HasValue) o.IncludeImages = f.IncludeImages.Value;
            if (f.IncludeMetadata.HasValue) o.IncludeMetadata = f.IncludeMetadata.Value;
            if (f.Title != null) o.Title = f.Title;
            if (f.InputEncoding != null) o.InputEncoding = ParseEncoding(f.InputEncoding, "inputEncoding");
            if (f.OutputEncoding != null) o.OutputEncoding = ParseEncoding(f.OutputEncoding, "outputEncoding");
            if (f.LineEnding != null) o.LineEnding = ParseLineEnding(f.LineEnding);
            if (f.Deterministic.HasValue) o.Deterministic = f.Deterministic.Value;
            if (f.TreatWarningsAsErrors.HasValue) o.TreatWarningsAsErrors = f.TreatWarningsAsErrors.Value;
            if (f.Images != null) ApplyImages(o, f.Images);
            if (f.HtmlMode != null)
            {
                string mode = f.HtmlMode.Trim().ToLowerInvariant();
                if (mode == "document") o.Html.Mode = HtmlOutputModeEnum.Document;
                else if (mode == "fragment") o.Html.Mode = HtmlOutputModeEnum.Fragment;
                else throw new CliUsageException("htmlMode must be document or fragment; got '" + f.HtmlMode + "'.");
            }

            if (f.HtmlIncludeStylesheet.HasValue) o.Html.IncludeStylesheet = f.HtmlIncludeStylesheet.Value;
            if (f.TextWrapColumn.HasValue) o.Text.WrapColumn = f.TextWrapColumn.Value;
            if (f.TextHeadingStyle != null) o.Text.HeadingStyle = ParseEnum<TextHeadingStyleEnum>(f.TextHeadingStyle, "textHeadingStyle");
            if (f.TextTableStyle != null) o.Text.TableStyle = ParseEnum<TextTableStyleEnum>(f.TextTableStyle, "textTableStyle");
            if (f.JsonIndented.HasValue) o.Json.Indented = f.JsonIndented.Value;
            if (f.JsonIncludeBinary.HasValue) o.Json.IncludeBinary = f.JsonIncludeBinary.Value;
            if (f.XmlIndented.HasValue) o.Xml.Indented = f.XmlIndented.Value;
            if (f.CsvDelimiter != null) o.Csv.Delimiter = ParseDelimiter(f.CsvDelimiter);
            if (f.CsvHasHeaderRow.HasValue) o.Csv.HasHeaderRow = f.CsvHasHeaderRow.Value;
            if (f.Table != null) ApplyTable(o, f.Table);
            if (f.PageSize != null)
            {
                PdfPageSizeEnum size = ParsePageSize(f.PageSize);
                o.Pdf.PageSize = size;
                o.Docx.PageSize = size;
            }

            if (f.MarginPoints.HasValue)
            {
                o.Pdf.MarginPoints = f.MarginPoints.Value;
                o.Docx.MarginPoints = f.MarginPoints.Value;
            }

            if (f.SlideSplitHeadingLevel.HasValue) o.Pptx.SlideSplitHeadingLevel = f.SlideSplitHeadingLevel.Value;
            if (f.IncludeNotes.HasValue) o.Pptx.IncludeNotes = f.IncludeNotes.Value;
            if (f.IncludeHiddenSheets.HasValue) o.Xlsx.IncludeHiddenSheets = f.IncludeHiddenSheets.Value;
            if (f.PreservePages.HasValue) o.Pdf.PreservePages = f.PreservePages.Value;
        }

        private static void ApplyImages(ConversionOptions o, string value)
        {
            ImageModeEnum mode;
            switch (value.Trim().ToLowerInvariant())
            {
                case "embed":
                    mode = ImageModeEnum.DataUri;
                    break;
                case "omit":
                    mode = ImageModeEnum.Omit;
                    o.Text.IncludeImagePlaceholders = false;
                    break;
                case "placeholder":
                    mode = ImageModeEnum.Placeholder;
                    o.Text.IncludeImagePlaceholders = true;
                    break;
                case "external":
                    mode = ImageModeEnum.External;
                    break;
                default:
                    throw new CliUsageException("--images must be embed, omit, placeholder or external; got '" + value + "'.");
            }

            o.Markdown.ImageMode = mode;
            o.Html.ImageMode = mode;
        }

        private static void ApplyTable(ConversionOptions o, string value)
        {
            string v = value.Trim().ToLowerInvariant();
            if (v == "first") o.Csv.TableSelection = TableSelectionEnum.First;
            else if (v == "all") o.Csv.TableSelection = TableSelectionEnum.All;
            else
            {
                int index = ParseInt(v, "--table");
                if (index < 0) throw new CliUsageException("--table index must be 0 or greater; got " + index + ".");
                o.Csv.TableSelection = TableSelectionEnum.Index;
                o.Csv.TableIndex = index;
            }
        }

        private static char ParseDelimiter(string value)
        {
            if (string.Equals(value, "tab", StringComparison.OrdinalIgnoreCase) || value == "\\t" || value == "\t") return '\t';
            if (value.Length != 1) throw new CliUsageException("--csv-delimiter must be a single character or 'tab'; got '" + value + "'.");
            return value[0];
        }

        private static LineEndingEnum ParseLineEnding(string value)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "lf": return LineEndingEnum.Lf;
                case "crlf": return LineEndingEnum.CrLf;
                case "platform": return LineEndingEnum.Platform;
                default: throw new CliUsageException("--line-ending must be lf, crlf or platform; got '" + value + "'.");
            }
        }

        private static PdfPageSizeEnum ParsePageSize(string value)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "a4": return PdfPageSizeEnum.A4;
                case "letter": return PdfPageSizeEnum.Letter;
                case "legal": return PdfPageSizeEnum.Legal;
                default: throw new CliUsageException("--page-size must be a4, letter or legal; got '" + value + "'.");
            }
        }

        private static Encoding ParseEncoding(string name, string option)
        {
            string n = name.Trim().ToLowerInvariant();
            if (n == "utf-8" || n == "utf8") return new UTF8Encoding(false);
            if (n == "utf-8-bom" || n == "utf8bom") return new UTF8Encoding(true);
            try
            {
                return Encoding.GetEncoding(name);
            }
            catch (ArgumentException ex)
            {
                throw new CliUsageException(option + " '" + name + "' is not a known encoding.", ex);
            }
        }

        private static TEnum ParseEnum<TEnum>(string value, string option) where TEnum : struct
        {
            if (Enum.TryParse(value, true, out TEnum parsed) && Enum.IsDefined(typeof(TEnum), parsed)) return parsed;
            throw new CliUsageException(option + " value '" + value + "' is not recognized.");
        }

        private static int ParseInt(string value, string option)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) return parsed;
            throw new CliUsageException(option + " expects a whole number; got '" + value + "'.");
        }

        private static double ParseDouble(string value, string option)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)) return parsed;
            throw new CliUsageException(option + " expects a number; got '" + value + "'.");
        }
    }
}
