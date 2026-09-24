namespace DocConverter.Options
{
    using DocConverter.Enums;
    using DocConverter.Exceptions;

    /// <summary>
    /// Options for reading and writing JSON.
    /// </summary>
    public class JsonOptions
    {
        /// <summary>
        /// When true, output is indented. Default true.
        /// </summary>
        public bool Indented { get; set; } = true;

        /// <summary>
        /// When true, resource bytes are written as base64; when false only resource metadata is written. Default true.
        /// </summary>
        public bool IncludeBinary { get; set; } = true;
    }
}
