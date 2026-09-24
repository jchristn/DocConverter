namespace DocConverter.Readers.Docx
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using A = DocumentFormat.OpenXml.Drawing;
    using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
    using V = DocumentFormat.OpenXml.Vml;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Reads the inline content of one paragraph: runs with styles, hyperlinks (relationship and field based), line and
    /// page breaks, images, text boxes and note references.
    /// </summary>
    internal sealed class DocxInlineReader
    {
        private readonly DocxReadSession _Session;
        private readonly List<DocxFieldFrame> _Fields = new List<DocxFieldFrame>();

        internal DocxInlineReader(DocxReadSession session)
        {
            _Session = session;
        }

        internal DocxParagraphContent Read(W.Paragraph paragraph, OpenXmlPart owner)
        {
            DocxParagraphContent content = new DocxParagraphContent();
            _Fields.Clear();
            foreach (OpenXmlElement child in paragraph.ChildElements)
                ReadParagraphChild(child, owner, content, null);
            return content;
        }

        private void ReadParagraphChild(OpenXmlElement child, OpenXmlPart owner, DocxParagraphContent content, List<Inline>? linkSink)
        {
            switch (child)
            {
                case W.Run run:
                    ReadRun(run, owner, content, linkSink);
                    break;
                case W.Hyperlink hyperlink:
                    ReadHyperlink(hyperlink, owner, content);
                    break;
                case W.SimpleField field:
                    ReadSimpleField(field, owner, content, linkSink);
                    break;
                case W.InsertedRun inserted:
                    foreach (OpenXmlElement c in inserted.ChildElements) ReadParagraphChild(c, owner, content, linkSink);
                    break;
                case W.DeletedRun _:
                case W.MoveFromRun _:
                    break;
                case W.SdtRun sdt:
                    if (sdt.SdtContentRun != null)
                        foreach (OpenXmlElement c in sdt.SdtContentRun.ChildElements) ReadParagraphChild(c, owner, content, linkSink);
                    break;
                case W.CustomXmlRun custom:
                    foreach (OpenXmlElement c in custom.ChildElements) ReadParagraphChild(c, owner, content, linkSink);
                    break;
                case W.MoveToRun moveTo:
                    foreach (OpenXmlElement c in moveTo.ChildElements) ReadParagraphChild(c, owner, content, linkSink);
                    break;
                case AlternateContent alternate:
                    OpenXmlElement? branch = PickBranch(alternate);
                    if (branch != null)
                        foreach (OpenXmlElement c in branch.ChildElements) ReadParagraphChild(c, owner, content, linkSink);
                    break;
            }
        }

        private void ReadHyperlink(W.Hyperlink hyperlink, OpenXmlPart owner, DocxParagraphContent content)
        {
            string url = "";
            string? relId = hyperlink.Id?.Value;
            if (!string.IsNullOrEmpty(relId))
            {
                foreach (HyperlinkRelationship rel in owner.HyperlinkRelationships)
                {
                    if (rel.Id == relId)
                    {
                        url = rel.Uri.OriginalString;
                        break;
                    }
                }
            }

            if (url.Length == 0 && !string.IsNullOrEmpty(hyperlink.Anchor?.Value)) url = "#" + hyperlink.Anchor!.Value;

            LinkInline link = new LinkInline();
            link.Url = url;
            link.Title = hyperlink.Tooltip?.Value;
            foreach (OpenXmlElement child in hyperlink.ChildElements) ReadParagraphChild(child, owner, content, link.Inlines);
            if (link.Inlines.Count == 0) return;
            if (url.Length == 0)
            {
                foreach (Inline inline in link.Inlines) Sink(content, null).Add(inline);
                return;
            }

            Sink(content, null).Add(link);
        }

        private void ReadSimpleField(W.SimpleField field, OpenXmlPart owner, DocxParagraphContent content, List<Inline>? linkSink)
        {
            string? url = HyperlinkTarget(field.Instruction?.Value);
            if (url == null)
            {
                foreach (OpenXmlElement child in field.ChildElements) ReadParagraphChild(child, owner, content, linkSink);
                return;
            }

            LinkInline link = new LinkInline();
            link.Url = url;
            foreach (OpenXmlElement child in field.ChildElements) ReadParagraphChild(child, owner, content, link.Inlines);
            if (link.Inlines.Count > 0) Sink(content, linkSink).Add(link);
        }

        private void ReadRun(W.Run run, OpenXmlPart owner, DocxParagraphContent content, List<Inline>? linkSink)
        {
            InlineStyleEnum style = RunStyle(run.RunProperties);
            bool monospace = (style & InlineStyleEnum.Code) == InlineStyleEnum.Code;
            ReadRunChildren(run.ChildElements, owner, content, linkSink, style, monospace);
        }

        private void ReadRunChildren(IEnumerable<OpenXmlElement> children, OpenXmlPart owner, DocxParagraphContent content, List<Inline>? linkSink, InlineStyleEnum style, bool monospace)
        {
            foreach (OpenXmlElement child in children)
            {
                switch (child)
                {
                    case W.FieldChar fieldChar:
                        HandleFieldChar(fieldChar, content, linkSink);
                        break;
                    case W.FieldCode code:
                        if (_Fields.Count > 0) _Fields[_Fields.Count - 1].Instruction.Append(code.Text);
                        break;
                    case W.Text text:
                        if (InInstruction()) break;
                        AddText(Sink(content, linkSink), text.Text, style);
                        content.CountText(text.Text, monospace);
                        break;
                    case W.TabChar _:
                    case W.PositionalTab _:
                        if (InInstruction()) break;
                        AddText(Sink(content, linkSink), "\t", style);
                        break;
                    case W.NoBreakHyphen _:
                        AddText(Sink(content, linkSink), "-", style);
                        break;
                    case W.Break br:
                        if (br.Type != null && br.Type.Value == W.BreakValues.Page)
                        {
                            if (linkSink == null && ActiveFieldLink() == null) content.BreakPage();
                        }
                        else
                        {
                            Sink(content, linkSink).Add(new LineBreakInline());
                        }

                        break;
                    case W.CarriageReturn _:
                        Sink(content, linkSink).Add(new LineBreakInline());
                        break;
                    case W.Drawing drawing:
                        ReadDrawing(drawing, owner, content, linkSink);
                        break;
                    case W.Picture picture:
                        ReadVml(picture, owner, content, linkSink);
                        break;
                    case W.EmbeddedObject embedded:
                        ReadVml(embedded, owner, content, linkSink);
                        break;
                    case W.FootnoteReference footnote:
                        if (_Session.Options.Docx.IncludeFootnotes && footnote.Id != null)
                            AddText(Sink(content, linkSink), "[" + _Session.FootnoteNumber(footnote.Id.Value) + "]", InlineStyleEnum.Superscript);
                        break;
                    case W.EndnoteReference endnote:
                        if (_Session.Options.Docx.IncludeFootnotes && endnote.Id != null)
                            AddText(Sink(content, linkSink), "[e" + _Session.EndnoteNumber(endnote.Id.Value) + "]", InlineStyleEnum.Superscript);
                        break;
                    case AlternateContent alternate:
                        OpenXmlElement? branch = PickBranch(alternate);
                        if (branch != null) ReadRunChildren(branch.ChildElements, owner, content, linkSink, style, monospace);
                        break;
                }
            }
        }

        private void HandleFieldChar(W.FieldChar fieldChar, DocxParagraphContent content, List<Inline>? linkSink)
        {
            W.FieldCharValues? type = fieldChar.FieldCharType?.Value;
            if (type == null) return;
            if (type.Value == W.FieldCharValues.Begin)
            {
                _Fields.Add(new DocxFieldFrame());
            }
            else if (type.Value == W.FieldCharValues.Separate)
            {
                if (_Fields.Count == 0) return;
                DocxFieldFrame frame = _Fields[_Fields.Count - 1];
                frame.Separated = true;
                string? url = HyperlinkTarget(frame.Instruction.ToString());
                if (url != null)
                {
                    LinkInline link = new LinkInline();
                    link.Url = url;
                    Sink(content, linkSink).Add(link);
                    frame.Link = link;
                }
            }
            else if (type.Value == W.FieldCharValues.End)
            {
                if (_Fields.Count == 0) return;
                DocxFieldFrame frame = _Fields[_Fields.Count - 1];
                _Fields.RemoveAt(_Fields.Count - 1);
                if (frame.Link != null && frame.Link.Inlines.Count == 0)
                {
                    List<Inline> sink = Sink(content, linkSink);
                    sink.Remove(frame.Link);
                }
            }
        }

        private bool InInstruction()
        {
            foreach (DocxFieldFrame frame in _Fields)
                if (!frame.Separated) return true;
            return false;
        }

        private LinkInline? ActiveFieldLink()
        {
            for (int i = _Fields.Count - 1; i >= 0; i--)
                if (_Fields[i].Link != null && _Fields[i].Separated) return _Fields[i].Link;
            return null;
        }

        private List<Inline> Sink(DocxParagraphContent content, List<Inline>? linkSink)
        {
            LinkInline? fieldLink = ActiveFieldLink();
            if (fieldLink != null) return fieldLink.Inlines;
            if (linkSink != null) return linkSink;
            return content.Current;
        }

        private void ReadDrawing(W.Drawing drawing, OpenXmlPart owner, DocxParagraphContent content, List<Inline>? linkSink)
        {
            foreach (W.TextBoxContent box in drawing.Descendants<W.TextBoxContent>())
                _Session.TextBoxes.Add(new DocxTextBox(box, owner));

            A.Blip? blip = drawing.Descendants<A.Blip>().FirstOrDefault();
            if (blip == null) return;
            string? embed = blip.Embed?.Value;
            if (string.IsNullOrEmpty(embed))
            {
                if (!string.IsNullOrEmpty(blip.Link?.Value))
                    _Session.Context.AddWarning(WarningCodeEnum.ImagesOmitted, "A linked (external) image was not embedded in the document and was skipped.");
                return;
            }

            string? resourceId = _Session.AddImage(owner, embed!);
            if (resourceId == null) return;

            DW.DocProperties? docPr = drawing.Descendants<DW.DocProperties>().FirstOrDefault();
            string? alt = docPr?.Description?.Value;
            if (string.IsNullOrEmpty(alt)) alt = docPr?.Title?.Value;
            if (string.IsNullOrEmpty(alt)) alt = null;

            double? width = null;
            double? height = null;
            DW.Extent? extent = drawing.Descendants<DW.Extent>().FirstOrDefault();
            if (extent != null && extent.Cx != null && extent.Cy != null && extent.Cx.Value > 0 && extent.Cy.Value > 0)
            {
                width = extent.Cx.Value / 12700.0;
                height = extent.Cy.Value / 12700.0;
            }

            ImageInline inline = new ImageInline(resourceId, alt);
            Sink(content, linkSink).Add(inline);
            content.Images.Add(new DocxImageInfo(inline, width, height));
        }

        private void ReadVml(OpenXmlElement container, OpenXmlPart owner, DocxParagraphContent content, List<Inline>? linkSink)
        {
            foreach (W.TextBoxContent box in container.Descendants<W.TextBoxContent>())
                _Session.TextBoxes.Add(new DocxTextBox(box, owner));

            V.ImageData? data = container.Descendants<V.ImageData>().FirstOrDefault();
            string? relId = data?.RelationshipId?.Value;
            if (string.IsNullOrEmpty(relId)) return;
            string? resourceId = _Session.AddImage(owner, relId!);
            if (resourceId == null) return;
            string? alt = data!.Title?.Value;
            ImageInline inline = new ImageInline(resourceId, string.IsNullOrEmpty(alt) ? null : alt);
            Sink(content, linkSink).Add(inline);
            content.Images.Add(new DocxImageInfo(inline, null, null));
        }

        private InlineStyleEnum RunStyle(W.RunProperties? rPr)
        {
            InlineStyleEnum style = InlineStyleEnum.None;
            if (rPr == null) return style;
            if (rPr.RunStyle?.Val?.Value != null) style |= _Session.Styles.CharacterStyle(rPr.RunStyle.Val.Value);
            if (rPr.Bold != null) style = DocxStyleResolver.IsOn(rPr.Bold) ? style | InlineStyleEnum.Bold : style & ~InlineStyleEnum.Bold;
            if (rPr.Italic != null) style = DocxStyleResolver.IsOn(rPr.Italic) ? style | InlineStyleEnum.Italic : style & ~InlineStyleEnum.Italic;
            if (DocxStyleResolver.IsOn(rPr.Strike) || DocxStyleResolver.IsOn(rPr.DoubleStrike)) style |= InlineStyleEnum.Strikethrough;
            if (rPr.Underline != null)
            {
                bool underlined = rPr.Underline.Val == null || rPr.Underline.Val.Value != W.UnderlineValues.None;
                style = underlined ? style | InlineStyleEnum.Underline : style & ~InlineStyleEnum.Underline;
            }

            if (rPr.VerticalTextAlignment?.Val != null)
            {
                if (rPr.VerticalTextAlignment.Val.Value == W.VerticalPositionValues.Superscript) style |= InlineStyleEnum.Superscript;
                else if (rPr.VerticalTextAlignment.Val.Value == W.VerticalPositionValues.Subscript) style |= InlineStyleEnum.Subscript;
            }

            if (rPr.RunFonts != null && (DocxFonts.IsMonospace(rPr.RunFonts.Ascii?.Value) || DocxFonts.IsMonospace(rPr.RunFonts.HighAnsi?.Value)))
                style |= InlineStyleEnum.Code;
            return style;
        }

        private static void AddText(List<Inline> sink, string text, InlineStyleEnum style)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (sink.Count > 0 && sink[sink.Count - 1] is TextInline last && last.Style == style)
            {
                last.Text += text;
                return;
            }

            sink.Add(new TextInline(text, style));
        }

        private static OpenXmlElement? PickBranch(AlternateContent alternate)
        {
            AlternateContentChoice? choice = alternate.GetFirstChild<AlternateContentChoice>();
            if (choice != null) return choice;
            return alternate.GetFirstChild<AlternateContentFallback>();
        }

        private static string? HyperlinkTarget(string? instruction)
        {
            if (string.IsNullOrWhiteSpace(instruction)) return null;
            string trimmed = instruction!.Trim();
            if (!trimmed.StartsWith("HYPERLINK", StringComparison.OrdinalIgnoreCase)) return null;
            string rest = trimmed.Substring("HYPERLINK".Length).Trim();
            string? anchor = null;
            string? url = null;
            int i = 0;
            while (i < rest.Length)
            {
                if (rest[i] == ' ')
                {
                    i++;
                    continue;
                }

                if (rest[i] == '\\' && i + 1 < rest.Length)
                {
                    char sw = rest[i + 1];
                    i += 2;
                    string arg = ReadArgument(rest, ref i);
                    if (sw == 'l') anchor = arg;
                    continue;
                }

                string value = ReadArgument(rest, ref i);
                if (url == null) url = value;
            }

            if (!string.IsNullOrEmpty(url)) return string.IsNullOrEmpty(anchor) ? url : url + "#" + anchor;
            if (!string.IsNullOrEmpty(anchor)) return "#" + anchor;
            return null;
        }

        private static string ReadArgument(string text, ref int i)
        {
            while (i < text.Length && text[i] == ' ') i++;
            if (i >= text.Length) return "";
            if (text[i] == '"')
            {
                int end = text.IndexOf('"', i + 1);
                if (end < 0) end = text.Length;
                string quoted = text.Substring(i + 1, end - i - 1);
                i = end + 1;
                return quoted;
            }

            int start = i;
            while (i < text.Length && text[i] != ' ') i++;
            return text.Substring(start, i - start);
        }
    }
}
