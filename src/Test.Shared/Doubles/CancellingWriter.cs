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
    /// A writer that cancels its token source and then observes the token, simulating cancellation mid-write.
    /// </summary>
    public sealed class CancellingWriter : IDocumentWriter
    {
        private readonly CancellationTokenSource _Source;

        /// <summary>
        /// Instantiate the writer.
        /// </summary>
        /// <param name="source">Token source to cancel.</param>
        public CancellingWriter(CancellationTokenSource source)
        {
            _Source = source;
        }

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats => new[] { DocumentFormatEnum.Text };

        /// <inheritdoc />
        public Task WriteAsync(DocumentModel document, Stream output, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            _Source.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
