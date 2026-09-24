namespace DocConverter.Writers.Docx
{
    using System.Collections.Generic;
    using System.Text;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Model.Serialization;
    using DocConverter.Readers.Docx;
    using DocumentFormat.OpenXml;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Writes inline content (styled runs, hyperlinks, images, line breaks) into a paragraph.
    /// </summary>
    internal sealed class DocxInlineWriter
    {
        private readonly DocxWriteSession _Session;

        internal DocxInlineWriter(DocxWriteSession session)
        {
            _Session = session;
        }

        internal void Write(IEnumerable<Inline> inlines, OpenXmlCompositeElement parent, InlineStyleEnum inherited, bool inLink)
        {
            foreach (Inline inline in inlines)
            {
                switch (inline)
                {
                    case TextInline text:
                        AppendText(parent, text.Text, text.Style | inherited, inLink);
                        break;
                    case LinkInline link:
                        WriteLink(link, parent, inherited, inLink);
                        break;
                    case ImageInline image:
                        WriteImage(parent, image.ResourceId, image.AltText, null, null);
                        break;
                    case LineBreakInline _:
                        parent.Append(new W.Run(new W.Break()));
                        break;
                }
            }
        }

        internal void WriteImage(OpenXmlCompositeElement parent, string resourceId, string? altText, double? width, double? height)
        {
            string? relId = _Session.ImageRelationship(resourceId, out BinaryResource? resource);
            if (relId == null || resource == null)
            {
                string label = "[Image" + (string.IsNullOrEmpty(altText) ? "" : ": " + altText) + "]";
                AppendText(parent, label, InlineStyleEnum.None, false);
                return;
            }

            W.Drawing drawing = DocxDrawingFactory.Create(relId, resource, altText, width, height, _Session.NextDrawingId(), _Session.Page.ContentWidthEmu);
            parent.Append(new W.Run(drawing));
        }

        internal static void AppendText(OpenXmlCompositeElement parent, string text, InlineStyleEnum style, bool inLink)
        {
            if (string.IsNullOrEmpty(text)) return;
            string clean = CanonicalXml.Clean(text).Replace("\r\n", "\n").Replace('\r', '\n');
            W.Run run = new W.Run();
            W.RunProperties? rPr = RunProperties(style, inLink);
            if (rPr != null) run.Append(rPr);

            StringBuilder pending = new StringBuilder();
            foreach (char c in clean)
            {
                if (c == '\n' || c == '\t')
                {
                    FlushText(run, pending);
                    if (c == '\n') run.Append(new W.Break());
                    else run.Append(new W.TabChar());
                    continue;
                }

                pending.Append(c);
            }

            FlushText(run, pending);
            parent.Append(run);
        }

        private void WriteLink(LinkInline link, OpenXmlCompositeElement parent, InlineStyleEnum inherited, bool inLink)
        {
            string url = link.Url.Trim();
            if (inLink || url.Length == 0)
            {
                Write(link.Inlines, parent, inherited, inLink);
                return;
            }

            if (!DocxLinkPolicy.IsAllowed(url))
            {
                _Session.Context.AddWarning(WarningCodeEnum.LinkRemovedUnsafe, "A link to '" + TextOf(url) + "' uses a disallowed URL scheme; its text was kept without the link.");
                Write(link.Inlines, parent, inherited, false);
                return;
            }

            W.Hyperlink hyperlink = new W.Hyperlink();
            if (DocxLinkPolicy.IsAnchor(url))
            {
                hyperlink.Anchor = url.Substring(1);
            }
            else
            {
                string? relId = _Session.LinkRelationship(url);
                if (relId == null)
                {
                    _Session.Context.AddWarning(WarningCodeEnum.LinkRemovedUnsafe, "A link to '" + TextOf(url) + "' is not a valid URL; its text was kept without the link.");
                    Write(link.Inlines, parent, inherited, false);
                    return;
                }

                hyperlink.Id = relId;
                hyperlink.History = OnOffValue.FromBoolean(true);
            }

            if (!string.IsNullOrEmpty(link.Title)) hyperlink.Tooltip = link.Title;
            Write(link.Inlines, hyperlink, inherited, true);
            if (!hyperlink.HasChildren) AppendText(hyperlink, url, inherited, true);
            parent.Append(hyperlink);
        }

        private static W.RunProperties? RunProperties(InlineStyleEnum style, bool inLink)
        {
            if (style == InlineStyleEnum.None && !inLink) return null;
            W.RunProperties rPr = new W.RunProperties();
            bool code = (style & InlineStyleEnum.Code) == InlineStyleEnum.Code;
            if (inLink) rPr.RunStyle = new W.RunStyle { Val = "Hyperlink" };
            else if (code) rPr.RunStyle = new W.RunStyle { Val = "InlineCode" };
            if (code) rPr.RunFonts = new W.RunFonts { Ascii = DocxFonts.CodeFont, HighAnsi = DocxFonts.CodeFont, ComplexScript = DocxFonts.CodeFont };
            if ((style & InlineStyleEnum.Bold) == InlineStyleEnum.Bold) rPr.Bold = new W.Bold();
            if ((style & InlineStyleEnum.Italic) == InlineStyleEnum.Italic) rPr.Italic = new W.Italic();
            if ((style & InlineStyleEnum.Strikethrough) == InlineStyleEnum.Strikethrough) rPr.Strike = new W.Strike();
            if ((style & InlineStyleEnum.Underline) == InlineStyleEnum.Underline) rPr.Underline = new W.Underline { Val = W.UnderlineValues.Single };
            if ((style & InlineStyleEnum.Superscript) == InlineStyleEnum.Superscript) rPr.VerticalTextAlignment = new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Superscript };
            else if ((style & InlineStyleEnum.Subscript) == InlineStyleEnum.Subscript) rPr.VerticalTextAlignment = new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Subscript };
            return rPr;
        }

        private static void FlushText(W.Run run, StringBuilder pending)
        {
            if (pending.Length == 0) return;
            run.Append(new W.Text(pending.ToString()) { Space = SpaceProcessingModeValues.Preserve });
            pending.Clear();
        }

        private static string TextOf(string url)
        {
            return url.Length > 80 ? url.Substring(0, 80) + "..." : url;
        }
    }
}
