# Options

DocConverter has two layers of configuration. `ConverterSettings` belongs to a `Converter` instance and holds limits,
the logger and the defaults. `ConversionOptions` is passed per call (or taken from `ConverterSettings.DefaultOptions`
when a call passes null) and holds everything that shapes one conversion, with a nested object per format.

Numeric members validate in their setters. A value outside the documented range throws
`InvalidConversionOptionsException` at the moment you assign it, not later during a conversion, so a bad configuration
fails where it is written. Object and collection members never hold null: assigning null stores a default.

## ConverterSettings

| Member | Type | Default | Range | Effect |
|---|---|---|---|---|
| `DefaultOptions` | `ConversionOptions` | new instance | not null | Used whenever a call passes null options. |
| `MaxInputBytes` | `long` | 268,435,456 (256 MB) | 1 to 2,147,483,647 | Larger input throws `InputTooLargeException`. Applies to byte arrays, the UTF-8 size of strings, and streams (checked up front for seekable streams, while reading for the rest). |
| `MaxDecompressedBytes` | `long` | 1,073,741,824 (1 GB) | at least 1 | Total declared uncompressed size of the parts in a DOCX, XLSX or PPTX. Checked before any part is parsed; the guard against zip bombs. |
| `MaxNestingDepth` | `int` | 64 | 1 to 1024 | Deepest nesting of lists, sections, HTML, JSON and XML that readers follow. Deeper content is flattened to text with `NestedDepthLimited`. |
| `DetectionBufferBytes` | `int` | 65,536 | 512 to 16,777,216 | Characters examined by the text heuristics during detection (Markdown score, CSV and TSV consistency, binary check). Signatures and zip structure are always examined in full. |
| `Logger` | `Action<SeverityEnum, string>?` | null | any | Receives debug, warning and error messages prefixed `[DocConverter]`. DocConverter never writes to the console. Exceptions from the logger are swallowed. |
| `OcrProvider` | `IOcrProvider?` | null | reserved | OCR is not implemented in 0.1.0. A provider combined with an `OcrMode` other than `Off` throws `NotImplementedException`. |

## ConversionOptions

| Member | Type | Default | Effect |
|---|---|---|---|
| `IncludeImages` | `bool` | true | False removes every image before writing and raises `ImagesOmitted`. |
| `IncludeMetadata` | `bool` | true | False clears title, author and the rest before writing, so no target carries them. |
| `Title` | `string?` | null | Overrides the document title (HTML `<title>`, DOCX, XLSX and PPTX core properties, PDF information, canonical metadata). |
| `InputEncoding` | `Encoding?` | null | Encoding of text sources without a byte order mark. Null means UTF-8 (UTF-16 without a byte order mark is still recognized). Ignored for string input, which is already text. |
| `OutputEncoding` | `Encoding` | UTF-8 without BOM | Encoding of text targets. An encoding with a preamble writes it; the XML declaration names this encoding. |
| `LineEnding` | `LineEndingEnum` | `Lf` | `Lf`, `CrLf` or `Platform` (`Environment.NewLine`) for every text target. |
| `Deterministic` | `bool` | false | Pins timestamps and generated identifiers so identical input produces identical bytes, including DOCX, XLSX, PPTX and PDF. |
| `TreatWarningsAsErrors` | `bool` | false | Any warning throws `ConversionWarningException` after writing. Output written into your stream stays written. |
| `OcrMode` | `OcrModeEnum` | `Off` | Reserved for OCR; see `ConverterSettings.OcrProvider`. |
| `Markdown`, `Html`, `Text`, `Json`, `Xml`, `Csv`, `Docx`, `Xlsx`, `Pptx`, `Pdf`, `Ocr` | options objects | new instances | The per-format sections below. |

### Presets

Three static factories return fresh, independent instances you can adjust further.

| Preset | What it sets |
|---|---|
| `ConversionOptions.ForLlmIngestion()` | `IncludeImages = false`, `IncludeMetadata = true`, Markdown and HTML `ImageMode = Omit`, text image placeholders off, CSV `TableSelection = All`. Output built for a language model: all the words and tables, no base64 image payloads. |
| `ConversionOptions.ForArchival()` | Images on, metadata on, `Deterministic = true`, Markdown and HTML images as data URIs. Output you can diff and store. |
| `ConversionOptions.Minimal()` | No images, no metadata, compact JSON and XML without binary data, HTML fragments without a stylesheet, no text image placeholders. |

## MarkdownOptions

| Member | Type | Default | Effect |
|---|---|---|---|
| `ImageMode` | `ImageModeEnum` | `Placeholder` | `Placeholder` (the default) writes `[Image: alt, PNG 96x64]` (`ImagePlaceholderEmitted`), keeping Markdown small and readable for language models. `DataUri` embeds images as base64 data URIs. `Omit` drops them (`ImagesOmitted`). `External` writes `![alt](name.png)` and returns the bytes in `ConversionResult.Resources`. |
| `TableSpanMode` | `TableSpanModeEnum` | `Repeat` | Markdown tables cannot merge cells: `Repeat` copies a spanned cell's text into every position it covers, `Empty` leaves the covered positions blank. Either raises `TableSpansFlattened`. |
| `EscapeHtml` | `bool` | true | When true, a `<` that could open a tag and an `&` that could form an entity are escaped, so text reads back unchanged. When false they pass through and Markdown renderers interpret them as HTML. |

## HtmlOptions

| Member | Type | Default | Effect |
|---|---|---|---|
| `Mode` | `HtmlOutputModeEnum` | `Document` | `Document` writes a full HTML5 page with head, title and metadata; `Fragment` writes body content only. |
| `IncludeStylesheet` | `bool` | true | A small embedded stylesheet in `Document` mode. |
| `ImageMode` | `ImageModeEnum` | `DataUri` | Same modes as Markdown, but HTML embeds by default: `<img src="data:...">`, omitted, a `<span class="docconverter-image">` placeholder, or an external file name. |

## TextOptions

| Member | Type | Default | Range | Effect |
|---|---|---|---|---|
| `HeadingStyle` | `TextHeadingStyleEnum` | `Underline` | | `Underline` puts `===` under level 1 and `---` under level 2; `Uppercase` upper-cases headings; `None` writes them as ordinary lines. |
| `TableStyle` | `TextTableStyleEnum` | `Aligned` | | `Aligned` pads columns with spaces and rules the header; `Tabs` separates cells with tabs. |
| `WrapColumn` | `int` | 0 | 0, or 20 to 1000 | Hard wraps paragraphs at this column. 0 disables wrapping. |
| `IncludeImagePlaceholders` | `bool` | true | | Images become `[Image: ...]` lines; false omits them with `ImagesOmitted`. |
| `IncludeLinkUrls` | `bool` | true | | Writes `text (url)` for links whose text differs from the URL. |
| `TableSpanMode` | `TableSpanModeEnum` | `Repeat` | | As for Markdown. |

## JsonOptions

| Member | Type | Default | Effect |
|---|---|---|---|
| `Indented` | `bool` | true | Indented output; false writes one line. |
| `IncludeBinary` | `bool` | true | False leaves resource `data` out (id, media type, size and pixel size remain) and raises `ImagesOmitted`. |

## XmlOptions

| Member | Type | Default | Effect |
|---|---|---|---|
| `Indented` | `bool` | true | Indented output. |
| `IncludeBinary` | `bool` | true | As for JSON. |
| `IncludeAttributes` | `bool` | true | Reading arbitrary XML: attributes become `@name` rows and columns. False ignores them. |

## CsvOptions

| Member | Type | Default | Range | Effect |
|---|---|---|---|---|
| `Delimiter` | `char?` | null | | Field delimiter for reading and writing. Null means comma for CSV and tab for TSV. |
| `HasHeaderRow` | `bool` | true | | Reading: the first record is the header row (`HeaderRowCount = 1`). |
| `TableSelection` | `TableSelectionEnum` | `First` | | Writing: `First`, `All` (tables separated by one empty record) or `Index`. Tables not written raise `NonTableContentDropped`. |
| `TableIndex` | `int` | 0 | 0 to 100,000 | Zero based table for `TableSelection.Index`. Beyond the last table, the write throws `DocumentWriteException`. |
| `NoTableBehavior` | `NoTableBehaviorEnum` | `ParagraphsAsRows` | | Writing a document without tables: one block per row in one column (`FormattingLost`), or `Error` to throw `DocumentWriteException`. |
| `TableSpanMode` | `TableSpanModeEnum` | `Repeat` | | As for Markdown. |

## DocxOptions

| Member | Type | Default | Range | Effect |
|---|---|---|---|---|
| `PageSize` | `PdfPageSizeEnum` | `Letter` | | Page size of written documents: `A4`, `Letter` or `Legal`. |
| `MarginPoints` | `double` | 72 | 0 to 216 | Page margin in points (72 per inch) on all sides. |
| `IncludeFootnotes` | `bool` | true | | Reading: referenced footnotes and endnotes are appended as a trailing section (`FormattingLost`). |

## XlsxOptions

| Member | Type | Default | Effect |
|---|---|---|---|
| `IncludeHiddenSheets` | `bool` | false | Reading: include hidden worksheets. |
| `DetectHeaderRow` | `bool` | true | Reading: score the table's first row to decide whether it is a header row (`HeaderRowCount = 1`). False never marks a header. Single-cell title rows above a table become paragraphs either way. |
| `IncludeNonTableContent` | `bool` | true | Writing: non-table blocks (and image placeholders) go on a leading "Document" sheet. False drops them with `NonTableContentDropped`. |
| `InferCellTypes` | `bool` | true | Writing: invariant-culture numbers, `TRUE`/`FALSE` and ISO dates become typed cells. Values with leading zeros or more than 15 digits stay text. |

## PptxOptions

| Member | Type | Default | Range | Effect |
|---|---|---|---|---|
| `IncludeNotes` | `bool` | false | | Reading: speaker notes are appended to each slide section (`NotesIncluded`). |
| `SlideSplitHeadingLevel` | `int` | 2 | 1 to 6 | Writing: a new slide starts at each heading of this level or lower; the heading becomes the slide title. |
| `MaxBlocksPerSlide` | `int` | 12 | 1 to 100 | Writing: a continuation slide starts after this many blocks. |
| `MaxTableRowsPerSlide` | `int` | 15 | 2 to 100 | Writing: longer tables continue on further slides with the header row repeated. |

## PdfOptions

| Member | Type | Default | Range | Effect |
|---|---|---|---|---|
| `PageSize` | `PdfPageSizeEnum` | `Letter` | | `A4` (595 x 842 points), `Letter` (612 x 792) or `Legal` (612 x 1008). |
| `MarginPoints` | `double` | 72 | 0 to 216 | Page margin in points. |
| `BaseFontSize` | `double` | 11 | 6 to 36 | Body font size in points; headings scale from it. |
| `HeadingSizeRatio` | `double` | 1.2 | 1.05 to 3.0 | Reading: text at least this many times the body font size is a heading (`HeadingsInferred`). Lower finds more headings, higher fewer. |
| `DetectTables` | `bool` | true | | Reading: extract ruled tables with Tabula. |
| `PreservePages` | `bool` | false | | Reading: wrap each page in a `SectionBlock` of kind `Page`. Pages are flattened otherwise; every block still carries `SourcePage`. |

## OcrOptions

Reserved for a future OCR provider; nothing reads these in 0.1.0.

| Member | Type | Default | Range |
|---|---|---|---|
| `Languages` | `List<string>` | `["eng"]` | never null |
| `MinimumConfidence` | `double` | 0.6 | 0.0 to 1.0 |
| `DetectTables` | `bool` | true | |
| `DetectLists` | `bool` | true | |

## Command line mapping

The `docconv` flags set these options. The same values can come from an `--options` JSON file, and flags always win over
the file. Out of range values exit with code 2.

| Flag | `--options` file key | Sets |
|---|---|---|
| `--no-images` | `includeImages` | `IncludeImages = false` |
| `--no-metadata` | `includeMetadata` | `IncludeMetadata = false` |
| `--title <text>` | `title` | `Title` |
| `--input-encoding <name>` | `inputEncoding` | `InputEncoding` (`utf-8`, `utf-8-bom`, or any .NET encoding name) |
| `--output-encoding <name>` | `outputEncoding` | `OutputEncoding` |
| `--line-ending <lf\|crlf\|platform>` | `lineEnding` | `LineEnding` |
| `--deterministic` | `deterministic` | `Deterministic = true` |
| `--strict` | `treatWarningsAsErrors` | `TreatWarningsAsErrors = true` |
| `--images <embed\|omit\|placeholder\|external>` | `images` | `Markdown.ImageMode` and `Html.ImageMode` (`embed` is `DataUri`); `omit` also turns `Text.IncludeImagePlaceholders` off and `placeholder` turns it on |
| `--html-fragment` | `htmlMode` (`document` or `fragment`) | `Html.Mode = Fragment` |
| `--html-no-css` | `htmlIncludeStylesheet` | `Html.IncludeStylesheet = false` |
| `--text-wrap <n>` | `textWrapColumn` | `Text.WrapColumn` |
| (none) | `textHeadingStyle` | `Text.HeadingStyle` |
| (none) | `textTableStyle` | `Text.TableStyle` |
| `--json-compact` | `jsonIndented` | `Json.Indented = false` |
| `--json-no-binary` | `jsonIncludeBinary` | `Json.IncludeBinary = false` |
| (none) | `xmlIndented` | `Xml.Indented` |
| `--csv-delimiter <char\|tab>` | `csvDelimiter` | `Csv.Delimiter` |
| `--no-header` | `csvHasHeaderRow` | `Csv.HasHeaderRow = false` |
| `--table <first\|all\|n>` | `table` | `Csv.TableSelection`, and `Csv.TableIndex` for a number |
| `--page-size <a4\|letter\|legal>` | `pageSize` | `Pdf.PageSize` and `Docx.PageSize` |
| `--margin <points>` | `marginPoints` | `Pdf.MarginPoints` and `Docx.MarginPoints` |
| `--slide-split <1-6>` | `slideSplitHeadingLevel` | `Pptx.SlideSplitHeadingLevel` |
| `--include-notes` | `includeNotes` | `Pptx.IncludeNotes = true` |
| `--include-hidden-sheets` | `includeHiddenSheets` | `Xlsx.IncludeHiddenSheets = true` |
| `--preserve-pages` | `preservePages` | `Pdf.PreservePages = true` |
| `--max-input-mb <n>` | `maxInputMb` | `ConverterSettings.MaxInputBytes = n * 1,048,576` |

Options without a flag or file key (for example `Pptx.MaxBlocksPerSlide`, `Pdf.BaseFontSize` or
`Xlsx.InferCellTypes`) keep their defaults on the command line; set them in code when you need them.
