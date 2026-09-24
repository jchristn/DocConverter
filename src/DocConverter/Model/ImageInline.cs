namespace DocConverter.Model
{
    /// <summary>
    /// An inline image referencing a resource in DocumentModel.Resources.
    /// </summary>
    public class ImageInline : Inline
    {
        private string _ResourceId = "";

        /// <summary>
        /// Id of the referenced resource. Never null.
        /// </summary>
        public string ResourceId
        {
            get => _ResourceId;
            set => _ResourceId = value ?? "";
        }

        /// <summary>
        /// Alternative text. Null when absent.
        /// </summary>
        public string? AltText { get; set; } = null;

        /// <summary>
        /// Instantiate an inline image with no resource.
        /// </summary>
        public ImageInline()
        {
        }

        /// <summary>
        /// Instantiate an inline image.
        /// </summary>
        /// <param name="resourceId">Id of the referenced resource.</param>
        /// <param name="altText">Alternative text, or null.</param>
        public ImageInline(string resourceId, string? altText)
        {
            ResourceId = resourceId;
            AltText = altText;
        }
    }
}
