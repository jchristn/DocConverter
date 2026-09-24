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
    using DocConverter.Readers;

    /// <summary>
    /// Test reader for PNG that keeps the raw input bytes as resource "raw", so a matching writer can echo them.
    /// </summary>
    public sealed class EchoImageReader : IDocumentReader
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Png };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <inheritdoc />
        public async Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                await input.CopyToAsync(ms, 81920, token).ConfigureAwait(false);
                DocumentModel doc = new DocumentModel();
                doc.AddResource(new BinaryResource { Id = "raw", MediaType = "image/png", Data = ms.ToArray() });
                doc.Blocks.Add(new ImageBlock("raw", "raw"));
                return doc;
            }
        }
    }
}
#endif
