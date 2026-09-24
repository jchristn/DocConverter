namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using Test.Shared.Suites.Docx;
    using Touchstone.Core;

    /// <summary>
    /// DOCX reader and writer tests.
    /// </summary>
    public static class DocxSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Docx",
                displayName: "DOCX reader and writer",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Docx", "ReadHeadings", "Reader keeps heading levels and text", executeAsync: DocxReaderCases.Headings),
                    new TestCaseDescriptor("Docx", "ReadInlineStyles", "Reader maps bold, italic, underline, strike and monospace runs", executeAsync: DocxReaderCases.InlineStyles),
                    new TestCaseDescriptor("Docx", "ReadHyperlink", "Reader turns relationship hyperlinks into links", executeAsync: DocxReaderCases.Hyperlink),
                    new TestCaseDescriptor("Docx", "ReadNestedBullets", "Reader nests bulleted paragraphs by level", executeAsync: DocxReaderCases.NestedBullets),
                    new TestCaseDescriptor("Docx", "ReadOrderedList", "Reader recognizes decimal numbering as an ordered list", executeAsync: DocxReaderCases.OrderedList),
                    new TestCaseDescriptor("Docx", "ReadTable", "Reader keeps table cells and the header row", executeAsync: DocxReaderCases.Table),
                    new TestCaseDescriptor("Docx", "ReadCodeBlock", "Reader merges preformatted paragraphs into a code block", executeAsync: DocxReaderCases.CodeBlock),
                    new TestCaseDescriptor("Docx", "ReadQuote", "Reader turns Quote styled paragraphs into a quote", executeAsync: DocxReaderCases.Quote),
                    new TestCaseDescriptor("Docx", "ReadImageInPosition", "Reader keeps image bytes, alt text and position", executeAsync: DocxReaderCases.ImageInPosition),
                    new TestCaseDescriptor("Docx", "ReadMetadata", "Reader fills metadata from core properties", executeAsync: DocxReaderCases.Metadata),
                    new TestCaseDescriptor("Docx", "ReadTextFidelity", "Reader keeps international and special characters exactly", executeAsync: DocxReaderCases.TextFidelity),
                    new TestCaseDescriptor("Docx", "ReadSpans", "Reader maps gridSpan and vMerge to spans", executeAsync: DocxReaderCases.Spans),
                    new TestCaseDescriptor("Docx", "ReadNotesIncluded", "Reader appends footnotes and endnotes with a warning", executeAsync: DocxReaderCases.NotesIncluded),
                    new TestCaseDescriptor("Docx", "ReadNotesExcluded", "Reader omits notes when IncludeFootnotes is false", executeAsync: DocxReaderCases.NotesExcluded),
                    new TestCaseDescriptor("Docx", "ReadTextBox", "Reader appends text box content as a section", executeAsync: DocxReaderCases.TextBox),
                    new TestCaseDescriptor("Docx", "ReadOutlineHeadings", "Reader honors outline levels, localized heading names and Title", executeAsync: DocxReaderCases.OutlineHeadings),
                    new TestCaseDescriptor("Docx", "ReadImageInMiddle", "Reader keeps an image between the paragraphs around it", executeAsync: DocxReaderCases.ImageInMiddle),
                    new TestCaseDescriptor("Docx", "ReadFieldHyperlink", "Reader turns HYPERLINK fields into links", executeAsync: DocxReaderCases.FieldHyperlink),
                    new TestCaseDescriptor("Docx", "ReadPageBreak", "Reader splits a paragraph at a page break", executeAsync: DocxReaderCases.PageBreak),
                    new TestCaseDescriptor("Docx", "ReadRealWorldSample", "Reader handles DocumentAtom's sample DOCX", executeAsync: DocxReaderCases.RealWorldSample),
                    new TestCaseDescriptor("Docx", "Detection", "Auto detection recognizes DOCX", executeAsync: DocxReaderCases.Detection),
                    new TestCaseDescriptor("Docx", "WriteReferenceValidates", "Writer output validates and holds all reference content", executeAsync: DocxWriterCases.ReferenceValidates),
                    new TestCaseDescriptor("Docx", "WriteMarkup", "Writer emits styles, numbering and header markup", executeAsync: DocxWriterCases.Markup),
                    new TestCaseDescriptor("Docx", "WriteDefaultPage", "Writer defaults to Letter with one inch margins", executeAsync: DocxWriterCases.DefaultPage),
                    new TestCaseDescriptor("Docx", "WritePageOptions", "Writer applies page size and margin options", executeAsync: DocxWriterCases.PageOptions),
                    new TestCaseDescriptor("Docx", "WriteDeterministic", "Writer produces identical bytes when deterministic", executeAsync: DocxWriterCases.Deterministic),
                    new TestCaseDescriptor("Docx", "WriteUnsafeLink", "Writer drops unsafe link schemes with a warning", executeAsync: DocxWriterCases.UnsafeLink),
                    new TestCaseDescriptor("Docx", "WriteSpans", "Writer emits gridSpan and vMerge and pads ragged rows", executeAsync: DocxWriterCases.Spans),
                    new TestCaseDescriptor("Docx", "WriteAllImageFormats", "Writer embeds PNG, JPEG, GIF, BMP, TIFF and WebP", executeAsync: DocxWriterCases.AllImageFormats),
                    new TestCaseDescriptor("Docx", "WriteMissingAndUnknownImages", "Writer substitutes placeholders for missing or unknown images", executeAsync: DocxWriterCases.MissingAndUnknownImages),
                    new TestCaseDescriptor("Docx", "WriteDeepList", "Writer caps list depth at nine levels with a warning", executeAsync: DocxWriterCases.DeepList),
                    new TestCaseDescriptor("Docx", "WriteListKindsRoundTrip", "Ordered start values and task lists round trip", executeAsync: DocxWriterCases.ListKindsRoundTrip),
                    new TestCaseDescriptor("Docx", "RoundTripReference", "Reference model survives DOCX round trip", executeAsync: DocxWriterCases.RoundTrip),
                    new TestCaseDescriptor("Docx", "WriteSectionsAndBreaks", "Sections, breaks, alignment and superscript round trip", executeAsync: DocxWriterCases.SectionsAndBreaks),
                    new TestCaseDescriptor("Docx", "WriteEmptyDocument", "An empty document writes and reads back empty", executeAsync: DocxWriterCases.EmptyDocument),
                    new TestCaseDescriptor("Docx", "WriteHostileText", "Control characters and markup-like text are written safely", executeAsync: DocxWriterCases.HostileText),
                    new TestCaseDescriptor("Docx", "ConvertAuto", "DOCX to DOCX with Auto detection", executeAsync: DocxWriterCases.ConvertAuto),
                    new TestCaseDescriptor("Docx", "NegativeTruncated", "Truncated zip throws DocumentReadException", executeAsync: DocxNegativeCases.Truncated),
                    new TestCaseDescriptor("Docx", "NegativeMissingMainPart", "Package without main part throws DocumentReadException", executeAsync: DocxNegativeCases.MissingMainPart),
                    new TestCaseDescriptor("Docx", "NegativePasswordProtected", "Encrypted package throws DocumentReadException", executeAsync: DocxNegativeCases.PasswordProtected),
                    new TestCaseDescriptor("Docx", "NegativeLegacyDoc", "Legacy .doc throws DocumentReadException", executeAsync: DocxNegativeCases.LegacyDoc),
                    new TestCaseDescriptor("Docx", "NegativeZipBomb", "Decompression limit throws InputTooLargeException", executeAsync: DocxNegativeCases.ZipBomb),
                    new TestCaseDescriptor("Docx", "NegativeEmpty", "Empty input throws DocumentReadException", executeAsync: DocxNegativeCases.Empty),
                    new TestCaseDescriptor("Docx", "NegativeNotAZip", "Random bytes throw DocumentReadException", executeAsync: DocxNegativeCases.NotAZip),
                    new TestCaseDescriptor("Docx", "NegativeCancelled", "Cancelled token stops read, write and convert", executeAsync: DocxNegativeCases.Cancelled),
                    new TestCaseDescriptor("Docx", "StreamInput", "Stream input is read from its position and left open", executeAsync: DocxNegativeCases.StreamInput)
                });
        }
    }
}
