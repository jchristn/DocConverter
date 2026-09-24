namespace DocConverter.Detection
{
    using System;
    using System.Collections.Generic;
    using DocConverter.Enums;

    /// <summary>
    /// Parses format names, aliases and file extensions, and maps formats to media types and extensions.
    /// Thread safe.
    /// </summary>
    public static class DocumentFormatParser
    {
        private static readonly Dictionary<string, DocumentFormatEnum> _Aliases = BuildAliases();

        /// <summary>
        /// Every accepted alias (lower case), for help text and documentation.
        /// </summary>
        public static IReadOnlyCollection<string> Aliases
        {
            get => _Aliases.Keys;
        }

        /// <summary>
        /// Parse a format name, alias or file extension, case-insensitively. A leading dot is ignored, so "docx", ".docx",
        /// "DOCX" and "word" all parse to Docx.
        /// </summary>
        /// <param name="value">Name, alias or extension. Null or empty fails.</param>
        /// <param name="format">Parsed format.</param>
        /// <returns>True when parsed.</returns>
        public static bool TryParse(string? value, out DocumentFormatEnum format)
        {
            format = DocumentFormatEnum.Auto;
            if (string.IsNullOrWhiteSpace(value)) return false;
            string key = value!.Trim().TrimStart('.').ToLowerInvariant();
            return _Aliases.TryGetValue(key, out format);
        }

        /// <summary>
        /// Parse a format name, alias or extension, or throw.
        /// </summary>
        /// <param name="value">Name, alias or extension.</param>
        /// <returns>Parsed format.</returns>
        /// <exception cref="ArgumentException">Thrown when the value is not a known format.</exception>
        public static DocumentFormatEnum Parse(string? value)
        {
            if (TryParse(value, out DocumentFormatEnum format)) return format;
            throw new ArgumentException("'" + value + "' is not a known document format. Known names: " + string.Join(", ", KnownNames()) + ".", nameof(value));
        }

        /// <summary>
        /// Format implied by a file name or path's extension. Null when there is no extension or it is unknown.
        /// "auto" is never returned.
        /// </summary>
        /// <param name="fileName">File name or path.</param>
        /// <returns>Format, or null.</returns>
        public static DocumentFormatEnum? FromExtension(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            string name = fileName!.Trim();
            int slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
            if (slash >= 0) name = name.Substring(slash + 1);
            int dot = name.LastIndexOf('.');
            if (dot < 0 || dot == name.Length - 1) return null;
            string ext = name.Substring(dot + 1).ToLowerInvariant();
            if (ext == "auto") return null;
            if (_Aliases.TryGetValue(ext, out DocumentFormatEnum format)) return format;
            return null;
        }

        /// <summary>
        /// Media type of a format, for example "application/pdf". Auto returns "application/octet-stream".
        /// </summary>
        /// <param name="format">Format.</param>
        /// <returns>Media type.</returns>
        public static string GetMediaType(DocumentFormatEnum format)
        {
            switch (format)
            {
                case DocumentFormatEnum.Text: return "text/plain";
                case DocumentFormatEnum.Markdown: return "text/markdown";
                case DocumentFormatEnum.Html: return "text/html";
                case DocumentFormatEnum.Json: return "application/json";
                case DocumentFormatEnum.Xml: return "application/xml";
                case DocumentFormatEnum.Csv: return "text/csv";
                case DocumentFormatEnum.Tsv: return "text/tab-separated-values";
                case DocumentFormatEnum.Rtf: return "application/rtf";
                case DocumentFormatEnum.Docx: return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case DocumentFormatEnum.Xlsx: return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                case DocumentFormatEnum.Pptx: return "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                case DocumentFormatEnum.Pdf: return "application/pdf";
                case DocumentFormatEnum.Png: return "image/png";
                case DocumentFormatEnum.Jpeg: return "image/jpeg";
                case DocumentFormatEnum.Gif: return "image/gif";
                case DocumentFormatEnum.Bmp: return "image/bmp";
                case DocumentFormatEnum.Tiff: return "image/tiff";
                case DocumentFormatEnum.WebP: return "image/webp";
                default: return "application/octet-stream";
            }
        }

        /// <summary>
        /// Usual file extension of a format without the dot, for example "md". Auto returns "bin".
        /// </summary>
        /// <param name="format">Format.</param>
        /// <returns>Extension.</returns>
        public static string GetDefaultExtension(DocumentFormatEnum format)
        {
            switch (format)
            {
                case DocumentFormatEnum.Text: return "txt";
                case DocumentFormatEnum.Markdown: return "md";
                case DocumentFormatEnum.Html: return "html";
                case DocumentFormatEnum.Json: return "json";
                case DocumentFormatEnum.Xml: return "xml";
                case DocumentFormatEnum.Csv: return "csv";
                case DocumentFormatEnum.Tsv: return "tsv";
                case DocumentFormatEnum.Rtf: return "rtf";
                case DocumentFormatEnum.Docx: return "docx";
                case DocumentFormatEnum.Xlsx: return "xlsx";
                case DocumentFormatEnum.Pptx: return "pptx";
                case DocumentFormatEnum.Pdf: return "pdf";
                case DocumentFormatEnum.Png: return "png";
                case DocumentFormatEnum.Jpeg: return "jpg";
                case DocumentFormatEnum.Gif: return "gif";
                case DocumentFormatEnum.Bmp: return "bmp";
                case DocumentFormatEnum.Tiff: return "tiff";
                case DocumentFormatEnum.WebP: return "webp";
                default: return "bin";
            }
        }

        /// <summary>
        /// True when the format is text based: its content is characters rather than binary data. String input for these
        /// formats is the document itself; for other formats a string must be base64.
        /// </summary>
        /// <param name="format">Format.</param>
        /// <returns>True for Text, Markdown, Html, Json, Xml, Csv, Tsv and Rtf.</returns>
        public static bool IsTextBased(DocumentFormatEnum format)
        {
            switch (format)
            {
                case DocumentFormatEnum.Text:
                case DocumentFormatEnum.Markdown:
                case DocumentFormatEnum.Html:
                case DocumentFormatEnum.Json:
                case DocumentFormatEnum.Xml:
                case DocumentFormatEnum.Csv:
                case DocumentFormatEnum.Tsv:
                case DocumentFormatEnum.Rtf:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// True when the format is an image format.
        /// </summary>
        /// <param name="format">Format.</param>
        /// <returns>True for Png, Jpeg, Gif, Bmp, Tiff and WebP.</returns>
        public static bool IsImage(DocumentFormatEnum format)
        {
            switch (format)
            {
                case DocumentFormatEnum.Png:
                case DocumentFormatEnum.Jpeg:
                case DocumentFormatEnum.Gif:
                case DocumentFormatEnum.Bmp:
                case DocumentFormatEnum.Tiff:
                case DocumentFormatEnum.WebP:
                    return true;
                default:
                    return false;
            }
        }

        private static IEnumerable<string> KnownNames()
        {
            foreach (DocumentFormatEnum value in Enum.GetValues(typeof(DocumentFormatEnum)))
                yield return value.ToString().ToLowerInvariant();
        }

        private static Dictionary<string, DocumentFormatEnum> BuildAliases()
        {
            Dictionary<string, DocumentFormatEnum> map = new Dictionary<string, DocumentFormatEnum>(StringComparer.Ordinal);
            foreach (DocumentFormatEnum value in Enum.GetValues(typeof(DocumentFormatEnum)))
                map[value.ToString().ToLowerInvariant()] = value;

            map["txt"] = DocumentFormatEnum.Text;
            map["plain"] = DocumentFormatEnum.Text;
            map["plaintext"] = DocumentFormatEnum.Text;
            map["md"] = DocumentFormatEnum.Markdown;
            map["mdown"] = DocumentFormatEnum.Markdown;
            map["mkd"] = DocumentFormatEnum.Markdown;
            map["markdn"] = DocumentFormatEnum.Markdown;
            map["htm"] = DocumentFormatEnum.Html;
            map["xhtml"] = DocumentFormatEnum.Html;
            map["tab"] = DocumentFormatEnum.Tsv;
            map["word"] = DocumentFormatEnum.Docx;
            map["excel"] = DocumentFormatEnum.Xlsx;
            map["powerpoint"] = DocumentFormatEnum.Pptx;
            map["jpg"] = DocumentFormatEnum.Jpeg;
            map["jpe"] = DocumentFormatEnum.Jpeg;
            map["tif"] = DocumentFormatEnum.Tiff;
            map["dib"] = DocumentFormatEnum.Bmp;
            return map;
        }
    }
}
