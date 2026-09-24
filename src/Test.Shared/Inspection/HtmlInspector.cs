namespace Test.Shared.Inspection
{
    using System;
    using System.Collections.Generic;
    using HtmlAgilityPack;

    /// <summary>
    /// Inspects HTML output with HtmlAgilityPack directly. Fails when the output contains script, event handler
    /// attributes or javascript: URLs.
    /// </summary>
    public static class HtmlInspector
    {
        /// <summary>
        /// Parse, check safety, and snapshot HTML output.
        /// </summary>
        /// <param name="bytes">UTF-8 HTML.</param>
        /// <returns>Snapshot.</returns>
        public static ContentSnapshot Inspect(byte[] bytes)
        {
            string text = TextInspector.DecodeStrict(bytes);
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(text);
            CheckSafety(doc);

            ContentSnapshot snapshot = new ContentSnapshot();
            HtmlNode? title = doc.DocumentNode.SelectSingleNode("//title");
            if (title != null) snapshot.Title = HtmlEntity.DeEntitize(title.InnerText).Trim();

            foreach (HtmlNode node in doc.DocumentNode.Descendants())
            {
                if (node.NodeType != HtmlNodeType.Element) continue;
                string name = node.Name.ToLowerInvariant();
                if (name.Length == 2 && name[0] == 'h' && name[1] >= '1' && name[1] <= '6') snapshot.Headings.Add(Clean(node.InnerText));
                else if (name == "li") snapshot.ListItems.Add(Clean(FirstLine(node)));
                else if (name == "img" && node.GetAttributeValue("src", "").StartsWith("data:image/", StringComparison.Ordinal)) snapshot.ImageCount++;
                else if (name == "a") snapshot.LinkUrls.Add(HtmlEntity.DeEntitize(node.GetAttributeValue("href", "")));
                else if (name == "tr")
                {
                    List<string> cells = new List<string>();
                    foreach (HtmlNode cell in node.ChildNodes)
                        if (cell.Name == "td" || cell.Name == "th") cells.Add(Clean(cell.InnerText));
                    snapshot.TableRows.Add(cells);
                }
            }

            HtmlNode body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
            foreach (HtmlNode style in body.Descendants("style")) style.InnerHtml = "";
            snapshot.AllText = Clean(body.InnerText);
            return snapshot;
        }

        /// <summary>
        /// Throw when the document contains script elements, event handler attributes or javascript: URLs.
        /// </summary>
        /// <param name="doc">Parsed document.</param>
        public static void CheckSafety(HtmlDocument doc)
        {
            foreach (HtmlNode node in doc.DocumentNode.Descendants())
            {
                if (node.NodeType != HtmlNodeType.Element) continue;
                if (node.Name.Equals("script", StringComparison.OrdinalIgnoreCase) || node.Name.Equals("iframe", StringComparison.OrdinalIgnoreCase))
                    throw new TestAssertionException("HTML output contains a <" + node.Name + "> element.");
                foreach (HtmlAttribute attribute in node.Attributes)
                {
                    if (attribute.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                        throw new TestAssertionException("HTML output contains an event handler attribute '" + attribute.Name + "'.");
                    string value = HtmlEntity.DeEntitize(attribute.Value ?? "").Trim().ToLowerInvariant();
                    if ((attribute.Name == "href" || attribute.Name == "src") && (value.StartsWith("javascript:", StringComparison.Ordinal) || value.StartsWith("vbscript:", StringComparison.Ordinal)))
                        throw new TestAssertionException("HTML output contains an unsafe URL in '" + attribute.Name + "'.");
                }
            }
        }

        private static string FirstLine(HtmlNode li)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (HtmlNode child in li.ChildNodes)
            {
                if (child.Name == "ul" || child.Name == "ol") break;
                sb.Append(child.InnerText);
            }

            return sb.ToString();
        }

        private static string Clean(string text)
        {
            return ContentSnapshot.Normalize(HtmlEntity.DeEntitize(text ?? ""));
        }
    }
}
