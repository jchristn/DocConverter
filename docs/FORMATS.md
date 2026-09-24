# Formats

DocConverter reads 18 formats and writes 11. Every conversion passes through one in-memory document model, so any
reader pairs with any writer: 198 built-in pairs, all supported. Where a target cannot hold something the source has,
the conversion still succeeds, substitutes a documented placeholder or projection, and raises a named warning. Nothing
is dropped silently. If you would rather fail than lose content, set `ConversionOptions.TreatWarningsAsErrors` (CLI:
`--strict`).

This page is the reference for what each reader understands, what each writer produces, what is lost where, and what
every warning means. The matrix below is generated from the running code and a test fails if the two disagree.

## Readers

| Format | Built on | What is read |
|---|---|---|
| Text | .NET | Paragraphs split on blank lines; single line breaks kept. Encoding from the byte order mark, then `InputEncoding`, then UTF-8; UTF-16 without a byte order mark is recognized. |
| Markdown | Markdig | CommonMark plus pipe and grid tables, task lists, strikethrough, sub and superscript, `++underline++`, autolinks and YAML front matter (title, author, subject, description, keywords, language). `data:` images become resources; other image URLs stay links and are never fetched. |
| HTML | HtmlAgilityPack | Headings, paragraphs, lists (start numbers, task lists), tables (`th`, `thead`, `colspan`, `rowspan`, `caption`), `pre`/`code` with `language-` classes, `blockquote`, `hr`, images, links and inline styles. `script`, `style`, `iframe`, forms and similar elements are ignored. Title, author, description, keywords and `lang` from the head. |
| JSON | System.Text.Json | Canonical DocConverter JSON (the `"docconverter": "1"` marker) is read losslessly. Any other JSON is mapped: scalar members to a key/value table, arrays of objects to a table, arrays of scalars to a list, nesting to titled sections. |
| XML | System.Xml | Canonical DocConverter XML (`<docconverter version="1">`) is read losslessly. Any other XML is mapped: leaves and attributes to key/value rows, repeated record elements to a table, repeated leaves to a list, other elements to titled sections. DTDs are prohibited. |
| CSV, TSV | CsvHelper | One table, RFC 4180 quoting, embedded newlines, ragged rows padded, optional header row, custom delimiter. |
| RTF | DocConverter | Headings (`\outlinelevel` and heading styles), bold, italic, underline, strike, super and subscript, Unicode escapes, tables, lists, hyperlink fields, PNG and JPEG pictures, title and author. |
| DOCX | DocumentFormat.OpenXml | Headings (styles and outline levels), inline styles, hyperlinks, nested and ordered lists, tables with spans, code (monospace or code styles), quotes, images at their position, page breaks, footnotes and endnotes (as a trailing section), text boxes, core properties. |
| XLSX | DocumentFormat.OpenXml | One section per visible sheet holding a table over the used range, detected header row, numbers, booleans, dates as ISO 8601, formula cached values, merged cells as spans, images, core properties. |
| PPTX | DocumentFormat.OpenXml | One section per slide: title and subtitle as headings, shapes in reading order, bulleted and numbered lists with nesting, tables, pictures, hyperlinks, optional speaker notes. |
| PDF | PdfPig, Tabula | Text blocks in reading order, headings inferred from font size and weight, lists, ruled tables, JPEG and PNG images, document information. Pages can be kept as sections (`PreservePages`). |
| PNG, JPEG, GIF, BMP, TIFF, WebP | DocConverter | One image with its pixel size, read from the header without decoding. No text is extracted. |

## Writers

| Format | Built on | What is written |
|---|---|---|
| Markdown | DocConverter | GitHub flavored Markdown: ATX headings, nested lists, task lists, pipe tables, fenced code, quotes. Escaping round trips: text written and read back is unchanged. Images per `MarkdownOptions.ImageMode`. |
| HTML | DocConverter | HTML5 document or fragment with an optional small stylesheet. Every text node and attribute is encoded; only http, https, mailto, relative and fragment links are written. |
| Text | DocConverter | Every word, with headings underlined or upper-cased, aligned or tab separated tables, optional wrapping, link URLs in parentheses and image placeholders. |
| JSON, XML | System.Text.Json, System.Xml | The canonical document form, which reads back exactly. |
| CSV, TSV | CsvHelper | Tables only (`CsvOptions.TableSelection`), RFC 4180 quoting. |
| DOCX | DocumentFormat.OpenXml | Styles, numbering, tables with spans and repeating header rows, hyperlinks, inline images, page size and margins, core properties. Built in code; no template. |
| XLSX | DocumentFormat.OpenXml | One sheet per table with a bold, frozen header row and typed numbers, booleans and dates; other content on a Document sheet. |
| PPTX | DocumentFormat.OpenXml | 16:9 slides from a code-built theme, master and layouts; a slide per heading at or below `SlideSplitHeadingLevel`, continuation slides for long content and tables. |
| PDF | PDFsharp, MigraDoc | Flowed pages with headings, lists, tables, code, quotes, images and links, using embedded Liberation fonts on every operating system. |

RTF and the image formats are inputs only.

## Conversion matrix

<!-- BEGIN GENERATED MATRIX: regenerate with dotnet run --project src/Test.Automated -- --update-docs . -->

| From \ To | Md | Html | Txt | Json | Xml | Csv | Tsv | Docx | Xlsx | Pptx | Pdf |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Text | F | F | F | F | F | P | P | F | P | F | F |
| Markdown | F | F | F | F | F | P | P | F | P | P | F |
| Html | F | F | F | F | F | P | P | F | P | P | F |
| Json | F | F | F | F | F | P | P | F | P | P | F |
| Xml | F | F | F | F | F | P | P | F | P | P | F |
| Csv | F | F | F | F | F | F | F | F | F | P | F |
| Tsv | F | F | F | F | F | F | F | F | F | P | F |
| Rtf | F | F | F | F | F | P | P | F | P | P | F |
| Docx | F | F | F | F | F | P | P | F | P | P | F |
| Xlsx | F | F | F | F | F | P | P | F | F | P | F |
| Pptx | F | F | F | F | F | P | P | F | P | F | F |
| Pdf | F* | F* | F | F* | F* | P | P | F* | P | P | F* |
| Png | F | F | P | F | F | P | P | F | P | F | F |
| Jpeg | F | F | P | F | F | P | P | F | P | F | F* |
| Gif | F | F | P | F | F | P | P | F | P | F | P |
| Bmp | F | F | P | F | F | P | P | F | P | F | F |
| Tiff | F | F | P | F | F | P | P | F | P | F | P |
| WebP | F | F | P | F | F | P | P | F* | P | F* | P |

F: full fidelity. P: projection (lossy by design, documented, warned). F*: full, with a note below. N: not supported.

Notes by pair:

- CMYK JPEG cannot be embedded and becomes a placeholder (ImageFormatUnsupported). Applies to: Jpeg to Pdf.
- Content is split across slides; long tables continue on extra slides. Applies to: Markdown to Pptx, Html to Pptx, Json to Pptx, Xml to Pptx, Csv to Pptx, Tsv to Pptx, Rtf to Pptx, Docx to Pptx, Xlsx to Pptx, Pdf to Pptx.
- No OCR: a placeholder names the image (ImagePlaceholderEmitted). Applies to: Png to Text, Png to Csv, Png to Tsv, Png to Xlsx, Jpeg to Text, Jpeg to Csv, Jpeg to Tsv, Jpeg to Xlsx, Gif to Text, Gif to Csv, Gif to Tsv, Gif to Xlsx, Bmp to Text, Bmp to Csv, Bmp to Tsv, Bmp to Xlsx, Tiff to Text, Tiff to Csv, Tiff to Tsv, Tiff to Xlsx, WebP to Text, WebP to Csv, WebP to Tsv, WebP to Xlsx.
- PDF structure is inferred: headings from font size, ruled tables only; scanned pages have no text (HeadingsInferred, NoTextLayer). Applies to: Pdf to Markdown, Pdf to Html, Pdf to Json, Pdf to Xml, Pdf to Docx, Pdf to Pdf.
- PDFsharp cannot embed this image format: a placeholder is written (ImageFormatUnsupported). Applies to: Gif to Pdf, Tiff to Pdf, WebP to Pdf.
- Tables become sheets; other blocks and image placeholders become rows on a Document sheet (FormattingLost, ImagePlaceholderEmitted). Applies to: Text to Xlsx, Markdown to Xlsx, Html to Xlsx, Json to Xlsx, Xml to Xlsx, Rtf to Xlsx, Docx to Xlsx, Pptx to Xlsx, Pdf to Xlsx.
- Tables only. Without tables, one row per block (NonTableContentDropped, FormattingLost). Applies to: Text to Csv, Text to Tsv, Markdown to Csv, Markdown to Tsv, Html to Csv, Html to Tsv, Json to Csv, Json to Tsv, Xml to Csv, Xml to Tsv, Rtf to Csv, Rtf to Tsv, Docx to Csv, Docx to Tsv, Xlsx to Csv, Xlsx to Tsv, Pptx to Csv, Pptx to Tsv, Pdf to Csv, Pdf to Tsv.
- WebP verified in Microsoft 365; older Office releases may not display it. Applies to: WebP to Docx, WebP to Pptx.

<!-- END GENERATED MATRIX -->

JSON and XML appear once in the matrix, with the more conservative of their two readings. Canonical JSON and XML are
lossless to every target that is F for Markdown; arbitrary JSON and XML become tables, lists and sections.

## What is lost

These are the conversions that lose something by design. Each raises the warning named in the last column, so a caller
or an agent can see exactly what happened.

| Situation | Behavior | Warning |
|---|---|---|
| An image-only source (PNG, JPEG, GIF, BMP, TIFF, WebP) to Text, CSV, TSV or XLSX | One placeholder line or row, for example `[Image: sample.png, PNG 96x64]`. No text is extracted because there is no OCR yet. | `ImagePlaceholderEmitted` |
| Any image to Text, CSV or TSV, or a block image to XLSX | The same placeholder, in place of the image | `ImagePlaceholderEmitted` |
| Images inside table cells or paragraphs, to XLSX | Omitted | `ImagesOmitted` |
| GIF, TIFF, WebP or CMYK JPEG in PDF output | A bordered placeholder naming the image, its format and size. PDFsharp cannot embed these formats. | `ImageFormatUnsupported` |
| Any document to CSV or TSV | Tables only, per `CsvOptions.TableSelection`. Without tables, one row per block. | `NonTableContentDropped`, `FormattingLost` |
| Any styled document to Text | Words are kept; bold, italic and other styles are dropped; link URLs are kept in parentheses | `FormattingLost` |
| Underline to Markdown | Markdown has no underline; the text is kept | `FormattingLost` |
| Strikethrough to PDF | MigraDoc cannot draw strikethrough; the text is kept | `FormattingLost` |
| A non-tabular document to XLSX | Tables become sheets; other blocks become rows on a Document sheet; styles are flattened | `FormattingLost`, `ImagePlaceholderEmitted` |
| Long tables or dense sections to PPTX | Split across continuation slides with the header row repeated. Nothing is lost. | none |
| Characters outside the embedded font to PDF (CJK, Arabic, emoji) | Rendered blank; the text layer may omit them | `GlyphsUnavailable` |
| PDF as the source | Headings are inferred from font size; pages without a text layer produce only their images | `HeadingsInferred`, `NoTextLayer` |
| Merged table cells to Markdown, Text, CSV or TSV | Repeated or emptied per `TableSpanMode` | `TableSpansFlattened` |
| DOCX footnotes, endnotes and text boxes | Appended as a trailing section | `FormattingLost` |
| Links with other schemes (`javascript:`, `data:`, `vbscript:`) | Written as plain text | `LinkRemovedUnsafe` |

## Warning codes

Every warning carries a code, a message describing the first occurrence, and a count. Repeated warnings of one code are
merged.

| Code | Meaning | How to avoid it |
|---|---|---|
| `ImagesOmitted` | Images were left out: `IncludeImages` is false, an image mode is `Omit`, binary data was excluded from JSON or XML, or XLSX met an image inside a cell or paragraph. | Keep `IncludeImages` true and choose a target that carries images. |
| `ImageFormatUnsupported` | An image format cannot be embedded in the target (GIF, TIFF, WebP or CMYK JPEG in PDF) and a placeholder was written. | Convert the image to PNG or JPEG first, or target DOCX, PPTX, HTML or Markdown. |
| `ImagePlaceholderEmitted` | An image was written as a text placeholder because the target cannot show it, or because `ImageMode` is `Placeholder`. | Target a format that carries images. OCR support is planned. |
| `FormattingLost` | Inline styles, links or layout the target cannot express were flattened. | Target Markdown, HTML, DOCX or PDF. |
| `TablesFlattened` | Tables were rendered as plain rows or text. | Target a format with tables. |
| `TableSpansFlattened` | Merged cells were repeated or emptied. | Target HTML, DOCX, XLSX, PPTX or PDF, or pick `TableSpanMode`. |
| `NonTableContentDropped` | Content outside the written tables was dropped, because the target keeps tables only or `IncludeNonTableContent` is false. | Use `TableSelection.All`, or target a document format. |
| `NestedDepthLimited` | Nesting deeper than `MaxNestingDepth` was flattened to text. | Raise `ConverterSettings.MaxNestingDepth`. |
| `LinkRemovedUnsafe` | A link with a disallowed scheme was written as plain text. | None; this is a safety rule. |
| `HeadingsInferred` | Headings were inferred from font size or weight rather than declared structure (PDF). | Tune `PdfOptions.HeadingSizeRatio`. |
| `EncryptedContentSkipped` | Encrypted or protected content could not be read and was skipped. | Remove the protection before converting. |
| `NotesIncluded` | Speaker notes were included as extra content. | Set `PptxOptions.IncludeNotes` to false. |
| `ContentTruncated` | Content was cut to fit a configured limit. | Raise the limit named in the message. |
| `GlyphsUnavailable` | Characters outside the embedded PDF font were rendered blank. | Target DOCX or HTML for CJK, Arabic and emoji. |
| `NoTextLayer` | A PDF page had no extractable text; it is probably scanned. | OCR support is planned. |
| `UnknownElementSkipped` | An element the reader does not understand was skipped (for example WMF pictures in RTF). | None. |

## Known limitations

Some losses cannot be detected while converting, so they have no warning. They are listed here so nobody finds them by
surprise.

- **PDF tables without ruling lines** are read as paragraphs. Tabula finds ruled tables reliably and misses unruled ones.
- **Multi-column PDF pages** are read line by line across the columns.
- **Scanned PDFs** yield their page images and `NoTextLayer`; the text needs OCR, which is planned as a separate package.
- **DOCX** drops the language of code blocks, does not read headers, footers or comments, and reads a paragraph set
  entirely in a monospace font as code.
- **RTF** has no quote structure, so quotes come through as italic paragraphs; footnotes are skipped.
- **PPTX output** estimates text height, so very long text can overflow a slide visually. All content is present.
- **WebP in DOCX and PPTX** displays in Microsoft 365; older Office releases and some viewers may not show it.
- **Legacy binary Office files** (`.doc`, `.xls`, `.ppt`), password protected Office files, OpenDocument, EPUB and
  archives are recognized and refused with a message naming the format.
- **PDF rendering is serialized** inside the process, because PDFsharp's shared state is not safe under concurrent
  renders. Parallel conversions to PDF are correct but run one render at a time.
- **Everything happens in memory.** Input is capped by `ConverterSettings.MaxInputBytes` (256 MB by default) and zip
  based formats by `MaxDecompressedBytes` (1 GB).

## Platforms

The library and the `docconv` tool run on Windows, macOS and Linux. Nothing depends on system fonts, system imaging or
native libraries: PDF output uses embedded Liberation fonts, and images are handled from their headers. CI runs every
test on all three operating systems, on .NET 8 and .NET 10, and a smoke test runs the netstandard2.0 build under .NET
Framework 4.8.

## Third-party components

Every dependency is permissively licensed. Resolved on 2026-09-24.

| Package | Version | License | Used for |
|---|---|---|---|
| DocumentFormat.OpenXml, DocumentFormat.OpenXml.Framework | 3.5.1 | MIT | DOCX, XLSX and PPTX |
| PdfPig | 0.1.16 | Apache-2.0 | PDF reading |
| Tabula | 1.0.1 | MIT | PDF tables |
| PDFsharp, PDFsharp-MigraDoc | 6.2.4 | MIT | PDF writing |
| HtmlAgilityPack | 1.12.4 | MIT | HTML reading |
| CsvHelper | 33.1.0 | MS-PL OR Apache-2.0 | CSV and TSV |
| Markdig | 1.4.0 | BSD-2-Clause | Markdown reading |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | MIT | `AddDocConverter()` |
| Microsoft.Extensions.Logging.Abstractions, System.IO.Packaging, System.Security.Cryptography.Pkcs | 8.0.x | MIT | Transitive (PDFsharp, OpenXml) |
| System.Text.Json, Microsoft.Bcl.AsyncInterfaces, System.Diagnostics.DiagnosticSource, System.Memory and their dependencies | 9.0.10, 4.6.3 | MIT | netstandard2.0 only |
| Liberation Sans and Liberation Mono 2.1.5 (embedded fonts) | | SIL Open Font License 1.1 | PDF output; the license ships in the package as `fonts/OFL.txt` |
