namespace DocConverter.Model
{
    using System;

    /// <summary>
    /// Descriptive metadata carried between formats that support it. Every member is optional.
    /// </summary>
    public class DocumentMetadata
    {
        /// <summary>
        /// Title. Null when unknown.
        /// </summary>
        public string? Title { get; set; } = null;

        /// <summary>
        /// Subject. Null when unknown.
        /// </summary>
        public string? Subject { get; set; } = null;

        /// <summary>
        /// Author. Null when unknown.
        /// </summary>
        public string? Author { get; set; } = null;

        /// <summary>
        /// Keywords, as a single comma separated string. Null when unknown.
        /// </summary>
        public string? Keywords { get; set; } = null;

        /// <summary>
        /// Description. Null when unknown.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Language tag, for example "en-US". Null when unknown.
        /// </summary>
        public string? Language { get; set; } = null;

        /// <summary>
        /// Creation timestamp in UTC. Null when unknown.
        /// </summary>
        public DateTime? CreatedUtc { get; set; } = null;

        /// <summary>
        /// Last modification timestamp in UTC. Null when unknown.
        /// </summary>
        public DateTime? ModifiedUtc { get; set; } = null;
    }
}
