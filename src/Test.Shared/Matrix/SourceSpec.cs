namespace Test.Shared.Matrix
{
    using System;
    using System.Collections.Generic;
    using DocConverter.Enums;

    /// <summary>
    /// One source variant in the conversion matrix: its bytes and what content it carries, so expectations for each
    /// target can be computed as the intersection of source content and target capability.
    /// </summary>
    public sealed class SourceSpec
    {
        private readonly Func<byte[]> _Bytes;
        private byte[]? _Cached = null;

        /// <summary>
        /// Short identifier used in case ids, for example "JsonCanonical".
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Source format.
        /// </summary>
        public DocumentFormatEnum Format { get; }

        /// <summary>
        /// Text snippets that must survive into any text carrying target.
        /// </summary>
        public List<string> Snippets { get; } = new List<string>();

        /// <summary>
        /// Table rows the source holds.
        /// </summary>
        public List<string[]> TableRows { get; } = new List<string[]>();

        /// <summary>
        /// Headings the source declares.
        /// </summary>
        public List<string> Headings { get; } = new List<string>();

        /// <summary>
        /// True when the source holds an image.
        /// </summary>
        public bool HasImage { get; set; } = false;

        /// <summary>
        /// True when that image can be embedded in PDF (PNG, BMP, RGB or gray JPEG).
        /// </summary>
        public bool ImageEmbeddableInPdf { get; set; } = true;

        /// <summary>
        /// True when the source is only an image.
        /// </summary>
        public bool ImageOnly { get; set; } = false;

        /// <summary>
        /// Instantiate a source.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="format">Format.</param>
        /// <param name="bytes">Factory for the source bytes; called once and cached.</param>
        public SourceSpec(string id, DocumentFormatEnum format, Func<byte[]> bytes)
        {
            Id = id;
            Format = format;
            _Bytes = bytes;
        }

        /// <summary>
        /// Source bytes.
        /// </summary>
        /// <returns>Bytes.</returns>
        public byte[] Bytes()
        {
            lock (_Bytes)
            {
                if (_Cached == null) _Cached = _Bytes();
                return _Cached;
            }
        }
    }
}
