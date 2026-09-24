namespace Test.Shared.Fixtures.Builders
{
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Builds the reference document as hand-written RTF: font table (with a modern, monospace font), color table,
    /// style sheet with "heading N" styles and outline levels, document information, styled runs, a HYPERLINK field,
    /// nested bullet and numbered lists, a table with a header row, monospace code paragraphs, a PNG picture, Unicode
    /// escapes for the international paragraph and Windows-1252 hex escapes for a code page paragraph.
    /// </summary>
    public static class RtfFixtureBuilder
    {
        /// <summary>
        /// Paragraph written with \'hh Windows-1252 escapes, decoding to this text.
        /// </summary>
        public const string CodePageText = "Caf\u00E9 cr\u00E8me \u2013 \u20AC5";

        /// <summary>
        /// The reference document as RTF.
        /// </summary>
        /// <returns>RTF source (ASCII only).</returns>
        public static string BuildReference()
        {
            StringBuilder r = new StringBuilder();
            r.Append("{\\rtf1\\ansi\\ansicpg1252\\deff0\n");
            r.Append("{\\fonttbl{\\f0\\fswiss\\fcharset0 Liberation Sans;}{\\f1\\fmodern\\fcharset0 Liberation Mono;}}\n");
            r.Append("{\\colortbl;\\red0\\green0\\blue0;\\red31\\green78\\blue140;}\n");
            r.Append("{\\stylesheet{\\s0 Normal;}{\\s1\\outlinelevel0\\b\\fs48 heading 1;}{\\s2\\outlinelevel1\\b\\fs36 heading 2;}{\\s3\\b\\fs28 heading 3;}}\n");
            r.Append("{\\info{\\title ").Append(ReferenceContent.Title).Append("}{\\author ").Append(ReferenceContent.Author)
                .Append("}{\\subject ").Append(ReferenceContent.Subject).Append("}{\\creatim\\yr2024\\mo5\\dy6\\hr7\\min8}}\n");

            r.Append("\\pard\\s1\\b\\fs48 ").Append(ReferenceContent.Heading1).Append("\\b0\\fs22\\par\n");
            r.Append("\\pard ").Append(ReferenceContent.StyledLead)
                .Append("{\\b ").Append(ReferenceContent.BoldText).Append("}, ")
                .Append("{\\i ").Append(ReferenceContent.ItalicText).Append("}, ")
                .Append("{\\ul ").Append(ReferenceContent.UnderlineText).Append("}, ")
                .Append("{\\strike ").Append(ReferenceContent.StrikeText).Append("}, ")
                .Append("{\\f1 ").Append(ReferenceContent.InlineCode).Append("} and a ")
                .Append("{\\field{\\*\\fldinst{HYPERLINK \"").Append(ReferenceContent.LinkUrl).Append("\"}}{\\fldrslt{\\ul ")
                .Append(ReferenceContent.LinkText).Append("}}}.\\par\n");

            r.Append("\\pard\\s2\\b\\fs36 ").Append(ReferenceContent.HeadingLists).Append("\\b0\\fs22\\par\n");
            r.Append("\\pard\\ls1\\ilvl0{\\listtext\\'95\\tab}").Append(ReferenceContent.Bullets[0]).Append("\\par\n");
            r.Append("{\\listtext\\'95\\tab}").Append(ReferenceContent.Bullets[1]).Append("\\par\n");
            r.Append("\\pard\\ls1\\ilvl1{\\listtext o\\tab}").Append(ReferenceContent.NestedBullet).Append("\\par\n");
            r.Append("\\pard\\ls1\\ilvl2{\\listtext\\'a7\\tab}").Append(ReferenceContent.DeepBullet).Append("\\par\n");
            r.Append("\\pard\\ls1\\ilvl0{\\listtext\\'95\\tab}").Append(ReferenceContent.Bullets[2]).Append("\\par\n");
            for (int i = 0; i < ReferenceContent.Steps.Length; i++)
            {
                r.Append("\\pard\\ls2\\ilvl0{\\listtext ").Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append(".\\tab}")
                    .Append(ReferenceContent.Steps[i]).Append("\\par\n");
            }

            r.Append("\\pard\\s2\\b\\fs36 ").Append(ReferenceContent.HeadingTable).Append("\\b0\\fs22\\par\n");
            string[][] rows = ReferenceContent.TableRows;
            for (int row = 0; row < rows.Length; row++)
            {
                r.Append("\\trowd").Append(row == 0 ? "\\trhdr" : "").Append("\\cellx3000\\cellx6000\\cellx8000\n\\pard\\intbl ");
                foreach (string cell in rows[row])
                {
                    if (row == 0) r.Append("{\\b ").Append(cell).Append("}\\cell ");
                    else r.Append(cell).Append("\\cell ");
                }

                r.Append("\\row\n");
            }

            r.Append("\\pard\\s3\\b\\fs28 ").Append(ReferenceContent.HeadingCode).Append("\\b0\\fs22\\par\n");
            foreach (string line in ReferenceContent.CodeText.Split('\n'))
                r.Append("\\pard\\f1 ").Append(line).Append("\\par\n");
            r.Append("\\pard\\plain\\f0\\li720\\i ").Append(ReferenceContent.QuoteText).Append("\\i0\\par\n");

            r.Append("\\pard{\\*\\shppict{\\pict\\pngblip\\picw16\\pich16\\picwgoal480\\pichgoal480\n");
            byte[] png = ReferenceContent.ImagePng();
            for (int i = 0; i < png.Length; i++)
            {
                r.Append(png[i].ToString("x2", CultureInfo.InvariantCulture));
                if (i % 32 == 31) r.Append('\n');
            }

            r.Append("}}{\\nonshppict{\\pict\\wmetafile8\\picw16\\pich16 0102030405}}\\par\n");
            r.Append("\\pard\\uc1 ").Append(Escape(ReferenceContent.International)).Append("\\par\n");
            r.Append("\\pard ").Append(ReferenceContent.Special).Append("\\par\n");
            r.Append("\\pard Caf\\'e9 cr\\'e8me \\'96 \\'805\\par\n");
            r.Append("\\pard ").Append(ReferenceContent.Closing).Append("\\par\n");
            r.Append("}");
            return r.ToString();
        }

        /// <summary>
        /// Escape a string for RTF: ASCII passes through (with \, { and } escaped); every other UTF-16 code unit becomes
        /// \uN? with N as a signed 16-bit value, so surrogate pairs (emoji) are written as two escapes.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>RTF text.</returns>
        public static string Escape(string text)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in text)
            {
                if (c == '\\' || c == '{' || c == '}') sb.Append('\\').Append(c);
                else if (c < 128) sb.Append(c);
                else sb.Append("\\u").Append(((short)c).ToString(CultureInfo.InvariantCulture)).Append('?');
            }

            return sb.ToString();
        }
    }
}
