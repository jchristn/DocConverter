namespace DocConverter.Model
{
    using System;

    /// <summary>
    /// A binary resource, in practice an image, referenced by id from the document.
    /// </summary>
    public class BinaryResource
    {
        private string _Id = "";
        private string _MediaType = "application/octet-stream";
        private byte[] _Data = Array.Empty<byte>();

        /// <summary>
        /// Resource id, unique within a document. Never null.
        /// </summary>
        public string Id
        {
            get => _Id;
            set => _Id = value ?? "";
        }

        /// <summary>
        /// Media type, for example "image/png". Default "application/octet-stream". Never null.
        /// </summary>
        public string MediaType
        {
            get => _MediaType;
            set => _MediaType = string.IsNullOrEmpty(value) ? "application/octet-stream" : value;
        }

        /// <summary>
        /// Raw bytes. Never null.
        /// </summary>
        public byte[] Data
        {
            get => _Data;
            set => _Data = value ?? Array.Empty<byte>();
        }

        /// <summary>
        /// Original or suggested file name. Null when unknown.
        /// </summary>
        public string? FileName { get; set; } = null;

        /// <summary>
        /// Width in pixels. Null when unknown.
        /// </summary>
        public int? PixelWidth { get; set; } = null;

        /// <summary>
        /// Height in pixels. Null when unknown.
        /// </summary>
        public int? PixelHeight { get; set; } = null;
    }
}
