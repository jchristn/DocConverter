namespace DocConverter.Options
{
    using System.Text;
    using DocConverter.Enums;
    using DocConverter.Ocr;

    /// <summary>
    /// Per-call conversion options. Every nested options object is never null: assigning null stores a default instance.
    /// An instance may be shared by concurrent conversions as long as nobody mutates it while they run.
    /// </summary>
    public class ConversionOptions
    {
        private Encoding _OutputEncoding = new UTF8Encoding(false);
        private MarkdownOptions _Markdown = new MarkdownOptions();
        private HtmlOptions _Html = new HtmlOptions();
        private TextOptions _Text = new TextOptions();
        private JsonOptions _Json = new JsonOptions();
        private XmlOptions _Xml = new XmlOptions();
        private CsvOptions _Csv = new CsvOptions();
        private DocxOptions _Docx = new DocxOptions();
        private XlsxOptions _Xlsx = new XlsxOptions();
        private PptxOptions _Pptx = new PptxOptions();
        private PdfOptions _Pdf = new PdfOptions();
        private OcrOptions _Ocr = new OcrOptions();

        /// <summary>
        /// When false, every image is dropped and the ImagesOmitted warning is raised. Default true.
        /// </summary>
        public bool IncludeImages { get; set; } = true;

        /// <summary>
        /// When true, metadata (title, author and so on) is carried into targets that support it. Default true.
        /// </summary>
        public bool IncludeMetadata { get; set; } = true;

        /// <summary>
        /// Overrides the document title in the output. Null (default) keeps the source title.
        /// </summary>
        public string? Title { get; set; } = null;

        /// <summary>
        /// Encoding of text based sources. Null (default) detects a byte order mark and otherwise uses UTF-8.
        /// Ignored for string input, which is already decoded.
        /// </summary>
        public Encoding? InputEncoding { get; set; } = null;

        /// <summary>
        /// Encoding of text based output. Default UTF-8 without a byte order mark. Never null.
        /// </summary>
        public Encoding OutputEncoding
        {
            get => _OutputEncoding;
            set => _OutputEncoding = value ?? new UTF8Encoding(false);
        }

        /// <summary>
        /// Line ending of text based output. Default Lf.
        /// </summary>
        public LineEndingEnum LineEnding { get; set; } = LineEndingEnum.Lf;

        /// <summary>
        /// When true, binary writers pin timestamps and generated identifiers so identical input yields identical bytes.
        /// Default false.
        /// </summary>
        public bool Deterministic { get; set; } = false;

        /// <summary>
        /// When true, a conversion that raised any warning throws ConversionWarningException after writing. Default false.
        /// </summary>
        public bool TreatWarningsAsErrors { get; set; } = false;

        /// <summary>
        /// When OCR runs. Default Off. Reserved: any other value with a configured provider throws NotImplementedException
        /// in this release.
        /// </summary>
        public OcrModeEnum OcrMode { get; set; } = OcrModeEnum.Off;

        /// <summary>
        /// Markdown options. Never null.
        /// </summary>
        public MarkdownOptions Markdown { get => _Markdown; set => _Markdown = value ?? new MarkdownOptions(); }

        /// <summary>
        /// HTML options. Never null.
        /// </summary>
        public HtmlOptions Html { get => _Html; set => _Html = value ?? new HtmlOptions(); }

        /// <summary>
        /// Plain text options. Never null.
        /// </summary>
        public TextOptions Text { get => _Text; set => _Text = value ?? new TextOptions(); }

        /// <summary>
        /// JSON options. Never null.
        /// </summary>
        public JsonOptions Json { get => _Json; set => _Json = value ?? new JsonOptions(); }

        /// <summary>
        /// XML options. Never null.
        /// </summary>
        public XmlOptions Xml { get => _Xml; set => _Xml = value ?? new XmlOptions(); }

        /// <summary>
        /// CSV and TSV options. Never null.
        /// </summary>
        public CsvOptions Csv { get => _Csv; set => _Csv = value ?? new CsvOptions(); }

        /// <summary>
        /// DOCX options. Never null.
        /// </summary>
        public DocxOptions Docx { get => _Docx; set => _Docx = value ?? new DocxOptions(); }

        /// <summary>
        /// XLSX options. Never null.
        /// </summary>
        public XlsxOptions Xlsx { get => _Xlsx; set => _Xlsx = value ?? new XlsxOptions(); }

        /// <summary>
        /// PPTX options. Never null.
        /// </summary>
        public PptxOptions Pptx { get => _Pptx; set => _Pptx = value ?? new PptxOptions(); }

        /// <summary>
        /// PDF options. Never null.
        /// </summary>
        public PdfOptions Pdf { get => _Pdf; set => _Pdf = value ?? new PdfOptions(); }

        /// <summary>
        /// OCR options. Never null. Reserved: OCR is not implemented in this release.
        /// </summary>
        public OcrOptions Ocr { get => _Ocr; set => _Ocr = value ?? new OcrOptions(); }

        /// <summary>
        /// Instantiate default options.
        /// </summary>
        public ConversionOptions()
        {
        }

        /// <summary>
        /// Options suited to feeding documents to a language model: images are omitted, metadata is kept, and text
        /// output uses Markdown friendly settings.
        /// </summary>
        /// <returns>A new options instance.</returns>
        public static ConversionOptions ForLlmIngestion()
        {
            ConversionOptions options = new ConversionOptions();
            options.IncludeImages = false;
            options.IncludeMetadata = true;
            options.Markdown.ImageMode = ImageModeEnum.Omit;
            options.Html.ImageMode = ImageModeEnum.Omit;
            options.Text.IncludeImagePlaceholders = false;
            options.Csv.TableSelection = TableSelectionEnum.All;
            return options;
        }

        /// <summary>
        /// Options suited to archiving: images embedded, metadata kept, deterministic output.
        /// </summary>
        /// <returns>A new options instance.</returns>
        public static ConversionOptions ForArchival()
        {
            ConversionOptions options = new ConversionOptions();
            options.IncludeImages = true;
            options.IncludeMetadata = true;
            options.Deterministic = true;
            options.Markdown.ImageMode = ImageModeEnum.DataUri;
            options.Html.ImageMode = ImageModeEnum.DataUri;
            return options;
        }

        /// <summary>
        /// Smallest reasonable output: no images, no metadata, compact JSON, HTML fragments without a stylesheet.
        /// </summary>
        /// <returns>A new options instance.</returns>
        public static ConversionOptions Minimal()
        {
            ConversionOptions options = new ConversionOptions();
            options.IncludeImages = false;
            options.IncludeMetadata = false;
            options.Json.Indented = false;
            options.Json.IncludeBinary = false;
            options.Xml.Indented = false;
            options.Xml.IncludeBinary = false;
            options.Html.Mode = HtmlOutputModeEnum.Fragment;
            options.Html.IncludeStylesheet = false;
            options.Text.IncludeImagePlaceholders = false;
            return options;
        }
    }
}
