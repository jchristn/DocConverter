namespace DocConverter.Internal
{
    using System;
    using DocConverter.Enums;

    /// <summary>
    /// Reads format, pixel size and (for JPEG) component count from image headers. Never decodes pixels and uses no
    /// platform imaging APIs, so it behaves identically on every operating system.
    /// </summary>
    internal static class ImageHeaderReader
    {
        internal static ImageInfo? Read(byte[] data)
        {
            if (data == null || data.Length < 8) return null;

            if (IsPng(data)) return ReadPng(data);
            if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return ReadJpeg(data);
            if (data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8') return ReadGif(data);
            if (data[0] == 'B' && data[1] == 'M' && data.Length >= 26) return ReadBmp(data);
            if ((data[0] == 'I' && data[1] == 'I' && data[2] == 42 && data[3] == 0) || (data[0] == 'M' && data[1] == 'M' && data[2] == 0 && data[3] == 42)) return ReadTiff(data);
            if (data.Length >= 16 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P') return ReadWebP(data);
            return null;
        }

        internal static string MediaTypeFor(DocumentFormatEnum format)
        {
            switch (format)
            {
                case DocumentFormatEnum.Png: return "image/png";
                case DocumentFormatEnum.Jpeg: return "image/jpeg";
                case DocumentFormatEnum.Gif: return "image/gif";
                case DocumentFormatEnum.Bmp: return "image/bmp";
                case DocumentFormatEnum.Tiff: return "image/tiff";
                case DocumentFormatEnum.WebP: return "image/webp";
                default: return "application/octet-stream";
            }
        }

        internal static string ExtensionFor(DocumentFormatEnum format)
        {
            switch (format)
            {
                case DocumentFormatEnum.Png: return "png";
                case DocumentFormatEnum.Jpeg: return "jpg";
                case DocumentFormatEnum.Gif: return "gif";
                case DocumentFormatEnum.Bmp: return "bmp";
                case DocumentFormatEnum.Tiff: return "tiff";
                case DocumentFormatEnum.WebP: return "webp";
                default: return "bin";
            }
        }

        private static bool IsPng(byte[] d)
        {
            return d[0] == 0x89 && d[1] == 'P' && d[2] == 'N' && d[3] == 'G' && d[4] == 0x0D && d[5] == 0x0A && d[6] == 0x1A && d[7] == 0x0A;
        }

        private static ImageInfo Create(DocumentFormatEnum format)
        {
            return new ImageInfo { Format = format, MediaType = MediaTypeFor(format) };
        }

        private static ImageInfo ReadPng(byte[] d)
        {
            ImageInfo info = Create(DocumentFormatEnum.Png);
            if (d.Length >= 24)
            {
                info.Width = BigEndian32(d, 16);
                info.Height = BigEndian32(d, 20);
            }

            return info;
        }

        private static ImageInfo ReadJpeg(byte[] d)
        {
            ImageInfo info = Create(DocumentFormatEnum.Jpeg);
            int i = 2;
            while (i + 9 < d.Length)
            {
                if (d[i] != 0xFF)
                {
                    i++;
                    continue;
                }

                byte marker = d[i + 1];
                if (marker == 0xFF)
                {
                    i++;
                    continue;
                }

                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    i += 2;
                    continue;
                }

                if (marker == 0xD9 || marker == 0xDA) break;

                int length = (d[i + 2] << 8) | d[i + 3];
                bool isSof = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                if (isSof)
                {
                    info.Height = (d[i + 5] << 8) | d[i + 6];
                    info.Width = (d[i + 7] << 8) | d[i + 8];
                    info.JpegComponents = d[i + 9];
                    break;
                }

                if (length < 2) break;
                i += 2 + length;
            }

            return info;
        }

        private static ImageInfo ReadGif(byte[] d)
        {
            ImageInfo info = Create(DocumentFormatEnum.Gif);
            if (d.Length >= 10)
            {
                info.Width = d[6] | (d[7] << 8);
                info.Height = d[8] | (d[9] << 8);
            }

            return info;
        }

        private static ImageInfo ReadBmp(byte[] d)
        {
            ImageInfo info = Create(DocumentFormatEnum.Bmp);
            int headerSize = LittleEndian32(d, 14);
            if (headerSize == 12)
            {
                info.Width = d[18] | (d[19] << 8);
                info.Height = d[20] | (d[21] << 8);
            }
            else
            {
                info.Width = LittleEndian32(d, 18);
                info.Height = Math.Abs(LittleEndian32(d, 22));
            }

            return info;
        }

        private static ImageInfo ReadTiff(byte[] d)
        {
            ImageInfo info = Create(DocumentFormatEnum.Tiff);
            bool little = d[0] == 'I';
            int ifd = little ? LittleEndian32(d, 4) : BigEndian32(d, 4);
            if (ifd <= 0 || ifd + 2 > d.Length) return info;

            int entries = little ? LittleEndian16(d, ifd) : BigEndian16(d, ifd);
            for (int e = 0; e < entries; e++)
            {
                int p = ifd + 2 + e * 12;
                if (p + 12 > d.Length) break;
                int tag = little ? LittleEndian16(d, p) : BigEndian16(d, p);
                int type = little ? LittleEndian16(d, p + 2) : BigEndian16(d, p + 2);
                int value = type == 3
                    ? (little ? LittleEndian16(d, p + 8) : BigEndian16(d, p + 8))
                    : (little ? LittleEndian32(d, p + 8) : BigEndian32(d, p + 8));
                if (tag == 256) info.Width = value;
                else if (tag == 257) info.Height = value;
            }

            return info;
        }

        private static ImageInfo ReadWebP(byte[] d)
        {
            ImageInfo info = Create(DocumentFormatEnum.WebP);
            if (d.Length < 30) return info;
            string chunk = new string(new char[] { (char)d[12], (char)d[13], (char)d[14], (char)d[15] });
            if (chunk == "VP8 ")
            {
                info.Width = (d[26] | (d[27] << 8)) & 0x3FFF;
                info.Height = (d[28] | (d[29] << 8)) & 0x3FFF;
            }
            else if (chunk == "VP8L")
            {
                int b0 = d[21];
                int b1 = d[22];
                int b2 = d[23];
                int b3 = d[24];
                info.Width = 1 + (((b1 & 0x3F) << 8) | b0);
                info.Height = 1 + (((b3 & 0x0F) << 10) | (b2 << 2) | ((b1 & 0xC0) >> 6));
            }
            else if (chunk == "VP8X")
            {
                info.Width = 1 + (d[24] | (d[25] << 8) | (d[26] << 16));
                info.Height = 1 + (d[27] | (d[28] << 8) | (d[29] << 16));
            }

            return info;
        }

        private static int BigEndian32(byte[] d, int o)
        {
            if (o + 4 > d.Length) return 0;
            return (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];
        }

        private static int LittleEndian32(byte[] d, int o)
        {
            if (o + 4 > d.Length) return 0;
            return d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24);
        }

        private static int BigEndian16(byte[] d, int o)
        {
            if (o + 2 > d.Length) return 0;
            return (d[o] << 8) | d[o + 1];
        }

        private static int LittleEndian16(byte[] d, int o)
        {
            if (o + 2 > d.Length) return 0;
            return d[o] | (d[o + 1] << 8);
        }
    }
}
