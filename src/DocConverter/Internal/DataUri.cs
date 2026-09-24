namespace DocConverter.Internal
{
    using System;
    using DocConverter.Model;

    /// <summary>
    /// Decodes and encodes data URIs for images. Only base64 image data URIs are decoded; everything else returns null.
    /// </summary>
    internal static class DataUri
    {
        internal static BinaryResource? TryDecode(string? uri)
        {
            if (string.IsNullOrEmpty(uri)) return null;
            string value = uri!.Trim();
            if (!value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return null;
            int comma = value.IndexOf(',');
            if (comma < 0) return null;
            string header = value.Substring(5, comma - 5);
            if (!header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return null;
            string mediaType = header.Substring(0, header.Length - 7).Trim();

            byte[] data;
            try
            {
                data = Convert.FromBase64String(value.Substring(comma + 1).Trim());
            }
            catch (FormatException)
            {
                return null;
            }

            ImageInfo? info = ImageHeaderReader.Read(data);
            if (info == null && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return null;

            BinaryResource resource = new BinaryResource();
            resource.Data = data;
            resource.MediaType = info != null ? info.MediaType : mediaType;
            if (info != null)
            {
                resource.PixelWidth = info.Width;
                resource.PixelHeight = info.Height;
                resource.FileName = "image." + ImageHeaderReader.ExtensionFor(info.Format);
            }

            return resource;
        }

        internal static string Encode(BinaryResource resource)
        {
            return "data:" + resource.MediaType + ";base64," + Convert.ToBase64String(resource.Data);
        }
    }
}
