namespace DocConverter.Writers.Docx
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
    /// State for one DOCX write: the main part, numbering, page geometry, and relationship and drawing id counters.
    /// Ids are sequential so identical input yields identical output.
    /// </summary>
    internal sealed class DocxWriteSession
    {
        private readonly Dictionary<string, string> _ImageRelationships = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _LinkRelationships = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _MissingImages = new HashSet<string>(StringComparer.Ordinal);
        private int _NextImage = 1;
        private int _NextLink = 1;
        private uint _NextDrawingId = 1;

        internal MainDocumentPart Main { get; }

        internal DocumentModel Document { get; }

        internal ConversionOptions Options { get; }

        internal ConversionContext Context { get; }

        internal CancellationToken Token { get; }

        internal DocxNumberingBuilder Numbering { get; } = new DocxNumberingBuilder();

        internal DocxPageSetup Page { get; }

        internal DocxWriteSession(MainDocumentPart main, DocumentModel document, ConversionOptions options, ConversionContext context, CancellationToken token)
        {
            Main = main;
            Document = document;
            Options = options;
            Context = context;
            Token = token;
            Page = new DocxPageSetup(options.Docx.PageSize, options.Docx.MarginPoints);
        }

        internal uint NextDrawingId()
        {
            return _NextDrawingId++;
        }

        internal string? LinkRelationship(string url)
        {
            if (_LinkRelationships.TryGetValue(url, out string? existing)) return existing;
            Uri uri;
            try
            {
                uri = new Uri(url, UriKind.RelativeOrAbsolute);
            }
            catch (UriFormatException)
            {
                return null;
            }

            string id = "rIdLink" + _NextLink++;
            Main.AddHyperlinkRelationship(uri, uri.IsAbsoluteUri, id);
            _LinkRelationships[url] = id;
            return id;
        }

        /// <summary>
        /// Relationship id of the embedded image for a resource, or null when the resource is missing or its format
        /// cannot be identified. Warnings are raised once per resource.
        /// </summary>
        internal string? ImageRelationship(string resourceId, out BinaryResource? resource)
        {
            resource = null;
            if (!Document.Resources.TryGetValue(resourceId, out BinaryResource? found))
            {
                if (_MissingImages.Add(resourceId))
                    Context.AddWarning(WarningCodeEnum.ImagesOmitted, "An image referenced resource '" + resourceId + "', which is not in the document, and was replaced by a placeholder.");
                return null;
            }

            resource = found;
            if (_ImageRelationships.TryGetValue(resourceId, out string? existing)) return existing;

            PartTypeInfo? type = PartType(found);
            if (type == null)
            {
                if (_MissingImages.Add(resourceId))
                    Context.AddWarning(WarningCodeEnum.ImageFormatUnsupported, "Image '" + resourceId + "' (" + found.MediaType + ") is not a format Word can embed and was replaced by a placeholder.");
                return null;
            }

            string id = "rIdImg" + _NextImage++;
            ImagePart part = Main.AddImagePart(type.Value, id);
            using (MemoryStream ms = new MemoryStream(found.Data, false))
            {
                part.FeedData(ms);
            }

            _ImageRelationships[resourceId] = id;
            return id;
        }

        private static PartTypeInfo? PartType(BinaryResource resource)
        {
            string media = resource.MediaType.ToLowerInvariant();
            ImageInfo? info = ImageHeaderReader.Read(resource.Data);
            if (info != null) media = info.MediaType;
            switch (media)
            {
                case "image/png": return ImagePartType.Png;
                case "image/jpeg":
                case "image/jpg": return ImagePartType.Jpeg;
                case "image/gif": return ImagePartType.Gif;
                case "image/bmp": return ImagePartType.Bmp;
                case "image/tiff": return ImagePartType.Tiff;
                case "image/webp": return new PartTypeInfo("image/webp", ".webp");
                default: return null;
            }
        }
    }
}
