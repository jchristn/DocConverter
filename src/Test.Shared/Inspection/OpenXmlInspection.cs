namespace Test.Shared.Inspection
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using DocumentFormat.OpenXml.Validation;

    /// <summary>
    /// Validation and metadata helpers shared by the Office inspectors.
    /// </summary>
    public static class OpenXmlInspection
    {
        /// <summary>
        /// Validate a package against the Office 2019 schema and throw listing every error.
        /// </summary>
        /// <param name="package">Package.</param>
        /// <param name="formatName">Format name for messages.</param>
        /// <exception cref="TestAssertionException">Thrown when validation errors exist.</exception>
        public static void Validate(OpenXmlPackage package, string formatName)
        {
            OpenXmlValidator validator = new OpenXmlValidator(FileFormatVersions.Office2019);
            List<ValidationErrorInfo> errors = validator.Validate(package).ToList();
            if (errors.Count == 0) return;
            List<string> lines = new List<string>();
            foreach (ValidationErrorInfo e in errors.Take(10))
                lines.Add((e.Part?.Uri?.ToString() ?? "") + " " + (e.Path?.XPath ?? "") + ": " + e.Description);
            throw new TestAssertionException(formatName + " output has " + errors.Count + " validation error(s): " + string.Join(" | ", lines));
        }

        /// <summary>
        /// Read dc:title from the core properties part, or null.
        /// </summary>
        /// <param name="package">Package.</param>
        /// <returns>Title.</returns>
        public static string? Title(OpenXmlPackage package)
        {
            CoreFilePropertiesPart? core = package.GetPartsOfType<CoreFilePropertiesPart>().FirstOrDefault();
            if (core == null) return null;
            using (Stream s = core.GetStream(FileMode.Open, FileAccess.Read))
            {
                XDocument doc = XDocument.Load(s);
                XElement? title = doc.Root?.Element(XName.Get("title", "http://purl.org/dc/elements/1.1/"));
                return title?.Value;
            }
        }

        /// <summary>
        /// Append text to a snapshot's AllText with single space separation.
        /// </summary>
        /// <param name="snapshot">Snapshot.</param>
        /// <param name="text">Text.</param>
        public static void AppendText(ContentSnapshot snapshot, string? text)
        {
            string normalized = ContentSnapshot.Normalize(text);
            if (normalized.Length == 0) return;
            snapshot.AllText = snapshot.AllText.Length == 0 ? normalized : snapshot.AllText + " " + normalized;
        }
    }
}
