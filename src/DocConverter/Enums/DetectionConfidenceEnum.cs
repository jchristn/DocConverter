namespace DocConverter.Enums
{
    /// <summary>
    /// How a detected format was determined.
    /// </summary>
    public enum DetectionConfidenceEnum
    {
        /// <summary>
        /// A binary signature (magic bytes) identified the format.
        /// </summary>
        Signature,

        /// <summary>
        /// The internal structure (for example zip parts or a successful parse) identified the format.
        /// </summary>
        Structure,

        /// <summary>
        /// Text heuristics identified the format.
        /// </summary>
        Heuristic,

        /// <summary>
        /// The file name hint decided between otherwise ambiguous text formats.
        /// </summary>
        ExtensionHint
    }
}
