# Extending DocConverter

DocConverter's readers and writers sit behind two small interfaces, `IDocumentReader` and `IDocumentWriter`, and a
`Converter` looks them up in a registry keyed by `DocumentFormatEnum`. You can replace a built-in reader or writer with
your own, wrap one to change its behavior, or register one for a format the built-ins leave out (RTF and the image
formats have no writer). This page covers the contracts, registration, a worked example of each, dependency injection,
observability, and the OCR extension point that a future package will implement.

`DocumentFormatEnum` is closed in 0.1.0: a custom reader or writer registers for one of its existing values. That covers
replacing and specializing the built-ins and filling the RTF and image gaps; a brand new file format needs a new enum
value and therefore a library release.

## The contracts

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

What the pipeline guarantees you, and what it expects back:

- **One instance, many threads.** The same reader or writer object serves every conversion on a converter, concurrently.
  Keep no state between calls; put per-call state in locals or in an object you create inside the call.
- **Streams.** The reader's input is a seekable, in-memory stream positioned at the start of the document. The writer's
  output is a seekable, writable in-memory stream; the converter copies it to the caller afterwards. Never close or
  dispose either one.
- **Text input.** For text formats read from a string, `context.SourceText` holds the caller's string. The stream always
  contains the same text as UTF-8, so a reader may use either; `SourceText` saves a decode.
- **Options.** `options` is never null. Read what applies to you; ignore the rest.
- **Losses.** When you drop or approximate something, call `context.AddWarning(code, message)`. Repeated codes are merged
  into one warning with a count, so calling it per occurrence is fine. `context.Log(severity, message)` goes to the
  configured logger.
- **Limits.** `context.MaxNestingDepth`, `MaxDecompressedBytes` and `MaxInputBytes` carry the converter's limits.
- **Cancellation.** Check `token.ThrowIfCancellationRequested()` between pages, rows or blocks, and pass the token to
  anything asynchronous. Use `.ConfigureAwait(false)` on every await.
- **Failures.** Throw `DocumentReadException` or `DocumentWriteException` with a message that says what was wrong. Any
  other exception (except `OperationCanceledException` and `NotImplementedException`) is wrapped in one of those two
  for you, with yours as the inner exception.
- **The model you receive.** Writers get a private copy with the option driven changes already applied (images removed
  when `IncludeImages` is false, metadata cleared when `IncludeMetadata` is false, `Title` applied, redundant bold on
  headings cleared). Writers may read it freely and should not need to modify it.

## Registering

```csharp
Converter converter = new Converter();
converter.RegisterReader(new IniTextReader());
converter.RegisterWriter(new SlackMarkdownWriter());
```

Registration replaces whatever was registered for the same formats, built-in or not, and is safe while conversions run.
A reader or writer that declares no formats, or declares `Auto`, is rejected with `ArgumentException`.

`CanConvert(from, to)` answers from the registry, and `GetSupportedConversions()` lists every registered reader format
times every registered writer format. Pairs that involve your registrations report `Full` fidelity with the note
"Provided by a caller-registered reader or writer.", since what they keep is up to you. Asking for a pair with no reader
or no writer throws `ConversionNotSupportedException` before any input is read:

```csharp
converter.CanConvert(DocumentFormatEnum.Markdown, DocumentFormatEnum.Rtf);   // false: no RTF writer is built in
```

## Example: a reader

This reader replaces the built-in Text reader with one that understands INI files: `[section]` headers become titled
sections and `key = value` lines become a two column table, while anything else stays a paragraph. It shows the usual
shape of a reader: decode, build the model, check the token, warn about anything skipped.

```csharp
namespace MyCompany.Conversion
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocConverter.Readers;

    /// <summary>
    /// Reads INI style text: sections become titled sections, key = value lines become tables.
    /// </summary>
    public sealed class IniTextReader : IDocumentReader
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Text };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <inheritdoc />
        public async Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            string text = context.SourceText ?? await ReadTextAsync(input, options).ConfigureAwait(false);
            DocumentModel document = new DocumentModel();
            List<Block> target = document.Blocks;
            TableBlock? pairs = null;
            int lineNumber = 0;

            using (StringReader reader = new StringReader(text))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if ((++lineNumber & 1023) == 0) token.ThrowIfCancellationRequested();
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith(";", StringComparison.Ordinal)) continue;

                    if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                    {
                        SectionBlock section = new SectionBlock(SectionKindEnum.Generic, trimmed.Substring(1, trimmed.Length - 2));
                        document.Blocks.Add(section);
                        target = section.Blocks;
                        pairs = null;
                        continue;
                    }

                    int equals = trimmed.IndexOf('=');
                    if (equals > 0)
                    {
                        if (pairs == null)
                        {
                            pairs = new TableBlock { HeaderRowCount = 1 };
                            pairs.Rows.Add(new TableRow(new[] { "Key", "Value" }));
                            target.Add(pairs);
                        }

                        pairs.Rows.Add(new TableRow(new[] { trimmed.Substring(0, equals).Trim(), trimmed.Substring(equals + 1).Trim() }));
                        continue;
                    }

                    context.AddWarning(WarningCodeEnum.UnknownElementSkipped, "A line that is neither a section nor a key = value pair was kept as a paragraph.");
                    target.Add(new ParagraphBlock(trimmed));
                }
            }

            return document;
        }

        private static async Task<string> ReadTextAsync(Stream input, ConversionOptions options)
        {
            using (StreamReader reader = new StreamReader(input, options.InputEncoding ?? new UTF8Encoding(false), true, 81920, true))
            {
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
        }
    }
}
```

`leaveOpen: true` on the `StreamReader` matters: the stream belongs to the pipeline.

## Example: a writer

This writer replaces the built-in Markdown writer with Slack's `mrkdwn` dialect, where bold is `*text*`, italic is
`_text_`, strike is `~text~` and links are `<url|text>`. Slack has no headings or tables, so headings become bold lines
and tables become lines of cells separated by `|`, and the writer says so with `FormattingLost`.

```csharp
namespace MyCompany.Conversion
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocConverter.Writers;

    /// <summary>
    /// Writes Slack mrkdwn in place of Markdown.
    /// </summary>
    public sealed class SlackMarkdownWriter : IDocumentWriter
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Markdown };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <inheritdoc />
        public async Task WriteAsync(DocumentModel document, Stream output, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Block block in document.Blocks)
            {
                token.ThrowIfCancellationRequested();
                WriteBlock(block, sb, context, "");
            }

            byte[] bytes = options.OutputEncoding.GetBytes(sb.ToString());
            await output.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
        }

        private static void WriteBlock(Block block, StringBuilder sb, ConversionContext context, string indent)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    sb.Append('*').Append(Inlines(heading.Inlines, context)).Append("*\n\n");
                    break;
                case ParagraphBlock paragraph:
                    sb.Append(Inlines(paragraph.Inlines, context)).Append("\n\n");
                    break;
                case ListBlock list:
                    int number = list.Start;
                    foreach (ListItemBlock item in list.Items)
                    {
                        string marker = list.Kind == ListKindEnum.Ordered ? (number++) + ". " : "• ";
                        foreach (Block child in item.Blocks)
                        {
                            if (child is ParagraphBlock p) sb.Append(indent).Append(marker).Append(Inlines(p.Inlines, context)).Append('\n');
                            else if (child is ListBlock nested) WriteBlock(nested, sb, context, indent + "    ");
                        }
                    }

                    if (indent.Length == 0) sb.Append('\n');
                    break;
                case CodeBlock code:
                    sb.Append("```\n").Append(code.Text).Append("\n```\n\n");
                    break;
                case QuoteBlock quote:
                    foreach (Block child in quote.Blocks)
                        if (child is ParagraphBlock p) sb.Append("> ").Append(Inlines(p.Inlines, context)).Append('\n');
                    sb.Append('\n');
                    break;
                case TableBlock table:
                    context.AddWarning(WarningCodeEnum.TablesFlattened, "Slack has no tables; rows were written as lines of cells separated by |.");
                    foreach (TableRow row in table.Rows)
                    {
                        List<string> cells = new List<string>();
                        foreach (TableCell cell in row.Cells)
                            foreach (Block b in cell.Blocks)
                                if (b is ParagraphBlock p) cells.Add(Inlines(p.Inlines, context));
                        sb.Append(string.Join(" | ", cells)).Append('\n');
                    }

                    sb.Append('\n');
                    break;
                case SectionBlock section:
                    if (!string.IsNullOrEmpty(section.Title)) sb.Append('*').Append(section.Title).Append("*\n\n");
                    foreach (Block child in section.Blocks) WriteBlock(child, sb, context, indent);
                    break;
                case ImageBlock _:
                    context.AddWarning(WarningCodeEnum.ImagesOmitted, "Slack messages cannot carry inline images; images were omitted.");
                    break;
            }
        }

        private static string Inlines(List<Inline> inlines, ConversionContext context)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Inline inline in inlines)
            {
                if (inline is TextInline text)
                {
                    string t = text.Text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
                    if ((text.Style & InlineStyleEnum.Code) != 0) t = "`" + t + "`";
                    if ((text.Style & InlineStyleEnum.Strikethrough) != 0) t = "~" + t + "~";
                    if ((text.Style & InlineStyleEnum.Italic) != 0) t = "_" + t + "_";
                    if ((text.Style & InlineStyleEnum.Bold) != 0) t = "*" + t + "*";
                    if ((text.Style & InlineStyleEnum.Underline) != 0)
                        context.AddWarning(WarningCodeEnum.FormattingLost, "Slack has no underline; the text was kept.");
                    sb.Append(t);
                }
                else if (inline is LinkInline link)
                {
                    sb.Append('<').Append(link.Url).Append('|').Append(Inlines(link.Inlines, context)).Append('>');
                }
                else if (inline is LineBreakInline)
                {
                    sb.Append('\n');
                }
            }

            return sb.ToString();
        }
    }
}
```

With it registered, every source converts to Slack text through the normal API:

```csharp
converter.RegisterWriter(new SlackMarkdownWriter());
StringConversionResult message = await converter.ConvertToStringAsync(docxBytes, DocumentFormatEnum.Docx, DocumentFormatEnum.Markdown);
```

To wrap a built-in rather than replace it, hold an instance of the built-in class (they are public, for example
`DocConverter.Writers.Markdown.MarkdownDocumentWriter`) and call it from yours, adjusting the document or the output
around the call.

## Dependency injection

`AddDocConverter` registers one converter as a singleton for both `IConverter` and `Converter`. Register custom readers
and writers on that instance once, at startup, for example from a hosted service:

```csharp
services.AddDocConverter(settings => settings.MaxInputBytes = 64L * 1024 * 1024);
services.AddHostedService<ConverterSetup>();

public sealed class ConverterSetup : IHostedService
{
    private readonly Converter _Converter;

    public ConverterSetup(Converter converter)
    {
        _Converter = converter;
    }

    public Task StartAsync(CancellationToken token)
    {
        _Converter.RegisterWriter(new SlackMarkdownWriter());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token)
    {
        return Task.CompletedTask;
    }
}
```

Registration is safe while conversions run, so ordering is a convenience, not a correctness requirement.

## Observability

Custom readers and writers run inside the converter's `docconverter.convert` activity, so their time shows up in
`docconverter.conversion.duration` and their warnings in `docconverter.warnings` (tagged with the code) without extra
work. For your own detail, create child activities from your own `ActivitySource`, or log through
`context.Log(severity, message)`, which reaches `ConverterSettings.Logger`.

## OCR extension point

OCR is not part of 0.1.0. The contract a future OCR package will implement is already public, so adding it later will
not change the API:

```csharp
public interface IOcrProvider
{
    string Name { get; }
    bool Supports(string mediaType);
    Task<OcrResult> RecognizeAsync(BinaryResource image, OcrOptions options, CancellationToken token = default);
}

public abstract class OcrProviderBase : IOcrProvider   // every member throws NotImplementedException until overridden
```

`OcrResult` returns the recognized content as ordinary model blocks (`Blocks`), with a `Confidence` between 0 and 1 and
an optional `Language`. `OcrOptions` carries `Languages` (default `["eng"]`), `MinimumConfidence` (default 0.6),
`DetectTables` and `DetectLists`.

Three settings wire a provider in: `ConverterSettings.OcrProvider`, `ConversionOptions.OcrMode` (`Off`, `ImagesOnly`,
`PagesWithoutText`, `All`) and `ConversionOptions.Ocr`.

The exact 0.1.0 behavior:

| Provider | `OcrMode` | Result |
|---|---|---|
| none | any | Conversions run normally; images are handled as documented (placeholders for text targets, `NoTextLayer` for scanned PDF pages). |
| configured | `Off` | Conversions run normally; the provider is never called. |
| configured | `ImagesOnly`, `PagesWithoutText` or `All` | Every conversion and read throws `NotImplementedException` before reading, saying OCR is not implemented in 0.1.0. |

Refusing loudly is deliberate: a configured provider that was silently ignored would look like OCR ran and found
nothing.

A future `DocConverter.Ocr.*` package (for example one built on Tesseract) will ship a class derived from
`OcrProviderBase` and a way to set it on `ConverterSettings.OcrProvider`. The converter will then call it for image
sources and embedded images (`ImagesOnly`), for PDF pages without a text layer (`PagesWithoutText`), or both (`All`), and
place the returned blocks into the model. At that point image to text conversions move from placeholder output to real
text, and pages that raise `NoTextLayer` today gain their content. Keeping OCR in its own package keeps the core free of
native dependencies on every platform.
