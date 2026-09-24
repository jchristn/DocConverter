namespace DocConverter.Internal
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Options;

    /// <summary>
    /// Reading and writing of whole text documents for the text based readers and writers.
    /// </summary>
    internal static class TextIO
    {
        internal static async Task<string> ReadAllTextAsync(Stream input, ConversionOptions options, ConversionContext context, CancellationToken token)
        {
            if (context.SourceText != null) return context.SourceText;

            byte[] data;
            if (input is MemoryStream memory && memory.TryGetBuffer(out ArraySegment<byte> segment))
            {
                int start = segment.Offset + (int)memory.Position;
                int count = (int)(memory.Length - memory.Position);
                return TextEncodingDetector.Decode(segment.Array!, start, count, options.InputEncoding);
            }

            using (MemoryStream ms = new MemoryStream())
            {
                await input.CopyToAsync(ms, 81920, token).ConfigureAwait(false);
                data = ms.ToArray();
            }

            return TextEncodingDetector.Decode(data, 0, data.Length, options.InputEncoding);
        }

        internal static async Task WriteAllTextAsync(string text, Stream output, ConversionOptions options, CancellationToken token)
        {
            string normalized = NormalizeLineEndings(text, options.LineEnding);
            byte[] bytes = options.OutputEncoding.GetBytes(normalized);
            byte[] preamble = options.OutputEncoding.GetPreamble();
            if (preamble.Length > 0) await output.WriteAsync(preamble, 0, preamble.Length, token).ConfigureAwait(false);
            await output.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
        }

        internal static string NormalizeLineEndings(string text, LineEndingEnum lineEnding)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string lf = text.Replace("\r\n", "\n").Replace("\r", "\n");
            switch (lineEnding)
            {
                case LineEndingEnum.CrLf:
                    return lf.Replace("\n", "\r\n");
                case LineEndingEnum.Platform:
                    return Environment.NewLine == "\n" ? lf : lf.Replace("\n", Environment.NewLine);
                default:
                    return lf;
            }
        }

        internal static bool IsTextFormat(DocumentFormatEnum format)
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

        internal static string DecodeUtf8(byte[] data)
        {
            return new UTF8Encoding(false).GetString(data);
        }
    }
}
