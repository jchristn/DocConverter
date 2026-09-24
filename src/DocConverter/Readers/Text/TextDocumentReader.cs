namespace DocConverter.Readers.Text
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Options;

    /// <summary>
    /// Reads plain text. Blank lines separate paragraphs; single line breaks inside a paragraph become line breaks.
    /// A byte order mark selects the encoding, otherwise ConversionOptions.InputEncoding or UTF-8. Stateless and thread safe.
    /// </summary>
    public sealed class TextDocumentReader : IDocumentReader
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
            string text = await TextIO.ReadAllTextAsync(input, options, context, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return Parse(text);
        }

        internal static DocumentModel Parse(string text)
        {
            DocumentModel document = new DocumentModel();
            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n').TrimStart('﻿');
            List<string> lines = new List<string>();
            foreach (string line in normalized.Split('\n'))
            {
                if (line.Trim().Length == 0)
                {
                    Flush(document, lines);
                    continue;
                }

                lines.Add(line.TrimEnd());
            }

            Flush(document, lines);
            return document;
        }

        private static void Flush(DocumentModel document, List<string> lines)
        {
            if (lines.Count == 0) return;
            ParagraphBlock paragraph = new ParagraphBlock();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) paragraph.Inlines.Add(new LineBreakInline());
                paragraph.Inlines.Add(new TextInline(lines[i]));
            }

            document.Blocks.Add(paragraph);
            lines.Clear();
        }
    }
}
