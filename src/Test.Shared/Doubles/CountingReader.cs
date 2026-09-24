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
    /// An RTF reader that counts calls, to prove unsupported pairs fail before reading.
    /// </summary>
    public sealed class CountingReader : IDocumentReader
    {
        /// <summary>
        /// Number of ReadAsync calls.
        /// </summary>
        public int Calls { get; private set; }

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats => new[] { DocumentFormatEnum.Rtf };

        /// <inheritdoc />
        public Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            Calls++;
            return Task.FromResult(new DocumentModel());
        }
    }
}
