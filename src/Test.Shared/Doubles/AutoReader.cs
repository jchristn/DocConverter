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
    /// A reader that illegally declares Auto, to prove registration rejects it.
    /// </summary>
    public sealed class AutoReader : IDocumentReader
    {
        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats => new[] { DocumentFormatEnum.Auto };

        /// <inheritdoc />
        public Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            return Task.FromResult(new DocumentModel());
        }
    }
}
