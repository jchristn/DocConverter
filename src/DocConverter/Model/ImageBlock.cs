namespace DocConverter.Model
{
    /// <summary>
    /// A block level image referencing a resource in DocumentModel.Resources.
    /// </summary>
    public class ImageBlock : Block
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
        /// Caption. Null when absent.
        /// </summary>
        public string? Caption { get; set; } = null;

        /// <summary>
        /// Display width in points. Null to derive from the pixel size.
        /// </summary>
        public double? Width { get; set; } = null;

        /// <summary>
        /// Display height in points. Null to derive from the pixel size.
        /// </summary>
        public double? Height { get; set; } = null;

        /// <summary>
        /// Instantiate an image block with no resource.
        /// </summary>
        public ImageBlock()
        {
        }

        /// <summary>
        /// Instantiate an image block.
        /// </summary>
        /// <param name="resourceId">Id of the referenced resource.</param>
        /// <param name="altText">Alternative text, or null.</param>
        public ImageBlock(string resourceId, string? altText)
        {
            ResourceId = resourceId;
            AltText = altText;
        }
    }
}
