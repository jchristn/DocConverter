namespace DocConverter.Model
{
    using System.Collections.Generic;

    /// <summary>
    /// A hyperlink wrapping inline content.
    /// </summary>
    public class LinkInline : Inline
    {
        private string _Url = "";
        private List<Inline> _Inlines = new List<Inline>();

        /// <summary>
        /// Target URL, unescaped. Never null.
        /// </summary>
        public string Url
        {
            get => _Url;
            set => _Url = value ?? "";
        }

        /// <summary>
        /// Link title (tooltip). Null when absent.
        /// </summary>
        public string? Title { get; set; } = null;

        /// <summary>
        /// Link content. Never null.
        /// </summary>
        public List<Inline> Inlines
        {
            get => _Inlines;
            set => _Inlines = value ?? new List<Inline>();
        }

        /// <summary>
        /// Instantiate an empty link.
        /// </summary>
        public LinkInline()
        {
        }

        /// <summary>
        /// Instantiate a link with plain text.
        /// </summary>
        /// <param name="url">Target URL.</param>
        /// <param name="text">Link text. When null or empty the URL is used.</param>
        public LinkInline(string? url, string? text)
        {
            Url = url ?? "";
            _Inlines.Add(new TextInline(string.IsNullOrEmpty(text) ? Url : text));
        }
    }
}
