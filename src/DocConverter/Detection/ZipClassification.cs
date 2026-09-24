namespace DocConverter.Detection
{
    using DocConverter.Enums;

    /// <summary>
    /// Outcome of classifying zip based content.
    /// </summary>
    internal sealed class ZipClassification
    {
        internal DocumentFormatEnum? Format { get; set; }

        internal string Description { get; set; } = "zip archive";
    }
}
