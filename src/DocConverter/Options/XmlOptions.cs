namespace DocConverter.Options
{
    using DocConverter.Enums;
    using DocConverter.Exceptions;

    /// <summary>
    /// Options for reading and writing XML.
    /// </summary>
    public class XmlOptions
    {
        /// <summary>
        /// When true, output is indented. Default true.
        /// </summary>
        public bool Indented { get; set; } = true;

        /// <summary>
        /// When true, resource bytes are written as base64; when false only resource metadata is written. Default true.
        /// </summary>
        public bool IncludeBinary { get; set; } = true;

        /// <summary>
        /// When reading arbitrary XML, include attributes as data. Default true.
        /// </summary>
        public bool IncludeAttributes { get; set; } = true;
    }
}
