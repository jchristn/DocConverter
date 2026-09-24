namespace Test.Shared.Doubles
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Ocr;
    using DocConverter.Options;
    using DocConverter.Readers;
    using DocConverter.Writers;
    using Test.Shared.Inspection;

    /// <summary>
    /// A text reader that upper-cases its input, to prove custom readers are used.
    /// </summary>
    public sealed class UpperTextReader : IDocumentReader
    {
        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats => new[] { DocumentFormatEnum.Text };

        /// <inheritdoc />
        public async Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            using (StreamReader reader = new StreamReader(input))
            {
                string text = await reader.ReadToEndAsync().ConfigureAwait(false);
                DocumentModel m = new DocumentModel();
                m.Blocks.Add(new ParagraphBlock(text.ToUpperInvariant()));
                return m;
            }
        }
    }
}
