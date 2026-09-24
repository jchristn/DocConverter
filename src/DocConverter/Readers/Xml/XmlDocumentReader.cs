namespace DocConverter.Readers.Xml
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Model.Serialization;
    using DocConverter.Options;

    /// <summary>
    /// Reads XML. A &lt;docconverter version="1"&gt; root is the canonical document form and is read losslessly. Any
    /// other XML is mapped structurally: each element becomes a section titled with its name; leaf children and
    /// attributes become a key/value table; repeated record-like children become a table with one column per field.
    /// DTDs and external entities are prohibited. Stateless and thread safe.
    /// </summary>
    public sealed class XmlDocumentReader : IDocumentReader
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Xml };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <inheritdoc />
        public async Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            string text = (await TextIO.ReadAllTextAsync(input, options, context, token).ConfigureAwait(false)).TrimStart('﻿');
            token.ThrowIfCancellationRequested();
            if (text.Trim().Length == 0) return new DocumentModel();

            if (CanonicalXml.IsCanonical(text))
                return CanonicalMapper.FromDto(CanonicalXml.Deserialize(text));

            XDocument xml;
            try
            {
                using (StringReader sr = new StringReader(text))
                using (XmlReader reader = XmlReader.Create(sr, CanonicalXml.SafeSettings()))
                {
                    xml = XDocument.Load(reader);
                }
            }
            catch (XmlException ex)
            {
                throw new DocumentReadException("The XML input is malformed or uses a prohibited DTD: " + ex.Message, ex);
            }

            DocumentModel document = new DocumentModel();
            if (xml.Root != null) MapElement(xml.Root, document.Blocks, 0, options.Xml.IncludeAttributes, context, token);
            return document;
        }

        private static void MapElement(XElement element, List<Block> target, int depth, bool includeAttributes, ConversionContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (depth >= context.MaxNestingDepth)
            {
                context.AddWarning(WarningCodeEnum.NestedDepthLimited, "XML nested deeper than " + context.MaxNestingDepth + " levels was written as text.");
                target.Add(new ParagraphBlock(element.Name.LocalName + ": " + Collapse(element.Value)));
                return;
            }

            SectionBlock section = new SectionBlock(SectionKindEnum.Generic, element.Name.LocalName);
            target.Add(section);

            TableBlock? pairs = null;
            if (includeAttributes)
            {
                foreach (XAttribute attribute in element.Attributes())
                {
                    if (attribute.IsNamespaceDeclaration) continue;
                    pairs = AddPair(section, pairs, "@" + attribute.Name.LocalName, attribute.Value);
                }
            }

            foreach (XNode node in element.Nodes())
            {
                if (node is XText textNode)
                {
                    string value = Collapse(textNode.Value);
                    if (value.Length > 0) section.Blocks.Add(new ParagraphBlock(value));
                }
            }

            List<XElement> children = element.Elements().ToList();
            HashSet<string> handled = new HashSet<string>(StringComparer.Ordinal);
            foreach (XElement child in children)
            {
                string name = child.Name.LocalName;
                if (handled.Contains(name)) continue;

                List<XElement> siblings = children.Where(c => c.Name.LocalName == name).ToList();
                if (IsLeaf(child, includeAttributes) && siblings.Count == 1)
                {
                    pairs = AddPair(section, pairs, name, Collapse(child.Value));
                    handled.Add(name);
                    continue;
                }

                if (siblings.Count > 1 && siblings.All(s => IsRecord(s, includeAttributes)))
                {
                    section.Blocks.Add(RecordTable(siblings, includeAttributes));
                    handled.Add(name);
                    continue;
                }

                if (siblings.Count > 1 && siblings.All(s => IsLeaf(s, includeAttributes)))
                {
                    ListBlock list = new ListBlock(ListKindEnum.Unordered);
                    foreach (XElement s in siblings) list.Items.Add(new ListItemBlock(Collapse(s.Value)));
                    SectionBlock listSection = new SectionBlock(SectionKindEnum.Generic, name);
                    listSection.Blocks.Add(list);
                    section.Blocks.Add(listSection);
                    handled.Add(name);
                    continue;
                }

                MapElement(child, section.Blocks, depth + 1, includeAttributes, context, token);
            }
        }

        private static TableBlock AddPair(SectionBlock section, TableBlock? pairs, string key, string value)
        {
            if (pairs == null)
            {
                pairs = new TableBlock();
                pairs.HeaderRowCount = 1;
                TableRow header = new TableRow(new string[] { "Key", "Value" });
                foreach (TableCell cell in header.Cells) cell.IsHeader = true;
                pairs.Rows.Add(header);
                section.Blocks.Add(pairs);
            }

            pairs.Rows.Add(new TableRow(new string[] { key, value }));
            return pairs;
        }

        private static bool IsLeaf(XElement element, bool includeAttributes)
        {
            return !element.HasElements && (!includeAttributes || !element.Attributes().Any(a => !a.IsNamespaceDeclaration));
        }

        private static bool IsRecord(XElement element, bool includeAttributes)
        {
            if (!element.HasElements && !(includeAttributes && element.HasAttributes)) return false;
            foreach (XElement child in element.Elements())
                if (child.HasElements) return false;
            return true;
        }

        private static TableBlock RecordTable(List<XElement> records, bool includeAttributes)
        {
            List<string> columns = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (XElement record in records)
            {
                if (includeAttributes)
                    foreach (XAttribute a in record.Attributes())
                        if (!a.IsNamespaceDeclaration && seen.Add("@" + a.Name.LocalName)) columns.Add("@" + a.Name.LocalName);
                foreach (XElement field in record.Elements())
                    if (seen.Add(field.Name.LocalName)) columns.Add(field.Name.LocalName);
            }

            TableBlock table = new TableBlock();
            table.HeaderRowCount = 1;
            TableRow header = new TableRow(columns);
            foreach (TableCell cell in header.Cells) cell.IsHeader = true;
            table.Rows.Add(header);
            foreach (XElement record in records)
            {
                TableRow row = new TableRow();
                foreach (string column in columns)
                {
                    string value = "";
                    if (column.StartsWith("@", StringComparison.Ordinal))
                    {
                        XAttribute? a = record.Attributes().FirstOrDefault(x => x.Name.LocalName == column.Substring(1));
                        if (a != null) value = a.Value;
                    }
                    else
                    {
                        XElement? field = record.Elements().FirstOrDefault(x => x.Name.LocalName == column);
                        if (field != null) value = Collapse(field.Value);
                    }

                    row.Cells.Add(new TableCell(value));
                }

                table.Rows.Add(row);
            }

            return table;
        }

        private static string Collapse(string value)
        {
            return string.Join(" ", value.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
