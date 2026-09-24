namespace DocConverter.Model
{
    using System.Collections.Generic;

    /// <summary>
    /// One list item. Holds paragraphs and nested lists.
    /// </summary>
    public class ListItemBlock : Block
    {
        private List<Block> _Blocks = new List<Block>();

        /// <summary>
        /// Checked state for task list items. Null for ordinary items.
        /// </summary>
        public bool? Checked { get; set; } = null;

        /// <summary>
        /// Item content: paragraphs and nested lists. Never null.
        /// </summary>
        public List<Block> Blocks
        {
            get => _Blocks;
            set => _Blocks = value ?? new List<Block>();
        }

        /// <summary>
        /// Instantiate an empty item.
        /// </summary>
        public ListItemBlock()
        {
        }

        /// <summary>
        /// Instantiate an item holding one paragraph of plain text.
        /// </summary>
        /// <param name="text">Item text. Null is treated as empty.</param>
        public ListItemBlock(string? text)
        {
            _Blocks.Add(new ParagraphBlock(text));
        }
    }
}
