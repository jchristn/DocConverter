namespace DocConverter.Writers.Json
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Model.Serialization;
    using DocConverter.Options;

    /// <summary>
    /// Writes the canonical JSON form of the document (the "docconverter": "1" shape documented in docs/DOCUMENT_MODEL.md).
    /// Reading the output back with the JSON reader reproduces the document exactly. Resource bytes are base64 unless
    /// JsonOptions.IncludeBinary is false. Stateless and thread safe.
    /// </summary>
    public sealed class JsonDocumentWriter : IDocumentWriter
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Json };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <inheritdoc />
        public Task WriteAsync(DocumentModel document, Stream output, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (output == null) throw new ArgumentNullException(nameof(output));
            token.ThrowIfCancellationRequested();
            CanonicalDocument dto = CanonicalMapper.ToDto(document, true, options.Json.IncludeBinary);
            if (!options.Json.IncludeBinary && document.Resources.Count > 0)
                context.AddWarning(WarningCodeEnum.ImagesOmitted, "Image bytes were left out of the JSON because JsonOptions.IncludeBinary is false.");
            byte[] utf8 = CanonicalJson.Serialize(dto, options.Json.Indented);
            string json = new UTF8Encoding(false).GetString(utf8);
            if (options.Json.Indented) json += "\n";
            return TextIO.WriteAllTextAsync(json, output, options, token);
        }
    }
}
