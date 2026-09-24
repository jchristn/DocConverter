# API reference

This page covers every public type in the `DocConverter` package, grouped by namespace. Signatures are taken from the
source; defaults and ranges are the ones the code enforces. For option details see [OPTIONS.md](OPTIONS.md), for the
model see [DOCUMENT_MODEL.md](DOCUMENT_MODEL.md), and for what each conversion keeps or loses see
[FORMATS.md](FORMATS.md).

## Contents

- [DocConverter](#docconverter): `Converter`, `IConverter`, `ConverterSettings`, `ConversionContext`
- [DocConverter.Enums](#docconverterenums)
- [DocConverter.Options](#docconverteroptions)
- [DocConverter.Results](#docconverterresults)
- [DocConverter.Exceptions](#docconverterexceptions)
- [DocConverter.Model](#docconvertermodel) and [DocConverter.Model.Serialization](#docconvertermodelserialization)
- [DocConverter.Readers and DocConverter.Writers](#docconverterreaders-and-docconverterwriters)
- [DocConverter.Detection](#docconverterdetection)
- [DocConverter.Observability](#docconverterobservability)
- [DocConverter.DependencyInjection](#docconverterdependencyinjection)
- [DocConverter.Ocr](#docconverterocr)

## DocConverter

### Converter

`Converter` is the entry point. It owns a registry of readers and writers, runs the read, transform and write pipeline,
and returns a result describing what happened. It implements `IConverter` and `IDisposable`.

```csharp
public class Converter : IConverter, IDisposable
{
    public Converter();
    public Converter(ConverterSettings? settings);   // null uses defaults

    public ConverterSettings Settings { get; }       // never null
}
```

**Thread safety.** One instance serves any number of concurrent conversions. `RegisterReader` and `RegisterWriter` may
be called while conversions run; the registry is guarded by a `ReaderWriterLockSlim`. Changing `Settings` members while
conversions run is not safe. PDF rendering is serialized inside the process because PDFsharp's shared state is not
safe under concurrent renders, so parallel PDF conversions are correct but render one at a time.

**Disposal.** `Dispose()` releases the registry lock and may be called more than once. Converters are cheap, so a
long-lived singleton is the usual pattern.

#### Converting

Every input shape (string, byte array, stream) has three output forms: write into a caller-owned stream, return a
string, or return bytes.

```csharp
Task<ConversionResult> ConvertAsync(string input, DocumentFormatEnum from, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default);
Task<ConversionResult> ConvertAsync(byte[] input, DocumentFormatEnum from, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default);
Task<ConversionResult> ConvertAsync(Stream input, DocumentFormatEnum from, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default);

Task<StringConversionResult> ConvertToStringAsync(string input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);
Task<StringConversionResult> ConvertToStringAsync(byte[] input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);
Task<StringConversionResult> ConvertToStringAsync(Stream input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);

Task<BytesConversionResult> ConvertToBytesAsync(string input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);
Task<BytesConversionResult> ConvertToBytesAsync(byte[] input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);
Task<BytesConversionResult> ConvertToBytesAsync(Stream input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);

Task<ConversionResult> ConvertFileAsync(string inputPath, string outputPath, DocumentFormatEnum from = DocumentFormatEnum.Auto, DocumentFormatEnum to = DocumentFormatEnum.Auto, bool overwrite = false, ConversionOptions? options = null, CancellationToken token = default);
```

`from` may be `Auto`, which detects the format from the content. `to` may never be `Auto`, except in
`ConvertFileAsync`, where `Auto` takes the target from the output file's extension. A null `options` uses
`Settings.DefaultOptions`.

A Word document already in memory, written into a file stream:

```csharp
Converter converter = new Converter();
byte[] wordBytes = File.ReadAllBytes("report.docx");

using (FileStream output = new FileStream("report.md", FileMode.Create))
{
    ConversionResult result = await converter.ConvertAsync(
        wordBytes, DocumentFormatEnum.Docx, DocumentFormatEnum.Markdown, output, null, token);
}
```

Markdown text in, PDF bytes out:

```csharp
BytesConversionResult pdf = await converter.ConvertToBytesAsync(
    "# Status\n\nAll systems **green**.", DocumentFormatEnum.Markdown, DocumentFormatEnum.Pdf);
File.WriteAllBytes("status.pdf", pdf.Output);
```

A stream of unknown content, returned as plain text:

```csharp
using (Stream upload = request.Body)
{
    StringConversionResult text = await converter.ConvertToStringAsync(
        upload, DocumentFormatEnum.Auto, DocumentFormatEnum.Text, null, token);
    Console.WriteLine(text.SourceFormat + ": " + text.Output.Length + " characters");
}
```

Files by path:

```csharp
ConversionResult r = await converter.ConvertFileAsync("deck.pptx", "deck.md");
await converter.ConvertFileAsync("deck.pptx", "deck.pdf", overwrite: true);
```

**String input.** For text based source formats (Text, Markdown, Html, Json, Xml, Csv, Tsv, Rtf) the string is the
document. For binary formats (Docx, Xlsx, Pptx, Pdf and the image formats) the string must be base64; a
`data:...;base64,` prefix is accepted. Invalid base64 throws `DocumentReadException`. With `Auto`, a string that is valid
base64 of a binary document is recognized as that document; otherwise the text itself is classified.

**String output.** `ConvertToStringAsync` returns the text for text based targets, decoded with
`ConversionOptions.OutputEncoding`, and base64 for binary targets (Docx, Xlsx, Pptx, Pdf), with
`StringConversionResult.IsBase64` set.

**Byte output.** `ConvertToBytesAsync` returns the file bytes. Text targets are encoded with `OutputEncoding` (UTF-8
without a byte order mark by default).

**Streams.** Streams passed in are never closed or disposed. An input stream is read from its current position to its
end; non-seekable streams work and are buffered in memory, bounded by `Settings.MaxInputBytes`. An output stream must be
writable (it need not be seekable); it is written from its current position, flushed, and left open and positioned after
the written data. Output is produced in memory first and copied only after the writer succeeds, so a failed conversion
writes nothing to your stream.

**ConvertFileAsync** refuses to replace an existing output unless `overwrite` is true, detects the source from the
content with the input file name as a hint, and opens the output file only after the conversion succeeded.

**Exceptions for the Convert family:**

| Exception | When |
|---|---|
| `ArgumentNullException` | `input`, `output` or a path is null |
| `ArgumentException` | `to` is `Auto`; the output stream is not writable; the input stream is not readable; `ConvertFileAsync` with `to = Auto` and an unknown output extension |
| `UnsupportedFormatException` | `from` is `Auto` and the content is unrecognized or recognized but unsupported (the message names what it is, for example a legacy `.doc`) |
| `ConversionNotSupportedException` | No registered reader or writer covers the pair (only possible with caller-registered formats) |
| `InputTooLargeException` | Input over `MaxInputBytes`, or zip parts over `MaxDecompressedBytes` |
| `DocumentReadException` | Corrupt, truncated, encrypted or malformed input; invalid base64 string input |
| `DocumentWriteException` | The writer failed, for example `CsvOptions.TableIndex` beyond the last table |
| `ConversionWarningException` | `TreatWarningsAsErrors` is set and warnings were raised. Stream output has already been written |
| `InvalidConversionOptionsException` | Raised by option setters when a value is out of range |
| `NotImplementedException` | An OCR provider is configured and `OcrMode` is not `Off` (see [DocConverter.Ocr](#docconverterocr)) |
| `OperationCanceledException` | The token was cancelled |
| `FileNotFoundException`, `IOException` | `ConvertFileAsync` only: missing input, existing output without `overwrite` |

#### Reading and writing separately

```csharp
Task<DocumentModel> ReadAsync(string input, DocumentFormatEnum from, ConversionOptions? options = null, CancellationToken token = default);
Task<DocumentModel> ReadAsync(byte[] input, DocumentFormatEnum from, ConversionOptions? options = null, CancellationToken token = default);
Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum from, ConversionOptions? options = null, CancellationToken token = default);

Task<ConversionResult> WriteAsync(DocumentModel document, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default);
Task<StringConversionResult> WriteToStringAsync(DocumentModel document, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);
Task<BytesConversionResult> WriteToBytesAsync(DocumentModel document, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);
```

`ReadAsync` returns the model exactly as the reader produced it. The write methods never modify the document you pass:
they copy it first, then apply the option driven changes (`IncludeImages`, `IncludeMetadata`, `Title`, and the removal of
redundant bold from headings and header cells) before writing. A write result's `SourceFormat` is `Auto` because no source
was read. The same input, string and stream rules as the Convert family apply, and so do the exceptions (reading
methods cannot throw the write-side ones and vice versa).

```csharp
DocumentModel doc = await converter.ReadAsync(wordBytes, DocumentFormatEnum.Docx);
doc.Blocks.RemoveAll(b => b is ImageBlock);
doc.Metadata.Title = "Internal summary";

StringConversionResult md = await converter.WriteToStringAsync(doc, DocumentFormatEnum.Markdown);
BytesConversionResult pdf = await converter.WriteToBytesAsync(doc, DocumentFormatEnum.Pdf);
```

#### Detection

```csharp
Task<DetectionResult> DetectFormatAsync(byte[] input, string? fileNameHint = null, CancellationToken token = default);
Task<DetectionResult> DetectFormatAsync(Stream input, string? fileNameHint = null, CancellationToken token = default);
Task<DetectionResult> DetectFormatAsync(string input, string? fileNameHint = null, CancellationToken token = default);
```

Binary signatures decide first (PDF, images, RTF), then structure (Office Open XML content types, OLE2 stream names,
zip manifests, a JSON or XML parse), then text heuristics (HTML markers, consistent CSV or TSV field counts, a Markdown
score). The file name hint only breaks ties between ambiguous text formats; it never overrides a signature. Detection
of a seekable stream restores its position; a non-seekable stream is consumed. `DetectFormatAsync` throws
`ArgumentNullException`, `ArgumentException` (unreadable stream) and `InputTooLargeException`; an unsupported format is
not an exception, it is a result with `Format == null`.

```csharp
DetectionResult d = await converter.DetectFormatAsync(bytes, "upload.bin");
if (!d.IsSupported) Console.WriteLine("Cannot read: " + d.RecognizedAs);   // for example "legacy Word 97-2003 document (.doc)"
else Console.WriteLine(d.Format + " (" + d.Confidence + ")");
```

#### Capabilities and extension

```csharp
bool CanConvert(DocumentFormatEnum from, DocumentFormatEnum to);
IReadOnlyList<SupportedConversion> GetSupportedConversions();
IReadOnlyList<DocumentFormatEnum> GetInputFormats();
IReadOnlyList<DocumentFormatEnum> GetOutputFormats();
void RegisterReader(IDocumentReader reader);
void RegisterWriter(IDocumentWriter writer);
```

`CanConvert` is true when a registered reader covers `from` and a registered writer covers `to`. `Auto` as the source is
true whenever any reader is registered; `Auto` as the target is always false. `GetSupportedConversions` lists every
reader format times every writer format with its fidelity and notes (198 pairs with the built-ins). Registration
replaces any reader or writer already registered for the same formats; a reader or writer that declares no formats or
declares `Auto` is rejected with `ArgumentException`.

```csharp
foreach (SupportedConversion pair in converter.GetSupportedConversions())
    if (pair.Fidelity == FidelityEnum.Projection)
        Console.WriteLine(pair + ": " + pair.Notes);
```

### IConverter

`IConverter` declares every public member listed above for `Converter` (all Convert, Read, Write, Detect, capability and
registration members, plus `Settings`), so callers can depend on the interface and substitute a test double.
`AddDocConverter` registers the same instance for `IConverter` and `Converter`.

### ConverterSettings

Instance settings shared by every conversion a converter performs. Configure before use.

| Member | Type | Default | Range |
|---|---|---|---|
| `DefaultOptions` | `ConversionOptions` | new instance | never null |
| `MaxInputBytes` | `long` | 268,435,456 (256 MB) | 1 to 2,147,483,647 |
| `MaxDecompressedBytes` | `long` | 1,073,741,824 (1 GB) | at least 1 |
| `MaxNestingDepth` | `int` | 64 | 1 to 1024 |
| `DetectionBufferBytes` | `int` | 65,536 | 512 to 16,777,216 |
| `Logger` | `Action<SeverityEnum, string>?` | null | any |
| `OcrProvider` | `IOcrProvider?` | null | reserved |

Out of range values throw `InvalidConversionOptionsException`. Logger messages are prefixed `[DocConverter]`; exceptions
thrown by the logger are swallowed. See [OPTIONS.md](OPTIONS.md) for what each setting does.

### ConversionContext

Per-conversion state handed to readers and writers. You only use it when implementing a reader or writer
([EXTENDING.md](EXTENDING.md)).

```csharp
public class ConversionContext
{
    public ConversionContext(ConverterSettings settings);          // ArgumentNullException when null

    public DocumentFormatEnum SourceFormat { get; }                // never Auto once reading starts
    public DocumentFormatEnum TargetFormat { get; }
    public long MaxInputBytes { get; }
    public long MaxDecompressedBytes { get; }
    public int MaxNestingDepth { get; }
    public IReadOnlyList<ConversionWarning> Warnings { get; }      // one entry per code
    public IReadOnlyList<BinaryResource> OutputResources { get; }  // side files for the caller
    public string? SourceText { get; }                             // decoded string input for text formats, else null
    public string? SourceFileName { get; }                         // file name hint, when known

    public void AddWarning(WarningCodeEnum code, string message);  // repeated codes increment Count
    public void AddOutputResource(BinaryResource resource);
    public void Log(SeverityEnum severity, string message);
}
```

A context belongs to one conversion and is not thread safe.

## DocConverter.Enums

| Enum | Values |
|---|---|
| `DocumentFormatEnum` | `Auto` (source only), `Text`, `Markdown`, `Html`, `Json`, `Xml`, `Csv`, `Tsv`, `Rtf` (input only), `Docx`, `Xlsx`, `Pptx`, `Pdf`, `Png`, `Jpeg`, `Gif`, `Bmp`, `Tiff`, `WebP` (images are inputs only) |
| `DetectionConfidenceEnum` | `Signature`, `Structure`, `Heuristic`, `ExtensionHint` |
| `FidelityEnum` | `Full`, `Projection` |
| `WarningCodeEnum` | `ImagesOmitted`, `ImageFormatUnsupported`, `ImagePlaceholderEmitted`, `FormattingLost`, `TablesFlattened`, `TableSpansFlattened`, `NonTableContentDropped`, `NestedDepthLimited`, `LinkRemovedUnsafe`, `HeadingsInferred`, `EncryptedContentSkipped`, `NotesIncluded`, `ContentTruncated`, `GlyphsUnavailable`, `NoTextLayer`, `UnknownElementSkipped` |
| `SeverityEnum` | `Debug`, `Info`, `Warn`, `Error` |
| `InlineStyleEnum` (flags) | `None = 0`, `Bold = 1`, `Italic = 2`, `Underline = 4`, `Strikethrough = 8`, `Code = 16`, `Superscript = 32`, `Subscript = 64` |
| `ListKindEnum` | `Unordered`, `Ordered`, `Task` |
| `SectionKindEnum` | `Generic`, `Page`, `Slide`, `Sheet` |
| `TextAlignmentEnum` | `Default`, `Left`, `Center`, `Right`, `Justify` |
| `ImageModeEnum` | `DataUri`, `Omit`, `Placeholder`, `External` |
| `LineEndingEnum` | `Lf`, `CrLf`, `Platform` |
| `TableSelectionEnum` | `First`, `All`, `Index` |
| `NoTableBehaviorEnum` | `ParagraphsAsRows`, `Error` |
| `TableSpanModeEnum` | `Repeat`, `Empty` |
| `HtmlOutputModeEnum` | `Document`, `Fragment` |
| `PdfPageSizeEnum` | `A4`, `Letter`, `Legal` |
| `TextHeadingStyleEnum` | `Underline`, `Uppercase`, `None` |
| `TextTableStyleEnum` | `Aligned`, `Tabs` |
| `OcrModeEnum` | `Off`, `ImagesOnly`, `PagesWithoutText`, `All` (reserved) |

The meaning of every warning code, and how to avoid it, is in [FORMATS.md](FORMATS.md#warning-codes).

## DocConverter.Options

`ConversionOptions` is the per-call options object; its nested members are never null (assigning null stores a default
instance). One instance may be shared by concurrent conversions as long as nobody mutates it while they run.

```csharp
public class ConversionOptions
{
    public bool IncludeImages { get; set; }              // true
    public bool IncludeMetadata { get; set; }            // true
    public string? Title { get; set; }                   // null keeps the source title
    public Encoding? InputEncoding { get; set; }         // null: byte order mark, then UTF-8
    public Encoding OutputEncoding { get; set; }         // UTF-8 without BOM
    public LineEndingEnum LineEnding { get; set; }       // Lf
    public bool Deterministic { get; set; }              // false
    public bool TreatWarningsAsErrors { get; set; }      // false
    public OcrModeEnum OcrMode { get; set; }             // Off

    public MarkdownOptions Markdown { get; set; }
    public HtmlOptions Html { get; set; }
    public TextOptions Text { get; set; }
    public JsonOptions Json { get; set; }
    public XmlOptions Xml { get; set; }
    public CsvOptions Csv { get; set; }
    public DocxOptions Docx { get; set; }
    public XlsxOptions Xlsx { get; set; }
    public PptxOptions Pptx { get; set; }
    public PdfOptions Pdf { get; set; }
    public OcrOptions Ocr { get; set; }                  // in DocConverter.Ocr

    public static ConversionOptions ForLlmIngestion();
    public static ConversionOptions ForArchival();
    public static ConversionOptions Minimal();
}
```

The per-format classes (`MarkdownOptions`, `HtmlOptions`, `TextOptions`, `JsonOptions`, `XmlOptions`, `CsvOptions`,
`DocxOptions`, `XlsxOptions`, `PptxOptions`, `PdfOptions`) are documented member by member, with defaults and ranges, in
[OPTIONS.md](OPTIONS.md).

## DocConverter.Results

Failures are exceptions; a returned result means the conversion succeeded, possibly with warnings.

```csharp
public class ConversionResult
{
    public DocumentFormatEnum SourceFormat { get; }        // resolved; Auto only for Write* results
    public DocumentFormatEnum TargetFormat { get; }
    public DetectionResult? DetectedSource { get; }        // set when the source was Auto
    public long BytesRead { get; }
    public long BytesWritten { get; }
    public DateTime StartedUtc { get; }
    public DateTime CompletedUtc { get; }
    public double TotalMs { get; }
    public double ReadMs { get; }
    public double WriteMs { get; }
    public IReadOnlyList<ConversionWarning> Warnings { get; }   // one per code
    public ConversionStatistics Statistics { get; }
    public DocumentMetadata Metadata { get; }
    public IReadOnlyList<BinaryResource> Resources { get; }     // side files, for ImageMode.External
    public bool HasWarnings { get; }
}

public class StringConversionResult : ConversionResult
{
    public string Output { get; }      // text, or base64 for binary targets
    public bool IsBase64 { get; }
}

public class BytesConversionResult : ConversionResult
{
    public byte[] Output { get; }
}
```

`ConversionWarning` has `Code` (`WarningCodeEnum`), `Message` (the first occurrence) and `Count` (at least 1), and
`ToString()` gives `Code (xN): Message`.

`ConversionStatistics` counts what passed through, taken from the model after reading: `Pages`, `Slides`, `Sheets`,
`Headings`, `Paragraphs` (including those inside list items and table cells), `Lists` (including nested lists),
`ListItems`, `Tables`, `TableRows`, `TableCells`, `Images` (block and inline), `Links` and `Characters` (`long`).

`DetectionResult` has `Format` (`DocumentFormatEnum?`, null when unsupported), `MediaType`, `Extension` (no dot),
`RecognizedAs` (a description, including unsupported formats), `Confidence` and `IsSupported`.

`SupportedConversion` has `From`, `To`, `Fidelity` and `Notes` (empty for most `Full` pairs).

With `ImageMode.External`, the Markdown and HTML writers reference images by file name and return the bytes in
`Resources`, each with a unique `FileName`; saving them next to the output is the caller's job (the CLI does it for you).

## DocConverter.Exceptions

Every domain exception derives from `DocConverterException : Exception` and has `(string message)` and
`(string message, Exception innerException)` constructors.

| Type | Extra members | Thrown when |
|---|---|---|
| `UnsupportedFormatException` | | Detection fails, or finds a format DocConverter cannot read |
| `ConversionNotSupportedException` | | No registered reader or writer covers a pair |
| `DocumentReadException` | | The input is corrupt, truncated, encrypted or malformed; the parser's exception is the `InnerException` |
| `DocumentWriteException` | | The output cannot be produced |
| `InputTooLargeException` | `long LimitBytes`; constructor `(string message, long limitBytes)` | Input exceeds `MaxInputBytes`, or zip parts exceed `MaxDecompressedBytes` |
| `InvalidConversionOptionsException` | | An option or setting is out of range |
| `ConversionWarningException` | `ConversionResult? Result`; constructor `(string message, ConversionResult result)` | Warnings were raised under `TreatWarningsAsErrors` |

Exceptions from a reader or writer that are not `DocConverterException`, `OperationCanceledException` or
`NotImplementedException` are wrapped in `DocumentReadException` or `DocumentWriteException` with the original as the
inner exception.

## DocConverter.Model

The intermediate document. Collections are never null: assigning null stores an empty collection. The full description,
with invariants and the canonical forms, is in [DOCUMENT_MODEL.md](DOCUMENT_MODEL.md).

```csharp
public class DocumentModel
{
    public DocumentMetadata Metadata { get; set; }
    public List<Block> Blocks { get; set; }
    public Dictionary<string, BinaryResource> Resources { get; set; }   // ordinal keys
    public string AddResource(BinaryResource resource);                 // assigns "img1", "img2" ... when Id is empty
}
```

| Type | Members |
|---|---|
| `DocumentMetadata` | `Title`, `Subject`, `Author`, `Keywords`, `Description`, `Language` (all `string?`), `CreatedUtc`, `ModifiedUtc` (`DateTime?`) |
| `BinaryResource` | `Id`, `MediaType` (default `application/octet-stream`), `Data` (`byte[]`), `FileName`, `PixelWidth`, `PixelHeight` |
| `Block` (abstract) | `Id`, `SourcePage` (`int?`), `SourceSheet` (`string?`), `SourceSlide` (`int?`) |
| `SectionBlock` | `Kind` (`SectionKindEnum`), `Title`, `Blocks`; constructors `()` and `(SectionKindEnum kind, string? title)` |
| `HeadingBlock` | `Level` (clamped to 1..6), `Inlines`; constructors `()` and `(int level, string? text)` |
| `ParagraphBlock` | `Inlines`, `Alignment`; constructors `()`, `(string? text)`, `(IEnumerable<Inline>? inlines)` |
| `ListBlock` | `Kind` (`ListKindEnum`), `Start` (default 1, negatives stored as 0), `Items`; constructors `()` and `(ListKindEnum kind)` |
| `ListItemBlock` | `Blocks`, `Checked` (`bool?`); constructors `()` and `(string? text)` |
| `TableBlock` | `Rows`, `HeaderRowCount`, `Caption`, `ColumnAlignments`, `ColumnCount` (read only: widest row counting spans) |
| `TableRow` | `Cells`; constructors `()` and `(IEnumerable<string?> texts)` (not a `Block`) |
| `TableCell` | `Blocks`, `ColumnSpan`, `RowSpan` (both at least 1), `IsHeader`; constructors `()` and `(string? text)` (not a `Block`) |
| `CodeBlock` | `Language`, `Text`; constructors `()` and `(string? text, string? language)` |
| `QuoteBlock` | `Blocks` |
| `ImageBlock` | `ResourceId`, `AltText`, `Caption`, `Width`, `Height` (points, `double?`); constructors `()` and `(string resourceId, string? altText)` |
| `ThematicBreakBlock`, `PageBreakBlock` | none |
| `Inline` (abstract) | none |
| `TextInline` | `Text`, `Style` (`InlineStyleEnum`); constructors `()` and `(string? text, InlineStyleEnum style = None)` |
| `LinkInline` | `Url`, `Title`, `Inlines`; constructors `()` and `(string? url, string? text)` (the URL is the text when text is empty) |
| `ImageInline` | `ResourceId`, `AltText`; constructors `()` and `(string resourceId, string? altText)` |
| `LineBreakInline` | none |

## DocConverter.Model.Serialization

The canonical form of the model, used by the JSON and XML writers and read back losslessly by the JSON and XML readers.

```csharp
public static class CanonicalMapper
{
    public static CanonicalDocument ToDto(DocumentModel document, bool includeMetadata = true, bool includeBinary = true);
    public static DocumentModel FromDto(CanonicalDocument dto);   // DocumentReadException on unknown types or bad base64
    public static DocumentModel Clone(DocumentModel document);    // deep copy
}
```

The DTO classes are `CanonicalDocument`, `CanonicalMetadata`, `CanonicalBlock`, `CanonicalInline`,
`CanonicalListItem`, `CanonicalTableRow`, `CanonicalTableCell` and `CanonicalResource`. Every field is listed in
[DOCUMENT_MODEL.md](DOCUMENT_MODEL.md#canonical-json). The DTOs carry `[JsonPropertyName]` attributes, so plain
`System.Text.Json` serialization of a `CanonicalDocument` produces the canonical JSON shape.

## DocConverter.Readers and DocConverter.Writers

```csharp
public interface IDocumentReader
{
    IReadOnlyList<DocumentFormatEnum> Formats { get; }
    Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default);
}

public interface IDocumentWriter
{
    IReadOnlyList<DocumentFormatEnum> Formats { get; }
    Task WriteAsync(DocumentModel document, Stream output, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default);
}
```

The built-in implementations are public sealed classes, each stateless and thread safe, so they can be composed or
wrapped. Readers are in `DocConverter.Readers.<Folder>` and writers in `DocConverter.Writers.<Folder>`.

| Class | Namespace | Formats |
|---|---|---|
| `TextDocumentReader` | `Readers.Text` | Text |
| `MarkdownDocumentReader` | `Readers.Markdown` | Markdown |
| `HtmlDocumentReader` | `Readers.Html` | Html |
| `JsonDocumentReader` | `Readers.Json` | Json |
| `XmlDocumentReader` | `Readers.Xml` | Xml |
| `DelimitedDocumentReader` | `Readers.Delimited` | Csv, Tsv |
| `RtfDocumentReader` | `Readers.Rtf` | Rtf |
| `DocxDocumentReader` | `Readers.Docx` | Docx |
| `XlsxDocumentReader` | `Readers.Xlsx` | Xlsx |
| `PptxDocumentReader` | `Readers.Pptx` | Pptx |
| `PdfDocumentReader` | `Readers.Pdf` | Pdf |
| `ImageDocumentReader` | `Readers.Image` | Png, Jpeg, Gif, Bmp, Tiff, WebP |
| `MarkdownDocumentWriter` | `Writers.Markdown` | Markdown |
| `HtmlDocumentWriter` | `Writers.Html` | Html |
| `PlainTextDocumentWriter` | `Writers.Text` | Text |
| `JsonDocumentWriter` | `Writers.Json` | Json |
| `XmlDocumentWriter` | `Writers.Xml` | Xml |
| `DelimitedDocumentWriter` | `Writers.Delimited` | Csv, Tsv |
| `DocxDocumentWriter` | `Writers.Docx` | Docx |
| `XlsxDocumentWriter` | `Writers.Xlsx` | Xlsx |
| `PptxDocumentWriter` | `Writers.Pptx` | Pptx |
| `PdfDocumentWriter` | `Writers.Pdf` | Pdf |

`DocConverter.Writers.Pdf.DocConverterFontResolver` is the PDFsharp `IFontResolver` that serves the embedded Liberation
fonts: every family resolves to Liberation Sans, and monospace families (Courier New, Consolas, Menlo, anything containing
"Mono") to Liberation Mono. DocConverter installs it into PDFsharp's global font settings the first time it writes a PDF.
If your application already set `GlobalFontSettings.FontResolver`, yours is kept and DocConverter's is registered as the
fallback resolver (when no fallback is set). With a host resolver in charge, `GlyphsUnavailable` is not reported.

How to write your own reader or writer is in [EXTENDING.md](EXTENDING.md).

## DocConverter.Detection

```csharp
public static class DocumentFormatParser
{
    public static IReadOnlyCollection<string> Aliases { get; }
    public static bool TryParse(string? value, out DocumentFormatEnum format);
    public static DocumentFormatEnum Parse(string? value);                      // ArgumentException when unknown
    public static DocumentFormatEnum? FromExtension(string? fileName);          // null when absent or unknown; never Auto
    public static string GetMediaType(DocumentFormatEnum format);
    public static string GetDefaultExtension(DocumentFormatEnum format);        // without the dot, for example "md"
    public static bool IsTextBased(DocumentFormatEnum format);
    public static bool IsImage(DocumentFormatEnum format);
}
```

Parsing is case-insensitive, trims whitespace and ignores a leading dot. Beyond the enum names it accepts `txt`,
`plain`, `plaintext`, `md`, `mdown`, `mkd`, `markdn`, `htm`, `xhtml`, `tab`, `word`, `excel`, `powerpoint`, `jpg`,
`jpe`, `tif` and `dib`. `FromExtension` handles both `/` and `\` separators. The class is thread safe.

```csharp
DocumentFormatParser.TryParse(".DOCX", out DocumentFormatEnum f);     // true, Docx
DocumentFormatParser.FromExtension("/data/q3/notes.MD");              // Markdown
DocumentFormatParser.GetMediaType(DocumentFormatEnum.Xlsx);           // application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
```

## DocConverter.Observability

```csharp
public static class DocConverterDiagnostics
{
    public const string Name = "DocConverter";
    public static readonly ActivitySource ActivitySource;
    public static readonly Meter Meter;
    public static string Version { get; }        // library version, for example "0.1.1"
}
```

Each conversion starts one activity named `docconverter.convert` with the tags `docconverter.from` and
`docconverter.to` (and `error` on failure), and records these instruments:

| Instrument | Type | Tags |
|---|---|---|
| `docconverter.conversions` | counter | `from`, `to`, `outcome` (`success`, `failure`, `cancelled`) |
| `docconverter.conversion.duration` | histogram, ms | `from`, `to` |
| `docconverter.input.bytes` | histogram, bytes | `from` |
| `docconverter.warnings` | counter | `code` |

Instrumentation never throws into a conversion. Subscribe with an `ActivityListener`, a `MeterListener` or OpenTelemetry
(`AddSource("DocConverter")`, `AddMeter("DocConverter")`).

## DocConverter.DependencyInjection

```csharp
public static IServiceCollection AddDocConverter(this IServiceCollection services, Action<ConverterSettings>? configure = null);
```

Builds one `Converter` from settings you configure in the callback and registers it as a singleton for both `Converter`
and `IConverter`. Throws `ArgumentNullException` when `services` is null.

```csharp
services.AddDocConverter(settings =>
{
    settings.MaxInputBytes = 64L * 1024 * 1024;
    settings.Logger = (severity, message) => logger.LogInformation(message);
});

public class IngestService
{
    private readonly IConverter _Converter;
    public IngestService(IConverter converter) { _Converter = converter; }
}
```

## DocConverter.Ocr

OCR is not implemented in 0.1.0. These types define the contract a future OCR package will implement.

```csharp
public interface IOcrProvider
{
    string Name { get; }
    bool Supports(string mediaType);
    Task<OcrResult> RecognizeAsync(BinaryResource image, OcrOptions options, CancellationToken token = default);
}

public abstract class OcrProviderBase : IOcrProvider   // every member throws NotImplementedException until overridden

public class OcrOptions
{
    public List<string> Languages { get; set; }        // ["eng"], never null
    public double MinimumConfidence { get; set; }      // 0.6, range 0.0 to 1.0
    public bool DetectTables { get; set; }             // true
    public bool DetectLists { get; set; }              // true
}

public class OcrResult
{
    public List<Block> Blocks { get; set; }            // never null
    public double Confidence { get; set; }             // clamped to 0.0 to 1.0
    public string? Language { get; set; }
}
```

The wiring points are `ConverterSettings.OcrProvider`, `ConversionOptions.OcrMode` and `ConversionOptions.Ocr`. In
0.1.0, a configured provider with any `OcrMode` other than `Off` makes every conversion and read throw
`NotImplementedException` before reading, so a provider is never silently ignored. With the default `Off`, or with no
provider, nothing changes. See [EXTENDING.md](EXTENDING.md#ocr-extension-point).
