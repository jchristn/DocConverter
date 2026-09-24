namespace DocConverter.Writers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Options;

    /// <summary>
    /// Per-write state shared by the text based writers: the document, options, context, cancellation token, and the
    /// names given to images written as external files.
    /// </summary>
    internal sealed class RenderState
    {
        private readonly Dictionary<string, string> _ExternalNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _UsedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal DocumentModel Document { get; }

        internal ConversionOptions Options { get; }

        internal ConversionContext Context { get; }

        internal CancellationToken Token { get; }

        internal RenderState(DocumentModel document, ConversionOptions options, ConversionContext context, CancellationToken token)
        {
            Document = document;
            Options = options;
            Context = context;
            Token = token;
        }

        /// <summary>
        /// File name for a resource written as a side file. Registers the resource with the context the first time.
        /// </summary>
        internal string ExternalName(BinaryResource resource)
        {
            if (_ExternalNames.TryGetValue(resource.Id, out string? existing)) return existing;

            string extension = "bin";
            ImageInfo? info = ImageHeaderReader.Read(resource.Data);
            if (info != null) extension = ImageHeaderReader.ExtensionFor(info.Format);

            string baseName = !string.IsNullOrEmpty(resource.FileName) ? System.IO.Path.GetFileNameWithoutExtension(resource.FileName) : resource.Id;
            if (string.IsNullOrEmpty(baseName)) baseName = "image";
            foreach (char invalid in new char[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|', ' ' }) baseName = baseName.Replace(invalid, '_');

            string name = baseName + "." + extension;
            int n = 2;
            while (_UsedNames.Contains(name)) name = baseName + "-" + n++ + "." + extension;
            _UsedNames.Add(name);
            _ExternalNames[resource.Id] = name;

            BinaryResource side = new BinaryResource
            {
                Id = resource.Id,
                MediaType = resource.MediaType,
                Data = resource.Data,
                FileName = name,
                PixelWidth = resource.PixelWidth,
                PixelHeight = resource.PixelHeight
            };
            Context.AddOutputResource(side);
            return name;
        }
    }
}
