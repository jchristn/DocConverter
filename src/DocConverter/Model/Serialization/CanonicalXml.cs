namespace DocConverter.Model.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;
    using DocConverter.Exceptions;

    /// <summary>
    /// Serializes the canonical document form to and from XML. The root element is &lt;docconverter version="1"&gt;.
    /// Reading prohibits DTDs and external entities.
    /// </summary>
    internal static class CanonicalXml
    {
        internal const string RootName = "docconverter";

        internal static string Serialize(CanonicalDocument dto, bool indented, string encodingName)
        {
            StringBuilder sb = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = indented,
                IndentChars = "  ",
                OmitXmlDeclaration = true,
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Entitize
            };

            sb.Append("<?xml version=\"1.0\" encoding=\"").Append(encodingName).Append("\"?>");
            if (indented) sb.Append('\n');
            using (XmlWriter w = XmlWriter.Create(sb, settings))
            {
                w.WriteStartElement(RootName);
                w.WriteAttributeString("version", dto.DocConverter);
                if (dto.Metadata != null) WriteMetadata(w, dto.Metadata);
                w.WriteStartElement("blocks");
                foreach (CanonicalBlock block in dto.Blocks) WriteBlock(w, block);
                w.WriteEndElement();
                w.WriteStartElement("resources");
                foreach (CanonicalResource r in dto.Resources)
                {
                    w.WriteStartElement("resource");
                    w.WriteAttributeString("id", Clean(r.Id));
                    w.WriteAttributeString("mediaType", Clean(r.MediaType));
                    Attr(w, "fileName", r.FileName);
                    Attr(w, "pixelWidth", r.PixelWidth);
                    Attr(w, "pixelHeight", r.PixelHeight);
                    w.WriteAttributeString("size", r.Size.ToString(CultureInfo.InvariantCulture));
                    if (r.Data != null) w.WriteString(r.Data);
                    w.WriteEndElement();
                }

                w.WriteEndElement();
                w.WriteEndElement();
            }

            if (indented) sb.Append('\n');
            return sb.ToString();
        }

        internal static bool IsCanonical(string text)
        {
            return RootElementName(text) == RootName;
        }

        internal static string? RootElementName(string text)
        {
            try
            {
                using (StringReader sr = new StringReader(text))
                using (XmlReader reader = XmlReader.Create(sr, SafeSettings()))
                {
                    while (reader.Read())
                        if (reader.NodeType == XmlNodeType.Element) return reader.LocalName;
                }
            }
            catch (XmlException)
            {
                return null;
            }

            return null;
        }

        internal static XmlReaderSettings SafeSettings()
        {
            return new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersFromEntities = 1024,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            };
        }

        internal static CanonicalDocument Deserialize(string text)
        {
            XDocument doc;
            try
            {
                using (StringReader sr = new StringReader(text))
                using (XmlReader reader = XmlReader.Create(sr, SafeSettings()))
                {
                    doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
                }
            }
            catch (XmlException ex)
            {
                throw new DocumentReadException("The canonical XML document is malformed: " + ex.Message, ex);
            }

            XElement? root = doc.Root;
            if (root == null || root.Name.LocalName != RootName) throw new DocumentReadException("The XML document is not a canonical DocConverter document.");
            string version = (string?)root.Attribute("version") ?? "";
            if (version != "1") throw new DocumentReadException("Unsupported canonical document version '" + version + "'. This release reads version 1.");

            CanonicalDocument dto = new CanonicalDocument();
            XElement? metadata = root.Element("metadata");
            if (metadata != null)
            {
                dto.Metadata = new CanonicalMetadata
                {
                    Title = Child(metadata, "title"),
                    Subject = Child(metadata, "subject"),
                    Author = Child(metadata, "author"),
                    Keywords = Child(metadata, "keywords"),
                    Description = Child(metadata, "description"),
                    Language = Child(metadata, "language"),
                    Created = ParseDate(Child(metadata, "created")),
                    Modified = ParseDate(Child(metadata, "modified"))
                };
            }

            dto.Blocks = ReadBlocks(root.Element("blocks"));
            XElement? resources = root.Element("resources");
            if (resources != null)
            {
                foreach (XElement r in resources.Elements("resource"))
                {
                    string data = r.Value.Trim();
                    dto.Resources.Add(new CanonicalResource
                    {
                        Id = (string?)r.Attribute("id") ?? "",
                        MediaType = (string?)r.Attribute("mediaType") ?? "application/octet-stream",
                        FileName = (string?)r.Attribute("fileName"),
                        PixelWidth = IntAttr(r, "pixelWidth"),
                        PixelHeight = IntAttr(r, "pixelHeight"),
                        Size = LongAttr(r, "size") ?? 0,
                        Data = data.Length > 0 ? data : null
                    });
                }
            }

            return dto;
        }

        private static void WriteMetadata(XmlWriter w, CanonicalMetadata m)
        {
            w.WriteStartElement("metadata");
            Element(w, "title", m.Title);
            Element(w, "subject", m.Subject);
            Element(w, "author", m.Author);
            Element(w, "keywords", m.Keywords);
            Element(w, "description", m.Description);
            Element(w, "language", m.Language);
            if (m.Created.HasValue) Element(w, "created", m.Created.Value.ToString("o", CultureInfo.InvariantCulture));
            if (m.Modified.HasValue) Element(w, "modified", m.Modified.Value.ToString("o", CultureInfo.InvariantCulture));
            w.WriteEndElement();
        }

        private static void WriteBlock(XmlWriter w, CanonicalBlock b)
        {
            w.WriteStartElement("block");
            w.WriteAttributeString("type", b.Type);
            Attr(w, "id", b.Id);
            Attr(w, "sourcePage", b.SourcePage);
            Attr(w, "sourceSheet", b.SourceSheet);
            Attr(w, "sourceSlide", b.SourceSlide);
            Attr(w, "level", b.Level);
            Attr(w, "alignment", b.Alignment);
            Attr(w, "kind", b.Kind);
            Attr(w, "title", b.Title);
            Attr(w, "start", b.Start);
            Attr(w, "headerRowCount", b.HeaderRowCount);
            Attr(w, "caption", b.Caption);
            if (b.ColumnAlignments != null) Attr(w, "columnAlignments", string.Join(",", b.ColumnAlignments));
            Attr(w, "language", b.Language);
            Attr(w, "resourceId", b.ResourceId);
            Attr(w, "altText", b.AltText);
            if (b.Width.HasValue) Attr(w, "width", b.Width.Value.ToString("R", CultureInfo.InvariantCulture));
            if (b.Height.HasValue) Attr(w, "height", b.Height.Value.ToString("R", CultureInfo.InvariantCulture));

            if (b.Inlines != null) WriteInlines(w, b.Inlines);
            if (b.Text != null)
            {
                w.WriteStartElement("text");
                w.WriteString(Clean(b.Text));
                w.WriteEndElement();
            }

            if (b.Blocks != null)
            {
                w.WriteStartElement("blocks");
                foreach (CanonicalBlock child in b.Blocks) WriteBlock(w, child);
                w.WriteEndElement();
            }

            if (b.Items != null)
            {
                w.WriteStartElement("items");
                foreach (CanonicalListItem item in b.Items)
                {
                    w.WriteStartElement("item");
                    if (item.Checked.HasValue) w.WriteAttributeString("checked", item.Checked.Value ? "true" : "false");
                    w.WriteStartElement("blocks");
                    foreach (CanonicalBlock child in item.Blocks) WriteBlock(w, child);
                    w.WriteEndElement();
                    w.WriteEndElement();
                }

                w.WriteEndElement();
            }

            if (b.Rows != null)
            {
                w.WriteStartElement("rows");
                foreach (CanonicalTableRow row in b.Rows)
                {
                    w.WriteStartElement("row");
                    foreach (CanonicalTableCell cell in row.Cells)
                    {
                        w.WriteStartElement("cell");
                        Attr(w, "colSpan", cell.ColumnSpan);
                        Attr(w, "rowSpan", cell.RowSpan);
                        if (cell.IsHeader.HasValue) w.WriteAttributeString("header", cell.IsHeader.Value ? "true" : "false");
                        w.WriteStartElement("blocks");
                        foreach (CanonicalBlock child in cell.Blocks) WriteBlock(w, child);
                        w.WriteEndElement();
                        w.WriteEndElement();
                    }

                    w.WriteEndElement();
                }

                w.WriteEndElement();
            }

            w.WriteEndElement();
        }

        private static void WriteInlines(XmlWriter w, List<CanonicalInline> inlines)
        {
            w.WriteStartElement("inlines");
            foreach (CanonicalInline i in inlines)
            {
                w.WriteStartElement("inline");
                w.WriteAttributeString("type", i.Type);
                if (i.Style != null && i.Style.Count > 0) w.WriteAttributeString("style", string.Join(",", i.Style));
                Attr(w, "url", i.Url);
                Attr(w, "title", i.Title);
                Attr(w, "resourceId", i.ResourceId);
                Attr(w, "altText", i.AltText);
                if (i.Inlines != null) WriteInlines(w, i.Inlines);
                else if (i.Text != null)
                {
                    w.WriteAttributeString("xml", "space", null, "preserve");
                    w.WriteString(Clean(i.Text));
                }

                w.WriteEndElement();
            }

            w.WriteEndElement();
        }

        private static List<CanonicalBlock> ReadBlocks(XElement? container)
        {
            List<CanonicalBlock> list = new List<CanonicalBlock>();
            if (container == null) return list;
            foreach (XElement e in container.Elements("block")) list.Add(ReadBlock(e));
            return list;
        }

        private static CanonicalBlock ReadBlock(XElement e)
        {
            CanonicalBlock b = new CanonicalBlock
            {
                Type = (string?)e.Attribute("type") ?? "",
                Id = (string?)e.Attribute("id"),
                SourcePage = IntAttr(e, "sourcePage"),
                SourceSheet = (string?)e.Attribute("sourceSheet"),
                SourceSlide = IntAttr(e, "sourceSlide"),
                Level = IntAttr(e, "level"),
                Alignment = (string?)e.Attribute("alignment"),
                Kind = (string?)e.Attribute("kind"),
                Title = (string?)e.Attribute("title"),
                Start = IntAttr(e, "start"),
                HeaderRowCount = IntAttr(e, "headerRowCount"),
                Caption = (string?)e.Attribute("caption"),
                Language = (string?)e.Attribute("language"),
                ResourceId = (string?)e.Attribute("resourceId"),
                AltText = (string?)e.Attribute("altText"),
                Width = DoubleAttr(e, "width"),
                Height = DoubleAttr(e, "height")
            };

            string? aligns = (string?)e.Attribute("columnAlignments");
            if (aligns != null) b.ColumnAlignments = new List<string>(aligns.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

            XElement? inlines = e.Element("inlines");
            if (inlines != null) b.Inlines = ReadInlines(inlines);
            XElement? text = e.Element("text");
            if (text != null) b.Text = text.Value;
            XElement? blocks = e.Element("blocks");
            if (blocks != null) b.Blocks = ReadBlocks(blocks);

            XElement? items = e.Element("items");
            if (items != null)
            {
                b.Items = new List<CanonicalListItem>();
                foreach (XElement item in items.Elements("item"))
                {
                    string? c = (string?)item.Attribute("checked");
                    b.Items.Add(new CanonicalListItem
                    {
                        Blocks = ReadBlocks(item.Element("blocks")),
                        Checked = c == null ? (bool?)null : c == "true"
                    });
                }
            }

            XElement? rows = e.Element("rows");
            if (rows != null)
            {
                b.Rows = new List<CanonicalTableRow>();
                foreach (XElement row in rows.Elements("row"))
                {
                    CanonicalTableRow r = new CanonicalTableRow();
                    foreach (XElement cell in row.Elements("cell"))
                    {
                        string? h = (string?)cell.Attribute("header");
                        r.Cells.Add(new CanonicalTableCell
                        {
                            Blocks = ReadBlocks(cell.Element("blocks")),
                            ColumnSpan = IntAttr(cell, "colSpan"),
                            RowSpan = IntAttr(cell, "rowSpan"),
                            IsHeader = h == null ? (bool?)null : h == "true"
                        });
                    }

                    b.Rows.Add(r);
                }
            }

            return b;
        }

        private static List<CanonicalInline> ReadInlines(XElement container)
        {
            List<CanonicalInline> list = new List<CanonicalInline>();
            foreach (XElement e in container.Elements("inline"))
            {
                CanonicalInline i = new CanonicalInline
                {
                    Type = (string?)e.Attribute("type") ?? "",
                    Url = (string?)e.Attribute("url"),
                    Title = (string?)e.Attribute("title"),
                    ResourceId = (string?)e.Attribute("resourceId"),
                    AltText = (string?)e.Attribute("altText")
                };

                string? style = (string?)e.Attribute("style");
                if (style != null) i.Style = new List<string>(style.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                XElement? children = e.Element("inlines");
                if (children != null) i.Inlines = ReadInlines(children);
                else if (i.Type == "text") i.Text = e.Value;
                list.Add(i);
            }

            return list;
        }

        private static void Element(XmlWriter w, string name, string? value)
        {
            if (value == null) return;
            w.WriteStartElement(name);
            w.WriteString(Clean(value));
            w.WriteEndElement();
        }

        private static void Attr(XmlWriter w, string name, string? value)
        {
            if (value == null) return;
            w.WriteAttributeString(name, Clean(value));
        }

        private static void Attr(XmlWriter w, string name, int? value)
        {
            if (!value.HasValue) return;
            w.WriteAttributeString(name, value.Value.ToString(CultureInfo.InvariantCulture));
        }

        private static string? Child(XElement parent, string name)
        {
            XElement? e = parent.Element(name);
            return e?.Value;
        }

        private static int? IntAttr(XElement e, string name)
        {
            string? v = (string?)e.Attribute(name);
            if (v == null) return null;
            if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) return parsed;
            throw new DocumentReadException("Attribute '" + name + "' has invalid integer value '" + v + "'.");
        }

        private static long? LongAttr(XElement e, string name)
        {
            string? v = (string?)e.Attribute(name);
            if (v == null) return null;
            if (long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)) return parsed;
            throw new DocumentReadException("Attribute '" + name + "' has invalid integer value '" + v + "'.");
        }

        private static double? DoubleAttr(XElement e, string name)
        {
            string? v = (string?)e.Attribute(name);
            if (v == null) return null;
            if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)) return parsed;
            throw new DocumentReadException("Attribute '" + name + "' has invalid number value '" + v + "'.");
        }

        private static DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed)) return parsed;
            return null;
        }

        /// <summary>
        /// Remove characters XML 1.0 cannot represent (most C0 controls and unpaired surrogates).
        /// </summary>
        internal static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? "";
            StringBuilder? sb = null;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool ok;
                if (char.IsHighSurrogate(c))
                {
                    ok = i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]);
                    if (ok)
                    {
                        if (sb != null) sb.Append(c).Append(value[i + 1]);
                        i++;
                        continue;
                    }
                }
                else if (char.IsLowSurrogate(c))
                {
                    ok = false;
                }
                else
                {
                    ok = c == '\t' || c == '\n' || c == '\r' || (c >= 0x20 && c <= 0xD7FF) || (c >= 0xE000 && c <= 0xFFFD);
                }

                if (!ok)
                {
                    if (sb == null)
                    {
                        sb = new StringBuilder(value.Length);
                        sb.Append(value, 0, i);
                    }

                    continue;
                }

                if (sb != null) sb.Append(c);
            }

            return sb == null ? value : sb.ToString();
        }
    }
}
