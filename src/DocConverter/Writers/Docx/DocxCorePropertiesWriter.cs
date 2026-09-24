namespace DocConverter.Writers.Docx
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Xml;
    using DocConverter.Model;
    using DocConverter.Model.Serialization;

    /// <summary>
    /// Writes docProps/core.xml from document metadata. When deterministic, both timestamps are 2000-01-01T00:00:00Z.
    /// </summary>
    internal static class DocxCorePropertiesWriter
    {
        internal static readonly DateTime DeterministicTimestamp = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private const string _Cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
        private const string _Dc = "http://purl.org/dc/elements/1.1/";
        private const string _DcTerms = "http://purl.org/dc/terms/";
        private const string _Xsi = "http://www.w3.org/2001/XMLSchema-instance";

        internal static void Write(Stream stream, DocumentMetadata metadata, bool deterministic, bool includeMetadata)
        {
            DateTime now = DateTime.UtcNow;
            DateTime created = deterministic ? DeterministicTimestamp : (metadata.CreatedUtc ?? now);
            DateTime modified = deterministic ? DeterministicTimestamp : (metadata.ModifiedUtc ?? now);

            XmlWriterSettings settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false };
            using (XmlWriter w = XmlWriter.Create(stream, settings))
            {
                w.WriteStartDocument(true);
                w.WriteStartElement("cp", "coreProperties", _Cp);
                w.WriteAttributeString("xmlns", "dc", null, _Dc);
                w.WriteAttributeString("xmlns", "dcterms", null, _DcTerms);
                w.WriteAttributeString("xmlns", "xsi", null, _Xsi);
                if (includeMetadata)
                {
                    Element(w, "dc", "title", _Dc, metadata.Title);
                    Element(w, "dc", "subject", _Dc, metadata.Subject);
                    Element(w, "dc", "creator", _Dc, metadata.Author);
                    Element(w, "cp", "keywords", _Cp, metadata.Keywords);
                    Element(w, "dc", "description", _Dc, metadata.Description);
                    Element(w, "dc", "language", _Dc, metadata.Language);
                }

                Timestamp(w, "created", created);
                Timestamp(w, "modified", modified);
                w.WriteEndElement();
                w.WriteEndDocument();
            }
        }

        private static void Element(XmlWriter w, string prefix, string name, string ns, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            w.WriteStartElement(prefix, name, ns);
            w.WriteString(CanonicalXml.Clean(value!));
            w.WriteEndElement();
        }

        private static void Timestamp(XmlWriter w, string name, DateTime value)
        {
            DateTime utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            w.WriteStartElement("dcterms", name, _DcTerms);
            w.WriteAttributeString("xsi", "type", _Xsi, "dcterms:W3CDTF");
            w.WriteString(utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            w.WriteEndElement();
        }
    }
}
