namespace DocConverter.Results
{
    /// <summary>
    /// Outcome of a conversion that returns its output as a string.
    /// </summary>
    public class StringConversionResult : ConversionResult
    {
        private string _Output = "";

        /// <summary>
        /// The converted document. For text based targets this is the text; for binary targets it is base64. Never null.
        /// </summary>
        public string Output
        {
            get => _Output;
            internal set => _Output = value ?? "";
        }

        /// <summary>
        /// True when Output is base64 because the target format is binary.
        /// </summary>
        public bool IsBase64 { get; internal set; } = false;

        /// <summary>
        /// Instantiate an empty result.
        /// </summary>
        public StringConversionResult()
        {
        }
    }
}
