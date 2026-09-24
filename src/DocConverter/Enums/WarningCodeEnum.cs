namespace DocConverter.Enums
{
    /// <summary>
    /// Codes for conversion warnings. Every code is documented in docs/FORMATS.md with what was lost and how to avoid it.
    /// </summary>
    public enum WarningCodeEnum
    {
        /// <summary>
        /// One or more images were left out of the output.
        /// </summary>
        ImagesOmitted,

        /// <summary>
        /// An image format cannot be embedded in the target and was replaced by a placeholder.
        /// </summary>
        ImageFormatUnsupported,

        /// <summary>
        /// An image-only source went to a target that cannot show images, so a placeholder was written.
        /// </summary>
        ImagePlaceholderEmitted,

        /// <summary>
        /// Inline styles, links, or layout the target cannot express were flattened.
        /// </summary>
        FormattingLost,

        /// <summary>
        /// Tables were rendered as plain rows or text.
        /// </summary>
        TablesFlattened,

        /// <summary>
        /// Merged table cells were repeated or emptied.
        /// </summary>
        TableSpansFlattened,

        /// <summary>
        /// Content outside tables was dropped because the target keeps tables only.
        /// </summary>
        NonTableContentDropped,

        /// <summary>
        /// Nesting deeper than the configured limit was flattened.
        /// </summary>
        NestedDepthLimited,

        /// <summary>
        /// A link with a disallowed URL scheme was removed.
        /// </summary>
        LinkRemovedUnsafe,

        /// <summary>
        /// Headings were inferred from visual cues (font size or weight) rather than declared structure.
        /// </summary>
        HeadingsInferred,

        /// <summary>
        /// Encrypted or protected content could not be read and was skipped.
        /// </summary>
        EncryptedContentSkipped,

        /// <summary>
        /// Speaker notes were included as additional content.
        /// </summary>
        NotesIncluded,

        /// <summary>
        /// Content was truncated to fit a configured limit.
        /// </summary>
        ContentTruncated,

        /// <summary>
        /// Characters outside the embedded PDF font's coverage were rendered blank.
        /// </summary>
        GlyphsUnavailable,

        /// <summary>
        /// A PDF page had no extractable text. Optical character recognition would be required.
        /// </summary>
        NoTextLayer,

        /// <summary>
        /// An element the reader does not understand was skipped.
        /// </summary>
        UnknownElementSkipped
    }
}
