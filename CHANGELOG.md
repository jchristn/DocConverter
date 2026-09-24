# Changelog

All notable changes to DocConverter are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). While the version is below 1.0.0, any
release may carry breaking changes.

## [0.1.0] - Unreleased

The first release. DocConverter converts documents between formats in memory, through one intermediate document
model, and ships as a library and as the `docconv` global tool.

### Added

- `Converter` with `ConvertAsync`, `ConvertToStringAsync` and `ConvertToBytesAsync` for string, byte array and stream
  input, `ConvertFileAsync` for paths, the two-step `ReadAsync` and `WriteAsync` family, `DetectFormatAsync`,
  `CanConvert`, `GetSupportedConversions`, and `RegisterReader` and `RegisterWriter` for custom formats.
- Readers for Text, Markdown, HTML, JSON, XML, CSV, TSV, RTF, DOCX, XLSX, PPTX, PDF, PNG, JPEG, GIF, BMP, TIFF and WebP.
- Writers for Markdown, HTML, Text, JSON, XML, CSV, TSV, DOCX, XLSX, PPTX and PDF, giving 198 supported pairs.
- `DocumentModel` with headings, paragraphs, styled text runs, links, lists, tables with spans, code, quotes, images,
  breaks and sections for pages, slides and sheets, plus a canonical JSON and XML form that round trips exactly.
- Format detection from content: binary signatures, Office Open XML content types, OLE2 stream names for legacy and
  password protected Office files, and text heuristics for Markdown, CSV and TSV, with an optional file name hint.
- `ConversionOptions` with per-format options and the `ForLlmIngestion`, `ForArchival` and `Minimal` presets;
  `ConverterSettings` with input, decompression and nesting limits and a logger.
- Warnings with codes and counts for every documented loss, and `TreatWarningsAsErrors` for strict callers.
- `Deterministic` output: identical input produces identical bytes for every writer, including DOCX, XLSX, PPTX and PDF.
- PDF output on every operating system through embedded Liberation fonts, with placeholders for image formats PDFsharp
  cannot embed and `GlyphsUnavailable` for characters outside the fonts.
- The OCR extension point (`IOcrProvider`, `OcrProviderBase`, `OcrOptions`, `OcrResult`, `ConverterSettings.OcrProvider`,
  `ConversionOptions.OcrMode`) as stubs for a future OCR package.
- `AddDocConverter` for Microsoft.Extensions.DependencyInjection, and an `ActivitySource` and `Meter` named DocConverter.
- The `docconv` tool (package `DocConverter.Cli`) with `convert`, `detect` and `formats`, binary safe stdin and stdout,
  a one line JSON report, and documented exit codes; `install-tool`, `reinstall-tool` and `remove-tool` scripts for
  Windows, macOS and Linux; `publish-nuget.bat`.
- Touchstone test suites run by a console runner, xUnit and NUnit, covering every conversion pair in every output shape,
  golden files, round trips, hostile input, cancellation and concurrency, plus a .NET Framework 4.8 smoke harness and CI
  on Windows, macOS and Linux.

### Notes

- DocConverter is a library and a command line tool. Docker, REST, MCP, SDK and dashboard assets do not apply.
- The version is 0.1.0 without a pre-release label, as explicitly requested.
- DOCX, XLSX, PPTX, PDF, RTF, HTML, CSV, JSON and XML extraction started from DocumentAtom and was reshaped for stream
  based reading and a richer model. Defects fixed along the way include images placed at the end of DOCX documents, lost
  PPTX titles and RTF heading levels, missing PDF heading detection, and Markdown code fences split on blank lines.
