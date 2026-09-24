namespace DocConverter.Readers.Docx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Packaging;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Detection;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Reads DOCX (Word Open XML) into the document model. Headings come from heading styles and outline levels; runs keep
    /// bold, italic, underline, strikethrough, code (monospace fonts or code styles), superscript and subscript; hyperlinks
    /// (relationship and field based), nested numbered and bulleted lists, tables with column and row spans, code blocks,
    /// quotes, images at their position, and core properties are carried over. Footnotes and endnotes (when
    /// DocxOptions.IncludeFootnotes) and text boxes are appended as trailing sections with the FormattingLost warning.
    /// Headers, footers and comments are not read. Works entirely in memory. Stateless and thread safe.
    /// </summary>
    public sealed class DocxDocumentReader : IDocumentReader
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Docx };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <summary>
        /// Instantiate the reader.
        /// </summary>
        public DocxDocumentReader()
        {
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when input, options or context is null.</exception>
        /// <exception cref="DocumentReadException">Thrown when the input is not a readable DOCX (corrupt, encrypted, legacy .doc, or missing its main part).</exception>
        /// <exception cref="InputTooLargeException">Thrown when the package decompresses beyond MaxDecompressedBytes.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (context == null) throw new ArgumentNullException(nameof(context));
            token.ThrowIfCancellationRequested();

            byte[] data = ReadAll(input);
            if (data.Length == 0) throw new DocumentReadException("The DOCX input is empty.");
            if (OleDirectoryInspector.IsOle(data, 0, data.Length))
            {
                if (OleDirectoryInspector.IsEncryptedOoxml(data, 0, data.Length))
                    throw new DocumentReadException("The DOCX input is password protected (encrypted). Remove the password in Word and try again.");
                throw new DocumentReadException("The input is a " + OleDirectoryInspector.Describe(data, 0, data.Length) + ", not a DOCX file. Save it as .docx first.");
            }

            using (MemoryStream stream = new MemoryStream(data, false))
            {
                ZipSafety.EnsureSafe(stream, context.MaxDecompressedBytes, "DOCX");
                token.ThrowIfCancellationRequested();

                WordprocessingDocument package;
                try
                {
                    package = WordprocessingDocument.Open(stream, false);
                }
                catch (OpenXmlPackageException ex)
                {
                    throw new DocumentReadException("The input is not a valid DOCX package: " + ex.Message, ex);
                }
                catch (InvalidDataException ex)
                {
                    throw new DocumentReadException("The input is not a valid DOCX package: " + ex.Message, ex);
                }
                catch (FileFormatException ex)
                {
                    throw new DocumentReadException("The input is not a valid DOCX package: " + ex.Message, ex);
                }

                using (package)
                {
                    MainDocumentPart? main = package.MainDocumentPart;
                    if (main == null || main.Document == null || main.Document.Body == null)
                        throw new DocumentReadException("The DOCX package has no main document part (word/document.xml).");

                    DocxReadSession session = new DocxReadSession(package, main, options, context, token);
                    DocxBodyReader body = new DocxBodyReader(session);
                    List<Block> blocks = body.ReadBlocks(main.Document.Body.ChildElements, main, 0);
                    session.Document.Blocks.AddRange(blocks);

                    AppendTextBoxes(session, body);
                    if (options.Docx.IncludeFootnotes) AppendNotes(session, body);
                    ReadMetadata(session);
                    return Task.FromResult(session.Document);
                }
            }
        }

        private static byte[] ReadAll(Stream input)
        {
            if (input is MemoryStream memory && memory.Position == 0) return memory.ToArray();
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static void AppendTextBoxes(DocxReadSession session, DocxBodyReader body)
        {
            if (session.TextBoxes.Count == 0) return;
            SectionBlock section = new SectionBlock(SectionKindEnum.Generic, "Text boxes");
            int index = 0;
            while (index < session.TextBoxes.Count)
            {
                DocxTextBox box = session.TextBoxes[index++];
                session.Token.ThrowIfCancellationRequested();
                section.Blocks.AddRange(body.ReadBlocks(box.Content.ChildElements, box.Owner, 1));
            }

            if (section.Blocks.Count == 0) return;
            session.Document.Blocks.Add(section);
            session.Context.AddWarning(WarningCodeEnum.FormattingLost, "Text box content was appended as a trailing \"Text boxes\" section.");
        }

        private static void AppendNotes(DocxReadSession session, DocxBodyReader body)
        {
            FootnotesPart? footnotes = session.Main.FootnotesPart;
            if (footnotes != null && footnotes.Footnotes != null && session.FootnoteOrder.Count > 0)
            {
                Dictionary<long, W.Footnote> byId = new Dictionary<long, W.Footnote>();
                foreach (W.Footnote note in footnotes.Footnotes.Elements<W.Footnote>())
                    if (note.Id != null && !byId.ContainsKey(note.Id.Value)) byId[note.Id.Value] = note;

                SectionBlock section = new SectionBlock(SectionKindEnum.Generic, "Footnotes");
                for (int i = 0; i < session.FootnoteOrder.Count; i++)
                {
                    if (!byId.TryGetValue(session.FootnoteOrder[i], out W.Footnote? note)) continue;
                    List<Block> blocks = body.ReadBlocks(note.ChildElements, footnotes, 1);
                    Prefix(blocks, "[" + (i + 1) + "] ");
                    section.Blocks.AddRange(blocks);
                }

                if (section.Blocks.Count > 0)
                {
                    session.Document.Blocks.Add(section);
                    session.Context.AddWarning(WarningCodeEnum.FormattingLost, "Footnotes and endnotes were appended as trailing sections.");
                }
            }

            EndnotesPart? endnotes = session.Main.EndnotesPart;
            if (endnotes != null && endnotes.Endnotes != null && session.EndnoteOrder.Count > 0)
            {
                Dictionary<long, W.Endnote> byId = new Dictionary<long, W.Endnote>();
                foreach (W.Endnote note in endnotes.Endnotes.Elements<W.Endnote>())
                    if (note.Id != null && !byId.ContainsKey(note.Id.Value)) byId[note.Id.Value] = note;

                SectionBlock section = new SectionBlock(SectionKindEnum.Generic, "Endnotes");
                for (int i = 0; i < session.EndnoteOrder.Count; i++)
                {
                    if (!byId.TryGetValue(session.EndnoteOrder[i], out W.Endnote? note)) continue;
                    List<Block> blocks = body.ReadBlocks(note.ChildElements, endnotes, 1);
                    Prefix(blocks, "[e" + (i + 1) + "] ");
                    section.Blocks.AddRange(blocks);
                }

                if (section.Blocks.Count > 0)
                {
                    session.Document.Blocks.Add(section);
                    session.Context.AddWarning(WarningCodeEnum.FormattingLost, "Footnotes and endnotes were appended as trailing sections.");
                }
            }
        }

        private static void Prefix(List<Block> blocks, string prefix)
        {
            if (blocks.Count > 0 && blocks[0] is ParagraphBlock paragraph)
            {
                if (paragraph.Inlines.Count > 0 && paragraph.Inlines[0] is TextInline text && text.Style == InlineStyleEnum.None) text.Text = prefix + text.Text;
                else paragraph.Inlines.Insert(0, new TextInline(prefix));
            }
            else
            {
                blocks.Insert(0, new ParagraphBlock(prefix.Trim()));
            }
        }

        private static void ReadMetadata(DocxReadSession session)
        {
            DocumentMetadata metadata = session.Document.Metadata;
            CoreFilePropertiesPart? core = session.Package.CoreFilePropertiesPart;
            if (core != null)
            {
                try
                {
                    using (Stream stream = core.GetStream(FileMode.Open, FileAccess.Read))
                    {
                        DocxCoreProperties.Read(stream, metadata);
                    }
                }
                catch (System.Xml.XmlException ex)
                {
                    session.Context.AddWarning(WarningCodeEnum.UnknownElementSkipped, "The DOCX core properties could not be read: " + ex.Message);
                }
            }

            if (string.IsNullOrEmpty(metadata.Title) && !string.IsNullOrEmpty(session.TitleFallback)) metadata.Title = session.TitleFallback;
        }

        private static string? Clean(string? value)
        {
            if (value == null) return null;
            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
