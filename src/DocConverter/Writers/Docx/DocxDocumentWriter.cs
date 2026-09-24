namespace DocConverter.Writers.Docx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Writes the document model as DOCX (Word Open XML). The package is built in code with no template: styles (Normal,
    /// Title, Heading1 to Heading6, Code, Quote, ListParagraph, Caption, Hyperlink, TableGrid), numbering for bulleted and
    /// numbered lists up to nine levels, tables with repeated header rows and merged cells, hyperlinks (http, https, mailto,
    /// relative and anchors only), inline images (PNG, JPEG, GIF, BMP, TIFF, WebP), page size and margins from
    /// DocxOptions, and core properties from metadata. With ConversionOptions.Deterministic, timestamps and zip entry dates
    /// are fixed so identical input yields identical bytes. Stateless and thread safe.
    /// </summary>
    public sealed class DocxDocumentWriter : IDocumentWriter
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Docx };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <summary>
        /// Instantiate the writer.
        /// </summary>
        public DocxDocumentWriter()
        {
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when document, output, options or context is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancelled.</exception>
        public async Task WriteAsync(DocumentModel document, Stream output, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (context == null) throw new ArgumentNullException(nameof(context));
            token.ThrowIfCancellationRequested();

            byte[] package = Build(document, options, context, token);
            if (options.Deterministic) package = DocxDeterministicZip.Normalize(package);
            token.ThrowIfCancellationRequested();
            await output.WriteAsync(package, 0, package.Length, token).ConfigureAwait(false);
        }

        private static byte[] Build(DocumentModel document, ConversionOptions options, ConversionContext context, CancellationToken token)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (WordprocessingDocument package = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
                {
                    MainDocumentPart main = package.AddMainDocumentPart();
                    main.Document = new W.Document(new W.Body());

                    StyleDefinitionsPart styles = main.AddNewPart<StyleDefinitionsPart>("rIdStyles");
                    styles.Styles = DocxStyleSheet.Build();

                    DocxWriteSession session = new DocxWriteSession(main, document, options, context, token);
                    DocxBlockWriter writer = new DocxBlockWriter(session);
                    W.Body body = main.Document.Body!;
                    writer.WriteBlocks(document.Blocks, body);
                    body.Append(session.Page.Build());

                    if (session.Numbering.HasLists)
                    {
                        NumberingDefinitionsPart numbering = main.AddNewPart<NumberingDefinitionsPart>("rIdNumbering");
                        numbering.Numbering = session.Numbering.Build();
                    }

                    CoreFilePropertiesPart core = package.AddCoreFilePropertiesPart();
                    using (Stream coreStream = core.GetStream(FileMode.Create, FileAccess.Write))
                    {
                        DocxCorePropertiesWriter.Write(coreStream, document.Metadata, options.Deterministic, options.IncludeMetadata);
                    }

                    main.Document.Save();
                }

                return ms.ToArray();
            }
        }
    }
}
