<img src="https://raw.githubusercontent.com/jchristn/DocConverter/main/assets/icon.png" alt="DocConverter" width="128" height="128" />

[![NuGet](https://img.shields.io/nuget/v/DocConverter.svg)](https://www.nuget.org/packages/DocConverter/)
[![NuGet](https://img.shields.io/nuget/v/DocConverter.Cli.svg?label=DocConverter.Cli)](https://www.nuget.org/packages/DocConverter.Cli/)

# DocConverter

DocConverter converts documents between formats inside your process. Hand it a Word file as a byte array and ask for
Markdown; hand it Markdown as a string and ask for a PDF; hand it a stream of unknown bytes, let it work out what they
are, and write the result into another stream. It reads DOCX, XLSX, PPTX, PDF, RTF, HTML, Markdown, plain text, CSV,
TSV, JSON, XML and six image formats, and writes Markdown, HTML, text, JSON, XML, CSV, TSV, DOCX, XLSX, PPTX and PDF.
There is no server, no Office install, no LibreOffice, and nothing is written to disk.

The same engine ships as `docconv`, a .NET global tool built for agents: flag driven, stdin and stdout safe for binary
data, a one line JSON report, and exit codes that say exactly what went wrong.

The library targets .NET Standard 2.0, .NET 8 and .NET 10. The library and the tool run on Windows, macOS and Linux.

## Why it exists

Agents and ingestion pipelines keep hitting the same wall. The useful content is in a DOCX, a slide deck or a PDF, the
model wants Markdown, and the answer often has to go back out as a Word document or a PDF. The usual options are a
conversion server, a headless office suite, or a stack of format libraries glued together per pair. DocConverter puts
one intermediate document model in the middle, so every reader works with every writer, and it tells you precisely
what a conversion could not carry instead of dropping it quietly.

## Install

```
dotnet add package DocConverter
```

The command line tool:

```
dotnet tool install -g DocConverter.Cli
```

From source, `install-tool.bat` on Windows or `./install-tool.sh` on macOS and Linux packs the tool and installs it
globally (both accept `net8.0` or `net10.0`; the default is .NET 10 when its SDK is present).

## Quick start

A Word document already in memory, converted to Markdown and written into a file stream:

```csharp
using DocConverter;
using DocConverter.Enums;
using DocConverter.Options;
using DocConverter.Results;

Converter converter = new Converter();
byte[] wordBytes = File.ReadAllBytes("report.docx");

using (FileStream output = new FileStream("report.md", FileMode.Create))
{
    ConversionResult result = await converter.ConvertAsync(
        wordBytes, DocumentFormatEnum.Docx, DocumentFormatEnum.Markdown, output,
        new ConversionOptions(), CancellationToken.None);

    foreach (ConversionWarning warning in result.Warnings)
        Console.WriteLine(warning);
}
```

Every input shape (string, byte array, stream) has three output forms: write into a stream you own, return a string,
or return bytes.

```csharp
// Markdown string in, PDF bytes out.
BytesConversionResult pdf = await converter.ConvertToBytesAsync(
    "# Quarterly report\n\nRevenue grew **12%**.", DocumentFormatEnum.Markdown, DocumentFormatEnum.Pdf);

// Unknown bytes in: Auto detects the format from the content.
StringConversionResult text = await converter.ConvertToStringAsync(
    mysteryBytes, DocumentFormatEnum.Auto, DocumentFormatEnum.Text);
Console.WriteLine(text.SourceFormat + " detected by " + text.DetectedSource!.Confidence);

// Files by path, formats from the extensions.
await converter.ConvertFileAsync("deck.pptx", "deck.md");
```

String input is the document itself for text formats (Markdown, HTML, JSON, XML, CSV, TSV, text, RTF). For binary
formats (DOCX, XLSX, PPTX, PDF, images) the string must be base64. `ConvertToStringAsync` returns base64 for binary
targets and sets `IsBase64`, so nobody has to guess.

Streams you pass in are never closed. Input streams are read from their current position; output streams are written
from their current position and left positioned after the data.

## Supported formats

| From \ To | Md | Html | Txt | Json | Xml | Csv | Tsv | Docx | Xlsx | Pptx | Pdf |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Text | F | F | F | F | F | P | P | F | P | F | F |
| Markdown, HTML, JSON, XML, RTF, DOCX | F | F | F | F | F | P | P | F | P | P | F |
| CSV, TSV | F | F | F | F | F | F | F | F | F | P | F |
| XLSX | F | F | F | F | F | P | P | F | F | P | F |
| PPTX | F | F | F | F | F | P | P | F | P | F | F |
| PDF | F | F | F | F | F | P | P | F | P | P | F |
| PNG, JPEG, BMP | F | F | P | F | F | P | P | F | P | F | F |
| GIF, TIFF, WebP | F | F | P | F | F | P | P | F | P | F | P |

F means everything the target can hold is carried over. P means the target keeps a documented subset (CSV keeps tables,
PPTX splits long content across slides, an image sent to plain text becomes a placeholder line) and a warning names what
was dropped. The full generated matrix, per-format details and every warning are in [docs/FORMATS.md](docs/FORMATS.md).

## Reading and writing the model

Every conversion reads into a `DocumentModel` and writes from it. You can do the two halves yourself, which is useful
for redacting, merging, or writing one document to several targets without parsing it twice.

```csharp
DocumentModel doc = await converter.ReadAsync(wordBytes, DocumentFormatEnum.Docx);

// Drop the images, rename the document, then write it twice.
doc.Blocks.RemoveAll(b => b is ImageBlock);
doc.Metadata.Title = "Internal summary";

StringConversionResult md = await converter.WriteToStringAsync(doc, DocumentFormatEnum.Markdown);
BytesConversionResult pdf = await converter.WriteToBytesAsync(doc, DocumentFormatEnum.Pdf);
```

The model is plain classes: headings, paragraphs with styled text runs and links, lists, tables with spans, code, quotes,
images and sections for pages, slides and sheets. Its JSON form is canonical: converting any document to JSON and back
reproduces it exactly. See [docs/DOCUMENT_MODEL.md](docs/DOCUMENT_MODEL.md).

## Options

`ConverterSettings` holds instance settings: size limits (`MaxInputBytes`, default 256 MB, and `MaxDecompressedBytes`
for zip based formats), nesting depth, the default options, and a logger. `ConversionOptions` holds per-call options, with
a nested object per format (`Markdown`, `Html`, `Text`, `Json`, `Xml`, `Csv`, `Docx`, `Xlsx`, `Pptx`, `Pdf`).

```csharp
ConversionOptions options = new ConversionOptions
{
    IncludeImages = false,
    LineEnding = LineEndingEnum.CrLf,
    Deterministic = true
};
options.Pdf.PageSize = PdfPageSizeEnum.A4;
options.Csv.TableSelection = TableSelectionEnum.All;
```

Three presets cover common cases: `ConversionOptions.ForLlmIngestion()` (no images, Markdown friendly),
`ForArchival()` (images embedded, deterministic output) and `Minimal()`. Every option, with its default and range, is in
[docs/OPTIONS.md](docs/OPTIONS.md).

## Warnings and results

A conversion that loses something still succeeds, and its result says what was lost:

```csharp
StringConversionResult r = await converter.ConvertToStringAsync(wordBytes, DocumentFormatEnum.Docx, DocumentFormatEnum.Text);
// r.Warnings: FormattingLost (x12): Inline styles (bold, italic and so on) have no plain text form and were dropped.
//             ImagePlaceholderEmitted (x2): Images cannot be shown in plain text ...
```

Results also carry the resolved source format, byte counts, read and write timings, content statistics (headings,
tables, images, links, characters) and the document metadata. Set `TreatWarningsAsErrors` to turn any warning into a
`ConversionWarningException` instead.

Failures are exceptions with context: `UnsupportedFormatException` (for example a legacy `.doc` file, named as such),
`DocumentReadException` for corrupt, encrypted or malformed input, `DocumentWriteException`, `InputTooLargeException`,
and `ConversionNotSupportedException` for pairs no registered reader or writer covers.

## Command line: docconv

```
docconv convert -i report.docx -o report.md
docconv convert -i report.docx -o - --to md --quiet          # Markdown to stdout, nothing else on stdout
cat page.html | docconv convert -i - -o - --from html --to pdf > page.pdf
docconv detect -i mystery.bin --json
docconv formats
```

`--from` is detected from the content when omitted, using the file extension as a hint; `--to` comes from the output
extension. Pipes carry raw bytes, so binary formats survive stdin and stdout.

### For agents

stdout carries exactly one thing: the converted document (with `-o -`) or the JSON report (with `--json` and a file
output). Everything else goes to stderr. The report is one line:

```json
{"contractVersion":1,"success":true,"command":"convert","input":{"path":"report.docx","format":"Docx","bytes":49356,"detected":true},"output":{"path":"report.md","format":"Markdown","bytes":10650},"durationMs":84,"warnings":[{"code":"FormattingLost","message":"Underline has no Markdown form and was dropped.","count":1}],"statistics":{"pages":0,"slides":0,"sheets":0,"headings":12,"paragraphs":40,"lists":3,"tables":2,"images":3,"links":5},"error":null,"exitCode":0}
```

Exit codes: 0 success, 1 read or write failure, 2 usage error, 3 unsupported format or pair, 4 I/O error (missing input,
existing output without `--overwrite`), 5 warnings under `--strict`, 6 input over `--max-input-mb`, 130 cancelled.
`--strict` is the switch to reach for when silently losing content is worse than failing. Every flag is in
[docs/CLI.md](docs/CLI.md).

## Dependency injection

```csharp
services.AddDocConverter(settings => settings.MaxInputBytes = 64 * 1024 * 1024);
// Resolves IConverter (and Converter) as a singleton.
```

`Converter` is thread safe; one instance serves concurrent conversions.

## Extending

Register your own reader or writer for any of the formats in `DocumentFormatEnum`, replacing the built-in one or adding a missing direction (for example an RTF writer):

```csharp
converter.RegisterWriter(new MyAsciiDocWriter());
```

`IDocumentReader` and `IDocumentWriter` are small interfaces over the document model. See
[docs/EXTENDING.md](docs/EXTENDING.md).

OCR is not implemented yet. The extension point is in place: `IOcrProvider`, `OcrProviderBase` and
`ConverterSettings.OcrProvider` define the contract a future OCR package will implement. Configuring a provider and
turning `OcrMode` on currently throws `NotImplementedException`, so a provider is never silently ignored.

## Observability

An `ActivitySource` and a `Meter`, both named `DocConverter`, report each conversion.

| Instrument | Type | Tags |
|---|---|---|
| `docconverter.conversions` | counter | `from`, `to`, `outcome` |
| `docconverter.conversion.duration` | histogram, ms | `from`, `to` |
| `docconverter.input.bytes` | histogram, bytes | `from` |
| `docconverter.warnings` | counter | `code` |

Nothing is ever written to the console; pass a `Logger` in `ConverterSettings` to see what happens inside.

## Limits

- No OCR yet. Scanned PDFs yield their images and a `NoTextLayer` warning; images sent to text targets become
  placeholders.
- PDF structure is inferred. Headings come from font size, tables are found only when they have ruling lines, and
  multi-column pages read line by line.
- PDF output covers Latin, Greek and Cyrillic with the embedded Liberation fonts. CJK, Arabic and emoji render blank and
  raise `GlyphsUnavailable`.
- Legacy binary Office files (`.doc`, `.xls`, `.ppt`), password protected files, OpenDocument and EPUB are recognized and
  refused. RTF is an input only.
- WebP inside DOCX and PPTX is verified in Microsoft 365; older Office releases may not display it.
- Remote resources (image URLs in HTML or Markdown) are never fetched.
- Everything happens in memory, bounded by `MaxInputBytes`.

The complete list of losses and limitations is in [docs/FORMATS.md](docs/FORMATS.md).

## Building and testing

```
dotnet build src/DocConverter.sln
dotnet run --project src/Test.Automated -f net10.0
dotnet test src/Test.Xunit
dotnet test src/Test.Nunit
```

The test suites are written once, as Touchstone descriptors in `src/Test.Shared`, and run by all three runners. They
cover every conversion pair in every output shape, checked by inspectors that parse the output independently, plus
golden files, round trips, hostile input, cancellation and concurrency. `dotnet run --project src/Test.Automated -f
net10.0 -p:DocConverterTestNetStandard=true` runs everything against the netstandard2.0 build, and
`src/Test.NetFramework` smoke tests it under .NET Framework 4.8 on Windows.

## Documentation

- [docs/API.md](docs/API.md): every public type and member
- [docs/FORMATS.md](docs/FORMATS.md): readers, writers, the conversion matrix, losses and warnings
- [docs/OPTIONS.md](docs/OPTIONS.md): every setting and option with defaults and ranges
- [docs/CLI.md](docs/CLI.md): the `docconv` tool
- [docs/DOCUMENT_MODEL.md](docs/DOCUMENT_MODEL.md): the intermediate model and its canonical JSON and XML
- [docs/EXTENDING.md](docs/EXTENDING.md): custom readers, writers and OCR providers

## Acknowledgments

The extraction logic for DOCX, XLSX, PPTX, PDF, RTF, HTML, CSV, JSON and XML started from
[DocumentAtom](https://github.com/jchristn/DocumentAtom), reshaped for a richer model and stream based reading.
DocConverter builds on DocumentFormat.OpenXml (MIT), PdfPig (Apache-2.0), Tabula (MIT), PDFsharp and MigraDoc (MIT),
HtmlAgilityPack (MIT), CsvHelper (MS-PL or Apache-2.0) and Markdig (BSD-2-Clause). PDF output embeds the Liberation Sans
and Liberation Mono fonts under the SIL Open Font License 1.1.

## License

MIT. See [LICENSE.md](LICENSE.md).
