namespace DocConverter.Readers.Docx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocumentFormat.OpenXml.Packaging;

    /// <summary>
    /// Everything one DOCX read needs: the package, resolvers, the document being built and bookkeeping for images,
    /// text boxes and notes.
    /// </summary>
    internal sealed class DocxReadSession
    {
        private readonly Dictionary<string, string> _ImagesByUri = new Dictionary<string, string>(StringComparer.Ordinal);

        internal WordprocessingDocument Package { get; }

        internal MainDocumentPart Main { get; }

        internal ConversionOptions Options { get; }

        internal ConversionContext Context { get; }

        internal CancellationToken Token { get; }

        internal DocumentModel Document { get; } = new DocumentModel();

        internal DocxStyleResolver Styles { get; }

        internal DocxNumberingResolver Numbering { get; }

        internal List<DocxTextBox> TextBoxes { get; } = new List<DocxTextBox>();

        internal List<long> FootnoteOrder { get; } = new List<long>();

        internal List<long> EndnoteOrder { get; } = new List<long>();

        internal string? TitleFallback { get; set; } = null;

        internal DocxReadSession(WordprocessingDocument package, MainDocumentPart main, ConversionOptions options, ConversionContext context, CancellationToken token)
        {
            Package = package;
            Main = main;
            Options = options;
            Context = context;
            Token = token;
            Styles = new DocxStyleResolver(main.StyleDefinitionsPart);
            Numbering = new DocxNumberingResolver(main.NumberingDefinitionsPart);
        }

        internal int FootnoteNumber(long id)
        {
            int index = FootnoteOrder.IndexOf(id);
            if (index < 0)
            {
                FootnoteOrder.Add(id);
                index = FootnoteOrder.Count - 1;
            }

            return index + 1;
        }

        internal int EndnoteNumber(long id)
        {
            int index = EndnoteOrder.IndexOf(id);
            if (index < 0)
            {
                EndnoteOrder.Add(id);
                index = EndnoteOrder.Count - 1;
            }

            return index + 1;
        }

        internal string? AddImage(OpenXmlPart owner, string relationshipId)
        {
            OpenXmlPart? part;
            try
            {
                part = owner.GetPartById(relationshipId);
            }
            catch (ArgumentOutOfRangeException)
            {
                part = null;
            }
            catch (KeyNotFoundException)
            {
                part = null;
            }

            if (!(part is ImagePart image))
            {
                Context.AddWarning(WarningCodeEnum.UnknownElementSkipped, "An image relationship '" + relationshipId + "' does not resolve to an image part and was skipped.");
                return null;
            }

            string key = image.Uri.ToString();
            if (_ImagesByUri.TryGetValue(key, out string? existing)) return existing;

            byte[] data;
            using (Stream stream = image.GetStream(FileMode.Open, FileAccess.Read))
            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                data = ms.ToArray();
            }

            ImageInfo? info = ImageHeaderReader.Read(data);
            string fileName = key;
            int slash = fileName.LastIndexOf('/');
            if (slash >= 0) fileName = fileName.Substring(slash + 1);

            BinaryResource resource = new BinaryResource
            {
                MediaType = info != null ? info.MediaType : image.ContentType,
                Data = data,
                FileName = fileName,
                PixelWidth = info?.Width,
                PixelHeight = info?.Height
            };

            string id = Document.AddResource(resource);
            _ImagesByUri[key] = id;
            return id;
        }
    }
}
