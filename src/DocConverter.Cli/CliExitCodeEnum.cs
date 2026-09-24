namespace DocConverter.Cli
{
    /// <summary>
    /// Process exit codes returned by docconv.
    /// </summary>
    public enum CliExitCodeEnum
    {
        /// <summary>
        /// Success. Warnings are allowed unless --strict is set.
        /// </summary>
        Success = 0,

        /// <summary>
        /// The input could not be read or the output could not be written.
        /// </summary>
        ConversionFailed = 1,

        /// <summary>
        /// Usage error: unknown option, missing argument or invalid value.
        /// </summary>
        Usage = 2,

        /// <summary>
        /// The format is not recognized or not supported, or the conversion pair is not supported.
        /// </summary>
        Unsupported = 3,

        /// <summary>
        /// I/O error: input not found, output exists without --overwrite, or access denied.
        /// </summary>
        IO = 4,

        /// <summary>
        /// Warnings were raised while --strict was set.
        /// </summary>
        Warnings = 5,

        /// <summary>
        /// The input exceeds --max-input-mb.
        /// </summary>
        InputTooLarge = 6,

        /// <summary>
        /// The operation was cancelled (Ctrl+C).
        /// </summary>
        Cancelled = 130
    }
}
