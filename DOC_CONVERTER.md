# DocConverter: Implementation Plan

DocConverter is a C# library and a companion command line tool. It converts a document from one format to another in memory, with no temp files. The input can be a `string`, a `byte[]` or a `Stream`, and the output can be any of the three. The CLI (`docconv`) is a .NET global tool. Its main audience is agents that shell out over stdio and need clean, predictable output and exit codes.

The library and the CLI run on Windows, macOS and Linux. Nothing in either may depend on a Windows-only API (no System.Drawing, no COM, no registry, no installed system fonts). CI proves this on all three operating systems.

This plan covers the whole job:

- repository initialization
- solution and project layout
- the public API
- the internal architecture
- every reader and writer
- the CLI
- the test strategy and the full conversion matrix
- documentation, packaging and CI
- a phased build order with exit criteria

It follows the TextChunker repository (`C:\Code\TextChunker`) as the structural exemplar. It obeys every rule in `C:\Code\agents\requirements`. Where the exemplar and a requirement document disagree, the requirement document wins, as `EXAMPLE_APPLICATIONS.md` directs.

---

## 1. Decisions already made

These came out of the planning conversation and are not open for re-litigation during implementation.

| Topic | Decision |
|---|---|
| Output formats | Markdown, HTML, plain text, JSON, XML, CSV, TSV, DOCX, XLSX, PPTX, PDF. No RTF output. |
| Licensing | **Hard constraint:** only permissively licensed dependencies (MIT, Apache-2.0, BSD, MS-PL, or a font under SIL OFL 1.1). No GPL, AGPL, LGPL, commercial or split licenses (this rules out QuestPDF, iText, Aspose, Syncfusion, SixLabors.ImageSharp). |
| DocumentAtom | Port as much DocumentAtom code as is useful, directly into this solution. There is **no** package or project reference to DocumentAtom. |
| Repository | `https://github.com/jchristn/DocConverter` |
| Platforms | Windows, macOS and Linux, for both the library and the `docconv` tool. CI runs every suite on `windows-latest`, `macos-latest` and `ubuntu-latest`. |
| Lossiness | Lossy conversions are acceptable **as long as they are clearly documented**. Where a target cannot carry something, the conversion still succeeds. It substitutes a documented placeholder or projection and raises a named warning, instead of refusing. Every loss is listed in `docs/FORMATS.md` ("What is lost") and summarized in the README. |
| OCR | Out of scope for 0.1.0. No text is extracted from pixels. Image inputs are embeddable content, and targets that cannot show an image get a documented placeholder. The **extension point ships now as stubs**: `IOcrProvider`, `OcrProviderBase`, `OcrOptions`, `OcrResult` and `ConverterSettings.OcrProvider`. They throw `NotImplementedException`, so a later `DocConverter.Ocr.*` project can plug in without a breaking API change (Section 5.8). |
| CLI shape | Flag based: `docconv convert -i in.docx -o out.md --from docx --to md`. |
| Main class | `Converter` (in namespace `DocConverter`). A `DocConverter` class inside a `DocConverter` namespace forces `DocConverter.DocConverter` everywhere. `Converter` follows TextChunker's `Chunker`. |
| Format enum | One enum, `DocumentFormatEnum`, for both source and target. The source can be `Auto`, which triggers detection. |
| Output object | Chosen by method name: `ConvertAsync(..., Stream output, ...)`, `ConvertToStringAsync(...)`, `ConvertToBytesAsync(...)`. |
| Version | `0.1.0` (the user explicitly chose this over `0.1.0-alpha`). It lives only in `src/Directory.Build.props`. Agents never change it without an explicit request. |
| Target frameworks | Library: `netstandard2.0;net8.0;net10.0`. CLI and tests: `net8.0;net10.0`. |
| Packages | `DocConverter` (library, every reader and writer) and `DocConverter.Cli` (global tool, command `docconv`). |

### Why the API looks the way it does

The request sketched `converter.Convert(bytes, ContentType.Word, ConventType.Markdown, outputFile, options, token)`. The plan keeps that shape and changes four things:

1. **One enum for both ends.** Source and target formats come from the same universe. Two enums would make `CanConvert(from, to)` and the capability matrix awkward, and a user would have to learn that `ContentType.Word` and `ConvertType.Word` are different types.
2. **Format names over product names.** The enum says `Docx`, not `Word`, because legacy `.doc` is also "Word" and is deliberately unsupported. Friendly aliases (`word`, `excel`, `powerpoint`, `md`, `txt`) are accepted where strings are parsed, which means in the CLI and in `DocumentFormatParser`.
3. **`Async` suffix.** This matches TextChunker (`ChunkToResultAsync`) and the convention for `Task`-returning methods.
4. **The output object is chosen by method name.** This avoids a `ConversionResult` with nullable `Text` and `Bytes` properties that callers must remember to check.

---

## 2. Repository initialization

The remote is `https://github.com/jchristn/DocConverter`.

1. `git init -b main` in `C:\Code\DocConverter`, then `git remote add origin https://github.com/jchristn/DocConverter.git`. If the remote already has commits (for example a GitHub-generated README or LICENSE), fetch and base the scaffold on `origin/main` rather than force-pushing over it.
2. Create the root files listed in Section 3.
3. Scaffold the solution and empty projects (Section 4), then run `dotnet build src/DocConverter.sln` with zero warnings.
4. First commit: `chore: initial repository scaffold`. Commits follow the conventional style used in TextChunker (`feat:`, `fix:`, `docs:`, `test:`, `build:`, `refactor:`, `chore:`). Every commit message ends with the Claude co-author trailer. There are no em-dashes anywhere, commit messages included.
5. `Directory.Build.props` points `PackageProjectUrl` and `RepositoryUrl` at `https://github.com/jchristn/DocConverter`. Nothing is pushed unless the user asks.

---

## 3. Repository layout

```
DocConverter/
  .editorconfig                 copied from TextChunker verbatim (inside_namespace:error, _PascalCase fields, CRLF)
  .gitignore                    TextChunker's, plus: installers/, *.docconv-test/, TestResults/
  .github/
    workflows/
      tests.yaml                build, Touchstone runners, netstandard2.0 leg, tool smoke test
  CHANGELOG.md
  CLAUDE.md                     agent guide: layout, commands, code style, versioning, testing model
  CONTRIBUTING.md               copied from TextChunker, names changed
  DONATIONS.md                  copied from TextChunker
  LICENSE.md                    MIT, "Copyright (c) 2026 Joel Christner"
  README.md
  DOC_CONVERTER.md              this plan (kept in the repo as the design record)
  assets/
    icon.png                    256x256, packed as PackageIcon (TextChunker packs only .ico and never declares PackageIcon; fix that here)
    icon.ico
  docs/
    API.md                      every public type and member, with examples
    FORMATS.md                  capability and fidelity matrix, per-format notes and limits
    OPTIONS.md                  every option, default, minimum, maximum, effect
    CLI.md                      every command, flag, exit code, JSON report schema, agent recipes
    DOCUMENT_MODEL.md           the intermediate model and its JSON/XML serialization
    EXTENDING.md                custom readers and writers, registration, DI
  install-tool.bat / .sh        pack DocConverter.Cli and install it as a global tool (Mux pattern; .sh works on macOS and Linux)
  reinstall-tool.bat / .sh      uninstall, pack, install, `docconv --version`
  remove-tool.bat / .sh         uninstall
  publish-nuget.bat             pack both packages, push with --skip-duplicate (TextChunker pattern)
  src/
    Directory.Build.props
    DocConverter.sln
    DocConverter/               library (packable)
    DocConverter.Cli/           global tool (packable, PackAsTool)
    Test.Shared/                Touchstone suites, fixtures, fixture builders, output inspectors
    Test.Automated/             Touchstone console runner
    Test.Xunit/                 xUnit adapter (fact + theory)
    Test.Nunit/                 NUnit adapter (fact + case source)
    Test.NetFramework/          net48 smoke console for real .NET Framework consumers, Windows only, NOT in the .sln
    DocConverter.Benchmarks/    BenchmarkDotNet, NOT in the .sln (TextChunker pattern)
```

**Not included, on purpose:**

- `Dockerfile`, `.dockerignore`, `DOCKERHUB_README.md`, `docker/`
- `REST_API.md`, a Postman collection, `MCP_API.md`, `sdk/`
- a dashboard

DocConverter is a library and a CLI, with no server surface. The CHANGELOG `Notes` for 0.1.0 says so, the same way TextChunker does. `INSTALLERS.md` says library projects are out of scope. The CLI ships through `dotnet tool install -g`, and native installers are left for later (Section 16).

### 3.1 `src/Directory.Build.props`

```xml
<Project>
  <PropertyGroup>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Authors>Joel Christner</Authors>
    <Company>Joel Christner</Company>
    <Product>DocConverter</Product>
    <Copyright>Copyright (c) Joel Christner</Copyright>
    <PackageProjectUrl>https://github.com/jchristn/DocConverter</PackageProjectUrl>
    <RepositoryUrl>https://github.com/jchristn/DocConverter</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <Version>0.1.0</Version>
  </PropertyGroup>
</Project>
```

`ImplicitUsings` is `disable` everywhere, unlike TextChunker's props. The requirements put usings explicitly inside the namespace, and implicit global usings hide missing usings on the netstandard2.0 target. `TreatWarningsAsErrors` enforces "free of errors and warnings".

---

## 4. Solution and projects

`src/DocConverter.sln` is a classic VS 17 solution, flat, with no solution folders. It contains `DocConverter`, `DocConverter.Cli`, `Test.Shared`, `Test.Automated`, `Test.Xunit` and `Test.Nunit`. `DocConverter.Benchmarks` stays out of the .sln, as TextChunker's benchmarks do.

### 4.1 `src/DocConverter/DocConverter.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net8.0;net10.0</TargetFrameworks>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PackageId>DocConverter</PackageId>
    <RootNamespace>DocConverter</RootNamespace>
    <Description>Async, in-memory document conversion for .NET. Converts between DOCX, XLSX, PPTX, PDF, RTF, HTML, Markdown, text, CSV, TSV, JSON, XML and common image formats from a string, byte array or stream.</Description>
    <PackageTags>document;conversion;converter;docx;xlsx;pptx;pdf;markdown;html;csv;json;xml;rtf;openxml</PackageTags>
    <PackageIcon>icon.png</PackageIcon>
    <PackageLicenseFile>LICENSE.md</PackageLicenseFile>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
  </PropertyGroup>
  <ItemGroup>
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
    <None Include="..\..\LICENSE.md" Pack="true" PackagePath="\" />
    <None Include="..\..\assets\icon.png" Pack="true" PackagePath="\" />
    <EmbeddedResource Include="Resources\Fonts\*.ttf" />
    <InternalsVisibleTo Include="Test.Shared" />
    <InternalsVisibleTo Include="DocConverter.Benchmarks" />
  </ItemGroup>
  <!-- package references: Section 4.3 -->
</Project>
```

### 4.2 `src/DocConverter.Cli/DocConverter.Cli.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>docconv</ToolCommandName>
    <PackageId>DocConverter.Cli</PackageId>
    <RootNamespace>DocConverter.Cli</RootNamespace>
    <AssemblyName>docconv</AssemblyName>
    <Description>docconv: command line document conversion for humans and agents. Built on the DocConverter library.</Description>
    <PackageTags>document;conversion;cli;dotnet-tool;agent;markdown;pdf;docx</PackageTags>
    <PackageIcon>icon.png</PackageIcon>
    <PackageLicenseFile>LICENSE.md</PackageLicenseFile>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\DocConverter\DocConverter.csproj" />
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
    <None Include="..\..\LICENSE.md" Pack="true" PackagePath="\" />
    <None Include="..\..\assets\icon.png" Pack="true" PackagePath="\" />
    <InternalsVisibleTo Include="Test.Shared" />
  </ItemGroup>
</Project>
```

The TFMs are inherited from the props file (`net8.0;net10.0`). The tool therefore installs on machines that have either runtime, and the install scripts pick one the same way Mux's do.

### 4.3 Library dependencies

The versions below are the ones the Phase 0 spikes ran against on 2026-09-24 (Section 13). Licenses were read from each package's nuspec on nuget.org. `dotnet list package --vulnerable --include-transitive` reported no known vulnerabilities for this set on that date. Section 4.4 lists every transitive package.

| Package | Version | License | netstandard2.0 | Used for |
|---|---|---|---|---|
| DocumentFormat.OpenXml | 3.5.1 | MIT | yes (verified) | DOCX, XLSX, PPTX read and write; `OpenXmlValidator` in tests |
| PdfPig | 0.1.16 | Apache-2.0 | yes (verified) | PDF read (text, letters, fonts, images, metadata) |
| Tabula | 1.0.1 | MIT | yes (verified) | PDF table extraction (on PdfPig; requires PdfPig 0.1.14 or later) |
| PDFsharp | 6.2.4 | MIT | yes (verified) | PDF write (low level, image support checks) |
| PDFsharp-MigraDoc | 6.2.4 | MIT | yes (verified) | PDF write (flow layout: paragraphs, lists, tables) |
| HtmlAgilityPack | 1.12.4 | MIT | yes (verified) | HTML read. 1.13.0 is out; adopt it only after the HTML suites pass against it. |
| CsvHelper | 33.1.0 | MS-PL OR Apache-2.0 | yes (verified) | CSV and TSV read and write |
| Markdig | 1.4.0 | BSD-2-Clause | yes (verified) | Markdown read (CommonMark + GFM pipe tables, task lists, autolinks via `UseAdvancedExtensions`, which needs `using Markdig;`). Replaces DocumentAtom's hand-rolled Markdown splitter, which breaks fenced code that contains blank lines. |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | MIT | yes | `AddDocConverter()` |
| System.Text.Json | 9.0.10 | MIT | netstandard2.0 only | JSON read and write |
| Microsoft.Bcl.AsyncInterfaces | 9.0.10 | MIT | netstandard2.0 only | `IAsyncEnumerable`, `IAsyncDisposable` |
| System.Diagnostics.DiagnosticSource | 9.0.10 | MIT | netstandard2.0 only | `ActivitySource`, `Meter` |
| System.Memory | 4.6.3 | MIT | netstandard2.0 only | `Span<T>` and friends |

The netstandard2.0-only references sit in a conditioned `ItemGroup` with an explanatory comment, as in TextChunker. On net8.0 and net10.0 these ship in the BCL.

**Fonts for PDF output (required on macOS and Linux).** PDFsharp 6 on .NET Core and netstandard has no access to system fonts, so it needs an `IFontResolver`. Spike 1 proved that without one, **every** MigraDoc render fails on Linux, even an image-only page. The reason is that MigraDoc resolves `Courier New` as its internal error font. DocConverter therefore embeds Liberation Sans and Liberation Mono (regular, bold, italic, bold-italic; SIL OFL 1.1; 2.8 MB for the eight files) and serves them through `DocConverterFontResolver`. The resolver:

- maps every requested family to an embedded face, including `Courier New` and any other monospace name, which go to Liberation Mono
- is installed once, lazily, the first time a PDF is written
- gives way to a resolver the host has already set on `GlobalFontSettings`

Callers can supply their own resolver through `PdfOptions.FontResolver`. Noto Sans and Noto Sans Mono remain the alternative if CJK, Arabic or emoji glyphs matter more than package size (Section 15).

### 4.4 Complete dependency and license enumeration

Direct and transitive NuGet dependencies of the `DocConverter` library, resolved per target framework by `dotnet list package --include-transitive` on 2026-09-24. `DocConverter.Cli` adds nothing beyond a project reference to the library.

| Package | Version | License | Author | Direct? | netstandard2.0 | net8.0 | net10.0 |
|---|---|---|---|---|---|---|---|
| CsvHelper | 33.1.0 | MS-PL OR Apache-2.0 | Josh Close | direct | x | x | x |
| DocumentFormat.OpenXml | 3.5.1 | MIT | Microsoft | direct | x | x | x |
| DocumentFormat.OpenXml.Framework | 3.5.1 | MIT | Microsoft | via OpenXml | x | x | x |
| HtmlAgilityPack | 1.12.4 | MIT | ZZZ Projects et al. | direct | x | x | x |
| Markdig | 1.4.0 | BSD-2-Clause | Alexandre Mutel | direct | x | x | x |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | MIT | Microsoft | direct | x | x | x |
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | MIT | Microsoft | via PDFsharp | x | x | x |
| PdfPig | 0.1.16 | Apache-2.0 | UglyToad | direct | x | x | x |
| PDFsharp | 6.2.4 | MIT | PDFsharp Team | via MigraDoc | x | x | x |
| PDFsharp-MigraDoc | 6.2.4 | MIT | PDFsharp Team | direct | x | x | x |
| System.IO.Packaging | 8.0.1 (10.0.2 on net10.0) | MIT | Microsoft | via OpenXml | x | x | x |
| System.Security.Cryptography.Pkcs | 8.0.1 | MIT | Microsoft | via PDFsharp | x | x | x |
| Tabula | 1.0.1 | MIT | BobLd | direct | x | x | x |
| Microsoft.Bcl.AsyncInterfaces | 9.0.10 | MIT | Microsoft | direct | x | | |
| Microsoft.Bcl.HashCode | 6.0.0 | MIT | Microsoft | via PdfPig | x | | |
| Microsoft.CSharp | 4.7.0 | MIT | Microsoft | via CsvHelper | x | | |
| System.Buffers | 4.6.1 | MIT | Microsoft | via System.Memory | x | | |
| System.Diagnostics.DiagnosticSource | 9.0.10 | MIT | Microsoft | direct | x | | |
| System.Formats.Asn1 | 8.0.1 | MIT | Microsoft | via Pkcs | x | | |
| System.IO.Pipelines | 9.0.10 | MIT | Microsoft | via System.Text.Json | x | | |
| System.Memory | 4.6.3 | MIT | Microsoft | direct | x | | |
| System.Numerics.Vectors | 4.6.1 | MIT | Microsoft | via System.Memory | x | | |
| System.Runtime.CompilerServices.Unsafe | 6.1.2 | MIT | Microsoft | via System.Memory | x | | |
| System.Security.Cryptography.Cng | 5.0.0 | MIT | Microsoft | via Pkcs | x | | |
| System.Text.Encodings.Web | 9.0.10 | MIT | Microsoft | via System.Text.Json | x | | |
| System.Text.Json | 9.0.10 | MIT | Microsoft | direct | x | | |
| System.Threading.Tasks.Extensions | 4.5.4 | MIT (corefx LICENSE.TXT) | Microsoft | via AsyncInterfaces | x | | |
| NETStandard.Library | 2.0.3 | MIT | Microsoft | implicit SDK reference | build only | | |
| Microsoft.NETCore.Platforms | 1.1.0 | Microsoft .NET Library License | Microsoft | via NETStandard.Library | build only | | |

Two notes on the last rows:

- **`NETStandard.Library` and `Microsoft.NETCore.Platforms`** are implicit build-time references that the SDK adds to every netstandard2.0 project. They are not written into the packed nuspec. `Microsoft.NETCore.Platforms` 1.1.0 carries only a `runtime.json` and an empty `_._` placeholder, so no code under its license reaches a DocConverter consumer.
- **The pre-2019 package license** is Microsoft's .NET Library License, not an SPDX expression, which is why it's called out here rather than silently accepted. The same list goes into `docs/FORMATS.md` under "Third-party components", and a `DependencySuite` test keeps it in sync (Section 10.4).

Embedded, non-NuGet assets:

| Asset | License | Notes |
|---|---|---|
| Liberation Sans (4 faces), Liberation Mono (4 faces) 2.1.5 | SIL Open Font License 1.1 | Embedded resources in `Resources/Fonts/`. The OFL text ships alongside them as `OFL.txt`, and the README acknowledges it. |

Test-only packages add Touchstone.Core, Touchstone.Cli, Touchstone.XunitAdapter and Touchstone.NunitAdapter (MIT), plus xunit (Apache-2.0), xunit.runner.visualstudio (Apache-2.0), NUnit (MIT), NUnit.Analyzers (MIT), NUnit3TestAdapter (MIT), Microsoft.NET.Test.Sdk (MIT) and coverlet.collector (MIT). None of them ship in either package.

**Ported, not referenced.** The DocumentAtom logic comes across as source. It covers type detection, the DOCX, XLSX, PPTX, PDF, RTF, HTML, CSV, JSON and XML extraction, and the XLSX header-row scoring. `SerializableDataTable`, `SerializationHelper` and `TextChunker` are **not** brought along, because the DocConverter model has its own table type and uses System.Text.Json directly.

---

## 5. Architecture

Every conversion runs the same pipeline:

```
input (string | byte[] | Stream)
  -> InputBuffer          normalize to a seekable, size-limited stream; decode Base64 for binary-format strings
  -> FormatDetector       only when source == Auto (magic bytes, zip part inspection, text heuristics)
  -> IDocumentReader      source format -> DocumentModel
  -> IDocumentWriter      DocumentModel -> target format, written to a stream
  -> output (Stream | string | byte[]) + ConversionResult (warnings, statistics, timings)
```

The `DocumentModel` in the middle is the design. Converting through one rich intermediate model turns N x M converters into N readers plus M writers. It also makes a full test matrix achievable. DocumentAtom's `Atom` tree is not rich enough to be that model:

- It has no inline formatting.
- It has no links inside paragraphs.
- It puts DOCX images at the end of the document instead of where they appear.
- It stores PPTX titles outside `Text`.
- It loses RTF heading levels.

Porting keeps DocumentAtom's extraction logic but retargets its output onto the richer model, and fixes those defects along the way.

### 5.1 The document model (`DocConverter.Model`)

The model has one class per file and no partial classes. Every type is public, so callers can read or build documents directly (Section 6.3).

```
DocumentModel
  Metadata : DocumentMetadata        Title, Subject, Author, Keywords, Description, Language, Created, Modified
  Blocks   : List<Block>
  Resources: Dictionary<string, BinaryResource>   images referenced by id from ImageBlock / ImageInline

Block (abstract)                     Id, SourcePage (int?), SourceSheet (string?), SourceSlide (int?)
  SectionBlock        Kind (SectionKindEnum: Page, Slide, Sheet, Generic), Title, Blocks
  HeadingBlock        Level (1..6, clamped), Inlines
  ParagraphBlock      Inlines, Alignment (TextAlignmentEnum)
  ListBlock           Kind (ListKindEnum: Ordered, Unordered, Task), Start (int), Items : List<ListItemBlock>
  ListItemBlock       Blocks (paragraphs and nested ListBlocks), Checked (bool?, for task lists)
  TableBlock          Rows : List<TableRow>, HeaderRowCount (int), Caption, ColumnAlignments
  TableRow            Cells : List<TableCell>
  TableCell           Blocks, ColumnSpan, RowSpan, IsHeader
  CodeBlock           Language, Text
  QuoteBlock          Blocks
  ImageBlock          ResourceId, AltText, Caption, Width, Height (points, nullable)
  ThematicBreakBlock
  PageBreakBlock

Inline (abstract)
  TextInline          Text, Style (InlineStyleEnum [Flags]: Bold, Italic, Underline, Strikethrough, Code, Superscript, Subscript)
  LinkInline          Url, Title, Inlines
  ImageInline         ResourceId, AltText
  LineBreakInline

BinaryResource        Id, MediaType, Data (byte[]), FileName, PixelWidth, PixelHeight
```

**Invariants that readers must honor and tests assert:**

- Heading levels are 1 to 6.
- Every `ResourceId` resolves.
- Table rows can have ragged cell counts, and writers normalize them.
- No `null` collections anywhere: every list is initialized, and setters coalesce `null` to empty.
- Text is stored unescaped, and every writer does its own escaping.

`DocumentModelValidator` (internal) checks these invariants after every read in Debug builds, and always in tests.

**Serialization.** The model has a canonical JSON form and a canonical XML form, with a `"docconverter": "1"` / `<docconverter version="1">` marker. JSON and XML output of a document is this canonical form. JSON and XML input first looks for the marker:

- **Marker present:** the input is deserialized losslessly. This makes `X -> JSON -> Y` exactly equal to `X -> Y`, which the round-trip suites rely on.
- **Marker absent:** the input is arbitrary data and is mapped structurally, following DocumentAtom's JSON and XML processors. Objects and arrays become tables, and nesting becomes sections.

The model uses named DTO types for its fixed contract, never `JsonNode` or `JsonElement`, as `BACKEND_ARCHITECTURE.md` requires. The generic-JSON reader is the one place that walks `JsonDocument`, because its input has no fixed contract. That exception is documented in the code.

### 5.2 Readers and writers

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

`ConversionContext` is public so custom readers and writers can use it. It carries:

- the warning collector: `AddWarning(WarningCodeEnum, string)`
- the statistics counters
- the logger
- the resource limits (`MaxInputBytes`, `MaxDecompressedBytes`, `MaxNestingDepth`)

One context is created per conversion, so readers and writers never keep state between calls.

Reader and writer classes are named `<Format>DocumentReader` and `<Format>DocumentWriter`. This avoids collisions with `System.IO.TextWriter`, `System.Text.Json.Utf8JsonWriter` and `System.Xml.XmlWriter`.

`FormatRegistry` (internal, owned by `Converter`) maps each format to one reader and one writer. `Converter.RegisterReader` and `RegisterWriter` let callers replace or add implementations. The registry sits behind a `ReaderWriterLockSlim`, since it is read on every call and written rarely.

### 5.3 Formats

`DocumentFormatEnum` values: `Auto`, `Text`, `Markdown`, `Html`, `Json`, `Xml`, `Csv`, `Tsv`, `Rtf`, `Docx`, `Xlsx`, `Pptx`, `Pdf`, `Png`, `Jpeg`, `Gif`, `Bmp`, `Tiff`, `WebP`.

- `Auto` is valid only as a source.
- There is deliberately no `Unknown` value. Detection that fails throws `UnsupportedFormatException`, carrying whatever was recognized.
- Recognized formats that are unsupported still produce a clear message. For example: "Input is a legacy Word 97-2003 (.doc) file. Save it as .docx first." That covers `.doc`, `.xls`, `.ppt`, ODF, EPUB, iWork, archives and media.
- The `DetectionResult.RecognizedAs` string carries that description. The enum stays clean.

`DocumentFormatParser` (public, static) provides:

- `TryParse(string)`, which accepts names, aliases (`md`, `markdown`, `htm`, `txt`, `word`, `excel`, `powerpoint`, `jpg`, `tif`) and extensions with or without the dot
- `FromExtension(string)`
- `GetMediaType(DocumentFormatEnum)`
- `GetDefaultExtension(DocumentFormatEnum)`
- `IsTextBased(DocumentFormatEnum)`

The CLI uses all of these.

### 5.4 Format detection (`FormatDetector`)

This is ported from DocumentAtom's `TypeDetector`, with these changes:

- **Stream-based and in memory.** It peeks at up to `DetectionBufferBytes` (default 64 KB) of a seekable stream and rewinds. The zip check opens a `ZipArchive` over the buffered input and reads entry names only. DocumentAtom extracted archives to a temp directory; DocConverter never does.
- **Office detection by content type, not folder names.** `[Content_Types].xml` and the main part's content type decide DOCX, XLSX or PPTX. DocumentAtom also required `docProps/`, which some valid generators omit.
- **Tighter OLE2 classification.** DocumentAtom's `"PP"` marker for `.ppt` is loose. The `.doc`, `.xls` and `.ppt` split reads the stream names in the compound file directory. This affects only the error message.
- **Markdown by heuristic.** DocumentAtom recognized Markdown only when a content type was passed. DocConverter scores ATX headings, list markers, fences, pipe tables and link syntax, and returns `Markdown` above a documented threshold. Otherwise it returns `Text`.
- **TSV by heuristic.** Consistent tab counts across lines mean TSV. Consistent comma counts, with a CSV parse that succeeds, mean CSV.
- **Extension hint.** `DetectFormatAsync(..., string? fileNameHint)` lets the CLI pass the file extension. For ambiguous text formats the hint wins over the heuristics. For binary formats the magic bytes win.
- **Ordering** is the same as DocumentAtom: the binary-signature path first, then the text path (RTF, JSON parse, XML parse refined to HTML, HTML heuristic, Markdown, CSV and TSV, then Text).

### 5.5 Readers: source, port notes, fixes

| Format | Reader | Built on | Port source in DocumentAtom | Fixes and improvements |
|---|---|---|---|---|
| Text | `TextDocumentReader` | BCL | `TextProcessor` | BOM detection (UTF-8, UTF-16 LE and BE, UTF-32), then `ConversionOptions.InputEncoding`, default UTF-8. Paragraphs split on blank lines, single newlines become `LineBreakInline`. Streams, never `ReadToEnd` over the limit. |
| Markdown | `MarkdownDocumentReader` | Markdig | none (replaces `MarkdownProcessor`) | Full CommonMark plus GFM pipe tables, task lists, strikethrough, autolinks. Maps the Markdig AST onto the model. Inline images become `ImageInline`. `data:` URIs become resources. Remote URLs are kept as links and never fetched. |
| HTML | `HtmlDocumentReader` | HtmlAgilityPack | `HtmlProcessor`, `HtmlAtom` | Walks the body into the model. Inline `b`, `strong`, `i`, `em`, `u`, `s`, `del`, `code`, `sup`, `sub` and `a` become inline styles and links (DocumentAtom flattened them). `table` handles `th`, `thead`, `colspan` and `rowspan`. `script`, `style`, `noscript` and `template` are ignored. `<title>` and `<meta>` fill the metadata. Removes the `Console.WriteLine` calls. |
| JSON | `JsonDocumentReader` | System.Text.Json | `JsonProcessor` | Marker check first (lossless model). Otherwise the structural mapping: object to key/value table, array of objects to table, nesting to `SectionBlock`, depth-limited by `MaxNestingDepth` (default 64). |
| XML | `XmlDocumentReader` | System.Xml | `XmlProcessor` | Marker check first. `XmlReaderSettings { DtdProcessing = Prohibit, XmlResolver = null }` (no XXE). Mapping: elements to sections or tables, attributes optional. |
| CSV, TSV | `DelimitedDocumentReader` | CsvHelper | `CsvProcessor` | One `TableBlock`. Header row configurable (`CsvOptions.HasHeaderRow`, default true). Delimiter from format or options. Fixes DocumentAtom's `Length = hash length` bug (not carried over, since the model has no such field). Ragged rows are padded. |
| RTF | `RtfDocumentReader` | hand-written | `RtfProcessor`, `RtfToken*`, `RtfParseContext`, `RtfDocument*`, `RtfTableData` | Header levels are **kept** (DocumentAtom dropped them). Inline bold, italic and underline come from the `\b`, `\i` and `\ul` control words. Unicode `\uN` handling. Embedded `\pict` PNG and JPEG become resources. |
| DOCX | `DocxDocumentReader` | OpenXml (stream) | `DocxProcessor` | Opens the stream directly, with no temp file. Headings come from style ids and outline levels, including localized style names through `w:outlineLvl`. Runs carry bold, italic, underline, strike and code (monospace font). `w:hyperlink` becomes `LinkInline`. Images are inline **at their position**. Nested lists use `numPr/ilvl`. Tables handle `gridSpan` and `vMerge`. Text boxes, footnotes and endnotes are appended as sections with a warning. Core properties fill the metadata. |
| XLSX | `XlsxDocumentReader` | OpenXml (stream) | `XlsxProcessor`, `HeaderRowDetector`, `HeaderRowPatternWeights`, `CellData` | One `SectionBlock(Sheet)` per sheet, holding one `TableBlock` over the used range. Header detection is ported as is. Shared strings, inline strings, booleans, dates (number formats to ISO 8601), formulas (cached value, never evaluated) and merged cells as spans. Hidden sheets are skipped unless `XlsxOptions.IncludeHiddenSheets`. |
| PPTX | `PptxDocumentReader` | OpenXml (stream) | `PptxProcessor`, `SlideTitleInfo` | One `SectionBlock(Slide)` per slide. The title becomes a `HeadingBlock(1)` and the subtitle a `HeadingBlock(2)`. DocumentAtom stored these only in `Title` and `Subtitle`. Shapes are ordered top to bottom, then left to right. Bullet paragraphs become lists, with ordered numbering read from `a:buAutoNum`. Tables, pictures, and speaker notes (optional, `PptxOptions.IncludeNotes`) are included. |
| PDF | `PdfDocumentReader` | PdfPig + Tabula | `PdfProcessor`, `PdfRegion` | Text blocks come from PdfPig's `RecursiveXYCut` and reading order. **New: headings** come from font size relative to the page's median body size (`PdfOptions.HeadingSizeRatio`, default 1.2) and bold font names. List heuristics are ported. Tables are extracted with Tabula (`ObjectExtractor.Extract(document, page)` and `SpreadsheetExtractionAlgorithm`), and table regions are removed from text blocks. **Spike 3 found that ruled tables extract exactly, but `SimpleNurminenDetectionAlgorithm` missed an unruled table**, so unruled PDF tables arrive as paragraphs. This loss is documented. A page with no text layer (a scanned image) produces its images and the `NoTextLayer` warning; OCR would recover the text later (Section 5.8). Images: JPEG passes through, PNG uses `TryGetPng`, and anything else is skipped with a warning. Each page becomes a `SectionBlock(Page)` when `PdfOptions.PreservePages` is set (default false, which flattens the pages). Encrypted PDFs throw `DocumentReadException` with a clear message. |
| Images | `ImageDocumentReader` | BCL (header parsing only) | none | Produces one `ImageBlock` plus its resource. `ImageHeaderReader` reads pixel dimensions, and for JPEG the component count (to spot CMYK), from the PNG IHDR, JPEG SOF, GIF, BMP, TIFF and WebP (VP8, VP8L, VP8X) headers. Nothing is decoded, and nothing platform-specific is used. No OCR: an image-only source converted to a text-only target yields a documented placeholder line (Section 7). |

### 5.6 Writers

| Format | Writer | Built on | Behavior |
|---|---|---|---|
| Markdown | `MarkdownDocumentWriter` | BCL | GFM output: ATX headings, `-` and `1.` lists with 2-space nesting, pipe tables (cells with block content are flattened with `<br>`), fenced code with a language tag, `>` quotes, `---` breaks. Escaping of `\ * _ [ ] # | < >` where needed. Images follow `MarkdownOptions.ImageMode`: `DataUri` (default), `Omit`, `Placeholder` or `External` (writes a relative file name, and the caller gets the bytes through `ConversionResult.Resources`). |
| HTML | `HtmlDocumentWriter` | BCL | HTML5. `HtmlOptions.Mode`: `Document` (default, full page with an optional minimal embedded stylesheet) or `Fragment` (body content only). Every text node and attribute is encoded. Link URLs are allow-listed by scheme (`http`, `https`, `mailto`, relative, `#`); anything else, including `javascript:`, is dropped with a warning. Images are `data:` URIs or external names. |
| Text | `PlainTextDocumentWriter` | BCL | Headings get an optional underline style (`TextOptions.HeadingStyle`: `None`, `Underline`, `Uppercase`). Lists use `-` and `1.`. Tables are rendered as aligned columns (default) or tab-separated (`TextOptions.TableStyle`). Optional hard wrap at `TextOptions.WrapColumn` (default 0, meaning no wrap; allowed 0 or 20 to 1000). |
| JSON | `JsonDocumentWriter` | System.Text.Json | Canonical model JSON, camelCase, indented by default (`JsonOptions.Indented`). Resources are Base64 unless `JsonOptions.IncludeBinary = false`, in which case only metadata is written. |
| XML | `XmlDocumentWriter` | System.Xml | Canonical model XML with the `docconverter` root and version attribute. |
| CSV, TSV | `DelimitedDocumentWriter` | CsvHelper | Writes tables only. `CsvOptions.TableSelection`: `First` (default), `All` (tables separated by one empty record), or `Index` plus `TableIndex`. When the document has no tables, `CsvOptions.NoTableBehavior` decides: `ParagraphsAsRows` (default, one column, one row per block, with a warning) or `Error`. RFC 4180 quoting. |
| DOCX | `DocxDocumentWriter` | OpenXml | Built from scratch in code with no template binary. It adds styles (Normal, Title, Heading1 to 6, Code, Quote, ListParagraph, TableGrid), numbering definitions for ordered and unordered lists up to 9 levels, tables with header-row repeat, spans (`gridSpan`, `vMerge`), and inline images (`DrawingML`, sized from pixel dimensions and capped to the page width). Spike 2 verified every image format as an embedded part: PNG, JPEG including CMYK, GIF, BMP, TIFF and WebP. WebP parts get the `image/webp` content type through a `PartTypeInfo`. Hyperlinks are relationships. Core properties come from metadata. Page size and margins come from `DocxOptions`. |
| XLSX | `XlsxDocumentWriter` | OpenXml | One worksheet per `TableBlock`. `SectionBlock(Sheet)` titles become sheet names. Non-table content goes to a leading "Document" sheet, one block per row, when `XlsxOptions.IncludeNonTableContent` is set (default true). Sheet names are sanitized: 31 characters, `[]:*?/\` stripped, unique suffixes. `XlsxOptions.InferCellTypes` (default true) writes invariant-culture numbers, booleans and ISO dates as typed cells. The header row is bold and frozen. |
| PPTX | `PptxDocumentWriter` | OpenXml | Builds a minimal theme, slide master, title layout and content layout in code. Spike 2 confirmed that this approach needs no template binary: a code-built master, layout and theme passes `OpenXmlValidator` and opens in PowerPoint. A new slide starts at each `SectionBlock(Slide)`, or at each heading of level `PptxOptions.SlideSplitHeadingLevel` or lower (default 2). A slide also overflows after `PptxOptions.MaxBlocksPerSlide` blocks (default 12) or `MaxTableRowsPerSlide` rows (default 15). Titles go in the title placeholder. Lists become bulleted and numbered paragraphs. Tables become `a:tbl`. Images are sized to fit. |
| PDF | `PdfDocumentWriter` | MigraDoc + PDFsharp | The model is mapped to a MigraDoc `Document` and rendered with `PdfDocumentRenderer`. Page size (`A4`, `Letter`, `Legal`) and margins come from `PdfOptions`. It handles headings, lists, tables (fixed column widths computed from content length) and code in the monospace face. Metadata goes into the PDF info dictionary. **Images follow the Spike 1 results.** PNG (RGB, RGBA, palette), JPEG (baseline and progressive, 1 or 3 components) and BMP embed natively. GIF, TIFF, WebP and CMYK JPEG are **not** supported by PDFsharp. The writer checks every image against `ImageHeaderReader` *before* handing it to MigraDoc, because MigraDoc does not throw on an unsupported image: it silently prints "Image has no valid type." into the page. An unsupported image becomes a bordered placeholder paragraph (`[Image: <alt or file name>, <FORMAT> <w>x<h>, not embeddable in PDF]`) and raises the `ImageFormatUnsupported` warning. **Glyph coverage:** Liberation covers Latin, Greek and Cyrillic. The writer checks each character against the embedded font's `cmap` table (parsed once from the TTF). Missing characters (CJK, Arabic, emoji) render blank, so they raise `GlyphsUnavailable` with a count. |

**Deterministic output.** When `ConversionOptions.Deterministic` is true (default false), writers pin every timestamp and generated identifier:

- DOCX, XLSX and PPTX core-property dates
- the PDF `CreationDate`, `ModDate` and `/ID`
- zip entry timestamps

Identical input then yields identical bytes, which the golden-file tests rely on.

### 5.7 Warnings and statistics

`WarningCodeEnum` covers these codes:

- `ImagesOmitted`, `ImageFormatUnsupported`
- `FormattingLost`
- `TablesFlattened`, `TableSpansFlattened`, `NonTableContentDropped`
- `NestedDepthLimited`
- `LinkRemovedUnsafe`
- `HeadingsInferred`
- `EncryptedContentSkipped`
- `NotesIncluded`
- `ContentTruncated`
- `GlyphsUnavailable` (characters the embedded PDF font cannot draw)
- `ImagePlaceholderEmitted` (an image-only source went to a target that cannot show images)
- `NoTextLayer` (a PDF page with no extractable text; OCR would be needed)
- `UnknownElementSkipped`

Some losses cannot be detected at run time. The main one is an unruled PDF table read as paragraphs. These have no warning code and are listed in `docs/FORMATS.md` under "Known limitations".

A reader or writer adds a warning whenever it drops or approximates content that the other side could have carried. Each code gets at most one warning, with a count (`"3 images were omitted because ImageMode is Omit"`). Warnings never throw. `ConversionOptions.TreatWarningsAsErrors` (default false) turns any warning into a `ConversionWarningException` after the write completes. The output has already been written to a caller-owned stream at that point, and the documentation says so.

Every warning code has an entry in `docs/FORMATS.md` saying what was lost, when it happens, and which option (if any) avoids it. A test asserts that every `WarningCodeEnum` value has that entry.

`ConversionStatistics` counts:

- pages, slides and sheets
- headings, paragraphs, lists and list items
- tables, table rows and table cells
- images and links
- characters of text

### 5.8 OCR extension point (stubs only in 0.1.0)

OCR is out of scope for 0.1.0. The public shape it will attach to ships now, so a later OCR project is purely additive. All of these live in `src/DocConverter/Ocr/`, one type per file:

```csharp
/// Recognizes text in an image. Implementations must be thread safe.
public interface IOcrProvider
{
    string Name { get; }
    bool Supports(string mediaType);
    Task<OcrResult> RecognizeAsync(BinaryResource image, OcrOptions options, CancellationToken token = default);
}

/// Convenience base for providers. Every member throws NotImplementedException until overridden.
public abstract class OcrProviderBase : IOcrProvider
{
    public virtual string Name { get { throw new NotImplementedException("OcrProviderBase.Name must be overridden."); } }
    public virtual bool Supports(string mediaType) { throw new NotImplementedException("OcrProviderBase.Supports must be overridden."); }
    public virtual Task<OcrResult> RecognizeAsync(BinaryResource image, OcrOptions options, CancellationToken token = default)
    { throw new NotImplementedException("OcrProviderBase.RecognizeAsync must be overridden."); }
}

public class OcrOptions   { Languages (List<string>, default ["eng"]), MinimumConfidence (0.0 to 1.0, default 0.6), DetectTables (bool), DetectLists (bool) }
public class OcrResult    { Blocks : List<Block> (paragraphs, tables and lists in the document model), Confidence (0.0 to 1.0), Language }
```

Wiring points:

- **`ConverterSettings.OcrProvider`** (`IOcrProvider?`, default `null`).
- **`ConversionOptions.Ocr`** (`OcrOptions`), plus **`ConversionOptions.OcrMode`** (`OcrModeEnum`: `Off` (default), `ImagesOnly`, `PagesWithoutText`, `All`).
- **`OcrStage`** (internal) sits between the reader and the writer. In 0.1.0 it does one thing: when a provider is configured **and** `OcrMode` is not `Off`, it throws `NotImplementedException("OCR integration is not implemented in DocConverter 0.1.0.")`. With the defaults it is a no-op. A configured provider is therefore never silently ignored.
- **`Converter.CanConvert`** and `GetSupportedConversions` take the provider into account, so that when OCR lands, cells such as `Png -> Text` move from placeholder (P) to real text (F) without an API change.

The future project, `DocConverter.Ocr.Tesseract` (or any other engine), will subclass `OcrProviderBase`. It will be its own package, so the core stays free of native dependencies. Tests in 0.1.0 (`OcrStubSuite`) pin the stub behavior: every `OcrProviderBase` member throws `NotImplementedException`, a configured provider with `OcrMode.Off` converts normally, and one with `OcrMode.ImagesOnly` throws `NotImplementedException`.

---

## 6. Public API

Everything in the listings below has XML documentation, including defaults, minimums, maximums, nullability, thread safety and `<exception>` tags.

### 6.1 `Converter`

```csharp
namespace DocConverter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocConverter.Readers;
    using DocConverter.Results;
    using DocConverter.Writers;

    /// <summary>
    /// Converts documents between formats. Thread safe: one instance can serve concurrent conversions.
    /// </summary>
    public class Converter : IConverter
    {
        public Converter();
        public Converter(ConverterSettings? settings);

        public ConverterSettings Settings { get; }

        // Convert, writing into a caller-owned stream. The stream is not closed or rewound.
        public Task<ConversionResult> ConvertAsync(string input, DocumentFormatEnum from, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default);
        public Task<ConversionResult> ConvertAsync(byte[] input, DocumentFormatEnum from, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default);
        public Task<ConversionResult> ConvertAsync(Stream input, DocumentFormatEnum from, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default);

        // Convert, returning a string. Text targets return the text; binary targets return Base64.
        public Task<StringConversionResult> ConvertToStringAsync(string input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);
        public Task<StringConversionResult> ConvertToStringAsync(byte[] input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);
        public Task<StringConversionResult> ConvertToStringAsync(Stream input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);

        // Convert, returning bytes. Text targets are encoded with ConversionOptions.OutputEncoding (UTF-8, no BOM).
        public Task<BytesConversionResult> ConvertToBytesAsync(string input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);
        public Task<BytesConversionResult> ConvertToBytesAsync(byte[] input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);
        public Task<BytesConversionResult> ConvertToBytesAsync(Stream input, DocumentFormatEnum from, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);

        // Convert files by path. Auto infers the source from content plus extension and the target from the output extension.
        public Task<ConversionResult> ConvertFileAsync(string inputPath, string outputPath, DocumentFormatEnum from = DocumentFormatEnum.Auto, DocumentFormatEnum to = DocumentFormatEnum.Auto, bool overwrite = false, ConversionOptions? options = null, CancellationToken token = default);

        // Two-step API for callers that want to inspect or edit the model.
        public Task<DocumentModel> ReadAsync(string input, DocumentFormatEnum from, ConversionOptions? options = null, CancellationToken token = default);
        public Task<DocumentModel> ReadAsync(byte[] input, DocumentFormatEnum from, ConversionOptions? options = null, CancellationToken token = default);
        public Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum from, ConversionOptions? options = null, CancellationToken token = default);
        public Task<ConversionResult> WriteAsync(DocumentModel document, DocumentFormatEnum to, Stream output, ConversionOptions? options = null, CancellationToken token = default);
        public Task<StringConversionResult> WriteToStringAsync(DocumentModel document, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);
        public Task<BytesConversionResult> WriteToBytesAsync(DocumentModel document, DocumentFormatEnum to, ConversionOptions? options = null, CancellationToken token = default);

        // Detection and capabilities.
        public Task<DetectionResult> DetectFormatAsync(byte[] input, string? fileNameHint = null, CancellationToken token = default);
        public Task<DetectionResult> DetectFormatAsync(Stream input, string? fileNameHint = null, CancellationToken token = default);
        public Task<DetectionResult> DetectFormatAsync(string input, string? fileNameHint = null, CancellationToken token = default);
        public bool CanConvert(DocumentFormatEnum from, DocumentFormatEnum to);
        public IReadOnlyList<SupportedConversion> GetSupportedConversions();
        public IReadOnlyList<DocumentFormatEnum> GetInputFormats();
        public IReadOnlyList<DocumentFormatEnum> GetOutputFormats();

        // Extensibility.
        public void RegisterReader(IDocumentReader reader);
        public void RegisterWriter(IDocumentWriter writer);
    }
}
```

The ReadAsync and WriteAsync pairs are the answer to "more appropriate API to anchor onto". Callers who want to redact, merge or post-process a document can read it once, edit the `DocumentModel`, and write it to several targets without parsing it again. Custom readers and writers plug in without forking the library.

**Input semantics:**

- **`string` input.** For a text-based source format (Text, Markdown, HTML, JSON, XML, CSV, TSV, RTF) the string *is* the document. For a binary source format (DOCX, XLSX, PPTX, PDF, images) the string must be Base64. Anything else throws `DocumentReadException` with a message saying so. `DocumentFormatEnum.Auto` with a string input tries Base64 only if the text-format detection fails.
- **`byte[]` input.** Wrapped in a non-copying `MemoryStream`.
- **`Stream` input.** Never closed or disposed. A non-seekable stream is buffered into memory up to `ConverterSettings.MaxInputBytes`, then reading stops and `InputTooLargeException` is thrown. A seekable stream is read from its current position.
- **Empty input** (zero bytes, or an empty string) is valid for text formats and produces an empty document. For binary formats it throws `DocumentReadException`.

**Output semantics:**

- **Output `Stream`.** Must be writable (`ArgumentException` otherwise). The writer writes from the current position, flushes, and leaves the stream open at the end of the written data.
- **`ConvertToStringAsync` for binary targets** returns Base64. `StringConversionResult.IsBase64` is set, so callers never have to guess.

**Exceptions:**

- `ArgumentNullException` for null input, output or document.
- `ArgumentException` when the target is `Auto`, the output stream is not writable, or the input stream is not readable.
- `UnsupportedFormatException` when detection fails or recognizes an unsupported format.
- `ConversionNotSupportedException` when no registered reader or writer covers the pair. Every built-in pair is supported (Section 7), so this only happens with caller-registered formats.
- `InputTooLargeException`.
- `DocumentReadException` for corrupt, encrypted or malformed input, with the parser exception as `InnerException`.
- `DocumentWriteException`.
- `InvalidConversionOptionsException` when an option combination is invalid.
- `ConversionWarningException` under `TreatWarningsAsErrors`.
- `OperationCanceledException` on cancellation.
- `NotImplementedException` only from the OCR stubs (Section 5.8).

All the domain exceptions derive from `DocConverterException`, and each has both the `(message)` and `(message, inner)` constructors.

### 6.2 Results

```
ConversionResult
  SourceFormat : DocumentFormatEnum          resolved (never Auto)
  TargetFormat : DocumentFormatEnum
  DetectedSource : DetectionResult?          set when the source was Auto
  BytesRead : long
  BytesWritten : long
  StartedUtc, CompletedUtc : DateTime
  TotalMs, ReadMs, WriteMs : double
  Warnings : IReadOnlyList<ConversionWarning>    Code (WarningCodeEnum), Message, Count
  Statistics : ConversionStatistics
  Metadata : DocumentMetadata
  Resources : IReadOnlyList<BinaryResource>  populated only when an ImageMode of External asks for side files

StringConversionResult : ConversionResult
  Output : string
  IsBase64 : bool

BytesConversionResult : ConversionResult
  Output : byte[]

DetectionResult
  Format : DocumentFormatEnum?               null when unsupported
  MediaType : string
  Extension : string
  RecognizedAs : string                      human description, including unsupported formats
  Confidence : DetectionConfidenceEnum       Signature, Structure, Heuristic, ExtensionHint

SupportedConversion
  From, To : DocumentFormatEnum
  Fidelity : FidelityEnum                    Full, Projection (see Section 7)
  Notes : string
```

### 6.3 Settings and options

`ConverterSettings` holds instance-level settings, shared by every conversion on that instance:

| Member | Default | Range | Effect |
|---|---|---|---|
| `DefaultOptions` | `new ConversionOptions()` | not null | Used when a call passes `null` options |
| `MaxInputBytes` | 268,435,456 (256 MB) | 1 to `int.MaxValue` | Input larger than this throws `InputTooLargeException` |
| `MaxDecompressedBytes` | 1,073,741,824 (1 GB) | 1 to `long.MaxValue` | Zip-bomb guard for DOCX, XLSX and PPTX parts |
| `MaxNestingDepth` | 64 | 1 to 1024 | Lists, sections, JSON and XML depth |
| `DetectionBufferBytes` | 65,536 | 512 to 16 MB | Bytes peeked for detection |
| `Logger` | `null` | any | `Action<SeverityEnum, string>`. Never writes to the console on its own. |
| `OcrProvider` | `null` | any | Reserved for OCR (Section 5.8). In 0.1.0, a non-null provider combined with an `OcrMode` other than `Off` throws `NotImplementedException`. |

`ConversionOptions` holds per-call options:

| Member | Default | Effect |
|---|---|---|
| `IncludeImages` | `true` | `false` drops all images, with the `ImagesOmitted` warning |
| `IncludeMetadata` | `true` | Carry metadata into targets that support it |
| `Title` | `null` | Overrides the metadata title |
| `InputEncoding` | `null` (BOM detection, then UTF-8) | Text-based sources |
| `OutputEncoding` | UTF-8 without BOM | Text-based targets |
| `LineEnding` | `LineEndingEnum.Lf` | `Lf`, `CrLf` or `Platform` |
| `Deterministic` | `false` | Pinned timestamps and ids for binary targets |
| `TreatWarningsAsErrors` | `false` | See 5.7 |
| `OcrMode`, `Ocr` | `Off`, `new OcrOptions()` | Reserved for OCR (Section 5.8) |
| `Markdown`, `Html`, `Text`, `Json`, `Xml`, `Csv`, `Docx`, `Xlsx`, `Pptx`, `Pdf` | new instances | Per-format read and write options (`docs/OPTIONS.md` lists every member) |

Every numeric option follows TextChunker's validated-setter pattern (a backing `_Field` plus a throwing or clamping setter). For each option, the XML docs state the default, the minimum, the maximum and what changing it does. Static presets follow TextChunker's `ForRag()` idea:

- `ConversionOptions.ForLlmIngestion()`: Markdown-friendly, images omitted, metadata kept.
- `ConversionOptions.ForArchival()`: images embedded, deterministic output.
- `ConversionOptions.Minimal()`.

### 6.4 Dependency injection and observability

- `DocConverter.DependencyInjection.ServiceCollectionExtensions.AddDocConverter(this IServiceCollection services, Action<ConverterSettings>? configure = null)` registers `IConverter` as a singleton.
- `DocConverter.Observability.DocConverterDiagnostics` exposes an `ActivitySource` and a `Meter`, both named `DocConverter`, with these instruments:
  - `docconverter.conversions` (counter, tags `from`, `to`, `outcome`)
  - `docconverter.conversion.duration` (histogram, ms)
  - `docconverter.input.bytes` (histogram)
  - `docconverter.warnings` (counter, tag `code`)
- Tags stay low-cardinality. Instrumentation is best effort and never throws into a conversion.

### 6.5 Library source layout

```
src/DocConverter/
  Converter.cs
  IConverter.cs
  ConverterSettings.cs
  ConversionContext.cs
  Enums/            DocumentFormatEnum, DetectionConfidenceEnum, FidelityEnum, ImageModeEnum, InlineStyleEnum,
                    LineEndingEnum, ListKindEnum, SectionKindEnum, SeverityEnum, TextAlignmentEnum, WarningCodeEnum,
                    TableSelectionEnum, NoTableBehaviorEnum, HtmlOutputModeEnum, PdfPageSizeEnum,
                    TextHeadingStyleEnum, TextTableStyleEnum, OcrModeEnum
  Model/            DocumentModel, DocumentMetadata, BinaryResource, Block, SectionBlock, HeadingBlock, ParagraphBlock,
                    ListBlock, ListItemBlock, TableBlock, TableRow, TableCell, CodeBlock, QuoteBlock, ImageBlock,
                    ThematicBreakBlock, PageBreakBlock, Inline, TextInline, LinkInline, ImageInline, LineBreakInline
  Model/Serialization/  canonical JSON and XML DTOs and mappers (one class per file)
  Options/          ConversionOptions, MarkdownOptions, HtmlOptions, TextOptions, JsonOptions, XmlOptions, CsvOptions,
                    DocxOptions, XlsxOptions, PptxOptions, PdfOptions
  Results/          ConversionResult, StringConversionResult, BytesConversionResult, ConversionWarning,
                    ConversionStatistics, DetectionResult, SupportedConversion
  Exceptions/       DocConverterException, UnsupportedFormatException, ConversionNotSupportedException,
                    DocumentReadException, DocumentWriteException, InputTooLargeException,
                    InvalidConversionOptionsException, ConversionWarningException
  Detection/        FormatDetector, DocumentFormatParser, FormatSignature, ZipPartInspector, OleDirectoryInspector,
                    TextFormatHeuristics
  Registry/         FormatRegistry, CapabilityMatrix
  Readers/          IDocumentReader + Text/ Markdown/ Html/ Json/ Xml/ Delimited/ Rtf/ Docx/ Xlsx/ Pptx/ Pdf/ Image/
  Writers/          IDocumentWriter + Markdown/ Html/ Text/ Json/ Xml/ Delimited/ Docx/ Xlsx/ Pptx/ Pdf/
  Internal/         InputBuffer, LimitedReadStream, NonClosingStream, TextEncodingDetector, ImageHeaderReader,
                    InlineTextFlattener, DocumentModelValidator, Polyfills (netstandard2.0 guards and helpers)
  Ocr/              IOcrProvider, OcrProviderBase, OcrOptions, OcrResult, OcrStage (stubs, Section 5.8)
  Observability/    DocConverterDiagnostics
  DependencyInjection/ ServiceCollectionExtensions
  Resources/Fonts/  Liberation Sans and Liberation Mono (8 TTF files) + OFL.txt
```

One class or enum per file. No partial classes. Files over 500 lines use the five standard regions, and none are left empty. Namespaces use blocks, with `using` directives inside them, System and Microsoft first. No `var`, no tuples, no `Console` usage anywhere in the library. Every `await` carries `.ConfigureAwait(false)`. Every async method takes a `CancellationToken token = default` as its last parameter and checks it between pages, sheets, slides and blocks. `using (...) { }` blocks only, never using declarations, because `BACKEND_ARCHITECTURE.md` is the stricter rule. Guard clauses use explicit `if (x == null) throw new ArgumentNullException(nameof(x));`, since `ThrowIfNull` does not exist on netstandard2.0.

---

## 7. Capability and fidelity matrix

Legend:

- **F (Full):** everything the target format can express is carried over.
- **P (Projection):** lossy by design and documented. The target keeps a subset, or substitutes a placeholder, and a named warning says what was dropped. Examples: CSV keeps only tables, PPTX splits long tables across slides, and an image sent to plain text becomes a placeholder line.

Every built-in pair is supported. Following the lossiness decision in Section 1, no pair refuses to convert, and the worst case is a documented placeholder. `ConversionNotSupportedException` still exists for formats a caller registers without a matching reader or writer. Callers who would rather fail than lose content set `TreatWarningsAsErrors` (CLI: `--strict`).

The matrix suite exercises every cell (Section 10). The table is generated from `CapabilityMatrix` into `docs/FORMATS.md`, so the documentation cannot drift from the code. A test asserts the two are equal.

| From \ To | Md | Html | Txt | Json | Xml | Csv | Tsv | Docx | Xlsx | Pptx | Pdf |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Text | F | F | F | F | F | P | P | F | P | F | F |
| Markdown | F | F | F | F | F | P | P | F | P | P | F |
| Html | F | F | F | F | F | P | P | F | P | P | F |
| Json (model) | F | F | F | F | F | P | P | F | P | P | F |
| Json (generic) | F | F | F | F | F | P | P | F | F | P | F |
| Xml (model) | F | F | F | F | F | P | P | F | P | P | F |
| Xml (generic) | F | F | F | F | F | P | P | F | F | P | F |
| Csv | F | F | F | F | F | F | F | F | F | P | F |
| Tsv | F | F | F | F | F | F | F | F | F | P | F |
| Rtf | F | F | F | F | F | P | P | F | P | P | F |
| Docx | F | F | F | F | F | P | P | F | P | P | F |
| Xlsx | F | F | F | F | F | P | P | F | F | P | F |
| Pptx | F | F | F | F | F | P | P | F | P | F | F |
| Pdf | F* | F* | F | F* | F* | P | P | F* | P | P | F* |
| Png, Bmp | F | F | P | F | F | P | P | F | P | F | F |
| Jpeg | F | F | P | F | F | P | P | F | P | F | F‡ |
| Gif, Tiff | F | F | P | F | F | P | P | F | P | F | P |
| WebP | F | F | P | F | F | P | P | F§ | P | F§ | P |

\* PDF has no semantic structure. Headings, lists and paragraph boundaries are inferred, so "full" means everything the reader recovered is carried over. The `HeadingsInferred` warning makes that visible. Unruled tables arrive as paragraphs (Spike 3), and pages without a text layer yield `NoTextLayer`.
‡ Baseline and progressive RGB or grayscale JPEG embed natively. A CMYK JPEG cannot be embedded by PDFsharp (Spike 1) and becomes a placeholder with `ImageFormatUnsupported`.
§ Verified in Microsoft 365 Word and PowerPoint, build 16.0.20326 (Spike 2). Older Office releases and some third-party viewers may not display WebP. The file is still valid, and `docs/FORMATS.md` says so.

For the byte formats, the cells above are per format. JSON and XML appear twice because the reader branches on the canonical marker. The matrix suite covers both branches.

### 7.1 What is lost, and where it is documented

The rules below are the lossy behaviors the user accepted as long as they're documented. `docs/FORMATS.md` carries this table expanded with examples, and the README carries a short version under "Limits".

| Situation | Behavior | Warning |
|---|---|---|
| Image-only source (PNG, JPEG, GIF, BMP, TIFF, WebP) to Text, CSV, TSV or XLSX | One placeholder line or row: `[Image: sample.png, PNG 96x64]`. No text is extracted, because there's no OCR. | `ImagePlaceholderEmitted` |
| GIF, TIFF, WebP or CMYK JPEG embedded in, or converted to, PDF | Bordered placeholder with the alt text or file name, format and size | `ImageFormatUnsupported` |
| Any document to CSV or TSV | Tables only (`TableSelection`). Without tables, one row per block (`NoTableBehavior`). | `NonTableContentDropped` or `TablesFlattened` |
| Any document to Text | Inline styles, links (URL kept in parentheses) and images (placeholder) are flattened | `FormattingLost`, `ImagesOmitted` |
| Non-tabular document to XLSX | Tables become sheets. Other blocks become rows on a "Document" sheet. Images are omitted. | `FormattingLost`, `ImagesOmitted` |
| Long tables or dense sections to PPTX | Split across continuation slides. No content is lost. | none |
| Characters outside Liberation's coverage to PDF | Rendered blank | `GlyphsUnavailable` |
| PDF source | Headings inferred, unruled tables become paragraphs, scanned pages have no text | `HeadingsInferred`, `NoTextLayer` |
| Cell spans to Markdown or Text | Spanned cells are repeated or emptied, per `TableSpanMode` | `TableSpansFlattened` |
| DOCX footnotes, endnotes and text boxes | Appended as trailing sections | `FormattingLost` |

---

## 8. CLI (`docconv`)

### 8.1 Commands

```
docconv convert -i <path|-> -o <path|-> [--from <format>] [--to <format>] [options]
docconv detect  -i <path|->  [--json]
docconv formats [--json]
docconv --help | -h | -? | /?
docconv --version | -v
```

- `-` means stdin for `-i` and stdout for `-o`. Binary data flows through the raw streams (`Console.OpenStandardInput()` and `Console.OpenStandardOutput()`), never through `Console.In` and `Console.Out`, so bytes survive.
- If `--from` is omitted, it is inferred from content, using the input extension as a hint.
- If `--to` is omitted, it is inferred from the output file extension. `--to` is required when `-o -` is used.
- Format names are parsed by `DocumentFormatParser`, so `word`, `docx`, `.docx` and `DOCX` are all accepted.
- `detect` prints the `DetectionResult`.
- `formats` prints the input formats, the output formats and the full matrix with fidelity.
- `--version` prints `docconv 0.1.0` (the assembly informational version).
- Arguments are parsed by hand, following Mux's `CliArgumentParser`: both `--opt value` and `--opt=value` are accepted, unknown options are errors, and `--` ends option parsing. No System.CommandLine dependency.

### 8.2 Convert options

| Flag | Maps to |
|---|---|
| `--overwrite` | Allow replacing an existing output file. Without it an existing file is an error (exit 4). |
| `--options <file.json>` | Full `ConversionOptions` from JSON. Individual flags then override it. |
| `--title <text>` | `ConversionOptions.Title` |
| `--no-images`, `--images <embed\|omit\|placeholder\|external>` | `IncludeImages`, per-format `ImageMode` |
| `--no-metadata` | `IncludeMetadata = false` |
| `--input-encoding <name>`, `--output-encoding <name>` | Encodings |
| `--line-ending <lf\|crlf>` | `LineEnding` |
| `--deterministic` | `Deterministic` |
| `--strict` | `TreatWarningsAsErrors`. Exit 5 on warnings, and the output file is deleted. |
| `--csv-delimiter <char>`, `--no-header` | `CsvOptions` |
| `--table <first\|all\|N>` | `CsvOptions.TableSelection` and `TableIndex` |
| `--html-fragment`, `--html-no-css` | `HtmlOptions` |
| `--text-wrap <n>` | `TextOptions.WrapColumn` |
| `--json-compact`, `--json-no-binary` | `JsonOptions` |
| `--page-size <a4\|letter\|legal>`, `--margin <points>` | `PdfOptions` and `DocxOptions` |
| `--slide-split <1-6>` | `PptxOptions.SlideSplitHeadingLevel` |
| `--include-notes`, `--include-hidden-sheets`, `--preserve-pages` | Reader options |
| `--max-input-mb <n>` | `ConverterSettings.MaxInputBytes` |
| `--json` | Machine-readable report (8.3) |
| `-q`, `--quiet` | Suppress the human summary line (errors still go to stderr) |

### 8.3 Output streams, report and exit codes

The streams follow the rule Mux uses for `print`:

- **stdout** carries only the converted document (when `-o -`) or the JSON report (when `--json` and `-o` is a file).
- **stderr** carries everything else: the human summary, the warnings, the errors, and the JSON report when stdout is carrying the document.

An agent can always parse exactly one thing from each stream.

Default human summary on stderr:

```
docconv: report.docx (Docx) -> report.md (Markdown), 48.2 KB -> 10.4 KB, 84 ms, 2 warnings
  warning ImagesOmitted: 3 images were omitted because --images omit was set
```

JSON report (`--json`), camelCase, one line, `contractVersion` for forward compatibility:

```json
{"contractVersion":1,"success":true,"command":"convert",
 "input":{"path":"report.docx","format":"Docx","bytes":49356,"detected":false},
 "output":{"path":"report.md","format":"Markdown","bytes":10650},
 "durationMs":84,
 "warnings":[{"code":"ImagesOmitted","message":"3 images were omitted because --images omit was set","count":3}],
 "statistics":{"pages":0,"headings":12,"paragraphs":40,"tables":2,"images":3,"links":5},
 "error":null}
```

On failure, `success` is false and `"error":{"code":"ConversionNotSupported","message":"..."}` is set.

| Exit code | Meaning |
|---|---|
| 0 | Success (warnings allowed unless `--strict`) |
| 1 | Conversion failed: the input could not be read or the output could not be written (`DocumentReadException`, `DocumentWriteException`) |
| 2 | Usage error: unknown option, missing argument, invalid value |
| 3 | Unsupported: the format is not recognized or not supported, or the pair is not supported |
| 4 | I/O error: input not found, output exists without `--overwrite`, access denied |
| 5 | Warnings raised under `--strict` |
| 6 | Input exceeds `--max-input-mb` |
| 130 | Cancelled (Ctrl+C) |

### 8.4 CLI source layout and testability

```
src/DocConverter.Cli/
  Program.cs              static Main: wires Console streams and Ctrl+C to a CancellationTokenSource, calls CliApplication
  CliApplication.cs       RunAsync(string[] args, Stream stdin, Stream stdout, TextWriter stderr, CancellationToken token) -> int
  CliArguments.cs
  CliArgumentParser.cs
  CliCommandEnum.cs
  CliExitCodeEnum.cs
  CliUsageException.cs
  Commands/ConvertCommand.cs, DetectCommand.cs, FormatsCommand.cs
  Reporting/CliReport.cs, CliReportInput.cs, CliReportOutput.cs, CliReportError.cs, CliReportWriter.cs
  HelpText.cs
```

`Program` is a class with `static async Task<int> Main`. It does not use top-level statements, so the usings can stay inside a namespace. The same applies to `Test.Automated`. `CliApplication` takes its streams as parameters, which lets the CLI suites run the tool in-process with memory streams. The tests do not spawn a process, except for the one smoke test in CI.

### 8.5 Tool scripts

These are copied from Mux and renamed: `install-tool.bat/.sh`, `reinstall-tool.bat/.sh`, `remove-tool.bat/.sh`. The `.sh` scripts use only POSIX tools, so they run unchanged on macOS (bash 3.2 and zsh) and Linux, and they are committed with the executable bit set (`git update-index --chmod=+x`). The CLI itself uses `Path.Combine`, never assumes a path separator or case-insensitive file system, and writes `\n` to stderr. Behavior:

- Resolve the framework (`net10.0` if a .NET 10 SDK is present, else `net8.0`, or an explicit argument).
- `dotnet pack src/DocConverter.Cli/DocConverter.Cli.csproj -c Release -p:TargetFrameworks=<tfm> -o artifacts/tool-packages/<tfm>`
- `dotnet tool install -g --source artifacts/tool-packages/<tfm> --framework <tfm> --disable-parallel DocConverter.Cli`
- Finish with `docconv --version`.

`reinstall-tool.bat` keeps Mux's "is docconv.exe running" check and its tolerant uninstall. `publish-nuget.bat` packs `src/DocConverter.sln`. Both packable projects produce packages, and it pushes them with `--skip-duplicate`.

---

## 9. Porting plan from DocumentAtom

Porting is a copy-then-reshape job, done file by file. For each ported file:

1. Copy it into the target folder under the new namespace.
2. Apply the house style: explicit types instead of `var`, `_PascalCase` fields, usings inside the namespace, one class per file, guard clauses, and no `Console`.
3. Replace file-path APIs with stream APIs (`WordprocessingDocument.Open(stream, false)`, `SpreadsheetDocument.Open(stream, false)`, `PresentationDocument.Open(stream, false)`, `PdfDocument.Open(stream)`).
4. Change the output from `Atom` to model blocks.
5. Make the entry point async, with cancellation checks per page, sheet, slide or block. The underlying libraries are synchronous, so the reader runs on the calling thread and checks the token often. It does not wrap work in `Task.Run`.
6. Fix the defects listed in the DocumentAtom exploration: the inverted ordered/unordered condition in the image processor (moot, since there is no OCR), the CSV `Length`, PPTX titles, RTF heading levels, PDF headings, DOCX image position, and `Console.WriteLine` in the HTML processor.

The DocumentAtom files in scope are:

- `Core\TypeDetection\TypeDetector.cs` (split across `Detection/`)
- `Documents\Word\DocxProcessor.cs`
- `Documents\Excel\XlsxProcessor.cs`, `HeaderRowDetector.cs`, `HeaderRowPatternWeights.cs`, `CellData.cs`
- `Documents\PowerPoint\PptxProcessor.cs`, `SlideTitleInfo.cs`
- `Documents\Pdf\PdfProcessor.cs`, `PdfRegion.cs`
- `Documents\RichText\*`
- `Text\Html\HtmlProcessor.cs`
- `Text\Csv\CsvProcessor.cs`
- `Text\Json\JsonProcessor.cs`
- `Text\Xml\XmlProcessor.cs`
- the table rendering in `DataIngestion\Converters\AtomToIngestionElementConverter.cs` (a starting point for the Markdown table writer)

The README credits DocumentAtom as the origin of the extraction logic. The code is MIT-licensed by the same author, so no NOTICE file is needed.

---

## 10. Testing

The tests follow `BACKEND_TEST_ARCHITECTURE.md` exactly. Test logic lives once, in `Test.Shared`, as Touchstone descriptors, and three front ends run it:

- `Test.Automated`: console runner, JSON results, exit code
- `Test.Xunit`: `TouchstoneFactBase` plus a theory over every case
- `Test.Nunit`: `TouchstoneNunitBase` plus a `TestCaseSource` over every case

### 10.1 Project settings

- All test projects target `net8.0;net10.0`, with `ImplicitUsings` disabled, `Nullable` enabled and `IsPackable` false.
- Package versions match TextChunker, which are the newest in use across the reference repositories:
  - Touchstone.Core, Touchstone.Cli, Touchstone.XunitAdapter and Touchstone.NunitAdapter 0.1.12
  - xunit 2.9.3, xunit.runner.visualstudio 4.0.0
  - Microsoft.NET.Test.Sdk 18.9.0, coverlet.collector 10.0.1
  - NUnit 4.6.1, NUnit.Analyzers 4.14.0, NUnit3TestAdapter 6.2.0
- `Test.Shared` references `DocConverter`, `DocConverter.Cli` and `Touchstone.Core`. It also references OpenXml, PdfPig, PDFsharp, HtmlAgilityPack, CsvHelper and Markdig directly, for fixture builders and output inspectors.
- `Test.Shared` never writes to the console.
- Assertions throw `TestAssertionException`, a specific type. TextChunker's `TestSupport` throws plain `Exception`, which the requirements forbid.

### 10.2 Fixtures

The fixtures are built so the matrix tests actually check content, not just that nothing threw.

**One reference document.** A single canonical specification, `ReferenceContent`, describes the content every fixture carries:

- a title and metadata (author, subject, keywords)
- H1, H2 and H3 headings
- paragraphs with bold, italic, underline, strike, inline code and a hyperlink
- a nested unordered list (3 levels) and an ordered list starting at 1
- a 4x3 table with a header row and a numeric column
- a code block
- a block quote
- a small PNG generated in code (a 16x16 solid color, bytes built in memory)
- an international paragraph: `Grüße aus Zürich. 你好，世界。 مرحبا بالعالم. Emoji 🚀✅.`
- a paragraph containing characters that need escaping in every target (`<tag> & "quotes" | pipes * stars _ underscores [brackets] #hash`)

**Built fixtures.** `Test.Shared/Fixtures/Builders/` holds one builder per source format. Each builder produces the reference content in that format **without using DocConverter's writers**. The DOCX, XLSX and PPTX builders call the raw OpenXml SDK. The PDF builder calls PDFsharp's low-level `XGraphics`, not MigraDoc. HTML, Markdown, RTF, CSV, TSV, JSON, XML and Text are embedded text files, written by hand once. Images are built in code. Keeping the builders independent means a writer bug cannot hide a reader bug behind a matching round trip.

**Real-world fixtures.** `Test.Shared/Fixtures/RealWorld/` holds files saved by real producers, with a `PROVENANCE.md` listing the producer and version for each:

- Microsoft Word, Excel and PowerPoint
- LibreOffice Writer, Calc and Impress (including an ODF-to-OOXML save)
- Google Docs export
- a browser "Print to PDF" and a LaTeX PDF
- a WordPad RTF
- GitHub-flavored README Markdown
- a scraped HTML page with scripts, styles and navigation

The first set comes from DocumentAtom's `sdk/test-fixtures/` files. **The user is asked to supply** Office-authored files, because they cannot be made here with the permissive-license constraint and no Office install assumed. Real-world fixtures carry their own looser `ContentManifest` JSON listing the snippets that must survive.

**Negative fixtures**, built in code:

- a truncated zip, a DOCX with a missing main part, a password-protected DOCX and an encrypted PDF
- a PDF with a corrupt xref and a PDF with no text layer (image only)
- malformed JSON and XML, an XML document with a DTD and external entity (XXE attempt), a JSON document nested 10,000 levels deep
- a zip bomb: a DOCX whose `document.xml` inflates past `MaxDecompressedBytes`
- zero-length input, a single byte, 1 MB of random bytes
- a legacy `.doc`, `.xls` and `.ppt` (OLE2 header plus directory built in code), an ODT and an EPUB

**Golden files.** `Test.Shared/Fixtures/Golden/<from>-to-<to>.<ext>` holds expected output for text-based targets (Markdown, HTML, Text, JSON, XML, CSV, TSV) from the built fixtures, rendered with `Deterministic = true`. Comparison normalizes line endings only. They are regenerated with `dotnet run --project src/Test.Automated -- --update-golden`, a flag handled in `Test.Automated/Program.cs`, never in `Test.Shared` I/O paths. Regenerated golden files show up in `git diff` for review.

### 10.3 Output inspectors

`Test.Shared/Inspection/` has one inspector per target format. Each one parses the output independently of DocConverter's readers and returns a `ContentSnapshot`: headings, text snippets, list items, table cells, image count, link URLs and metadata.

| Target | Validation before extracting the snapshot |
|---|---|
| Docx, Xlsx, Pptx | Opens with the OpenXml SDK, and `OpenXmlValidator` (Office 2019 file format) reports **zero errors** |
| Pdf | Opens with PdfPig, page count at least 1, the text layer holds the expected snippets, the info dictionary holds the title |
| Html | Parses with HtmlAgilityPack with `ParseErrors` empty, no `<script>`, no `javascript:` URLs |
| Markdown | Parses with Markdig and walks the AST |
| Json, Xml | Parse, then validate against the canonical DTOs (a strict deserialize) |
| Csv, Tsv | Parse with CsvHelper, rectangular, header matches |
| Text | Decode strictly as UTF-8 (`throwOnInvalidBytes`) |

**Fidelity expectations.** `FidelityExpectation.For(from, to)` combines three things:

- what the source fixture carries (for example, CSV carries only the table)
- what the target can express (for example, Text has no inline styles)
- the matrix cell

The result is the exact set of snapshot elements that must survive. A cell marked F must preserve every intersecting element. A cell marked P must preserve its documented subset **and** raise the documented warning. This way each cell's tests say what the cell means, rather than a blanket "output is not empty".

### 10.4 Suites

The suites are registered in `Test.Shared/DocConverterSuites.cs` through a static `All` property. Suites marked *(generated)* build their cases in loops over `CapabilityMatrix`, so a new format joins the matrix automatically.

| Suite | Contents | Approx. cases |
|---|---|---|
| `DetectionSuite` | Every built, real-world and negative fixture through `DetectFormatAsync` from bytes, seekable stream, non-seekable stream and string. Legacy and ODF formats give the right `RecognizedAs`. Extension hints resolve CSV, TSV and Markdown ambiguity. Detection does not move a seekable stream's position. | ~120 |
| `FormatParserSuite` | Every alias, extension, case variant and invalid name. Media-type and extension round trips. | ~60 |
| `ReaderSuites` (one per format) | Each built fixture read into the model and compared against `ReferenceContent` for that format's capability. Format-specific cases: DOCX gridSpan and vMerge, nested numbering, image position, footnotes. XLSX dates, formulas, merged cells, hidden sheets, header detection. PPTX ordering, notes, numbered bullets. PDF headings, tables, multi-page, rotated page. RTF Unicode and headings. HTML spans and script stripping. JSON and XML canonical versus generic, depth limit. CSV quoting, embedded newlines, ragged rows, BOM. | ~180 |
| `WriterSuites` (one per format) | Hand-built models written and inspected: escaping, spans, deep nesting, empty document, empty table, a 10,000-row table, very long paragraph, every `InlineStyleEnum` combination, every option value. OpenXml validation on every DOCX, XLSX and PPTX. | ~200 |
| `ConversionMatrixSuite` *(generated)* | **Every F and P cell** x **every output shape** (Stream, string, byte[]), with `byte[]` input from the built fixture. Each case validates with the inspector, asserts the fidelity expectation, asserts the warnings for P cells, and asserts that the `ConversionResult` fields are consistent (formats, byte counts that match the output length, `IsBase64` for binary targets through string). 20 source variants (14 document sources counting the JSON and XML branches, plus 6 image formats) x 11 targets = 220 cells, x 3 output shapes = ~660 cases. | ~660 |
| `LossDocumentationSuite` *(generated)* | Every P cell raises exactly the warnings listed for it in Section 7.1. Every `WarningCodeEnum` value has a "What is lost" entry in `docs/FORMATS.md`. Placeholders have the documented text shape. The MigraDoc fallback text "Image has no valid type." never appears in any PDF output. | ~120 |
| `RegistryGapSuite` | A caller-registered format with a reader but no writer (and the reverse) throws `ConversionNotSupportedException` before reading input, and `CanConvert` returns false for it. | ~6 |
| `OcrStubSuite` | Every `OcrProviderBase` member throws `NotImplementedException`. With a provider and `OcrMode.Off`, conversion is normal. With a provider and any other mode, `NotImplementedException` is thrown before reading. With no provider, `OcrMode` is ignored. | ~8 |
| `PdfFontSuite` | The embedded resolver serves all eight faces. `Courier New`, `Arial`, `Times New Roman` and unknown families resolve. A host-installed resolver is left alone. `GlyphsUnavailable` counts CJK, Arabic and emoji characters exactly. | ~12 |
| `DependencySuite` | The dependency table in `docs/FORMATS.md` matches the resolved package graph (read from `obj/project.assets.json`), and every license is in the allow-list: MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, MS-PL, and OFL-1.1 for fonts. | 2 |
| `InputShapeSuite` *(generated)* | Every input format x every input shape: string (raw text for text formats, Base64 for binary), byte[], seekable `MemoryStream` at position 0 and at a non-zero offset, non-seekable wrapper, and a slow stream that returns 1 byte per read. All go to JSON, and the result must equal the byte[] baseline exactly. | ~110 |
| `GoldenSuite` *(generated)* | Built fixture x text target compared with the golden file. | ~90 |
| `RoundTripSuite` *(generated)* | `X -> Json -> X` versus direct, `Markdown -> Docx -> Markdown`, `Html -> Docx -> Html`, `Csv -> Xlsx -> Csv` (exact), `Markdown -> Pdf -> Markdown` (headings and text only). Equality uses a normalized model comparison, `ModelComparer`. | ~40 |
| `RealWorldSuite` *(generated)* | Each real-world file x each supported target: no exception, the inspector validates, the manifest snippets survive. | grows with fixtures |
| `NegativeInputSuite` | Every negative fixture gives the exact exception type, a message with context, no temp files, and no leaked handles (the output stream is still writable). XXE is never resolved (a sentinel file is never read). The zip bomb stops at the limit. | ~40 |
| `ArgumentValidationSuite` | Null input, output and document on every overload. `Auto` as target. Read-only output stream. Unreadable input stream. `MaxInputBytes` boundaries (limit - 1, limit, limit + 1). Every option setter's min, max and invalid values. | ~90 |
| `StreamSemanticsSuite` | The output stream is not closed, and its position is at the end of the written data. Writing starts at the existing position (appending after a prefix). The input stream is not closed. Non-seekable output (a write-only wrapper) works for every writer. | ~40 |
| `EncodingSuite` | UTF-8 with and without BOM, UTF-16 LE and BE, and UTF-32 input. `InputEncoding` override. `OutputEncoding` variants. CRLF versus LF. RTL, CJK and emoji survive to every text target and to DOCX, XLSX, PPTX and PDF (PDF: Latin, Greek and Cyrillic glyphs present; CJK, Arabic and emoji raise `GlyphsUnavailable`). | ~60 |
| `CancellationSuite` | A token cancelled up front throws `OperationCanceledException` for every overload. Mid-conversion cancellation, triggered by a slow stream that cancels after N reads, stops within a bounded time for each reader. The output stream stays usable. | ~40 |
| `ConcurrencySuite` | One `Converter`, 32 parallel conversions across mixed pairs, with deterministic output identical to sequential runs. Concurrent `RegisterReader` alongside conversions. The PDF font resolver initializes exactly once under a race. | ~6 |
| `DeterminismSuite` | With `Deterministic = true`, two runs of every binary target produce identical bytes. | ~10 |
| `SecuritySuite` | HTML output never contains unescaped user text, `<script>`, event-handler attributes or `javascript:` links, even when the input contains them. Markdown output escapes HTML. Remote image URLs are never fetched (a local listener on `127.0.0.1` records zero hits). No file system writes happen during conversion (the temp directory is snapshotted before and after). | ~25 |
| `OptionsSuite` | Every option's effect is observable in the output (for example `WrapColumn`, `SlideSplitHeadingLevel`, `TableSelection`, `ImageMode`, `PageSize` checked through the PDF page dimensions). Presets. `TreatWarningsAsErrors`. | ~80 |
| `ModelSuite` | Model invariants and the validator, canonical JSON and XML serialization round trip, `ModelComparer`, `ReadAsync` and `WriteAsync` two-step equals one-step. | ~30 |
| `ExtensibilitySuite` | A custom reader and writer registered and used. Replacing a built-in writer. DI registration resolves a singleton `IConverter` with configured settings. | ~10 |
| `DiagnosticsSuite` | An `ActivityListener` and a `MeterListener` observe one activity and the expected instruments with the expected tags per conversion. A throwing listener does not break a conversion. | ~8 |
| `CliSuite` | In-process `CliApplication.RunAsync` with memory streams: every command, every flag, `--opt=value`, unknown option (exit 2), missing `-i` (exit 2), unrecognized or legacy input format (exit 3), missing input and existing output without `--overwrite` (exit 4), `--strict` with warnings (exit 5, output deleted), `--max-input-mb` (exit 6), stdin to stdout binary integrity (DOCX bytes through `-`), inference of `--from` and `--to`, `--json` report schema (strict deserialize into `CliReport`), the report on stderr when the document is on stdout, `--help` and `--version` text, `detect` and `formats` in text and JSON. | ~80 |
| `CapabilityDocsSuite` | The matrix in `docs/FORMATS.md` matches `CapabilityMatrix` (the test reads the file from the repo root). | 1 |

That comes to roughly 2,100 cases. The matrix suites are the bulk of it, and they grow automatically with the matrix. Every case runs on Windows, macOS and Linux. Nothing is skipped by platform, because nothing in the library is platform-specific.

**The netstandard2.0 build gets exercised too.** Touchstone and the test SDKs do not run on netstandard2.0, so CI adds a leg that builds `Test.Automated` with the library reference forced to the netstandard2.0 asset: `SetTargetFramework="TargetFramework=netstandard2.0"` on the `ProjectReference`, switched on by `-p:DocConverterTestNetStandard=true`. The full suite then runs against it on net8.0. The polyfill code paths are tested, not just compiled.

Spike 3 showed one gap in that leg: the host still resolves the *package* dependencies for net8.0, so it tests DocConverter's own netstandard2.0 code but not the netstandard2.0 builds of PdfPig, Tabula, PDFsharp and the rest. Two things cover the gap:

- **`src/Test.NetFramework`** is a small net48 console. It is not in the Touchstone suites, because Touchstone targets net8.0 and net10.0 only; that is the one documented exception to the Test.Shared rule. It references the library through its netstandard2.0 build, just as a real .NET Framework consumer would, and runs one conversion per reader and one per writer, returning 0 or 1. It runs on the Windows CI leg only, since .NET Framework exists only there.
- Spike 3 already ran the literal netstandard2.0 binaries of every dependency on .NET 8 (Windows) and .NET 10 (Linux), so their correctness is established. The net48 console guards against regressions.

### 10.5 Running tests

```
dotnet build src/DocConverter.sln
dotnet run --project src/Test.Automated -f net10.0 [-- --results results.json]
dotnet run --project src/Test.Automated -f net8.0
dotnet run --project src/Test.Automated -f net10.0 -p:DocConverterTestNetStandard=true
dotnet run --project src/Test.NetFramework            (Windows only)
dotnet test src/Test.Xunit
dotnet test src/Test.Nunit
```

### 10.6 CI (`.github/workflows/tests.yaml`)

The workflow runs on pushes to `main` and `develop` and on pull requests to `main`, on a matrix of `windows-latest`, `macos-latest` and `ubuntu-latest`, because the library and the tool must work on all three. Running off Windows matters most for the PDF writer: Spike 1 showed that MigraDoc on Linux fails outright without the embedded font resolver, and macOS has the same lack of system-font access. It also catches path-separator, case-sensitivity and line-ending assumptions. It sets up .NET 8.0.x and 10.0.x.

Steps:

1. Restore, then build in Release (warnings are errors).
2. `Test.Automated` for net8.0, then for net10.0 with `--results results.json`.
3. The netstandard2.0 leg, plus `Test.NetFramework` on `windows-latest` only.
4. `dotnet test` for `Test.Xunit` and `Test.Nunit`.
5. **Tool smoke test:** pack `DocConverter.Cli` to a local folder, `dotnet tool install --tool-path ./.tools`, then run `docconv --version`, `docconv convert -i <fixture>.docx -o out.md` and `docconv convert -i - -o - --from md --to html < fixture.md`, checking each exit code. It runs on all three operating systems, and on macOS and Linux it also checks that the output PDF from `docconv convert -i fixture.md -o out.pdf` opens with PdfPig and contains the fixture text.
6. Upload `results.json` with `if: always()`.

The workflow file uses the `.yaml` extension, as the requirements direct.

---

## 11. Documentation

All prose follows `WRITING_DOCUMENTS.md`:

- no em-dashes anywhere
- no formulaic openers or stock phrases
- varied sentence length
- real examples
- lists that earn their place with interpreting prose

`C:\Code\agents\writing\WRITING_LIKE_JOEL.md` is the voice reference for the README and CHANGELOG, and is read before either is written. Every document goes through at least one explicit re-read pass for AI-sounding prose.

### 11.1 README.md

It starts with the TextChunker header block: the icon, the NuGet badges for `DocConverter` and `DocConverter.Cli`, and the title. Sections:

1. **Opening prose.** What it converts, from and to what, in memory, async, and the TFMs.
2. **Why it exists.** Agents and ingestion pipelines need one call that goes from "bytes of unknown document" to "Markdown I can reason about", and back to DOCX or PDF, without a server, Office or LibreOffice.
3. **Install.** `dotnet add package DocConverter`, and `dotnet tool install -g DocConverter.Cli`, or `install-tool.bat` / `./install-tool.sh` from source.
4. **Quick start.** The request's Word-to-Markdown example, rewritten for the final API. Then the string and bytes variants, auto-detection, and `ConvertFileAsync`.
5. **Supported formats.** A compact version of the matrix, with a link to `docs/FORMATS.md`.
6. **Reading and writing the model.** The two-step API, with an example that strips images and writes both Markdown and PDF.
7. **Options.** Instance settings versus per-call options, the presets, and a link to `docs/OPTIONS.md`.
8. **Warnings and results.**
9. **Command line (`docconv`).** Install, the four commands, examples for files and pipes, exit codes, the `--json` report, and a short "for agents" section with ready-to-paste invocations.
10. **Dependency injection.**
11. **Observability.** A table of instruments.
12. **Limits and non-goals.** A short, honest version of Section 7.1, covering:
    - lossy pairs and their warnings, with a link to the full list in `docs/FORMATS.md`
    - no OCR yet (with the `IOcrProvider` extension point named)
    - no legacy binary Office formats and no RTF output
    - heuristic PDF structure and undetected unruled PDF tables
    - PDF glyph coverage limited to Latin, Greek and Cyrillic
    - WebP in Office verified on Microsoft 365 only
    - no remote fetching
    - in-memory processing, bounded by `MaxInputBytes`

    It also states that the library and CLI support Windows, macOS and Linux.
13. **Building and testing.**
14. **Documentation.** Links to `docs/`.
15. **Acknowledgments.** DocumentAtom, plus the third-party libraries and their licenses, and the bundled OFL fonts.
16. **License.** "MIT. See [LICENSE.md](LICENSE.md)."

### 11.2 CHANGELOG.md

It uses the Keep a Changelog header, copied from TextChunker with the name changed. The first entry is `## [0.1.0] - <release date>`. It opens with a short "The first release." paragraph, followed by:

- `### Added`: the library, the model, the formats, the CLI, the scripts and the tests
- `### Notes`: no Docker, REST, MCP, SDK or dashboard assets, because DocConverter is a library and CLI. Also that the version is `0.1.0` without a pre-release label, as explicitly requested.

Bullets are full sentences, with API names in backticks.

### 11.3 docs/ and CLAUDE.md

- `API.md`, `FORMATS.md` (partly generated, see Section 7), `OPTIONS.md`, `CLI.md`, `DOCUMENT_MODEL.md` and `EXTENDING.md`, each with one `#` title and `##` sections, as in TextChunker.
- `CLAUDE.md` mirrors TextChunker's sections:
  - What this is
  - Layout
  - Build and test
  - Code style (the full rule list from `CODE_STYLE.md` and `BACKEND_ARCHITECTURE.md`, including `using (...) { }` blocks only)
  - Versioning ("Do not change the version number without an explicit request. The version lives in `src/Directory.Build.props`.")
  - Testing model (Test.Shared only, no console, independent fixture builders, golden update flag, matrix generation)

---

## 12. Phased implementation

Each phase ends with a clean Release build (zero warnings), all tests green, and a conventional commit. No phase changes the version.

**Phase 0: scaffold (the spikes are already done, see Section 13)**
- Repository init against `https://github.com/jchristn/DocConverter`, root files, solution and projects, CI workflow (three operating systems), tool scripts, `publish-nuget.bat`, `CLAUDE.md`, and a README stub.
- Carry the spike results into `docs/FORMATS.md`, and the dependency table (Section 4.4) into "Third-party components".
- Carry the spike code over as seeds: the font resolver (Spike 1), the PPTX scaffold and image-part code (Spike 2), and the Tabula calls (Spike 3). Each is rewritten to house style; spike code is never copied verbatim.
- *Exit:* empty projects build on all TFMs on Windows, macOS and Linux. Touchstone runs zero suites green in all three runners. `install-tool.bat` and `install-tool.sh` install a `docconv` that prints its version.

**Phase 1: core pipeline and text-family formats**
- Enums, model, options, results, exceptions, `ConversionContext`, `InputBuffer`, `FormatRegistry`, `CapabilityMatrix`, `Converter` (every overload), `FormatDetector`, `DocumentFormatParser`.
- Readers and writers for Text, Markdown, HTML, JSON, XML, CSV and TSV, plus the canonical model serialization.
- Test infrastructure: `ReferenceContent`, the text fixtures, the inspectors, `FidelityExpectation`, `ModelComparer`, and the generated matrix suites limited to the registered formats.
- *Exit:* the full 7x7 text-family matrix is green in every output and input shape, and the golden files are committed.

**Phase 2: Office formats**
- DOCX, XLSX and PPTX readers (ported) and writers (new), with the OpenXml fixture builders and the OpenXml validation inspectors.
- *Exit:* the matrix covers 10x10 and every Office output passes `OpenXmlValidator` with zero errors.

**Phase 3: PDF, RTF and images**
- The PDF reader (ported, plus heading inference), the PDF writer (MigraDoc), the RTF reader (ported), and the image reader.
- The PDF fixture builder (raw PDFsharp), the RTF fixture, and the image fixtures.
- *Exit:* the full matrix in Section 7 is green, including every placeholder and warning in Section 7.1. The OCR stubs (Section 5.8) and `OcrStubSuite` are in place.

**Phase 4: CLI**
- `CliApplication`, the parser, the commands, the report, the help text and the exit codes, plus `CliSuite`.
- The tool smoke test in CI.
- *Exit:* every exit code is covered, and stdin and stdout binary round trips are byte-exact.

**Phase 5: hardening**
- The negative, security, cancellation, concurrency, determinism, encoding, options, diagnostics and extensibility suites.
- The real-world fixtures (the user supplies the Office-authored files).
- The netstandard2.0 CI leg and `Test.NetFramework`.
- Benchmarks for representative sizes.
- *Exit:* every suite in Section 10.4 is green on Windows, macOS and Linux, on net8.0 and net10.0. The netstandard2.0 leg and the net48 smoke run are green.

**Phase 6: documentation and release readiness**
- README, CHANGELOG, `docs/*`, the generated `FORMATS.md` matrix, and the XML docs audit (every public member has a summary and default, min and max where relevant, plus exceptions, nullability and thread safety).
- A writing pass against `WRITING_DOCUMENTS.md`.
- An em-dash grep across the whole repository (`grep -rn $'\u2014'` returns nothing).
- `dotnet pack` inspection: the README, LICENSE and icon are present in both packages, and the snupkg is produced.
- *Exit:* the user reviews the release, and publishing happens only on explicit request (`publish-nuget.bat <key>`).

---

## 13. Phase 0 spike results (completed 2026-09-24)

The three spikes ran on 2026-09-24 against the dependency versions in Section 4.3. The hosts were:

- Windows 11 with .NET 8.0.11, .NET 10.0.12 and .NET Framework 4.8
- Ubuntu 24.04 in Docker with .NET 10.0.12
- Microsoft 365 Word and PowerPoint, build 16.0.20326

The test images were one 96x64 picture saved as PNG (RGB, RGBA, palette), JPEG (baseline, progressive, CMYK), GIF, BMP, TIFF and WebP (lossy and lossless). macOS was not available for the spikes. It is covered by CI from Phase 0 onward, and the risk is low because nothing in the stack touches the operating system's fonts or imaging.

### Spike 1: PDF output with PDFsharp and MigraDoc 6.2.4

| Question | Result |
|---|---|
| Does MigraDoc work on Linux without a font resolver? | **No.** Every render fails, even an image-only page, with `InvalidOperationException: The font 'Courier New' cannot be resolved for predefined error font`. |
| With an embedded Liberation resolver? | **Yes, identically on Windows and Linux.** All five requested faces were embedded as subsets. PdfPig extracted `Grüße aus Zürich. Ελληνικά. Кириллица.` verbatim. |
| Glyph coverage | CJK, Arabic and emoji render blank with Liberation, hence `GlyphsUnavailable` (Section 5.7) and the Noto option (Section 15). |
| Native image embedding (`XImage.FromStream`) | PNG RGB, PNG RGBA, PNG palette, JPEG baseline, JPEG progressive and BMP: **OK**. GIF, TIFF, WebP lossy, WebP lossless and CMYK JPEG: **`InvalidOperationException: Unsupported image format`**. |
| MigraDoc with an unsupported image | **Does not throw.** It writes the text "Image has no valid type." onto the page. The writer must check support up front (Section 5.6), and a test guards against this text (Section 10.4). |

### Spike 2: images in code-built DOCX and PPTX (OpenXml 3.5.1)

| Question | Result |
|---|---|
| `OpenXmlValidator` (Office2019 and Microsoft365) on a DOCX and a PPTX per image format | **0 errors for all 22 files.** This includes the code-built PPTX theme, master and layout, with no template binary. |
| Do Word and PowerPoint open them? | **Yes, all 22.** Each file reports exactly 1 inline shape (Word) or 1 picture (PowerPoint). |
| Do they actually render the image? | **Yes, all 22.** Each file was exported to PDF by Office and inspected with PdfPig: every export holds one 96x64 image. That includes GIF, TIFF, CMYK JPEG and both WebP variants. |
| Caveat | WebP rendering is verified on Microsoft 365 only (footnote § in Section 7). |

### Spike 3: Tabula and the rest of the stack through netstandard2.0

| Host | Result |
|---|---|
| .NET Framework 4.8 console referencing a netstandard2.0 library (the real-world netstandard consumer) | Tabula, MigraDoc (with image and font resolver), OpenXml, Markdig, HtmlAgilityPack, CsvHelper and System.Text.Json: **all OK** |
| .NET 8 (Windows) and .NET 10 (Linux) bound directly to the **netstandard2.0 binaries** of every package (confirmed through each loaded assembly's `TargetFrameworkAttribute`) | **All OK on both.** The first attempt failed only because the hand-built harness left out the netstandard2.0 transitive packages (`Microsoft.Bcl.HashCode`, `Microsoft.Bcl.AsyncInterfaces`, `System.Text.Json`). NuGet adds these automatically for real consumers. |
| Tabula extraction quality | A ruled 4x4 table came out cell-exact through both `SpreadsheetExtractionAlgorithm` (lattice) and `BasicExtractionAlgorithm` (stream). **An unruled 3x3 table on the same page was not detected** by `SimpleNurminenDetectionAlgorithm`, so it arrives as paragraphs (documented in Section 7.1). |
| API notes for implementation | Tabula 1.x: `ObjectExtractor` is static (`ObjectExtractor.Extract(document, pageNumber)`), results are `IReadOnlyList<T>`, and the document is opened with `ParsingOptions { ClipPaths = true }`. Markdig 1.x: `UseAdvancedExtensions()` is an extension method in the `Markdig` namespace. |

### What the spikes changed in this plan

- The embedded font resolver went from recommended to mandatory, and it maps `Courier New`.
- GIF, TIFF, WebP and CMYK JPEG in PDF output became placeholders with a warning.
- The PDF writer now checks images up front because MigraDoc fails silently.
- GIF, TIFF and WebP to DOCX and PPTX became full fidelity.
- The PPTX scaffold is confirmed to need no template binary.
- The unruled-table limitation was added.
- `Test.NetFramework` was added.
- The dependency versions were updated: PdfPig 0.1.16, Markdig 1.4.0, System.* 9.0.10.

---

## 14. Risks

| Risk | Mitigation |
|---|---|
| PDFsharp's font resolver is process-global (`GlobalFontSettings`), and a host application may already have set one | Set it once, lazily, under a lock, and only if none is set. If the host has its own, respect it and document that. Spike 1 used only the global resolver. Whether MigraDoc 6.2 can take a per-document resolver gets checked early in Phase 3. If it can't, `PdfOptions.FontResolver` is dropped in favor of a `ConverterSettings`-level font source applied once. |
| PdfPig is pre-1.0, and its API can shift | Pin the exact version. All PdfPig usage stays in `Readers/Pdf/`. |
| PDF reading is heuristic by nature | Mark the cells F\*, raise `HeadingsInferred`, and expose the thresholds in `PdfOptions`. Test against real-world PDFs, not just generated ones. |
| PPTX writing needs a lot of PresentationML scaffolding | Spike 2 built a scaffold that validates and opens in PowerPoint. It becomes `PresentationScaffold`, and a test checks it with `OpenXmlValidator`. |
| macOS was not available during the spikes | CI runs every suite on `macos-latest` from Phase 0. The stack uses no system fonts, imaging or native libraries, and the Linux results carry over. |
| WebP in DOCX and PPTX verified only on Microsoft 365 | Documented (footnote § in Section 7). Add an option to swap WebP for a placeholder if users report problems with older Office. |
| Memory: everything is in memory, including the full model | `MaxInputBytes` and `MaxDecompressedBytes` cap it, and the README states the bound. Streaming conversion is future work. |
| netstandard2.0 API gaps | Polyfills in `Internal/`. The dedicated CI leg runs the full suite against the netstandard2.0 build, and `Test.NetFramework` exercises a real .NET Framework consumer. |
| Real Office-authored fixtures cannot be generated here | The user supplies them in Phase 5. Until then, generated fixtures and DocumentAtom's small samples cover the matrix. |

---

## 15. Open items for the user

1. **Font choice for PDF output.** Liberation Sans and Mono (proven in Spike 1; 2.8 MB; Latin, Greek and Cyrillic) or Noto Sans and Sans Mono (larger; adds much wider script coverage, though CJK needs a separate, much larger Noto CJK face). Both are SIL OFL 1.1. The plan defaults to Liberation, so CJK, Arabic and emoji render blank in PDFs, with the `GlyphsUnavailable` warning.
2. **Real-world Office fixtures.** A handful of Word, Excel and PowerPoint files, and ideally one scanned or complex PDF, cleared for inclusion in a public repository.
3. **Icon.** An `assets/icon.png` (256x256) and `icon.ico`, or approval to use a simple generated placeholder.

---

## 16. Future work (explicitly out of scope for 0.1.0)

- ODT, ODS and ODP input and output (OpenDocument is zip plus XML and needs no new dependency). EPUB input.
- RTF output.
- A batch mode in the CLI (`docconv convert -i "*.docx" --to md --out-dir out/`).
- Streaming conversion for very large CSV, XLSX and text inputs.
- OCR: a separate package (for example `DocConverter.Ocr.Tesseract`) that subclasses `OcrProviderBase` and plugs into `ConverterSettings.OcrProvider`. The 0.1.0 stubs (Section 5.8) define the contract, so this is additive. When it lands, image-to-text cells move from P to F, and `NoTextLayer` pages gain text.
- Better detection of unruled PDF tables (for example `BasicExtractionAlgorithm` over whole text regions with a column-alignment heuristic).
- Native installers per `INSTALLERS.md` (winget, scoop, brew) for `docconv`, if a standalone binary becomes worthwhile.
- An MCP server wrapper, if agents prefer a tool call to a CLI invocation. It would require `MCP_API.md`.
