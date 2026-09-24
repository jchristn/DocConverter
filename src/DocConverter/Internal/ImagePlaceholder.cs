namespace DocConverter.Internal
{
    using System.Collections.Generic;
    using System.Globalization;
    using DocConverter.Model;

    /// <summary>
    /// Builds the documented placeholder text for images a target cannot show, for example
    /// "[Image: Reference image, PNG 16x16]".
    /// </summary>
    internal static class ImagePlaceholder
    {
        internal static string Describe(string? altText, string resourceId, Dictionary<string, BinaryResource> resources)
        {
            BinaryResource? resource = null;
            if (!string.IsNullOrEmpty(resourceId)) resources.TryGetValue(resourceId, out resource);

            string name = !string.IsNullOrWhiteSpace(altText)
                ? altText!.Trim()
                : (resource != null && !string.IsNullOrEmpty(resource.FileName) ? resource.FileName! : "image");

            if (resource == null) return "[Image: " + name + "]";

            string format = resource.MediaType.StartsWith("image/", System.StringComparison.Ordinal)
                ? resource.MediaType.Substring(6).ToUpperInvariant()
                : resource.MediaType;
            if (format == "JPEG" || format == "PJPEG") format = "JPEG";

            ImageInfo? info = ImageHeaderReader.Read(resource.Data);
            if (info != null) format = info.FormatName;

            int? w = resource.PixelWidth ?? info?.Width;
            int? h = resource.PixelHeight ?? info?.Height;
            string size = w.HasValue && h.HasValue
                ? " " + w.Value.ToString(CultureInfo.InvariantCulture) + "x" + h.Value.ToString(CultureInfo.InvariantCulture)
                : "";
            return "[Image: " + name + ", " + format + size + "]";
        }
    }
}
