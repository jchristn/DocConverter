# docconv

`docconv` is the command line face of DocConverter. It converts files or pipes, detects formats, and lists what it can
do. It was built for agents as much as for people. Every option is a flag, stdin and stdout carry raw bytes so binary
formats survive a pipe, stdout carries exactly one thing, and each failure has its own exit code. It runs on Windows,
macOS and Linux with .NET 8 or .NET 10.

## Install

From NuGet:

```
dotnet tool install -g DocConverter.Cli
docconv --version
```

From a clone of the repository, the scripts pack the tool and install it globally for exactly one target framework.
Name it as the only argument, or with `--framework` or `-f`; `net8` and `net10` are accepted too. Without one, they pick
.NET 10 when its SDK is installed and .NET 8 otherwise.

```
install-tool.bat net8.0                      (Windows)
./install-tool.sh net8.0                     (macOS, Linux)
reinstall-tool.bat --framework net10.0       uninstall, rebuild and reinstall for net10.0 only
./reinstall-tool.sh -f net8.0
remove-tool.bat net8.0                       uninstall and delete artifacts/tool-packages/net8.0
./remove-tool.sh                             uninstall and delete the local packages for every framework
```

Install and reinstall pack `DocConverter.Cli` for the chosen framework only, into `artifacts/tool-packages/<framework>`,
install it with `--framework`, and then check the installed tool store: if it holds any other framework the script fails.
`install-tool` refuses to run over an existing install and points to `reinstall-tool`. `reinstall-tool` and
`remove-tool` refuse to run while `docconv` is running (`tasklist` on Windows, `pgrep` on macOS and Linux). The scripts
honor `DOTNET_CLI_HOME` when it relocates the global tools directory, and the `.sh` scripts run under the bash 3.2 that
ships with macOS.

## Commands

```
docconv convert -i <path|-> -o <path|-> [--from <format>] [--to <format>] [options]
docconv detect  -i <path|-> [--json]
docconv formats [--json]
docconv --help | -h | -? | /?
docconv --version | -v
```

`-` means stdin for `-i` and stdout for `-o`. Options accept `--opt value` and `--opt=value`. Flags can be switched
explicitly with `--flag=true` or `--flag=false` (also `1`/`0`, `yes`/`no`, `on`/`off`). An unknown option, a missing
value, a stray positional argument or an option that does not apply to the command is a usage error (exit 2). Running
`docconv` with no arguments prints the help to stderr and exits 2; `docconv --help` prints it to stdout and exits 0.

### convert

Reads one document and writes it in another format.

**Formats.** `--from` and `--to` accept format names, aliases and extensions, with or without a dot and in any case:
`md`, `markdown`, `.md`, `word`, `docx`, `excel`, `powerpoint`, `txt`, `htm`, `jpg`, `tif` and so on. The inputs are
text, markdown, html, json, xml, csv, tsv, rtf, docx, xlsx, pptx, pdf, png, jpeg, gif, bmp, tiff and webp; the outputs
are the same list without rtf and the images.

**Inference.** Without `--from`, the source format is detected from the content, with the input file extension as a
hint for ambiguous text (a `.csv` file of plain lines stays CSV). A file that turns out to be something DocConverter
cannot read, such as a legacy `.doc`, fails with exit 3 and a message naming what it is. Without `--to`, the target comes
from the output file extension. When `-o -` is used there is no extension to read, so `--to` is required.

**Output files.** An existing output file is an error (exit 4) unless you pass `--overwrite`. The document is written to
a hidden temporary file in the same folder and moved into place only when the conversion succeeds, so a failed or
`--strict` run never leaves a partial or stale file behind. The output folder must exist. With `-o -` the document has
already gone to stdout by the time `--strict` finds a warning, so check the exit code before using what you captured.

| Flag | Effect |
|---|---|
| `-i`, `--input <path\|->` | Input file, or `-` for stdin. Required. |
| `-o`, `--output <path\|->` | Output file, or `-` for stdout. Required. |
| `--from <format>` | Source format. Omitted: detected. |
| `--to <format>` | Target format. Omitted: from the output extension. Required with `-o -`. |
| `--overwrite` | Replace an existing output file (and existing image side files). |
| `--options <file.json>` | Load conversion options from a JSON file (below). Flags override it. |
| `--title <text>` | Title written into targets that carry one. |
| `--no-images` | Drop all images. |
| `--images <embed\|omit\|placeholder\|external>` | How Markdown and HTML write images. `external` writes the images as files next to the output file and cannot be used with `-o -`. `omit` also drops plain text placeholders. |
| `--no-metadata` | Do not carry title, author and other metadata. |
| `--input-encoding <name>` | Encoding of text input without a byte order mark. Default UTF-8. |
| `--output-encoding <name>` | Encoding of text output. `utf-8` (default, no BOM), `utf-8-bom`, or any .NET encoding name. |
| `--line-ending <lf\|crlf\|platform>` | Line endings of text output. Default `lf`. |
| `--deterministic` | Pin timestamps and identifiers so identical input gives identical bytes. |
| `--strict` | Treat warnings as errors: exit 5, and no output file is left behind. |
| `--csv-delimiter <char\|tab>` | CSV or TSV delimiter for reading and writing. |
| `--no-header` | CSV or TSV input has no header row. |
| `--table <first\|all\|n>` | Which tables to write to CSV or TSV; `n` is a zero based index. |
| `--html-fragment` | HTML body content only. |
| `--html-no-css` | No embedded stylesheet in HTML. |
| `--text-wrap <n>` | Wrap plain text at `n` columns: 0, or 20 to 1000. |
| `--json-compact` | JSON on one line. |
| `--json-no-binary` | Leave image bytes out of JSON. |
| `--page-size <a4\|letter\|legal>` | PDF and DOCX page size. Default letter. |
| `--margin <points>` | PDF and DOCX margin in points, 0 to 216. Default 72. |
| `--slide-split <1-6>` | PPTX: new slide at headings of this level or lower. Default 2. |
| `--include-notes` | PPTX input: include speaker notes. |
| `--include-hidden-sheets` | XLSX input: include hidden sheets. |
| `--preserve-pages` | PDF input: keep each page as its own section. |
| `--max-input-mb <n>` | Largest accepted input in megabytes (default 256). Larger input exits 6 before it is read. |
| `--json` | Write the JSON report (below). |
| `-q`, `--quiet` | Suppress the summary line and warning lines. Errors are still written. |

Each flag maps to a library option; [OPTIONS.md](OPTIONS.md#command-line-mapping) has the full mapping.

### detect

Reports what a file or stdin contains without converting it. It accepts `-i`, `--json`, `--quiet` and `--max-input-mb`.
The exit code is 0 when DocConverter can read the input and 3 when it cannot.

```
$ docconv detect -i notes.docx
Format:        Docx
Recognized as: Word document (Office Open XML)
Media type:    application/vnd.openxmlformats-officedocument.wordprocessingml.document
Extension:     docx
Confidence:    Structure
Bytes:         3949
```

### formats

Lists the input and output formats and the full conversion matrix (`F` full, `P` projection). `--json` adds the notes
that say what each projection loses.

```
$ docconv formats
Input formats:  Text, Markdown, Html, Json, Xml, Csv, Tsv, Rtf, Docx, Xlsx, Pptx, Pdf, Png, Jpeg, Gif, Bmp, Tiff, WebP
Output formats: Text, Markdown, Html, Json, Xml, Csv, Tsv, Docx, Xlsx, Pptx, Pdf

Conversion matrix (F = full, P = projection: lossy by design, with a warning; . = not supported)

From \ To txt   md    html  json  xml   csv   tsv   docx  xlsx  pptx  pdf
txt       F     F     F     F     F     P     P     F     P     F     F
md        F     F     F     F     F     P     P     F     P     P     F
...
```

## Streams and output

The rule that matters for scripts: **stdout carries exactly one thing.** With `-o -` it is the converted document, as raw
bytes. With `--json` and a file output it is the JSON report. Everything else goes to stderr: the summary line, warning
lines, error lines, and the JSON report when stdout is busy carrying the document. `detect --json` and `formats --json`
write their JSON to stdout.

Unless `--quiet` is set, a successful conversion writes one summary line to stderr, followed by one line per warning:

```
docconv: notes.docx (Docx) -> notes.txt (Text), 3.9 KB -> 193 B, 269 ms, 1 warning
  warning FormattingLost (x2): Inline styles (bold, italic and so on) have no plain text form and were dropped.
```

Errors are one line on stderr: `docconv: error: <message>`. Paths in the summary and the report are shown as you passed
them; stdin and stdout appear as `stdin` and `stdout`.

## JSON report

With `--json`, `convert` writes one line of JSON. The field names are fixed; `contractVersion` changes only if the
shape does.

```json
{"contractVersion":1,"success":true,"command":"convert","input":{"path":"notes.md","format":"Markdown","bytes":246,"detected":true},"output":{"path":"notes.docx","format":"Docx","bytes":3949},"durationMs":944,"warnings":[],"statistics":{"pages":0,"slides":0,"sheets":0,"headings":1,"paragraphs":9,"lists":1,"tables":1,"images":0,"links":1},"error":null,"exitCode":0}
```

| Field | Type | Meaning |
|---|---|---|
| `contractVersion` | int | Always 1 for this release. |
| `success` | bool | True when the document was written. |
| `command` | string | `convert`. |
| `input.path` | string | The `-i` value, or `stdin`. |
| `input.format` | string or null | Source format, detected or given. |
| `input.bytes` | long | Input size. |
| `input.detected` | bool | True when the format was detected rather than given with `--from`. |
| `output.path` | string | The `-o` value, or `stdout`. |
| `output.format` | string or null | Target format. |
| `output.bytes` | long | Bytes written; 0 when nothing was kept. |
| `durationMs` | long | Wall time of the command. |
| `warnings[]` | array | `code`, `message` (first occurrence) and `count` for each warning code. |
| `statistics` | object or null | `pages`, `slides`, `sheets`, `headings`, `paragraphs`, `lists`, `tables`, `images`, `links`. |
| `error` | object or null | `code` and `message` on failure. |
| `exitCode` | int | The process exit code. |

On failure, `success` is false and `error.code` is one of `Usage`, `InvalidOptions`, `UnsupportedFormat`,
`ConversionNotSupported`, `DocumentRead`, `DocumentWrite`, `InputTooLarge`, `Warnings`, `OutputExists`, `NotFound`,
`AccessDenied`, `IO`, `NotImplemented`, `Cancelled` or `Internal`. A usage error found while parsing the command line
still produces a report when `--json` was given.

`detect --json`:

```json
{"contractVersion":1,"success":true,"command":"detect","input":{"path":"notes.docx","bytes":3949},"detection":{"format":"Docx","mediaType":"application/vnd.openxmlformats-officedocument.wordprocessingml.document","extension":"docx","recognizedAs":"Word document (Office Open XML)","confidence":"Structure","supported":true},"error":null,"exitCode":0}
```

`detection.format` is null for recognized but unsupported content, `recognizedAs` names what it is (for example
`legacy Word 97-2003 document (.doc)`), and `confidence` is `Signature`, `Structure`, `Heuristic` or `ExtensionHint`.

`formats --json` returns `contractVersion`, `inputs` and `outputs` (format names), and `conversions`, one object per pair:

```json
{"contractVersion":1,"inputs":["Text","Markdown","Html",...],"outputs":["Text","Markdown",...],"conversions":[{"from":"Text","to":"Text","fidelity":"Full","notes":""},{"from":"Png","to":"Text","fidelity":"Projection","notes":"No OCR: a placeholder names the image (ImagePlaceholderEmitted)."},...]}
```

## Options file

`--options <file.json>` loads a JSON object. Every key is optional, names are camelCase and matched case-insensitively,
comments and trailing commas are allowed, and an unknown key is a usage error so a typo cannot pass silently. Flags on
the command line override the file.

```json
{
  // Output shaping
  "images": "placeholder",
  "includeMetadata": true,
  "title": "Weekly report",
  "lineEnding": "lf",
  "deterministic": true,
  "treatWarningsAsErrors": false,

  // Format specific
  "htmlMode": "fragment",
  "htmlIncludeStylesheet": false,
  "textWrapColumn": 100,
  "textHeadingStyle": "uppercase",
  "textTableStyle": "tabs",
  "jsonIndented": false,
  "jsonIncludeBinary": false,
  "xmlIndented": true,
  "csvDelimiter": ";",
  "csvHasHeaderRow": true,
  "table": "all",
  "pageSize": "a4",
  "marginPoints": 54,
  "slideSplitHeadingLevel": 1,
  "includeNotes": true,
  "includeHiddenSheets": false,
  "preservePages": false,
  "maxInputMb": 64
}
```

The remaining keys are `includeImages`, `inputEncoding` and `outputEncoding`.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Success. Warnings are allowed unless `--strict` is set. |
| 1 | The input could not be read or the output could not be written (corrupt, encrypted or malformed input). |
| 2 | Usage error: unknown option, missing argument, invalid or out of range value. |
| 3 | Unsupported: the format is not recognized or not supported, or no reader or writer covers the pair. |
| 4 | I/O error: input not found, output exists without `--overwrite`, missing output folder, access denied. |
| 5 | Warnings were raised under `--strict`. |
| 6 | The input is larger than `--max-input-mb`. |
| 130 | Cancelled (Ctrl+C). |

## Agent recipes

Each of these is safe to run unattended: the only thing on stdout is what you asked for.

Convert a document to Markdown for reading, with nothing but Markdown on stdout:

```
docconv convert -i contract.docx -o - --to md --quiet
```

Convert and collect a machine-readable result (stdout is the one line report; the summary goes to stderr):

```
docconv convert -i deck.pptx -o deck.md --overwrite --json
```

Parse `success`, then `exitCode`; read `warnings[].code` to learn what was lost (for example `ImagePlaceholderEmitted`
means images became text placeholders).

Refuse to lose content silently. Exit 5 means the pair lost something; the report's warnings say what:

```
docconv convert -i page.html -o page.txt --strict --json
```

Find out what an unknown upload is before deciding what to do with it:

```
docconv detect -i upload.bin --json
```

Exit 0 with `detection.format` set means it can be converted; exit 3 means it cannot, and `detection.recognizedAs` says why.

Build a Word document from Markdown produced in the same pipeline, with reproducible bytes:

```
generate-summary | docconv convert -i - -o summary.docx --from md --deterministic --overwrite
```

Extract spreadsheet data for tools that want CSV, every table in order:

```
docconv convert -i figures.xlsx -o - --to csv --table all --quiet
```

Produce LLM friendly Markdown without base64 image payloads:

```
docconv convert -i report.pdf -o report.md --images omit --overwrite
```

Check what a conversion can carry before running it:

```
docconv formats --json
```

Look up the pair in `conversions[]`: `fidelity` is `Full` or `Projection`, and `notes` explains a projection.

In a shell, branch on the exit code rather than parsing text:

```
docconv convert -i "$IN" -o "$OUT" --overwrite --quiet
case $? in
  0) echo ok ;;
  3) echo "unsupported format" ;;
  5) echo "lossy conversion refused" ;;
  *) echo "failed" ;;
esac
```
