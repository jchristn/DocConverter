namespace DocConverter.Internal
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using DocConverter.Model;

    /// <summary>
    /// Builds the documented placeholder text for images a target cannot show, for example
    /// "[Image: Reference image, PNG 16x16]".
    /// </summary>
    internal static class ImagePlaceholder
    {
        internal static string SingleLine(string? text)
        {
            // Alt text from Office often spans lines (a name, a blank line, then "Description automatically
            // generated"); inline image syntax and placeholders must stay on one line.
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder sb = new StringBuilder(text!.Length);
            bool space = false;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    space = sb.Length > 0;
                    continue;
                }

                if (space) sb.Append(' ');
                space = false;
                sb.Append(c);
            }

            return sb.ToString();
        }

        internal static string Describe(string? altText, string resourceId, Dictionary<string, BinaryResource> resources)
        {
            BinaryResource? resource = null;
            if (!string.IsNullOrEmpty(resourceId)) resources.TryGetValue(resourceId, out resource);

            string name = !string.IsNullOrWhiteSpace(altText)
                ? SingleLine(altText)
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
