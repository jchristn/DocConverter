namespace DocConverter.Writers.Docx
{
    using DocConverter.Enums;
    using DocumentFormat.OpenXml;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Page geometry for written documents, in twentieths of a point (twips).
    /// </summary>
    internal sealed class DocxPageSetup
    {
        internal uint WidthTwips { get; }

        internal uint HeightTwips { get; }

        internal int MarginTwips { get; }

        internal DocxPageSetup(PdfPageSizeEnum size, double marginPoints)
        {
            switch (size)
            {
                case PdfPageSizeEnum.A4:
                    WidthTwips = 11906;
                    HeightTwips = 16838;
                    break;
                case PdfPageSizeEnum.Legal:
                    WidthTwips = 12240;
                    HeightTwips = 20160;
                    break;
                default:
                    WidthTwips = 12240;
                    HeightTwips = 15840;
                    break;
            }

            MarginTwips = (int)(marginPoints * 20.0 + 0.5);
        }

        /// <summary>
        /// Usable text width in EMU (914400 per inch, 635 per twip).
        /// </summary>
        internal long ContentWidthEmu
        {
            get
            {
                long twips = (long)WidthTwips - 2L * MarginTwips;
                if (twips < 1440) twips = 1440;
                return twips * 635L;
            }
        }

        internal int ContentWidthTwips
        {
            get
            {
                int twips = (int)WidthTwips - 2 * MarginTwips;
                return twips < 1440 ? 1440 : twips;
            }
        }

        internal W.SectionProperties Build()
        {
            W.SectionProperties sectPr = new W.SectionProperties();
            sectPr.Append(new W.PageSize { Width = WidthTwips, Height = HeightTwips });
            sectPr.Append(new W.PageMargin
            {
                Top = MarginTwips,
                Right = (UInt32Value)(uint)MarginTwips,
                Bottom = MarginTwips,
                Left = (UInt32Value)(uint)MarginTwips,
                Header = 720U,
                Footer = 720U,
                Gutter = 0U
            });
            return sectPr;
        }
    }
}
