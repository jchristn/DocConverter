namespace DocConverter.Enums
{
    /// <summary>
    /// How a text based writer represents images.
    /// </summary>
    public enum ImageModeEnum
    {
        /// <summary>
        /// Embed the image inline as a base64 data URI.
        /// </summary>
        DataUri,

        /// <summary>
        /// Leave the image out and raise the ImagesOmitted warning.
        /// </summary>
        Omit,

        /// <summary>
        /// Write a short text placeholder that names the image.
        /// </summary>
        Placeholder,

        /// <summary>
        /// Reference the image by a relative file name. The bytes are returned in ConversionResult.Resources.
        /// </summary>
        External
    }
}
