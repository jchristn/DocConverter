namespace DocConverter.Detection
{
    using System;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Results;

    /// <summary>
    /// Detects the format of in-memory content. Binary signatures are checked first, then zip and OLE structure, then
    /// text heuristics. For binary formats the content decides; for ambiguous text formats a file name hint decides.
    /// Nothing is written to disk. Thread safe.
    /// </summary>
    internal static class FormatDetector
    {
        internal static DetectionResult Detect(byte[] d, int offset, int length, string? fileNameHint, int textSampleBytes)
        {
            DocumentFormatEnum? hint = DocumentFormatParser.FromExtension(fileNameHint);

            if (length == 0)
            {
                if (hint.HasValue && DocumentFormatParser.IsTextBased(hint.Value)) return Supported(hint.Value, "empty " + hint.Value + " document", DetectionConfidenceEnum.ExtensionHint);
                return Supported(DocumentFormatEnum.Text, "empty text", DetectionConfidenceEnum.Heuristic);
            }

            DetectionResult? binary = DetectBinary(d, offset, length);
            if (binary != null) return binary;

            if (TextFormatHeuristics.LooksBinary(d, offset, length, textSampleBytes) && !TextEncodingDetector.HasUnicodeBom(d, offset, length) && !TextEncodingDetector.LooksLikeUtf16WithoutBom(d, offset, length))
                return Unsupported("unrecognized binary data", DetectionConfidenceEnum.Heuristic);

            return DetectText(d, offset, length, hint, textSampleBytes);
        }

        private static DetectionResult? DetectBinary(byte[] d, int offset, int length)
        {
            if (StartsWith(d, offset, length, "%PDF-") || FindWithin(d, offset, length, "%PDF-", 1024))
                return Supported(DocumentFormatEnum.Pdf, "PDF document", DetectionConfidenceEnum.Signature);

            byte[] head = new byte[Math.Min(length, 64)];
            Buffer.BlockCopy(d, offset, head, 0, head.Length);
            if (head.Length >= 8)
            {
                ImageInfo? image = ImageHeaderReader.Read(head);
                if (image != null) return Supported(image.Format, image.FormatName + " image", DetectionConfidenceEnum.Signature);
            }

            if (length >= 4 && d[offset] == 0x00 && d[offset + 1] == 0x00 && d[offset + 2] == 0x01 && d[offset + 3] == 0x00)
                return Unsupported("ICO icon image", DetectionConfidenceEnum.Signature);

            if (length >= 4 && d[offset] == 'P' && d[offset + 1] == 'K' && (d[offset + 2] == 3 || d[offset + 2] == 5 || d[offset + 2] == 7))
            {
                ZipClassification zip = ZipPartInspector.Classify(d, offset, length);
                if (zip.Format.HasValue) return Supported(zip.Format.Value, zip.Description, DetectionConfidenceEnum.Structure);
                return Unsupported(zip.Description, DetectionConfidenceEnum.Structure);
            }

            if (OleDirectoryInspector.IsOle(d, offset, length))
                return Unsupported(OleDirectoryInspector.Describe(d, offset, length), DetectionConfidenceEnum.Structure);

            if (StartsWith(d, offset, length, "{\\rtf"))
                return Supported(DocumentFormatEnum.Rtf, "RTF document", DetectionConfidenceEnum.Signature);

            if (length >= 2 && d[offset] == 0x1F && d[offset + 1] == 0x8B) return Unsupported("gzip archive", DetectionConfidenceEnum.Signature);
            if (length >= 6 && d[offset] == '7' && d[offset + 1] == 'z' && d[offset + 2] == 0xBC && d[offset + 3] == 0xAF) return Unsupported("7-Zip archive", DetectionConfidenceEnum.Signature);
            if (StartsWith(d, offset, length, "Rar!")) return Unsupported("RAR archive", DetectionConfidenceEnum.Signature);
            if (StartsWith(d, offset, length, "PAR1")) return Unsupported("Parquet file", DetectionConfidenceEnum.Signature);
            if (StartsWith(d, offset, length, "SQLite format 3")) return Unsupported("SQLite database", DetectionConfidenceEnum.Signature);
            if (StartsWith(d, offset, length, "%!PS")) return Unsupported("PostScript document", DetectionConfidenceEnum.Signature);
            if (StartsWith(d, offset, length, "ID3")) return Unsupported("MP3 audio", DetectionConfidenceEnum.Signature);
            if (StartsWith(d, offset, length, "MZ")) return Unsupported("Windows executable", DetectionConfidenceEnum.Signature);
            if (length >= 12 && d[offset + 4] == 'f' && d[offset + 5] == 't' && d[offset + 6] == 'y' && d[offset + 7] == 'p') return Unsupported("MP4 or QuickTime media", DetectionConfidenceEnum.Signature);
            if (length >= 262 && d[offset + 257] == 'u' && d[offset + 258] == 's' && d[offset + 259] == 't' && d[offset + 260] == 'a' && d[offset + 261] == 'r') return Unsupported("tar archive", DetectionConfidenceEnum.Signature);
            return null;
        }

        private static DetectionResult DetectText(byte[] d, int offset, int length, DocumentFormatEnum? hint, int textSampleBytes)
        {
            string text = TextEncodingDetector.Decode(d, offset, length, null);
            string trimmed = text.TrimStart('﻿', ' ', '\t', '\r', '\n');
            string sample = text.Length > textSampleBytes ? text.Substring(0, textSampleBytes) : text;

            if (trimmed.StartsWith("{\\rtf", StringComparison.Ordinal))
                return Supported(DocumentFormatEnum.Rtf, "RTF document", DetectionConfidenceEnum.Signature);

            if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(trimmed);
                if (TextFormatHeuristics.IsJson(utf8, 0, utf8.Length))
                    return Supported(DocumentFormatEnum.Json, "JSON document", DetectionConfidenceEnum.Structure);
            }

            if (trimmed.StartsWith("<", StringComparison.Ordinal))
            {
                if (TextFormatHeuristics.LooksLikeHtml(trimmed) && !(hint.HasValue && hint.Value == DocumentFormatEnum.Xml))
                    return Supported(DocumentFormatEnum.Html, "HTML document", DetectionConfidenceEnum.Heuristic);

                string? root = TextFormatHeuristics.XmlRootName(trimmed);
                if (root != null)
                {
                    if (string.Equals(root, "html", StringComparison.OrdinalIgnoreCase) && !(hint.HasValue && hint.Value == DocumentFormatEnum.Xml))
                        return Supported(DocumentFormatEnum.Html, "XHTML document", DetectionConfidenceEnum.Structure);
                    return Supported(DocumentFormatEnum.Xml, "XML document", DetectionConfidenceEnum.Structure);
                }
            }

            if (hint.HasValue)
            {
                DocumentFormatEnum h = hint.Value;
                if (h == DocumentFormatEnum.Text || h == DocumentFormatEnum.Markdown || h == DocumentFormatEnum.Csv || h == DocumentFormatEnum.Tsv || h == DocumentFormatEnum.Html)
                    return Supported(h, h + " document (from file extension)", DetectionConfidenceEnum.ExtensionHint);
            }

            if (TextFormatHeuristics.LooksLikeHtml(trimmed))
                return Supported(DocumentFormatEnum.Html, "HTML document", DetectionConfidenceEnum.Heuristic);

            if (sample.IndexOf('\t') >= 0 && TextFormatHeuristics.IsDelimited(sample, '\t'))
                return Supported(DocumentFormatEnum.Tsv, "tab separated values", DetectionConfidenceEnum.Heuristic);

            if (TextFormatHeuristics.MarkdownScore(sample) >= TextFormatHeuristics.MarkdownThreshold)
                return Supported(DocumentFormatEnum.Markdown, "Markdown document", DetectionConfidenceEnum.Heuristic);

            if (sample.IndexOf(',') >= 0 && TextFormatHeuristics.IsDelimited(sample, ','))
                return Supported(DocumentFormatEnum.Csv, "comma separated values", DetectionConfidenceEnum.Heuristic);

            return Supported(DocumentFormatEnum.Text, "plain text", DetectionConfidenceEnum.Heuristic);
        }

        private static DetectionResult Supported(DocumentFormatEnum format, string description, DetectionConfidenceEnum confidence)
        {
            return new DetectionResult
            {
                Format = format,
                MediaType = DocumentFormatParser.GetMediaType(format),
                Extension = DocumentFormatParser.GetDefaultExtension(format),
                RecognizedAs = description,
                Confidence = confidence
            };
        }

        private static DetectionResult Unsupported(string description, DetectionConfidenceEnum confidence)
        {
            return new DetectionResult
            {
                Format = null,
                MediaType = "application/octet-stream",
                Extension = "",
                RecognizedAs = description,
                Confidence = confidence
            };
        }

        private static bool StartsWith(byte[] d, int offset, int length, string ascii)
        {
            if (length < ascii.Length) return false;
            for (int i = 0; i < ascii.Length; i++)
                if (d[offset + i] != (byte)ascii[i]) return false;
            return true;
        }

        private static bool FindWithin(byte[] d, int offset, int length, string ascii, int window)
        {
            int end = Math.Min(length, window) - ascii.Length;
            for (int i = 0; i <= end; i++)
            {
                bool match = true;
                for (int j = 0; j < ascii.Length; j++)
                {
                    if (d[offset + i + j] != (byte)ascii[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match) return true;
            }

            return false;
        }
    }
}
