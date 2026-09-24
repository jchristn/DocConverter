namespace DocConverter.Cli
{
    /// <summary>
    /// Commands understood by docconv.
    /// </summary>
    public enum CliCommandEnum
    {
        /// <summary>
        /// No command was given.
        /// </summary>
        None,

        /// <summary>
        /// Convert a document.
        /// </summary>
        Convert,

        /// <summary>
        /// Detect the format of a document.
        /// </summary>
        Detect,

        /// <summary>
        /// List formats and supported conversions.
        /// </summary>
        Formats,

        /// <summary>
        /// Print help.
        /// </summary>
        Help,

        /// <summary>
        /// Print the version.
        /// </summary>
        Version
    }
}
