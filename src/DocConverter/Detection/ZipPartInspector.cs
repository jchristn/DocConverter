namespace DocConverter.Detection
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using DocConverter.Enums;

    /// <summary>
    /// Classifies zip based content by reading entry names and small manifest entries only. Nothing is extracted to disk.
    /// </summary>
    internal static class ZipPartInspector
    {
        internal static ZipClassification Classify(byte[] d, int offset, int length)
        {
            ZipClassification result = new ZipClassification();
            try
            {
                using (MemoryStream ms = new MemoryStream(d, offset, length, false))
                using (ZipArchive archive = new ZipArchive(ms, ZipArchiveMode.Read, false))
                {
                    HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (ZipArchiveEntry entry in archive.Entries) names.Add(entry.FullName.Replace('\\', '/'));

                    ZipArchiveEntry? contentTypes = archive.GetEntry("[Content_Types].xml");
                    if (contentTypes != null)
                    {
                        string xml = ReadSmall(contentTypes, 1048576);
                        if (xml.IndexOf("wordprocessingml.document.main+xml", StringComparison.OrdinalIgnoreCase) >= 0
                            || xml.IndexOf("wordprocessingml.template.main+xml", StringComparison.OrdinalIgnoreCase) >= 0
                            || xml.IndexOf("ms-word.document.macroEnabled.main+xml", StringComparison.OrdinalIgnoreCase) >= 0
                            || xml.IndexOf("ms-word.template.macroEnabledTemplate.main+xml", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.Format = DocumentFormatEnum.Docx;
                            result.Description = "Word document (Office Open XML)";
                            return result;
                        }

                        if (xml.IndexOf("spreadsheetml.sheet.main+xml", StringComparison.OrdinalIgnoreCase) >= 0
                            || xml.IndexOf("spreadsheetml.template.main+xml", StringComparison.OrdinalIgnoreCase) >= 0
                            || xml.IndexOf("ms-excel.sheet.macroEnabled.main+xml", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.Format = DocumentFormatEnum.Xlsx;
                            result.Description = "Excel workbook (Office Open XML)";
                            return result;
                        }

                        if (xml.IndexOf("presentationml.presentation.main+xml", StringComparison.OrdinalIgnoreCase) >= 0
                            || xml.IndexOf("presentationml.slideshow.main+xml", StringComparison.OrdinalIgnoreCase) >= 0
                            || xml.IndexOf("presentationml.template.main+xml", StringComparison.OrdinalIgnoreCase) >= 0
                            || xml.IndexOf("ms-powerpoint.presentation.macroEnabled.main+xml", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.Format = DocumentFormatEnum.Pptx;
                            result.Description = "PowerPoint presentation (Office Open XML)";
                            return result;
                        }

                        if (names.Contains("word/document.xml"))
                        {
                            result.Format = DocumentFormatEnum.Docx;
                            result.Description = "Word document (Office Open XML)";
                            return result;
                        }

                        if (names.Contains("xl/workbook.xml"))
                        {
                            result.Format = DocumentFormatEnum.Xlsx;
                            result.Description = "Excel workbook (Office Open XML)";
                            return result;
                        }

                        if (names.Contains("ppt/presentation.xml"))
                        {
                            result.Format = DocumentFormatEnum.Pptx;
                            result.Description = "PowerPoint presentation (Office Open XML)";
                            return result;
                        }

                        result.Description = "Office Open XML package of an unsupported type";
                        return result;
                    }

                    ZipArchiveEntry? mimetype = archive.GetEntry("mimetype");
                    if (mimetype != null)
                    {
                        string mime = ReadSmall(mimetype, 1024).Trim();
                        if (mime == "application/epub+zip") result.Description = "EPUB e-book";
                        else if (mime == "application/vnd.oasis.opendocument.text") result.Description = "OpenDocument text (.odt)";
                        else if (mime == "application/vnd.oasis.opendocument.spreadsheet") result.Description = "OpenDocument spreadsheet (.ods)";
                        else if (mime == "application/vnd.oasis.opendocument.presentation") result.Description = "OpenDocument presentation (.odp)";
                        else result.Description = "zip package (" + mime + ")";
                        return result;
                    }

                    foreach (string name in names)
                    {
                        if (name.StartsWith("Index/", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".iwa", StringComparison.OrdinalIgnoreCase))
                        {
                            result.Description = "Apple iWork document (Pages, Numbers or Keynote)";
                            return result;
                        }
                    }

                    result.Description = "zip archive";
                    return result;
                }
            }
            catch (InvalidDataException)
            {
                result.Description = "damaged or truncated zip archive";
                return result;
            }
        }

        private static string ReadSmall(ZipArchiveEntry entry, int maxBytes)
        {
            using (Stream stream = entry.Open())
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buffer = new byte[8192];
                int total = 0;
                int read;
                while (total < maxBytes && (read = stream.Read(buffer, 0, Math.Min(buffer.Length, maxBytes - total))) > 0)
                {
                    ms.Write(buffer, 0, read);
                    total += read;
                }

                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }
}
