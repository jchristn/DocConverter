namespace DocConverter.Readers.Docx
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Xml;
    using System.Xml.Linq;
    using DocConverter.Model;

    /// <summary>
    /// Reads Office core properties (docProps/core.xml) into document metadata with DTDs prohibited.
    /// </summary>
    internal static class DocxCoreProperties
    {
        private const string _Dc = "http://purl.org/dc/elements/1.1/";
        private const string _Cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
        private const string _DcTerms = "http://purl.org/dc/terms/";

        internal static void Read(Stream stream, DocumentMetadata metadata)
        {
            XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            XDocument doc;
            using (XmlReader reader = XmlReader.Create(stream, settings))
            {
                doc = XDocument.Load(reader);
            }

            XElement? root = doc.Root;
            if (root == null) return;
            metadata.Title = Value(root, _Dc, "title");
            metadata.Subject = Value(root, _Dc, "subject");
            metadata.Author = Value(root, _Dc, "creator");
            metadata.Keywords = Value(root, _Cp, "keywords");
            metadata.Description = Value(root, _Dc, "description");
            metadata.Language = Value(root, _Dc, "language");
            metadata.CreatedUtc = Date(Value(root, _DcTerms, "created"));
            metadata.ModifiedUtc = Date(Value(root, _DcTerms, "modified"));
        }

        private static string? Value(XElement root, string ns, string name)
        {
            XElement? e = root.Element(XName.Get(name, ns));
            if (e == null) return null;
            string v = e.Value.Trim();
            return v.Length == 0 ? null : v;
        }

        private static DateTime? Date(string? value)
        {
            if (value == null) return null;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed))
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return null;
        }
    }
}
