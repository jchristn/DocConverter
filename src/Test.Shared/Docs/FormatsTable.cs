namespace Test.Shared.Docs
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Results;

    /// <summary>
    /// Renders the capability and fidelity matrix from a live Converter, so docs/FORMATS.md cannot drift from the code.
    /// The rendered block sits between the BEGIN and END markers in FORMATS.md.
    /// </summary>
    public static class FormatsTable
    {
        /// <summary>
        /// Marker opening the generated block.
        /// </summary>
        public const string BeginMarker = "<!-- BEGIN GENERATED MATRIX: regenerate with dotnet run --project src/Test.Automated -- --update-docs . -->";

        /// <summary>
        /// Marker closing the generated block.
        /// </summary>
        public const string EndMarker = "<!-- END GENERATED MATRIX -->";

        private static readonly DocumentFormatEnum[] _Targets = new DocumentFormatEnum[]
        {
            DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, DocumentFormatEnum.Text, DocumentFormatEnum.Json,
            DocumentFormatEnum.Xml, DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv, DocumentFormatEnum.Docx,
            DocumentFormatEnum.Xlsx, DocumentFormatEnum.Pptx, DocumentFormatEnum.Pdf
        };

        /// <summary>
        /// Render the generated block, markers included, with \n line endings.
        /// </summary>
        /// <param name="converter">Converter whose capabilities are rendered.</param>
        /// <returns>Markdown.</returns>
        public static string Render(Converter converter)
        {
            IReadOnlyList<SupportedConversion> pairs = converter.GetSupportedConversions();
            StringBuilder sb = new StringBuilder();
            sb.Append(BeginMarker).Append('\n').Append('\n');
            sb.Append("| From \\ To |");
            foreach (DocumentFormatEnum t in _Targets) sb.Append(' ').Append(Short(t)).Append(" |");
            sb.Append('\n').Append("|---|");
            foreach (DocumentFormatEnum t in _Targets) sb.Append("---|");
            sb.Append('\n');

            foreach (DocumentFormatEnum from in converter.GetInputFormats())
            {
                sb.Append("| ").Append(from).Append(" |");
                foreach (DocumentFormatEnum to in _Targets)
                {
                    SupportedConversion? pair = pairs.FirstOrDefault(p => p.From == from && p.To == to);
                    string cell = pair == null ? "N" : (pair.Fidelity == FidelityEnum.Full ? "F" : "P");
                    if (pair != null && pair.Fidelity == FidelityEnum.Full && pair.Notes.Length > 0) cell += "*";
                    sb.Append(' ').Append(cell).Append(" |");
                }

                sb.Append('\n');
            }

            sb.Append('\n').Append("F: full fidelity. P: projection (lossy by design, documented, warned). F*: full, with a note below. N: not supported.").Append('\n').Append('\n');
            sb.Append("Notes by pair:").Append('\n').Append('\n');
            foreach (IGrouping<string, SupportedConversion> group in pairs.Where(p => p.Notes.Length > 0).GroupBy(p => p.Notes).OrderBy(g => g.Key))
            {
                sb.Append("- ").Append(group.Key).Append(" Applies to: ");
                sb.Append(string.Join(", ", group.Select(p => p.From + " to " + p.To))).Append('.').Append('\n');
            }

            sb.Append('\n').Append(EndMarker);
            return sb.ToString();
        }

        private static string Short(DocumentFormatEnum format)
        {
            switch (format)
            {
                case DocumentFormatEnum.Markdown: return "Md";
                case DocumentFormatEnum.Text: return "Txt";
                default: return format.ToString();
            }
        }
    }
}
