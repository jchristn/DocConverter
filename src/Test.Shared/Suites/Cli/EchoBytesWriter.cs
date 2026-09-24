#if !DOCCONVERTER_NETSTANDARD_TEST
namespace Test.Shared.Suites.Cli
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocConverter.Writers;

    /// <summary>
    /// Test writer for PDF that writes resource "raw" unchanged, proving the CLI moves bytes without transcoding.
    /// </summary>
    public sealed class EchoBytesWriter : IDocumentWriter
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Pdf };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <inheritdoc />
        public Task WriteAsync(DocumentModel document, Stream output, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            byte[] data = document.Resources.TryGetValue("raw", out BinaryResource? raw) ? raw.Data : new byte[0];
            return output.WriteAsync(data, 0, data.Length, token);
        }
    }
}
#endif
