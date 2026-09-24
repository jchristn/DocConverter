namespace DocConverter.Benchmarks
{
    using System.Globalization;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Model;

    /// <summary>
    /// Representative conversions over a small document (a page of mixed content) and a large one (a 5,000 row table and
    /// 500 paragraphs). Run with: dotnet run -c Release --project src/DocConverter.Benchmarks -- --filter *
    /// </summary>
    [MemoryDiagnoser]
    public class ConversionBenchmarks
    {
        private readonly Converter _Converter = new Converter();
        private byte[] _Markdown = new byte[0];
        private byte[] _Docx = new byte[0];
        private byte[] _Xlsx = new byte[0];
        private byte[] _Pdf = new byte[0];

        /// <summary>
        /// Document size: "Small" or "Large".
        /// </summary>
        [Params("Small", "Large")]
        public string Size { get; set; } = "Small";

        /// <summary>
        /// Build the inputs once per parameter value.
        /// </summary>
        /// <returns>Task.</returns>
        [GlobalSetup]
        public async Task SetupAsync()
        {
            DocumentModel model = Build(Size == "Large");
            _Markdown = (await _Converter.WriteToBytesAsync(model, DocumentFormatEnum.Markdown).ConfigureAwait(false)).Output;
            _Docx = (await _Converter.WriteToBytesAsync(model, DocumentFormatEnum.Docx).ConfigureAwait(false)).Output;
            _Xlsx = (await _Converter.WriteToBytesAsync(model, DocumentFormatEnum.Xlsx).ConfigureAwait(false)).Output;
            _Pdf = (await _Converter.WriteToBytesAsync(model, DocumentFormatEnum.Pdf).ConfigureAwait(false)).Output;
        }

        /// <summary>
        /// Markdown to HTML.
        /// </summary>
        /// <returns>Output length.</returns>
        [Benchmark]
        public async Task<int> MarkdownToHtml()
        {
            return (await _Converter.ConvertToBytesAsync(_Markdown, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html).ConfigureAwait(false)).Output.Length;
        }

        /// <summary>
        /// DOCX to Markdown, the common ingestion path.
        /// </summary>
        /// <returns>Output length.</returns>
        [Benchmark]
        public async Task<int> DocxToMarkdown()
        {
            return (await _Converter.ConvertToBytesAsync(_Docx, DocumentFormatEnum.Docx, DocumentFormatEnum.Markdown).ConfigureAwait(false)).Output.Length;
        }

        /// <summary>
        /// Markdown to DOCX.
        /// </summary>
        /// <returns>Output length.</returns>
        [Benchmark]
        public async Task<int> MarkdownToDocx()
        {
            return (await _Converter.ConvertToBytesAsync(_Markdown, DocumentFormatEnum.Markdown, DocumentFormatEnum.Docx).ConfigureAwait(false)).Output.Length;
        }

        /// <summary>
        /// Markdown to PDF.
        /// </summary>
        /// <returns>Output length.</returns>
        [Benchmark]
        public async Task<int> MarkdownToPdf()
        {
            return (await _Converter.ConvertToBytesAsync(_Markdown, DocumentFormatEnum.Markdown, DocumentFormatEnum.Pdf).ConfigureAwait(false)).Output.Length;
        }

        /// <summary>
        /// PDF to Markdown.
        /// </summary>
        /// <returns>Output length.</returns>
        [Benchmark]
        public async Task<int> PdfToMarkdown()
        {
            return (await _Converter.ConvertToBytesAsync(_Pdf, DocumentFormatEnum.Pdf, DocumentFormatEnum.Markdown).ConfigureAwait(false)).Output.Length;
        }

        /// <summary>
        /// XLSX to CSV.
        /// </summary>
        /// <returns>Output length.</returns>
        [Benchmark]
        public async Task<int> XlsxToCsv()
        {
            return (await _Converter.ConvertToBytesAsync(_Xlsx, DocumentFormatEnum.Xlsx, DocumentFormatEnum.Csv).ConfigureAwait(false)).Output.Length;
        }

        /// <summary>
        /// Format detection of a DOCX.
        /// </summary>
        /// <returns>Detected format name length.</returns>
        [Benchmark]
        public async Task<int> DetectDocx()
        {
            return (await _Converter.DetectFormatAsync(_Docx).ConfigureAwait(false)).RecognizedAs.Length;
        }

        private static DocumentModel Build(bool large)
        {
            DocumentModel m = new DocumentModel();
            m.Metadata.Title = "Benchmark";
            m.Blocks.Add(new HeadingBlock(1, "Benchmark document"));
            int paragraphs = large ? 500 : 12;
            for (int i = 0; i < paragraphs; i++)
            {
                if (i % 50 == 0) m.Blocks.Add(new HeadingBlock(2, "Section " + i.ToString(CultureInfo.InvariantCulture)));
                ParagraphBlock p = new ParagraphBlock();
                p.Inlines.Add(new TextInline("Paragraph " + i.ToString(CultureInfo.InvariantCulture) + " has "));
                p.Inlines.Add(new TextInline("bold", InlineStyleEnum.Bold));
                p.Inlines.Add(new TextInline(" text and enough words to wrap across a line or two in most renderers, which is typical of prose."));
                m.Blocks.Add(p);
            }

            ListBlock list = new ListBlock(ListKindEnum.Unordered);
            for (int i = 0; i < 10; i++) list.Items.Add(new ListItemBlock("Item " + i.ToString(CultureInfo.InvariantCulture)));
            m.Blocks.Add(list);

            TableBlock table = new TableBlock { HeaderRowCount = 1 };
            table.Rows.Add(new TableRow(new[] { "Id", "Name", "Amount" }));
            int rows = large ? 5000 : 20;
            for (int i = 0; i < rows; i++)
                table.Rows.Add(new TableRow(new[] { i.ToString(CultureInfo.InvariantCulture), "Row " + i.ToString(CultureInfo.InvariantCulture), (i * 1.5).ToString(CultureInfo.InvariantCulture) }));
            m.Blocks.Add(table);
            return m;
        }
    }
}
