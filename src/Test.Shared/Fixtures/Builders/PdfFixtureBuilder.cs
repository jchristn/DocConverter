namespace Test.Shared.Fixtures.Builders
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using DocConverter.Writers.Pdf;
    using PdfSharp.Drawing;
    using PdfSharp.Pdf;

    /// <summary>
    /// Builds PDF fixtures with PDFsharp's low level drawing API only (no MigraDoc, no DocConverter writer), so a writer bug
    /// cannot mask a reader bug. Layout: title 24pt bold, level 2 headings 18pt bold, level 3 headings 14pt bold, body 11pt,
    /// bullets drawn as "\u2022" plus text, a ruled table, code in Liberation Mono, the reference PNG, and a second page.
    /// </summary>
    public static class PdfFixtureBuilder
    {
        /// <summary>Body font size in points.</summary>
        public const double BodySize = 11;

        /// <summary>Text drawn on the second page.</summary>
        public const string SecondPageText = "This paragraph sits on the second page of the reference PDF.";

        private const double Left = 72;
        private const double Right = 540;

        /// <summary>
        /// The reference document as PDF, two pages.
        /// </summary>
        /// <returns>PDF bytes.</returns>
        public static byte[] BuildReference()
        {
            PdfFontInstaller.EnsureInstalled();
            using (PdfDocument document = new PdfDocument())
            {
                document.Info.Title = ReferenceContent.Title;
                document.Info.Author = ReferenceContent.Author;
                document.Info.Subject = ReferenceContent.Subject;
                document.Info.CreationDate = new System.DateTime(2000, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
                document.Info.ModificationDate = new System.DateTime(2000, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);

                PdfPage page = document.AddPage();
                page.Width = XUnit.FromPoint(612);
                page.Height = XUnit.FromPoint(792);
                double y = 72;
                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                {
                    XFont title = new XFont("Liberation Sans", 24, XFontStyleEx.Bold);
                    XFont h2 = new XFont("Liberation Sans", 18, XFontStyleEx.Bold);
                    XFont h3 = new XFont("Liberation Sans", 14, XFontStyleEx.Bold);
                    XFont body = new XFont("Liberation Sans", BodySize, XFontStyleEx.Regular);
                    XFont bold = new XFont("Liberation Sans", BodySize, XFontStyleEx.Bold);
                    XFont italic = new XFont("Liberation Sans", BodySize, XFontStyleEx.Italic);
                    XFont mono = new XFont("Liberation Mono", 10, XFontStyleEx.Regular);

                    y = Line(gfx, ReferenceContent.Heading1, title, Left, y) + 8;

                    List<PdfFixtureRun> runs = new List<PdfFixtureRun>
                    {
                        new PdfFixtureRun(ReferenceContent.StyledLead, body, null, false, false),
                        new PdfFixtureRun(ReferenceContent.BoldText, bold, null, false, false),
                        new PdfFixtureRun(", ", body, null, false, false),
                        new PdfFixtureRun(ReferenceContent.ItalicText, italic, null, false, false),
                        new PdfFixtureRun(", ", body, null, false, false),
                        new PdfFixtureRun(ReferenceContent.UnderlineText, body, null, true, false),
                        new PdfFixtureRun(", ", body, null, false, false),
                        new PdfFixtureRun(ReferenceContent.StrikeText, body, null, false, true),
                        new PdfFixtureRun(", ", body, null, false, false),
                        new PdfFixtureRun(ReferenceContent.InlineCode, mono, null, false, false),
                        new PdfFixtureRun(" and a ", body, null, false, false),
                        new PdfFixtureRun(ReferenceContent.LinkText, body, ReferenceContent.LinkUrl, true, false),
                        new PdfFixtureRun(".", body, null, false, false)
                    };
                    y = Paragraph(gfx, page, runs, Left, y) + 8;

                    y = Line(gfx, ReferenceContent.HeadingLists, h2, Left, y) + 6;
                    y = Bullet(gfx, ReferenceContent.Bullets[0], body, Left, y);
                    y = Bullet(gfx, ReferenceContent.Bullets[1], body, Left, y);
                    y = Bullet(gfx, ReferenceContent.NestedBullet, body, Left + 18, y);
                    y = Bullet(gfx, ReferenceContent.DeepBullet, body, Left + 36, y);
                    y = Bullet(gfx, ReferenceContent.Bullets[2], body, Left, y) + 8;
                    for (int i = 0; i < ReferenceContent.Steps.Length; i++)
                    {
                        gfx.DrawString((i + 1) + ".", body, XBrushes.Black, Left, y, XStringFormats.TopLeft);
                        gfx.DrawString(ReferenceContent.Steps[i], body, XBrushes.Black, Left + 18, y, XStringFormats.TopLeft);
                        y += BodySize * 1.35;
                    }

                    y += 8;
                    y = Line(gfx, ReferenceContent.HeadingTable, h2, Left, y) + 6;
                    y = Table(gfx, body, bold, Left, y) + 10;

                    y = Line(gfx, ReferenceContent.HeadingCode, h3, Left, y) + 6;
                    foreach (string codeLine in ReferenceContent.CodeText.Split('\n'))
                    {
                        gfx.DrawString(codeLine, mono, XBrushes.Black, Left, y, XStringFormats.TopLeft);
                        y += 10 * 1.3;
                    }

                    y += 8;
                    gfx.DrawString(ReferenceContent.QuoteText, italic, XBrushes.Black, Left + 24, y, XStringFormats.TopLeft);
                    y += BodySize * 1.35 + 8;

                    using (MemoryStream png = new MemoryStream(ReferenceContent.ImagePng()))
                    {
                        XImage image = XImage.FromStream(png);
                        gfx.DrawImage(image, Left, y, 48, 48);
                    }

                    y += 56;
                    y = Line(gfx, ReferenceContent.InternationalLatin, body, Left, y) + 4;
                    y = Line(gfx, ReferenceContent.Special, body, Left, y) + 4;
                }

                PdfPage second = document.AddPage();
                second.Width = XUnit.FromPoint(612);
                second.Height = XUnit.FromPoint(792);
                using (XGraphics gfx = XGraphics.FromPdfPage(second))
                {
                    XFont body = new XFont("Liberation Sans", BodySize, XFontStyleEx.Regular);
                    double y2 = Line(gfx, SecondPageText, body, Left, 72) + 8;
                    Line(gfx, ReferenceContent.Closing, body, Left, y2);
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    document.Save(ms, false);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// A one page PDF holding only the reference image and no text layer.
        /// </summary>
        /// <returns>PDF bytes.</returns>
        public static byte[] BuildImageOnly()
        {
            PdfFontInstaller.EnsureInstalled();
            using (PdfDocument document = new PdfDocument())
            {
                PdfPage page = document.AddPage();
                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                using (MemoryStream png = new MemoryStream(TestImages.SolidPng(64, 48, 0x80, 0x20, 0x20)))
                {
                    XImage image = XImage.FromStream(png);
                    gfx.DrawImage(image, 72, 72, 256, 192);
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    document.Save(ms, false);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// A hand-written one page PDF with no text whose only image is the 96 by 64 sample JPEG wrapped in FlateDecode
        /// (Filter [/FlateDecode /DCTDecode]), as scanners commonly produce.
        /// </summary>
        /// <returns>PDF bytes.</returns>
        public static byte[] BuildFlateWrappedJpeg()
        {
            byte[] jpeg = TestImages.Sample("sample.jpg");
            byte[] wrapped;
            using (MemoryStream zs = new MemoryStream())
            {
                using (System.IO.Compression.ZLibStream z = new System.IO.Compression.ZLibStream(zs, System.IO.Compression.CompressionLevel.Optimal, true))
                {
                    z.Write(jpeg, 0, jpeg.Length);
                }

                wrapped = zs.ToArray();
            }

            string content = "q 192 0 0 128 72 600 cm /Im1 Do Q";
            List<byte[]> objects = new List<byte[]>
            {
                Ascii("<< /Type /Catalog /Pages 2 0 R >>"),
                Ascii("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
                Ascii("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Im1 4 0 R >> >> /Contents 5 0 R >>"),
                Concat(Ascii("<< /Type /XObject /Subtype /Image /Width 96 /Height 64 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter [/FlateDecode /DCTDecode] /Length " + wrapped.Length + " >>\nstream\n"), wrapped, Ascii("\nendstream")),
                Ascii("<< /Length " + content.Length + " >>\nstream\n" + content + "\nendstream")
            };

            using (MemoryStream pdf = new MemoryStream())
            {
                Write(pdf, Ascii("%PDF-1.4\n"));
                List<long> offsets = new List<long>();
                for (int i = 0; i < objects.Count; i++)
                {
                    offsets.Add(pdf.Position);
                    Write(pdf, Ascii((i + 1) + " 0 obj\n"));
                    Write(pdf, objects[i]);
                    Write(pdf, Ascii("\nendobj\n"));
                }

                long xref = pdf.Position;
                StringBuilder table = new StringBuilder();
                table.Append("xref\n0 ").Append(objects.Count + 1).Append("\n0000000000 65535 f \n");
                foreach (long offset in offsets) table.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
                table.Append("trailer\n<< /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
                Write(pdf, Ascii(table.ToString()));
                return pdf.ToArray();
            }
        }

        private static byte[] Ascii(string text)
        {
            return Encoding.ASCII.GetBytes(text);
        }

        private static byte[] Concat(params byte[][] parts)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                foreach (byte[] part in parts) ms.Write(part, 0, part.Length);
                return ms.ToArray();
            }
        }

        private static void Write(Stream stream, byte[] bytes)
        {
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// A PDF protected with a user password ("user-secret").
        /// </summary>
        /// <returns>PDF bytes.</returns>
        public static byte[] BuildEncrypted()
        {
            PdfFontInstaller.EnsureInstalled();
            using (PdfDocument document = new PdfDocument())
            {
                PdfPage page = document.AddPage();
                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                {
                    gfx.DrawString("Secret content", new XFont("Liberation Sans", 12, XFontStyleEx.Regular), XBrushes.Black, 72, 72, XStringFormats.TopLeft);
                }

                document.SecuritySettings.UserPassword = "user-secret";
                document.SecuritySettings.OwnerPassword = "owner-secret";
                using (MemoryStream ms = new MemoryStream())
                {
                    document.Save(ms, false);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// The reference PDF truncated to its first 40 percent, so the cross reference table and trailer are missing.
        /// </summary>
        /// <returns>Corrupt PDF bytes.</returns>
        public static byte[] BuildCorrupt()
        {
            byte[] full = BuildReference();
            byte[] truncated = new byte[full.Length * 2 / 5];
            Buffer.BlockCopy(full, 0, truncated, 0, truncated.Length);
            return truncated;
        }

        private static double Line(XGraphics gfx, string text, XFont font, double x, double y)
        {
            gfx.DrawString(text, font, XBrushes.Black, x, y, XStringFormats.TopLeft);
            return y + font.Size * 1.35;
        }

        private static double Bullet(XGraphics gfx, string text, XFont font, double x, double y)
        {
            gfx.DrawString("\u2022", font, XBrushes.Black, x, y, XStringFormats.TopLeft);
            gfx.DrawString(text, font, XBrushes.Black, x + 14, y, XStringFormats.TopLeft);
            return y + font.Size * 1.35;
        }

        private static double Paragraph(XGraphics gfx, PdfPage page, List<PdfFixtureRun> runs, double x, double y)
        {
            double cursor = x;
            double lineHeight = BodySize * 1.35;
            double space = gfx.MeasureString(" ", runs[0].Font).Width;
            foreach (PdfFixtureRun run in runs)
            {
                string[] words = run.Text.Split(' ');
                for (int i = 0; i < words.Length; i++)
                {
                    string word = words[i];
                    if (i > 0) cursor += space;
                    if (word.Length == 0) continue;
                    double width = gfx.MeasureString(word, run.Font).Width;
                    if (cursor + width > Right && cursor > x)
                    {
                        cursor = x;
                        y += lineHeight;
                    }

                    XBrush brush = run.Url != null ? XBrushes.DarkBlue : XBrushes.Black;
                    gfx.DrawString(word, run.Font, brush, cursor, y, XStringFormats.TopLeft);
                    if (run.Underline) gfx.DrawLine(XPens.Black, cursor, y + run.Font.Size * 1.05, cursor + width, y + run.Font.Size * 1.05);
                    if (run.Strike) gfx.DrawLine(XPens.Black, cursor, y + run.Font.Size * 0.6, cursor + width, y + run.Font.Size * 0.6);
                    if (run.Url != null)
                    {
                        double pageHeight = page.Height.Point;
                        PdfSharp.Pdf.PdfRectangle rect = new PdfSharp.Pdf.PdfRectangle(
                            new XPoint(cursor, pageHeight - (y + run.Font.Size * 1.2)),
                            new XPoint(cursor + width, pageHeight - y));
                        page.AddWebLink(rect, run.Url);
                    }

                    cursor += width;
                }
            }

            return y + lineHeight;
        }

        private static double Table(XGraphics gfx, XFont body, XFont bold, double x, double y)
        {
            double[] widths = new double[] { 150, 150, 80 };
            double rowHeight = 20;
            double total = 0;
            foreach (double w in widths) total += w;
            string[][] rows = ReferenceContent.TableRows;
            for (int r = 0; r < rows.Length; r++)
            {
                double cx = x;
                for (int c = 0; c < widths.Length; c++)
                {
                    gfx.DrawString(rows[r][c], r == 0 ? bold : body, XBrushes.Black, cx + 4, y + r * rowHeight + 4, XStringFormats.TopLeft);
                    cx += widths[c];
                }
            }

            XPen pen = new XPen(XColors.Black, 0.75);
            for (int r = 0; r <= rows.Length; r++) gfx.DrawLine(pen, x, y + r * rowHeight, x + total, y + r * rowHeight);
            double lx = x;
            gfx.DrawLine(pen, lx, y, lx, y + rows.Length * rowHeight);
            foreach (double w in widths)
            {
                lx += w;
                gfx.DrawLine(pen, lx, y, lx, y + rows.Length * rowHeight);
            }

            return y + rows.Length * rowHeight;
        }
    }
}
