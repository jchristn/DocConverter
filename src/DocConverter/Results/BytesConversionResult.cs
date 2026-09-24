namespace DocConverter.Results
{
    using System;

    /// <summary>
    /// Outcome of a conversion that returns its output as a byte array.
    /// </summary>
    public class BytesConversionResult : ConversionResult
    {
        private byte[] _Output = Array.Empty<byte>();

        /// <summary>
        /// The converted document. Text based targets are encoded with ConversionOptions.OutputEncoding. Never null.
        /// </summary>
        public byte[] Output
        {
            get => _Output;
            internal set => _Output = value ?? Array.Empty<byte>();
        }

        /// <summary>
        /// Instantiate an empty result.
        /// </summary>
        public BytesConversionResult()
        {
        }
    }
}
