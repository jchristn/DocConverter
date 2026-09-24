namespace DocConverter.Readers.Xlsx
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using System.Xml.Linq;
    using DocConverter.Detection;
    using DocConverter.Exceptions;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocumentFormat.OpenXml.Packaging;

    /// <summary>
    /// Shared plumbing for the Office Open XML readers: container checks, metadata and image resources.
    /// </summary>
    internal static class OfficeReadHelper
    {
        private static readonly XNamespace _Cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
        private static readonly XNamespace _Dc = "http://purl.org/dc/elements/1.1/";
        private static readonly XNamespace _DcTerms = "http://purl.org/dc/terms/";

        internal static void EnsureReadable(Stream input, string formatName, ConversionContext context)
        {
            long start = input.Position;
            byte[] head = new byte[4096];
            int total = 0;
            int read;
            while (total < head.Length && (read = input.Read(head, total, head.Length - total)) > 0) total += read;

            if (OleDirectoryInspector.IsOle(head, 0, total))
            {
                byte[] all = ReadAll(input, start);
                string description = OleDirectoryInspector.Describe(all, 0, all.Length);
                input.Position = start;
                if (OleDirectoryInspector.IsEncryptedOoxml(all, 0, all.Length))
                    throw new DocumentReadException("The " + formatName + " file is password protected (encrypted) and cannot be read. Remove the password and try again.");
                throw new DocumentReadException("The input is a " + description + ", not a " + formatName + " file.");
            }

            input.Position = start;
            ZipSafety.EnsureSafe(input, context.MaxDecompressedBytes, formatName);
            input.Position = start;
        }

        internal static DocumentReadException Wrap(Exception ex, string formatName)
        {
            return new DocumentReadException("The " + formatName + " file could not be opened: " + ex.Message, ex);
        }

        internal static void ReadMetadata(OpenXmlPackage package, DocumentModel document)
        {
            try
            {
                CoreFilePropertiesPart? core = package.GetPartsOfType<CoreFilePropertiesPart>().FirstOrDefault();
                if (core == null) return;
                XDocument xml;
                using (Stream s = core.GetStream(FileMode.Open, FileAccess.Read))
                using (XmlReader reader = XmlReader.Create(s, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                {
                    xml = XDocument.Load(reader);
                }

                if (xml.Root == null) return;
                document.Metadata.Title = Value(xml.Root, _Dc + "title");
                document.Metadata.Subject = Value(xml.Root, _Dc + "subject");
                document.Metadata.Author = Value(xml.Root, _Dc + "creator");
                document.Metadata.Keywords = Value(xml.Root, _Cp + "keywords");
                document.Metadata.Description = Value(xml.Root, _Dc + "description");
                document.Metadata.Language = Value(xml.Root, _Dc + "language");
                document.Metadata.CreatedUtc = Date(Value(xml.Root, _DcTerms + "created"));
                document.Metadata.ModifiedUtc = Date(Value(xml.Root, _DcTerms + "modified"));
            }
            catch (Exception)
            {
                // Metadata is best effort; a malformed core properties part must not fail the read.
            }
        }

        internal static string? AddImage(DocumentModel document, OpenXmlPart part, Dictionary<string, string> seen)
        {
            string key = part.Uri.ToString();
            if (seen.TryGetValue(key, out string? existing)) return existing;

            byte[] data;
            using (Stream s = part.GetStream(FileMode.Open, FileAccess.Read))
            using (MemoryStream ms = new MemoryStream())
            {
                s.CopyTo(ms);
                data = ms.ToArray();
            }

            if (data.Length == 0) return null;
            ImageInfo? info = ImageHeaderReader.Read(data);
            string fileName = Path.GetFileName(key);
            BinaryResource resource = new BinaryResource
            {
                MediaType = info != null ? info.MediaType : part.ContentType,
                Data = data,
                FileName = fileName,
                PixelWidth = info?.Width,
                PixelHeight = info?.Height
            };

            string id = document.AddResource(resource);
            seen[key] = id;
            return id;
        }

        private static byte[] ReadAll(Stream input, long start)
        {
            input.Position = start;
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static string? Value(XElement root, XName name)
        {
            XElement? e = root.Element(name);
            if (e == null) return null;
            string trimmed = e.Value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static DateTime? Date(string? value)
        {
            if (value == null) return null;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed)) return parsed;
            return null;
        }
    }
}
