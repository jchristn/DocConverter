namespace DocConverter.Internal
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using DocConverter.Exceptions;

    /// <summary>
    /// Guards zip based formats (DOCX, XLSX, PPTX) against zip bombs by checking the declared uncompressed sizes of all
    /// entries before a parser inflates them. Also rejects input that is not a readable zip archive.
    /// </summary>
    internal static class ZipSafety
    {
        internal static void EnsureSafe(Stream input, long maxDecompressedBytes, string formatName)
        {
            long start = input.Position;
            try
            {
                using (ZipArchive archive = new ZipArchive(input, ZipArchiveMode.Read, true))
                {
                    long total = 0;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        total += entry.Length;
                        if (total > maxDecompressedBytes)
                            throw new InputTooLargeException(
                                "The " + formatName + " parts decompress to more than MaxDecompressedBytes (" + maxDecompressedBytes + "). The input may be a zip bomb.",
                                maxDecompressedBytes);
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                throw new DocumentReadException("The input is not a valid " + formatName + " file: the zip container is corrupt or truncated.", ex);
            }
            finally
            {
                input.Position = start;
            }
        }
    }
}
