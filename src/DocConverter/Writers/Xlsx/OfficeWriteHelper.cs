namespace DocConverter.Writers.Xlsx
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using System.Xml;
    using DocConverter.Model;
    using DocConverter.Model.Serialization;
    using DocConverter.Options;
    using DocumentFormat.OpenXml.Packaging;

    /// <summary>
    /// Shared plumbing for the Office Open XML writers: core properties, deterministic packaging and link safety.
    /// </summary>
    internal static class OfficeWriteHelper
    {
        internal static readonly DateTime DeterministicTimestamp = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Write /docProps/core.xml directly (not through System.IO.Packaging's properties, which use random part names).
        /// </summary>
        internal static void WriteCoreProperties(OpenXmlPackage package, DocumentMetadata metadata, ConversionOptions options)
        {
            CoreFilePropertiesPart part;
            if (package is SpreadsheetDocument spreadsheet) part = spreadsheet.AddCoreFilePropertiesPart();
            else if (package is PresentationDocument presentation) part = presentation.AddCoreFilePropertiesPart();
            else if (package is WordprocessingDocument word) part = word.AddCoreFilePropertiesPart();
            else return;

            DateTime created = options.Deterministic ? DeterministicTimestamp : (metadata.CreatedUtc ?? DateTime.UtcNow);
            DateTime modified = options.Deterministic ? DeterministicTimestamp : (metadata.ModifiedUtc ?? DateTime.UtcNow);

            StringBuilder sb = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings { OmitXmlDeclaration = true };
            using (XmlWriter w = XmlWriter.Create(sb, settings))
            {
                const string cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
                const string dc = "http://purl.org/dc/elements/1.1/";
                const string dcterms = "http://purl.org/dc/terms/";
                const string xsi = "http://www.w3.org/2001/XMLSchema-instance";
                w.WriteStartElement("cp", "coreProperties", cp);
                w.WriteAttributeString("xmlns", "dc", null, dc);
                w.WriteAttributeString("xmlns", "dcterms", null, dcterms);
                w.WriteAttributeString("xmlns", "dcmitype", null, "http://purl.org/dc/dcmitype/");
                w.WriteAttributeString("xmlns", "xsi", null, xsi);
                Element(w, "dc", "title", dc, metadata.Title);
                Element(w, "dc", "subject", dc, metadata.Subject);
                Element(w, "dc", "creator", dc, metadata.Author);
                Element(w, "cp", "keywords", cp, metadata.Keywords);
                Element(w, "dc", "description", dc, metadata.Description);
                Element(w, "dc", "language", dc, metadata.Language);
                w.WriteStartElement("dcterms", "created", dcterms);
                w.WriteAttributeString("xsi", "type", xsi, "dcterms:W3CDTF");
                w.WriteString(created.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
                w.WriteEndElement();
                w.WriteStartElement("dcterms", "modified", dcterms);
                w.WriteAttributeString("xsi", "type", xsi, "dcterms:W3CDTF");
                w.WriteString(modified.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
                w.WriteEndElement();
                w.WriteEndElement();
            }

            byte[] bytes = new UTF8Encoding(false).GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + sb.ToString());
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                part.FeedData(ms);
            }
        }

        /// <summary>
        /// Copy a finished package into the output. When deterministic, the zip is rewritten with fixed entry timestamps so
        /// identical input yields identical bytes.
        /// </summary>
        internal static void CopyPackage(MemoryStream package, Stream output, bool deterministic)
        {
            package.Position = 0;
            if (!deterministic)
            {
                package.CopyTo(output);
                return;
            }

            using (ZipArchive source = new ZipArchive(package, ZipArchiveMode.Read, true))
            using (ZipArchive target = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                DateTimeOffset stamp = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                foreach (ZipArchiveEntry entry in source.Entries)
                {
                    ZipArchiveEntry copy = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    copy.LastWriteTime = stamp;
                    using (Stream from = entry.Open())
                    using (Stream to = copy.Open())
                    {
                        if (entry.FullName == "_rels/.rels")
                        {
                            byte[] rels = RenumberPackageRelationships(from);
                            to.Write(rels, 0, rels.Length);
                        }
                        else
                        {
                            from.CopyTo(to);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// True when a link URL uses a scheme that is safe to embed: http, https, mailto, relative or fragment.
        /// </summary>
        internal static bool IsSafeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            string trimmed = url.Trim();
            int colon = trimmed.IndexOf(':');
            int slash = trimmed.IndexOfAny(new char[] { '/', '?', '#' });
            if (colon < 0 || (slash >= 0 && slash < colon)) return true;
            string scheme = trimmed.Substring(0, colon).ToLowerInvariant();
            return scheme == "http" || scheme == "https" || scheme == "mailto";
        }

        /// <summary>
        /// Remove characters XML cannot hold.
        /// </summary>
        internal static string CleanText(string? text)
        {
            return CanonicalXml.Clean(text ?? "");
        }

        private static byte[] RenumberPackageRelationships(Stream relationships)
        {
            string xml;
            using (StreamReader reader = new StreamReader(relationships, Encoding.UTF8))
            {
                xml = reader.ReadToEnd();
            }

            int n = 0;
            string renumbered = System.Text.RegularExpressions.Regex.Replace(xml, "Id=\"[^\"]*\"", delegate (System.Text.RegularExpressions.Match m)
            {
                n++;
                return "Id=\"rIdPkg" + n.ToString(CultureInfo.InvariantCulture) + "\"";
            });
            return new UTF8Encoding(false).GetBytes(renumbered);
        }

        private static void Element(XmlWriter w, string prefix, string name, string ns, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            w.WriteStartElement(prefix, name, ns);
            w.WriteString(CanonicalXml.Clean(value!));
            w.WriteEndElement();
        }
    }
}
