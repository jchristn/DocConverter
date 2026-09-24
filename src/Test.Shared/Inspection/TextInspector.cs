namespace Test.Shared.Inspection
{
    using System.Text;

    /// <summary>
    /// Inspects plain text output: strict UTF-8 decoding and whitespace normalized text.
    /// </summary>
    public static class TextInspector
    {
        /// <summary>
        /// Snapshot plain text output.
        /// </summary>
        /// <param name="bytes">UTF-8 text.</param>
        /// <returns>Snapshot.</returns>
        public static ContentSnapshot Inspect(byte[] bytes)
        {
            string text = DecodeStrict(bytes);
            ContentSnapshot snapshot = new ContentSnapshot();
            snapshot.AllText = ContentSnapshot.Normalize(text);
            int index = 0;
            while ((index = text.IndexOf("[Image:", index, System.StringComparison.Ordinal)) >= 0)
            {
                snapshot.ImageCount++;
                index++;
            }

            return snapshot;
        }

        /// <summary>
        /// Decode UTF-8, throwing TestAssertionException on invalid bytes, and strip a byte order mark.
        /// </summary>
        /// <param name="bytes">Bytes.</param>
        /// <returns>Text.</returns>
        public static string DecodeStrict(byte[] bytes)
        {
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes).TrimStart('﻿');
            }
            catch (DecoderFallbackException ex)
            {
                throw new TestAssertionException("Output is not valid UTF-8: " + ex.Message, ex);
            }
        }
    }
}
