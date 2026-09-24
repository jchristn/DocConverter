namespace DocConverter.Enums
{
    /// <summary>
    /// How faithfully a conversion pair carries content.
    /// </summary>
    public enum FidelityEnum
    {
        /// <summary>
        /// Everything the target format can express is carried over.
        /// </summary>
        Full,

        /// <summary>
        /// The target keeps a documented subset or substitutes a documented placeholder, and a warning names what was dropped.
        /// </summary>
        Projection
    }
}
