namespace DocConverter.Model
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The intermediate document every conversion passes through. Readers produce it and writers consume it.
    /// Collections are never null: assigning null stores an empty collection.
    /// Not thread safe: do not mutate an instance from several threads at once.
    /// </summary>
    public class DocumentModel
    {
        private DocumentMetadata _Metadata = new DocumentMetadata();
        private List<Block> _Blocks = new List<Block>();
        private Dictionary<string, BinaryResource> _Resources = new Dictionary<string, BinaryResource>(StringComparer.Ordinal);

        /// <summary>
        /// Document metadata. Never null.
        /// </summary>
        public DocumentMetadata Metadata
        {
            get => _Metadata;
            set => _Metadata = value ?? new DocumentMetadata();
        }

        /// <summary>
        /// Top level blocks in reading order. Never null.
        /// </summary>
        public List<Block> Blocks
        {
            get => _Blocks;
            set => _Blocks = value ?? new List<Block>();
        }

        /// <summary>
        /// Binary resources (images) referenced by id from image blocks and image inlines. Keys are ordinal. Never null.
        /// </summary>
        public Dictionary<string, BinaryResource> Resources
        {
            get => _Resources;
            set => _Resources = value ?? new Dictionary<string, BinaryResource>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Instantiate an empty document.
        /// </summary>
        public DocumentModel()
        {
        }

        /// <summary>
        /// Add a resource and return its id. When the resource has no id, one is assigned ("img1", "img2", and so on).
        /// </summary>
        /// <param name="resource">Resource to add.</param>
        /// <returns>The resource id.</returns>
        /// <exception cref="ArgumentNullException">Thrown when resource is null.</exception>
        public string AddResource(BinaryResource resource)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (string.IsNullOrEmpty(resource.Id))
            {
                int n = _Resources.Count + 1;
                while (_Resources.ContainsKey("img" + n)) n++;
                resource.Id = "img" + n;
            }

            _Resources[resource.Id] = resource;
            return resource.Id;
        }
    }
}
