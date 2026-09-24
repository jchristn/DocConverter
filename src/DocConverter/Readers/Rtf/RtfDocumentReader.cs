namespace DocConverter.Readers.Rtf
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Model;
    using DocConverter.Options;

    /// <summary>
    /// Reads RTF into the document model. Heading levels come from \outlinelevel and "heading N" styles, inline styles
    /// from \b, \i, \ul, \strike, \super and \sub, lists from \ls, \listtext and \pntext, tables from \trowd ... \row
    /// (with merged cells and \trhdr header rows), PNG and JPEG pictures from \pict, links from HYPERLINK fields, and
    /// metadata from \info. Text is decoded with the document code page (\ansicpg, Windows-1252 by default) and \u
    /// escapes. Stateless and thread safe.
    /// </summary>
    public sealed class RtfDocumentReader : IDocumentReader
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Rtf };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <summary>
        /// Instantiate the reader.
        /// </summary>
        public RtfDocumentReader()
        {
        }

        /// <inheritdoc />
        public async Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (context == null) throw new ArgumentNullException(nameof(context));
            token.ThrowIfCancellationRequested();

            string rtf;
            bool rawBytes;
            if (context.SourceText != null)
            {
                rtf = context.SourceText;
                rawBytes = false;
            }
            else
            {
                byte[] data;
                using (MemoryStream ms = new MemoryStream())
                {
                    await input.CopyToAsync(ms, 81920, token).ConfigureAwait(false);
                    data = ms.ToArray();
                }

                int offset = 0;
                if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) offset = 3;
                StringBuilder sb = new StringBuilder(data.Length);
                for (int i = offset; i < data.Length; i++) sb.Append((char)data[i]);
                rtf = sb.ToString();
                rawBytes = true;
            }

            string trimmed = rtf.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            if (!trimmed.StartsWith("{\\rtf", StringComparison.Ordinal))
                throw new DocumentReadException("The input is not RTF: it does not begin with the {\\rtf header.");

            List<RtfToken> tokens = RtfTokenizer.Tokenize(trimmed);
            token.ThrowIfCancellationRequested();
            RtfParser parser = new RtfParser(context, rawBytes, token);
            return parser.Parse(tokens);
        }
    }
}
