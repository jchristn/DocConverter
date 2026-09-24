namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocConverter.Results;
    using DocumentFormat.OpenXml.Packaging;
    using DocumentFormat.OpenXml.Spreadsheet;
    using Test.Shared.Fixtures;
    using Test.Shared.Fixtures.Builders;
    using Test.Shared.Inspection;
    using Test.Shared.Suites.Xlsx;
    using Touchstone.Core;

    /// <summary>
    /// XLSX reader and writer tests.
    /// </summary>
    public static class XlsxSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            AddReaderCases(cases);
            AddNegativeCases(cases);
            AddWriterCases(cases);
            return new TestSuiteDescriptor(suiteId: "Xlsx", displayName: "XLSX reader and writer", cases: cases);
        }

        private static void AddReaderCases(List<TestCaseDescriptor> cases)
        {
            cases.Add(Case("ReadSheetsAsSections", "Visible worksheets become sheet sections in workbook order; hidden sheets are skipped", async ct =>
            {
                DocumentModel doc = await Read(XlsxFixtureBuilder.BuildReference(), null, ct).ConfigureAwait(false);
                List<SectionBlock> sections = doc.Blocks.OfType<SectionBlock>().ToList();
                TestSupport.AssertEqual(2, sections.Count, "section count");
                TestSupport.AssertEqual("Staff", sections[0].Title, "first sheet");
                TestSupport.AssertEqual("Notes", sections[1].Title, "second sheet");
                TestSupport.Assert(sections.All(s => s.Kind == SectionKindEnum.Sheet), "sections are sheets");
                TestSupport.AssertNotContains(OfficeSuiteSupport.AllText(doc), XlsxFixtureBuilder.HiddenText, "hidden sheet skipped");
            }));

            cases.Add(Case("ReadStaffTable", "The Staff sheet is one table with a detected header row and exact cell text", async ct =>
            {
                DocumentModel doc = await Read(XlsxFixtureBuilder.BuildReference(), null, ct).ConfigureAwait(false);
                SectionBlock staff = doc.Blocks.OfType<SectionBlock>().First();
                TableBlock table = staff.Blocks.OfType<TableBlock>().Single();
                TestSupport.AssertEqual(1, table.HeaderRowCount, "header row detected");
                TestSupport.AssertEqual("Staff", table.SourceSheet, "source sheet");
                List<List<string>> cells = OfficeSuiteSupport.Cells(table);
                TestSupport.AssertEqual(ReferenceContent.TableRows.Length, cells.Count, "row count");
                for (int r = 0; r < cells.Count; r++)
                    for (int c = 0; c < 3; c++)
                        TestSupport.AssertEqual(ReferenceContent.TableRows[r][c], cells[r][c], "cell " + r + "," + c);
                TestSupport.Assert(table.Rows[0].Cells.All(c => c.IsHeader), "header cells flagged");
                TestSupport.Assert(!table.Rows[1].Cells.Any(c => c.IsHeader), "data cells not flagged");
            }));

            cases.Add(Case("ReadTypedValues", "Dates become ISO 8601, formulas give cached values, booleans TRUE, text preserved", async ct =>
            {
                DocumentModel doc = await Read(XlsxFixtureBuilder.BuildReference(), null, ct).ConfigureAwait(false);
                TableBlock notes = doc.Blocks.OfType<SectionBlock>().ElementAt(1).Blocks.OfType<TableBlock>().Single();
                Dictionary<string, string> values = OfficeSuiteSupport.Cells(notes).Where(r => r.Count >= 2).ToDictionary(r => r[0], r => r[1]);
                TestSupport.AssertEqual(XlsxFixtureBuilder.DateIso, values["Hired"], "date as ISO");
                TestSupport.AssertEqual(XlsxFixtureBuilder.FormulaCached, values["Total"], "formula cached value");
                TestSupport.AssertEqual("TRUE", values["Active"], "boolean");
                TestSupport.AssertEqual(ReferenceContent.International, values["International"], "international text");
                TestSupport.AssertEqual(ReferenceContent.Special, values["Special"], "special text");
            }));

            cases.Add(Case("ReadMetadata", "Core properties become document metadata", async ct =>
            {
                DocumentModel doc = await Read(XlsxFixtureBuilder.BuildReference(), null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual(ReferenceContent.Title, doc.Metadata.Title, "title");
                TestSupport.AssertEqual(ReferenceContent.Author, doc.Metadata.Author, "author");
                TestSupport.Assert(doc.Metadata.CreatedUtc.HasValue && doc.Metadata.CreatedUtc.Value.Year == 2024, "created");
            }));

            cases.Add(Case("IncludeHiddenSheets", "IncludeHiddenSheets reads hidden worksheets too", async ct =>
            {
                ConversionOptions options = new ConversionOptions();
                options.Xlsx.IncludeHiddenSheets = true;
                DocumentModel doc = await Read(XlsxFixtureBuilder.BuildReference(), options, ct).ConfigureAwait(false);
                TestSupport.AssertEqual(3, doc.Blocks.OfType<SectionBlock>().Count(), "three sheets");
                TestSupport.AssertContains(OfficeSuiteSupport.AllText(doc), XlsxFixtureBuilder.HiddenText, "hidden content");
            }));

            cases.Add(Case("HeaderDetectionOff", "DetectHeaderRow false leaves HeaderRowCount at 0", async ct =>
            {
                ConversionOptions options = new ConversionOptions();
                options.Xlsx.DetectHeaderRow = false;
                DocumentModel doc = await Read(XlsxFixtureBuilder.BuildReference(), options, ct).ConfigureAwait(false);
                TableBlock table = OfficeSuiteSupport.Find<TableBlock>(doc).First();
                TestSupport.AssertEqual(0, table.HeaderRowCount, "no header");
            }));

            cases.Add(Case("MergedCells", "Merged ranges become column and row spans with covered cells removed", async ct =>
            {
                DocumentModel doc = await Read(XlsxFixtureBuilder.BuildMerged(), null, ct).ConfigureAwait(false);
                TableBlock table = OfficeSuiteSupport.Find<TableBlock>(doc).Single();
                TableCell merged = table.Rows[0].Cells[0];
                TestSupport.AssertEqual("Merged header", OfficeSuiteSupport.Text(merged.Blocks[0]), "anchor text");
                TestSupport.AssertEqual(2, merged.ColumnSpan, "column span");
                TestSupport.AssertEqual(2, table.Rows[0].Cells.Count, "covered cell removed");
                TableCell tall = table.Rows[2].Cells[0];
                TestSupport.AssertEqual(2, tall.RowSpan, "row span");
                TestSupport.AssertEqual(2, table.Rows[3].Cells.Count, "vertically covered cell removed");
            }));

            cases.Add(Case("TitleRowAboveTable", "A single-cell row above a table becomes a paragraph and the table header is detected", async ct =>
            {
                DocumentModel doc = await Read(XlsxFixtureBuilder.BuildWithTitleRow(), null, ct).ConfigureAwait(false);
                SectionBlock section = doc.Blocks.OfType<SectionBlock>().Single();
                TestSupport.Assert(section.Blocks[0] is ParagraphBlock, "first block is a paragraph");
                TestSupport.AssertEqual("Quarterly Report", OfficeSuiteSupport.Text(section.Blocks[0]), "title text");
                TableBlock table = section.Blocks.OfType<TableBlock>().Single();
                TestSupport.AssertEqual(1, table.HeaderRowCount, "header detected");
                TestSupport.AssertEqual("Region", OfficeSuiteSupport.Cells(table)[0][0], "header cell");
            }));

            cases.Add(Case("DrawingImages", "Pictures in a worksheet drawing become image blocks with alt text", async ct =>
            {
                DocumentModel doc = await Read(XlsxFixtureBuilder.BuildWithImage(), null, ct).ConfigureAwait(false);
                ImageBlock image = OfficeSuiteSupport.Find<ImageBlock>(doc).Single();
                TestSupport.AssertEqual(ReferenceContent.ImageAlt, image.AltText, "alt text");
                BinaryResource resource = doc.Resources[image.ResourceId];
                TestSupport.AssertEqual("image/png", resource.MediaType, "media type");
                TestSupport.AssertEqual(ReferenceContent.ImageSize, resource.PixelWidth ?? 0, "pixel width");
            }));

            cases.Add(Case("EmptySheet", "A sheet with no rows yields an empty sheet section", async ct =>
            {
                DocumentModel doc = await Read(XlsxFixtureBuilder.BuildEmpty(), null, ct).ConfigureAwait(false);
                SectionBlock section = doc.Blocks.OfType<SectionBlock>().Single();
                TestSupport.AssertEqual(0, section.Blocks.Count, "no blocks");
            }));

            cases.Add(Case("RealWorldSample", "DocumentAtom's generated sample.xlsx reads and yields table content", async ct =>
            {
                DocumentModel doc = await Read(OfficeSuiteSupport.Fixture("RealWorld/DocumentAtom/sample.xlsx"), null, ct).ConfigureAwait(false);
                List<TableBlock> tables = OfficeSuiteSupport.Find<TableBlock>(doc);
                TestSupport.Assert(tables.Count > 0 && tables[0].Rows.Count > 0, "has a table with rows");
            }));

            cases.Add(Case("ConvertAutoDetect", "Auto detection routes XLSX bytes to the XLSX reader", async ct =>
            {
                using (Converter converter = new Converter())
                {
                    BytesConversionResult result = await converter.ConvertToBytesAsync(XlsxFixtureBuilder.BuildReference(), DocumentFormatEnum.Auto, DocumentFormatEnum.Xlsx, null, ct).ConfigureAwait(false);
                    TestSupport.AssertEqual(DocumentFormatEnum.Xlsx, result.SourceFormat, "detected source");
                    ContentSnapshot snapshot = XlsxInspector.Inspect(result.Output);
                    foreach (string[] row in ReferenceContent.TableRows)
                        TestSupport.Assert(snapshot.HasTableRow(row), "row survives: " + string.Join("|", row));
                }
            }));
        }

        private static void AddNegativeCases(List<TestCaseDescriptor> cases)
        {
            cases.Add(Case("TruncatedZip", "A truncated XLSX throws DocumentReadException", async ct =>
            {
                byte[] full = XlsxFixtureBuilder.BuildReference();
                byte[] truncated = full.Take(full.Length / 2).ToArray();
                await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(truncated, null, ct), "truncated").ConfigureAwait(false);
            }));

            cases.Add(Case("MissingWorkbook", "A zip without a workbook part throws DocumentReadException", async ct =>
            {
                await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(XlsxFixtureBuilder.BuildMissingWorkbook(), null, ct), "missing workbook").ConfigureAwait(false);
            }));

            cases.Add(Case("PasswordProtected", "An encrypted (OLE EncryptedPackage) file throws DocumentReadException naming password protection", async ct =>
            {
                DocumentReadException ex = await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(XlsxFixtureBuilder.BuildEncrypted(), null, ct), "encrypted").ConfigureAwait(false);
                TestSupport.AssertContains(ex.Message, "password", "message mentions password");
            }));

            cases.Add(Case("ZipBombGuard", "Parts larger than MaxDecompressedBytes throw InputTooLargeException", async ct =>
            {
                ConverterSettings settings = new ConverterSettings { MaxDecompressedBytes = 100 };
                using (Converter converter = new Converter(settings))
                {
                    await TestSupport.ExpectThrowsAsync<InputTooLargeException>(() => converter.ReadAsync(XlsxFixtureBuilder.BuildReference(), DocumentFormatEnum.Xlsx, null, ct), "zip bomb").ConfigureAwait(false);
                }
            }));

            cases.Add(Case("RandomBytes", "Random bytes declared as XLSX throw DocumentReadException", async ct =>
            {
                byte[] junk = new byte[2048];
                new Random(7).NextBytes(junk);
                await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(junk, null, ct), "random").ConfigureAwait(false);
            }));

            cases.Add(Case("PreCancelled", "A cancelled token throws OperationCanceledException for reads and writes", async ct =>
            {
                using (CancellationTokenSource cts = new CancellationTokenSource())
                using (Converter converter = new Converter())
                {
                    cts.Cancel();
                    await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => converter.ReadAsync(XlsxFixtureBuilder.BuildReference(), DocumentFormatEnum.Xlsx, null, cts.Token), "read").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => converter.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Xlsx, null, cts.Token), "write").ConfigureAwait(false);
                }
            }));
        }

        private static void AddWriterCases(List<TestCaseDescriptor> cases)
        {
            cases.Add(Case("WriteReference", "The reference model writes a valid workbook with every table cell and a Document sheet", async ct =>
            {
                BytesConversionResult result = await Write(ReferenceContent.ToModel(), null, ct).ConfigureAwait(false);
                ContentSnapshot snapshot = XlsxInspector.Inspect(result.Output);
                TestSupport.AssertEqual("Document", snapshot.Headings[0], "Document sheet first");
                TestSupport.AssertEqual(2, snapshot.Headings.Count, "two sheets");
                foreach (string[] row in ReferenceContent.TableRows)
                    TestSupport.Assert(snapshot.HasTableRow(row), "row: " + string.Join("|", row));
                foreach (string snippet in new string[] { ReferenceContent.Heading1, ReferenceContent.BoldText, ReferenceContent.LinkText, ReferenceContent.NestedBullet, "Step two", ReferenceContent.QuoteText, ReferenceContent.InternationalLatin, ReferenceContent.Closing })
                    TestSupport.Assert(snapshot.ContainsText(snippet), "text: " + snippet);
                TestSupport.AssertEqual(ReferenceContent.Title, snapshot.Title, "title");
            }));

            cases.Add(Case("WriteWarnings", "Writing the reference raises ImagesOmitted and FormattingLost", async ct =>
            {
                BytesConversionResult result = await Write(ReferenceContent.ToModel(), null, ct).ConfigureAwait(false);
                TestSupport.Assert(result.Warnings.Any(w => w.Code == WarningCodeEnum.ImagesOmitted), "ImagesOmitted");
                TestSupport.Assert(result.Warnings.Any(w => w.Code == WarningCodeEnum.FormattingLost), "FormattingLost");
            }));

            cases.Add(Case("NonTableContentDropped", "IncludeNonTableContent false drops the Document sheet with a warning", async ct =>
            {
                ConversionOptions options = new ConversionOptions();
                options.Xlsx.IncludeNonTableContent = false;
                BytesConversionResult result = await Write(ReferenceContent.ToModel(), options, ct).ConfigureAwait(false);
                ContentSnapshot snapshot = XlsxInspector.Inspect(result.Output);
                TestSupport.AssertEqual(1, snapshot.Headings.Count, "one sheet");
                TestSupport.Assert(!snapshot.Headings.Contains("Document"), "no Document sheet");
                TestSupport.Assert(result.Warnings.Any(w => w.Code == WarningCodeEnum.NonTableContentDropped), "warning");
            }));

            cases.Add(Case("InferCellTypes", "Typed cells: numbers, booleans, dates; leading zeros and long digit strings stay text", async ct =>
            {
                DocumentModel doc = TableModel("Types", new string[][]
                {
                    new string[] { "Value", "Kind" },
                    new string[] { "3.14", "decimal" },
                    new string[] { "TRUE", "boolean" },
                    new string[] { "2024-05-06", "date" },
                    new string[] { "2024-05-06T07:08:09", "datetime" },
                    new string[] { "007", "leading zero" },
                    new string[] { "1234567890123456789", "long digits" },
                    new string[] { "-2", "negative" }
                });
                BytesConversionResult result = await Write(doc, null, ct).ConfigureAwait(false);
                XlsxInspector.Inspect(result.Output);
                TestSupport.AssertEqual("n:3.14:style0", XlsxInspector.CellTypeAndValue(result.Output, "Types", "A2"), "number");
                TestSupport.AssertEqual("b:1:style0", XlsxInspector.CellTypeAndValue(result.Output, "Types", "A3"), "boolean");
                TestSupport.Assert((XlsxInspector.CellTypeAndValue(result.Output, "Types", "A4") ?? "").StartsWith("n:45418:style2", StringComparison.Ordinal), "date serial");
                TestSupport.Assert((XlsxInspector.CellTypeAndValue(result.Output, "Types", "A5") ?? "").StartsWith("n:45418.29", StringComparison.Ordinal), "datetime serial");
                TestSupport.Assert((XlsxInspector.CellTypeAndValue(result.Output, "Types", "A6") ?? "").StartsWith("s:", StringComparison.Ordinal), "leading zero stays text");
                TestSupport.Assert((XlsxInspector.CellTypeAndValue(result.Output, "Types", "A7") ?? "").StartsWith("s:", StringComparison.Ordinal), "long digits stay text");
                TestSupport.AssertEqual("n:-2:style0", XlsxInspector.CellTypeAndValue(result.Output, "Types", "A8"), "negative");
                TestSupport.Assert((XlsxInspector.CellTypeAndValue(result.Output, "Types", "A1") ?? "").StartsWith("s:", StringComparison.Ordinal), "header is text");

                DocumentModel back = await Read(result.Output, null, ct).ConfigureAwait(false);
                List<List<string>> cells = OfficeSuiteSupport.Cells(OfficeSuiteSupport.Find<TableBlock>(back).Single());
                TestSupport.AssertEqual("2024-05-06", cells[3][0], "date round trips");
                TestSupport.AssertEqual("2024-05-06T07:08:09", cells[4][0], "datetime round trips");
                TestSupport.AssertEqual("007", cells[5][0], "leading zero round trips");
            }));

            cases.Add(Case("InferCellTypesOff", "InferCellTypes false writes every cell as text", async ct =>
            {
                ConversionOptions options = new ConversionOptions();
                options.Xlsx.InferCellTypes = false;
                BytesConversionResult result = await Write(ReferenceContent.ToModel(), options, ct).ConfigureAwait(false);
                ContentSnapshot snapshot = XlsxInspector.Inspect(result.Output);
                string sheet = snapshot.Headings[1];
                TestSupport.Assert((XlsxInspector.CellTypeAndValue(result.Output, sheet, "C2") ?? "").StartsWith("s:", StringComparison.Ordinal), "text cell");
            }));

            cases.Add(Case("NumbersTyped", "With inference on, the reference Years column is numeric", async ct =>
            {
                BytesConversionResult result = await Write(ReferenceContent.ToModel(), null, ct).ConfigureAwait(false);
                ContentSnapshot snapshot = XlsxInspector.Inspect(result.Output);
                TestSupport.AssertEqual("n:7:style0", XlsxInspector.CellTypeAndValue(result.Output, snapshot.Headings[1], "C2"), "numeric years");
            }));

            cases.Add(Case("SheetNames", "Sheet names are sanitized, truncated to 31 characters and made unique", async ct =>
            {
                DocumentModel doc = new DocumentModel();
                foreach (string name in new string[] { "Bad/Name*?[x]", "Data", "data", new string('L', 40), "" })
                {
                    SectionBlock section = new SectionBlock(SectionKindEnum.Sheet, name);
                    TableBlock table = new TableBlock();
                    table.Rows.Add(new TableRow(new string[] { "a", "b" }));
                    section.Blocks.Add(table);
                    doc.Blocks.Add(section);
                }

                BytesConversionResult result = await Write(doc, null, ct).ConfigureAwait(false);
                List<string> names = XlsxInspector.Inspect(result.Output).Headings;
                TestSupport.AssertEqual(5, names.Count, "five sheets");
                TestSupport.AssertEqual("BadNamex", names[0], "invalid characters removed");
                TestSupport.AssertEqual("Data", names[1], "first Data");
                TestSupport.AssertEqual("data (2)", names[2], "case-insensitive duplicate suffixed");
                TestSupport.AssertEqual(31, names[3].Length, "truncated");
                TestSupport.AssertEqual(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "unique");
                TestSupport.Assert(names[4].Length > 0, "blank replaced");
            }));

            cases.Add(Case("SpansAndFrozenHeader", "Spans become merged cells, headers are bold and frozen, and the result validates", async ct =>
            {
                DocumentModel doc = new DocumentModel();
                TableBlock table = new TableBlock { HeaderRowCount = 1 };
                TableRow header = new TableRow(new string[] { "Wide", "C" });
                header.Cells[0].ColumnSpan = 2;
                table.Rows.Add(header);
                TableRow r1 = new TableRow(new string[] { "Tall", "b1", "c1" });
                r1.Cells[0].RowSpan = 2;
                table.Rows.Add(r1);
                table.Rows.Add(new TableRow(new string[] { "b2", "c2" }));
                doc.Blocks.Add(table);

                BytesConversionResult result = await Write(doc, null, ct).ConfigureAwait(false);
                ContentSnapshot snapshot = XlsxInspector.Inspect(result.Output);
                TestSupport.Assert(snapshot.HasTableRow(new string[] { "", "b2", "c2" }), "b2 placed after covered cell");
                using (MemoryStream ms = new MemoryStream(result.Output))
                using (SpreadsheetDocument spreadsheet = SpreadsheetDocument.Open(ms, false))
                {
                    Worksheet ws = spreadsheet.WorkbookPart!.WorksheetParts.First().Worksheet!;
                    List<string> merges = ws.Descendants<MergeCell>().Select(m => m.Reference!.Value!).ToList();
                    TestSupport.Assert(merges.Contains("A1:B1") && merges.Contains("A2:A3"), "merges: " + string.Join(",", merges));
                    Pane? pane = ws.Descendants<Pane>().FirstOrDefault();
                    TestSupport.Assert(pane != null && pane.State != null && pane.State.Value == PaneStateValues.Frozen, "frozen pane");
                    TestSupport.AssertEqual("s:0:style1", XlsxInspector.CellTypeAndValue(result.Output, "Table 1", "A1"), "bold header");
                }

                DocumentModel back = await Read(result.Output, null, ct).ConfigureAwait(false);
                TableBlock read = OfficeSuiteSupport.Find<TableBlock>(back).Single();
                TestSupport.AssertEqual(2, read.Rows[0].Cells[0].ColumnSpan, "column span round trips");
                TestSupport.AssertEqual(2, read.Rows[1].Cells[0].RowSpan, "row span round trips");
            }));

            cases.Add(Case("Deterministic", "Deterministic output is byte-identical across runs", async ct =>
            {
                ConversionOptions options = new ConversionOptions { Deterministic = true };
                BytesConversionResult a = await Write(ReferenceContent.ToModel(), options, ct).ConfigureAwait(false);
                await Task.Delay(1100, ct).ConfigureAwait(false);
                BytesConversionResult b = await Write(ReferenceContent.ToModel(), options, ct).ConfigureAwait(false);
                TestSupport.Assert(a.Output.SequenceEqual(b.Output), "identical bytes: " + OfficeSuiteSupport.DescribeZipDifference(a.Output, b.Output));
                XlsxInspector.Inspect(a.Output);
            }));

            cases.Add(Case("EmptyDocument", "An empty document writes one valid empty sheet named Sheet1", async ct =>
            {
                BytesConversionResult result = await Write(new DocumentModel(), null, ct).ConfigureAwait(false);
                ContentSnapshot snapshot = XlsxInspector.Inspect(result.Output);
                TestSupport.AssertEqual(1, snapshot.Headings.Count, "one sheet");
                TestSupport.AssertEqual("Sheet1", snapshot.Headings[0], "name");
            }));

            cases.Add(Case("SheetSectionsNameSheets", "Sheet sections name their tables' worksheets", async ct =>
            {
                DocumentModel doc = TableModel("Alpha", new string[][] { new string[] { "k", "v" }, new string[] { "a", "1" } });
                BytesConversionResult result = await Write(doc, null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual("Alpha", XlsxInspector.Inspect(result.Output).Headings[0], "sheet name");
            }));

            cases.Add(Case("RoundTripReference", "Model to XLSX to model preserves every table cell", async ct =>
            {
                BytesConversionResult result = await Write(ReferenceContent.ToModel(), null, ct).ConfigureAwait(false);
                DocumentModel back = await Read(result.Output, null, ct).ConfigureAwait(false);
                TableBlock table = OfficeSuiteSupport.Find<TableBlock>(back).First(t => OfficeSuiteSupport.Cells(t)[0][0] == "Name");
                List<List<string>> cells = OfficeSuiteSupport.Cells(table);
                for (int r = 0; r < ReferenceContent.TableRows.Length; r++)
                    for (int c = 0; c < 3; c++)
                        TestSupport.AssertEqual(ReferenceContent.TableRows[r][c], cells[r][c], "cell " + r + "," + c);
                TestSupport.AssertEqual(ReferenceContent.Title, back.Metadata.Title, "title");
            }));

            cases.Add(Case("XlsxToXlsx", "Converting the reference workbook to XLSX keeps both visible sheets and their data", async ct =>
            {
                using (Converter converter = new Converter())
                {
                    BytesConversionResult result = await converter.ConvertToBytesAsync(XlsxFixtureBuilder.BuildReference(), DocumentFormatEnum.Xlsx, DocumentFormatEnum.Xlsx, null, ct).ConfigureAwait(false);
                    ContentSnapshot snapshot = XlsxInspector.Inspect(result.Output);
                    TestSupport.Assert(snapshot.Headings.Contains("Staff") && snapshot.Headings.Contains("Notes"), "sheet names kept");
                    TestSupport.Assert((XlsxInspector.CellTypeAndValue(result.Output, "Notes", "B4") ?? "").StartsWith("n:44972:style2", StringComparison.Ordinal), "date written as a typed date cell");
                    DocumentModel back = await Read(result.Output, null, ct).ConfigureAwait(false);
                    TestSupport.AssertContains(OfficeSuiteSupport.AllText(back), XlsxFixtureBuilder.DateIso, "date reads back as ISO");
                }
            }));

            cases.Add(Case("LongCellTruncated", "Cell text over 32767 characters is truncated with ContentTruncated", async ct =>
            {
                DocumentModel doc = TableModel("Long", new string[][] { new string[] { new string('x', 40000), "b" } });
                BytesConversionResult result = await Write(doc, null, ct).ConfigureAwait(false);
                XlsxInspector.Inspect(result.Output);
                TestSupport.Assert(result.Warnings.Any(w => w.Code == WarningCodeEnum.ContentTruncated), "warning");
            }));
        }

        private static DocumentModel TableModel(string sheet, string[][] rows)
        {
            DocumentModel doc = new DocumentModel();
            SectionBlock section = new SectionBlock(SectionKindEnum.Sheet, sheet);
            TableBlock table = new TableBlock { HeaderRowCount = 1 };
            foreach (string[] row in rows) table.Rows.Add(new TableRow(row));
            section.Blocks.Add(table);
            doc.Blocks.Add(section);
            return doc;
        }

        private static async Task<DocumentModel> Read(byte[] bytes, ConversionOptions? options, CancellationToken token)
        {
            using (Converter converter = new Converter())
            {
                return await converter.ReadAsync(bytes, DocumentFormatEnum.Xlsx, options, token).ConfigureAwait(false);
            }
        }

        private static async Task<BytesConversionResult> Write(DocumentModel document, ConversionOptions? options, CancellationToken token)
        {
            using (Converter converter = new Converter())
            {
                return await converter.WriteToBytesAsync(document, DocumentFormatEnum.Xlsx, options, token).ConfigureAwait(false);
            }
        }

        private static TestCaseDescriptor Case(string id, string name, Func<CancellationToken, Task> body)
        {
            return new TestCaseDescriptor("Xlsx", id, name, executeAsync: body);
        }
    }
}
