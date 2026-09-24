namespace Test.Shared.Inspection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Xml.Linq;
    using DocConverter.Model.Serialization;

    /// <summary>
    /// Inspects canonical JSON and XML output. JSON is strictly deserialized into the published contract types with
    /// System.Text.Json directly; XML is walked with LINQ to XML. Neither goes through DocConverter's readers.
    /// </summary>
    public static class CanonicalInspector
    {
        /// <summary>
        /// Snapshot canonical JSON output.
        /// </summary>
        /// <param name="bytes">UTF-8 JSON.</param>
        /// <returns>Snapshot.</returns>
        public static ContentSnapshot InspectJson(byte[] bytes)
        {
            string text = TextInspector.DecodeStrict(bytes);
            CanonicalDocument? doc;
            try
            {
                doc = JsonSerializer.Deserialize<CanonicalDocument>(text, new JsonSerializerOptions { MaxDepth = 1024 });
            }
            catch (JsonException ex)
            {
                throw new TestAssertionException("Output is not valid canonical JSON: " + ex.Message, ex);
            }

            if (doc == null || doc.DocConverter != "1") throw new TestAssertionException("Output lacks the docconverter version marker.");
            ContentSnapshot snapshot = new ContentSnapshot();
            snapshot.Title = doc.Metadata?.Title;
            StringBuilder all = new StringBuilder();
            WalkBlocks(doc.Blocks, snapshot, all);
            snapshot.ImageCount = doc.Resources.Count(r => r.MediaType.StartsWith("image/", StringComparison.Ordinal) && !string.IsNullOrEmpty(r.Data)) > 0
                ? CountImageBlocks(doc.Blocks)
                : 0;
            snapshot.AllText = ContentSnapshot.Normalize(all.ToString());
            return snapshot;
        }

        /// <summary>
        /// Snapshot canonical XML output.
        /// </summary>
        /// <param name="bytes">XML bytes.</param>
        /// <returns>Snapshot.</returns>
        public static ContentSnapshot InspectXml(byte[] bytes)
        {
            string text = TextInspector.DecodeStrict(bytes);
            XDocument doc;
            try
            {
                doc = XDocument.Parse(text);
            }
            catch (System.Xml.XmlException ex)
            {
                throw new TestAssertionException("Output is not well formed XML: " + ex.Message, ex);
            }

            XElement root = doc.Root ?? throw new TestAssertionException("XML output has no root.");
            if (root.Name.LocalName != "docconverter" || (string?)root.Attribute("version") != "1") throw new TestAssertionException("XML output lacks the docconverter root and version.");

            ContentSnapshot snapshot = new ContentSnapshot();
            snapshot.Title = root.Element("metadata")?.Element("title")?.Value;
            StringBuilder all = new StringBuilder();
            foreach (XElement block in root.Descendants("block"))
            {
                string type = (string?)block.Attribute("type") ?? "";
                string own = string.Concat(block.Elements("inlines").Descendants("inline").Where(i => (string?)i.Attribute("type") == "text").Select(i => i.Value));
                if (type == "heading") snapshot.Headings.Add(ContentSnapshot.Normalize(own));
                else if (type == "image" && root.Element("resources")?.Elements("resource").Any(r => r.Value.Trim().Length > 0) == true) snapshot.ImageCount++;
                else if (type == "table")
                {
                    foreach (XElement row in block.Element("rows")?.Elements("row") ?? Enumerable.Empty<XElement>())
                        snapshot.TableRows.Add(row.Elements("cell").Select(c => ContentSnapshot.Normalize(string.Concat(c.Descendants("inline").Select(i => i.Value)))).ToList());
                }
                else if (type == "list")
                {
                    foreach (XElement item in block.Element("items")?.Elements("item") ?? Enumerable.Empty<XElement>())
                    {
                        XElement? first = item.Element("blocks")?.Elements("block").FirstOrDefault();
                        if (first != null) snapshot.ListItems.Add(ContentSnapshot.Normalize(string.Concat(first.Elements("inlines").Descendants("inline").Select(i => i.Value))));
                    }
                }

                if (own.Length > 0) all.Append(' ').Append(own);
                XElement? code = block.Element("text");
                if (code != null) all.Append(' ').Append(code.Value);
                string? title = (string?)block.Attribute("title");
                if (title != null) all.Append(' ').Append(title);
            }

            foreach (XElement link in root.Descendants("inline").Where(i => (string?)i.Attribute("type") == "link")) snapshot.LinkUrls.Add((string?)link.Attribute("url") ?? "");
            snapshot.AllText = ContentSnapshot.Normalize(all.ToString());
            return snapshot;
        }

        private static void WalkBlocks(List<CanonicalBlock>? blocks, ContentSnapshot snapshot, StringBuilder all)
        {
            if (blocks == null) return;
            foreach (CanonicalBlock block in blocks)
            {
                string own = Inlines(block.Inlines, snapshot);
                if (block.Type == "heading") snapshot.Headings.Add(ContentSnapshot.Normalize(own));
                if (own.Length > 0) all.Append(' ').Append(own);
                if (block.Text != null) all.Append(' ').Append(block.Text);
                if (block.Title != null) all.Append(' ').Append(block.Title);
                WalkBlocks(block.Blocks, snapshot, all);
                if (block.Items != null)
                {
                    foreach (CanonicalListItem item in block.Items)
                    {
                        if (item.Blocks.Count > 0) snapshot.ListItems.Add(ContentSnapshot.Normalize(Inlines(item.Blocks[0].Inlines, new ContentSnapshot())));
                        WalkBlocks(item.Blocks, snapshot, all);
                    }
                }

                if (block.Rows != null)
                {
                    foreach (CanonicalTableRow row in block.Rows)
                    {
                        List<string> cells = new List<string>();
                        foreach (CanonicalTableCell cell in row.Cells)
                        {
                            StringBuilder cellText = new StringBuilder();
                            WalkBlocks(cell.Blocks, new ContentSnapshot(), cellText);
                            cells.Add(ContentSnapshot.Normalize(cellText.ToString()));
                            all.Append(' ').Append(cellText);
                        }

                        snapshot.TableRows.Add(cells);
                    }
                }
            }
        }

        private static string Inlines(List<CanonicalInline>? inlines, ContentSnapshot snapshot)
        {
            if (inlines == null) return "";
            StringBuilder sb = new StringBuilder();
            foreach (CanonicalInline inline in inlines)
            {
                if (inline.Type == "text") sb.Append(inline.Text);
                else if (inline.Type == "link")
                {
                    snapshot.LinkUrls.Add(inline.Url ?? "");
                    sb.Append(Inlines(inline.Inlines, snapshot));
                }
                else if (inline.Type == "lineBreak") sb.Append(' ');
            }

            return sb.ToString();
        }

        private static int CountImageBlocks(List<CanonicalBlock>? blocks)
        {
            if (blocks == null) return 0;
            int count = 0;
            foreach (CanonicalBlock block in blocks)
            {
                if (block.Type == "image") count++;
                count += CountImageBlocks(block.Blocks);
                if (block.Items != null) foreach (CanonicalListItem item in block.Items) count += CountImageBlocks(item.Blocks);
                if (block.Rows != null) foreach (CanonicalTableRow row in block.Rows) foreach (CanonicalTableCell cell in row.Cells) count += CountImageBlocks(cell.Blocks);
                if (block.Inlines != null) count += block.Inlines.Count(i => i.Type == "image");
            }

            return count;
        }
    }
}
