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
    /// A text writer that reverses the first paragraph, to prove custom writers are used.
    /// </summary>
    public sealed class ReverseTextWriter : IDocumentWriter
    {
        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats => new[] { DocumentFormatEnum.Text };

        /// <inheritdoc />
        public async Task WriteAsync(DocumentModel document, Stream output, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            string text = ModelInspector.Text(((ParagraphBlock)document.Blocks[0]).Inlines);
            char[] chars = text.ToCharArray();
            Array.Reverse(chars);
            byte[] bytes = Encoding.UTF8.GetBytes(new string(chars));
            await output.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
        }
    }
}
