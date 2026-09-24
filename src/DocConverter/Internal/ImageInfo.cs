namespace DocConverter.Internal
{
    using DocConverter.Enums;

    /// <summary>
    /// Facts about an image read from its header without decoding pixels.
    /// </summary>
    internal sealed class ImageInfo
    {
        internal DocumentFormatEnum Format { get; set; }

        internal string MediaType { get; set; } = "application/octet-stream";

        internal int? Width { get; set; }

        internal int? Height { get; set; }

        /// <summary>
        /// JPEG color components (1 gray, 3 RGB or YCbCr, 4 CMYK). Null for other formats.
        /// </summary>
        internal int? JpegComponents { get; set; }

        internal string FormatName
        {
            get
            {
                if (Format == DocumentFormatEnum.Jpeg && JpegComponents == 4) return "JPEG (CMYK)";
                return Format.ToString().ToUpperInvariant();
            }
        }
    }
}
