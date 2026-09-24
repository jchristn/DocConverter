namespace DocConverter.Writers.Xml
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Model.Serialization;
    using DocConverter.Options;

    /// <summary>
    /// Writes the canonical XML form of the document (root &lt;docconverter version="1"&gt;). Reading the output back with
    /// the XML reader reproduces the document exactly. Characters XML 1.0 cannot represent are removed. Resource bytes are
    /// base64 unless XmlOptions.IncludeBinary is false. Stateless and thread safe.
    /// </summary>
    public sealed class XmlDocumentWriter : IDocumentWriter
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Xml };

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
            CanonicalDocument dto = CanonicalMapper.ToDto(document, true, options.Xml.IncludeBinary);
            if (!options.Xml.IncludeBinary && document.Resources.Count > 0)
                context.AddWarning(WarningCodeEnum.ImagesOmitted, "Image bytes were left out of the XML because XmlOptions.IncludeBinary is false.");
            string xml = CanonicalXml.Serialize(dto, options.Xml.Indented, options.OutputEncoding.WebName);
            return TextIO.WriteAllTextAsync(xml, output, options, token);
        }
    }
}
