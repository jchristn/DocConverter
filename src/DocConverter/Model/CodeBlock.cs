namespace DocConverter.Model
{
    /// <summary>
    /// A block of preformatted code.
    /// </summary>
    public class CodeBlock : Block
    {
        private string _Text = "";

        /// <summary>
        /// Language hint, for example "csharp". Null when unknown.
        /// </summary>
        public string? Language { get; set; } = null;

        /// <summary>
        /// Code text, with lines separated by \n. Never null.
        /// </summary>
        public string Text
        {
            get => _Text;
            set => _Text = value ?? "";
        }

        /// <summary>
        /// Instantiate an empty code block.
        /// </summary>
        public CodeBlock()
        {
        }

        /// <summary>
        /// Instantiate a code block.
        /// </summary>
        /// <param name="text">Code text.</param>
        /// <param name="language">Language hint, or null.</param>
        public CodeBlock(string? text, string? language)
        {
            Text = text ?? "";
            Language = language;
        }
    }
}
