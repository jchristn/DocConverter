namespace DocConverter.Registry
{
    using System.Collections.Generic;
    using DocConverter.Enums;

    /// <summary>
    /// Fidelity of every built-in conversion pair (plan Section 7). docs/FORMATS.md is generated from this table and a
    /// test keeps the two identical.
    /// </summary>
    internal static class CapabilityMatrix
    {
        internal static readonly DocumentFormatEnum[] BuiltInInputs = new DocumentFormatEnum[]
        {
            DocumentFormatEnum.Text, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, DocumentFormatEnum.Json,
            DocumentFormatEnum.Xml, DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv, DocumentFormatEnum.Rtf,
            DocumentFormatEnum.Docx, DocumentFormatEnum.Xlsx, DocumentFormatEnum.Pptx, DocumentFormatEnum.Pdf,
            DocumentFormatEnum.Png, DocumentFormatEnum.Jpeg, DocumentFormatEnum.Gif, DocumentFormatEnum.Bmp,
            DocumentFormatEnum.Tiff, DocumentFormatEnum.WebP
        };

        internal static readonly DocumentFormatEnum[] BuiltInOutputs = new DocumentFormatEnum[]
        {
            DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, DocumentFormatEnum.Text, DocumentFormatEnum.Json,
            DocumentFormatEnum.Xml, DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv, DocumentFormatEnum.Docx,
            DocumentFormatEnum.Xlsx, DocumentFormatEnum.Pptx, DocumentFormatEnum.Pdf
        };

        private const string TablesOnly = "Tables only. Without tables, one row per block (NonTableContentDropped, TablesFlattened).";
        private const string XlsxProjection = "Tables become sheets; other blocks become rows on a Document sheet; images omitted.";
        private const string PptxProjection = "Content is split across slides; long tables continue on extra slides.";
        private const string ImageNoOcr = "No OCR: a placeholder names the image (ImagePlaceholderEmitted).";
        private const string ImageNotInPdf = "PDFsharp cannot embed this image format: a placeholder is written (ImageFormatUnsupported).";

        internal static bool TryGet(DocumentFormatEnum from, DocumentFormatEnum to, out FidelityEnum fidelity, out string notes)
        {
            fidelity = FidelityEnum.Full;
            notes = "";

            bool image = IsImage(from);
            if (image)
            {
                switch (to)
                {
                    case DocumentFormatEnum.Text:
                    case DocumentFormatEnum.Csv:
                    case DocumentFormatEnum.Tsv:
                    case DocumentFormatEnum.Xlsx:
                        fidelity = FidelityEnum.Projection;
                        notes = ImageNoOcr;
                        return true;
                    case DocumentFormatEnum.Pdf:
                        if (from == DocumentFormatEnum.Gif || from == DocumentFormatEnum.Tiff || from == DocumentFormatEnum.WebP)
                        {
                            fidelity = FidelityEnum.Projection;
                            notes = ImageNotInPdf;
                        }
                        else if (from == DocumentFormatEnum.Jpeg)
                        {
                            notes = "CMYK JPEG cannot be embedded and becomes a placeholder (ImageFormatUnsupported).";
                        }

                        return true;
                    case DocumentFormatEnum.Docx:
                    case DocumentFormatEnum.Pptx:
                        if (from == DocumentFormatEnum.WebP) notes = "WebP verified in Microsoft 365; older Office releases may not display it.";
                        return true;
                    default:
                        return IsBuiltInOutput(to);
                }
            }

            if (!IsBuiltInInput(from) || !IsBuiltInOutput(to)) return false;

            if (to == DocumentFormatEnum.Csv || to == DocumentFormatEnum.Tsv)
            {
                if (from == DocumentFormatEnum.Csv || from == DocumentFormatEnum.Tsv) return true;
                fidelity = FidelityEnum.Projection;
                notes = TablesOnly;
                return true;
            }

            if (to == DocumentFormatEnum.Xlsx)
            {
                if (from == DocumentFormatEnum.Csv || from == DocumentFormatEnum.Tsv || from == DocumentFormatEnum.Xlsx) return true;
                fidelity = FidelityEnum.Projection;
                notes = XlsxProjection;
                return true;
            }

            if (to == DocumentFormatEnum.Pptx)
            {
                if (from == DocumentFormatEnum.Text || from == DocumentFormatEnum.Pptx) return true;
                fidelity = FidelityEnum.Projection;
                notes = PptxProjection;
                return true;
            }

            if (from == DocumentFormatEnum.Pdf && to != DocumentFormatEnum.Text)
                notes = "PDF structure is inferred: headings from font size, ruled tables only; scanned pages have no text (HeadingsInferred, NoTextLayer).";

            return true;
        }

        internal static bool IsBuiltInInput(DocumentFormatEnum format)
        {
            foreach (DocumentFormatEnum f in BuiltInInputs) if (f == format) return true;
            return false;
        }

        internal static bool IsBuiltInOutput(DocumentFormatEnum format)
        {
            foreach (DocumentFormatEnum f in BuiltInOutputs) if (f == format) return true;
            return false;
        }

        internal static List<KeyValuePair<DocumentFormatEnum, DocumentFormatEnum>> AllBuiltInPairs()
        {
            List<KeyValuePair<DocumentFormatEnum, DocumentFormatEnum>> pairs = new List<KeyValuePair<DocumentFormatEnum, DocumentFormatEnum>>();
            foreach (DocumentFormatEnum from in BuiltInInputs)
                foreach (DocumentFormatEnum to in BuiltInOutputs)
                    pairs.Add(new KeyValuePair<DocumentFormatEnum, DocumentFormatEnum>(from, to));
            return pairs;
        }

        private static bool IsImage(DocumentFormatEnum format)
        {
            return format == DocumentFormatEnum.Png || format == DocumentFormatEnum.Jpeg || format == DocumentFormatEnum.Gif
                || format == DocumentFormatEnum.Bmp || format == DocumentFormatEnum.Tiff || format == DocumentFormatEnum.WebP;
        }
    }
}
