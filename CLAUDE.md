# CLAUDE.md

## What this is

DocConverter is a C# library that converts documents between formats in memory, through one intermediate document
model (`DocumentModel`). It targets netstandard2.0, net8.0 and net10.0 and runs on Windows, macOS and Linux. The
`docconv` global tool (`src/DocConverter.Cli`, net8.0 and net10.0) wraps it for agents. There is no server, Docker
image, dashboard, REST or MCP surface. The design record is `DOC_CONVERTER.md`; the format reference is
`docs/FORMATS.md`.

## Layout

```
src/
  Directory.Build.props        shared settings; the version lives here
  DocConverter.sln
  DocConverter/                the library
    Converter.cs               public entry point and pipeline
    Model/                     DocumentModel, blocks, inlines; Model/Serialization holds the canonical JSON and XML form
    Readers/<Format>/          one reader per format (IDocumentReader)
    Writers/<Format>/          one writer per format (IDocumentWriter)
    Detection/                 format detection and DocumentFormatParser
    Registry/                  FormatRegistry and CapabilityMatrix (the fidelity table behind docs/FORMATS.md)
    Internal/                  shared helpers (input buffering, text IO, image headers, table grids, transforms)
    Ocr/                       OCR extension point, stubs only
    Resources/Fonts/           Liberation fonts for PDF output (SIL OFL 1.1)
  DocConverter.Cli/            docconv
  Test.Shared/                 every test, as Touchstone descriptors; fixtures, builders, inspectors
  Test.Automated/              console runner, plus --update-docs and --update-golden
  Test.Xunit/, Test.Nunit/     adapters over Test.Shared
  Test.NetFramework/           net48 smoke harness (Windows, not in the .sln)
  DocConverter.Benchmarks/     BenchmarkDotNet (not in the .sln)
docs/                          API, FORMATS, OPTIONS, CLI, DOCUMENT_MODEL, EXTENDING
```

## Build and test

```
dotnet build src/DocConverter.sln
dotnet run --project src/Test.Automated -f net10.0
dotnet run --project src/Test.Automated -f net10.0 -p:DocConverterTestNetStandard=true
dotnet test src/Test.Xunit
dotnet test src/Test.Nunit
dotnet run --project src/Test.NetFramework            (Windows)
dotnet run --project src/Test.Automated -f net10.0 -- --update-docs .
dotnet run --project src/Test.Automated -f net10.0 -- --update-golden src/Test.Shared/Fixtures/Golden
```

Warnings are errors. After changing capabilities or warning codes, regenerate the matrix in `docs/FORMATS.md` with
`--update-docs`. After an intended change to text output, regenerate the golden files and review the diff.

## Code style

- Namespace declaration at the top, using statements inside the namespace block.
- System and Microsoft usings first in alphabetical order, then other usings in alphabetical order.
- Public members, constructors and public methods carry XML documentation, including defaults, minimums, maximums,
  nullability, thread safety and `<exception>` tags. Private members and methods carry none.
- Use explicit types, never `var`. No tuples. No partial classes.
- Private fields and constants are `_PascalCase`.
- Validated or nullable public members use explicit getters and setters over backing fields; collections never hold
  null (setters coalesce).
- Every async method takes a `CancellationToken` and every await uses `.ConfigureAwait(false)`.
- Classic `using (...) { }` blocks, not using declarations.
- Guard clauses at method entry. Specific exception types with contextual messages.
- One class or one enum per file. Regions (Public-Members, Private-Members, Constructors-and-Factories, Public-Methods,
  Private-Methods) in files over 500 lines, never empty.
- The library must compile for netstandard2.0: no `Math.Clamp`, `ArgumentNullException.ThrowIfNull`,
  `string.Contains(char)`, `Dictionary.TryAdd`, ranges or `init` accessors.
- No `Console` in the library. Nothing platform specific: no System.Drawing, no system fonts, no registry.
- Never use em dashes anywhere, including comments, docs and commit messages.

## Versioning

Do not change the version number without an explicit request. The version lives in the `<Version>` element of
`src/Directory.Build.props`.

## Testing model

All test logic lives in `src/Test.Shared` as Touchstone `TestCaseDescriptor`s registered in `DocConverterSuites.All`.
Test.Shared never writes to the console and never writes files; `Test.Automated` does the writing for `--update-docs`
and `--update-golden`. Fixtures in other formats are built in code by `Fixtures/Builders` from `ReferenceContent`,
using the underlying libraries directly and never DocConverter's writers, so a writer bug cannot hide a reader bug.
Output is checked by `Inspection/*Inspector`, which parse it independently. The conversion matrix, golden and round
trip suites are generated from `Matrix/MatrixCatalog`, so a new source or target joins them automatically.
