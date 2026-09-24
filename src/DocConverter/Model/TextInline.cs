namespace DocConverter.Model
{
    using DocConverter.Enums;

    /// <summary>
    /// A run of text with a style.
    /// </summary>
    public class TextInline : Inline
    {
        private string _Text = "";

        /// <summary>
        /// Text, unescaped. Never null.
        /// </summary>
        public string Text
        {
            get => _Text;
            set => _Text = value ?? "";
        }

        /// <summary>
        /// Style flags. Default None.
        /// </summary>
        public InlineStyleEnum Style { get; set; } = InlineStyleEnum.None;

        /// <summary>
        /// Instantiate an empty run.
        /// </summary>
        public TextInline()
        {
        }

        /// <summary>
        /// Instantiate a run.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="style">Style flags.</param>
        public TextInline(string? text, InlineStyleEnum style = InlineStyleEnum.None)
        {
            Text = text ?? "";
            Style = style;
        }
    }
}
