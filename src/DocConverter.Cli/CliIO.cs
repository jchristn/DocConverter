namespace DocConverter.Cli
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Exceptions;

    /// <summary>
    /// Binary safe input and output helpers for the CLI. Documents always travel as raw bytes; only reports, help and
    /// summaries are text, written as UTF-8 without a byte order mark.
    /// </summary>
    public static class CliIO
    {
        /// <summary>
        /// Write UTF-8 text to a stream and flush it.
        /// </summary>
        /// <param name="stream">Destination stream.</param>
        /// <param name="text">Text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task WriteTextAsync(Stream stream, string text, CancellationToken token)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            await stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Read a stream to its end, refusing to read more than maxBytes.
        /// </summary>
        /// <param name="stream">Source stream.</param>
        /// <param name="maxBytes">Largest accepted size.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All bytes read.</returns>
        /// <exception cref="InputTooLargeException">Thrown when the stream holds more than maxBytes.</exception>
        public static async Task<byte[]> ReadAllAsync(Stream stream, long maxBytes, CancellationToken token)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buffer = new byte[81920];
                long total = 0;
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (read <= 0) break;
                    total += read;
                    if (total > maxBytes)
                        throw new InputTooLargeException("The input exceeds the size limit of " + FormatSize(maxBytes) + " (--max-input-mb).", maxBytes);
                    ms.Write(buffer, 0, read);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Read the input named on the command line: a file path, or "-" for stdin.
        /// </summary>
        /// <param name="path">Path or "-".</param>
        /// <param name="maxBytes">Largest accepted size.</param>
        /// <param name="stdin">Standard input stream.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Input bytes.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="InputTooLargeException">Thrown when the input is larger than maxBytes.</exception>
        public static async Task<byte[]> ReadInputAsync(string path, long maxBytes, Stream stdin, CancellationToken token)
        {
            if (path == "-") return await ReadAllAsync(stdin, maxBytes, token).ConfigureAwait(false);

            FileInfo info = new FileInfo(path);
            if (!info.Exists)
            {
                if (Directory.Exists(path)) throw new FileNotFoundException("The input path is a directory, not a file: " + path, path);
                throw new FileNotFoundException("The input file was not found: " + path, path);
            }

            if (info.Length > maxBytes)
                throw new InputTooLargeException("The input file is " + FormatSize(info.Length) + ", larger than the limit of " + FormatSize(maxBytes) + " (--max-input-mb).", maxBytes);

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            {
                return await ReadAllAsync(fs, maxBytes, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Human readable size: "512 B", "48.2 KB", "3.1 MB".
        /// </summary>
        /// <param name="bytes">Size in bytes.</param>
        /// <returns>Formatted size.</returns>
        public static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes.ToString(CultureInfo.InvariantCulture) + " B";
            if (bytes < 1048576) return (bytes / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
            if (bytes < 1073741824) return (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
            return (bytes / 1073741824.0).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
        }

        /// <summary>
        /// Name of an input or output for messages and reports: the path as given, or "stdin"/"stdout" for "-".
        /// </summary>
        /// <param name="path">Path or "-".</param>
        /// <param name="isInput">True for the input.</param>
        /// <returns>Display name.</returns>
        public static string DisplayName(string? path, bool isInput)
        {
            if (path == null) return "";
            if (path == "-") return isInput ? "stdin" : "stdout";
            return path;
        }

        /// <summary>
        /// Write a line to the error writer and flush, ignoring failures of the writer itself.
        /// </summary>
        /// <param name="stderr">Error writer.</param>
        /// <param name="line">Line.</param>
        /// <returns>Task.</returns>
        public static async Task ErrorLineAsync(TextWriter stderr, string line)
        {
            try
            {
                await stderr.WriteAsync(line + "\n").ConfigureAwait(false);
                await stderr.FlushAsync().ConfigureAwait(false);
            }
            catch (IOException)
            {
                // A closed stderr must not turn a result into a crash.
            }
            catch (ObjectDisposedException)
            {
                // Same as above.
            }
        }
    }
}
