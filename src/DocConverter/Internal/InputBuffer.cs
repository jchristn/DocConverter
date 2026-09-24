namespace DocConverter.Internal
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Exceptions;

    /// <summary>
    /// The whole input, held in memory as a byte segment, plus the decoded string when the caller passed one.
    /// </summary>
    internal sealed class InputBuffer
    {
        internal byte[] Data { get; }

        internal int Offset { get; }

        internal int Length { get; }

        internal string? Text { get; }

        private InputBuffer(byte[] data, int offset, int length, string? text)
        {
            Data = data;
            Offset = offset;
            Length = length;
            Text = text;
        }

        internal MemoryStream CreateStream()
        {
            return new MemoryStream(Data, Offset, Length, false, true);
        }

        internal byte[] ToArray()
        {
            if (Offset == 0 && Length == Data.Length) return Data;
            byte[] copy = new byte[Length];
            Buffer.BlockCopy(Data, Offset, copy, 0, Length);
            return copy;
        }

        internal static InputBuffer FromBytes(byte[] data, long maxBytes)
        {
            if (data.LongLength > maxBytes)
                throw new InputTooLargeException("Input is " + data.LongLength + " bytes, larger than MaxInputBytes (" + maxBytes + ").", maxBytes);
            return new InputBuffer(data, 0, data.Length, null);
        }

        internal static InputBuffer FromText(string text, byte[] utf8, long maxBytes)
        {
            if (utf8.LongLength > maxBytes)
                throw new InputTooLargeException("Input is " + utf8.LongLength + " bytes, larger than MaxInputBytes (" + maxBytes + ").", maxBytes);
            return new InputBuffer(utf8, 0, utf8.Length, text);
        }

        internal static async Task<InputBuffer> FromStreamAsync(Stream stream, long maxBytes, CancellationToken token)
        {
            if (!stream.CanRead) throw new ArgumentException("The input stream is not readable.", nameof(stream));

            if (stream.CanSeek)
            {
                long remaining = stream.Length - stream.Position;
                if (remaining > maxBytes)
                    throw new InputTooLargeException("Input is " + remaining + " bytes, larger than MaxInputBytes (" + maxBytes + ").", maxBytes);
            }

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
                        throw new InputTooLargeException("Input exceeds MaxInputBytes (" + maxBytes + ").", maxBytes);
                    ms.Write(buffer, 0, read);
                }

                byte[] data = ms.GetBuffer();
                return new InputBuffer(data, 0, (int)ms.Length, null);
            }
        }
    }
}
