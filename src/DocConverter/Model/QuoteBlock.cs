namespace DocConverter.Model
{
    using System.Collections.Generic;

    /// <summary>
    /// A block quotation.
    /// </summary>
    public class QuoteBlock : Block
    {
        private List<Block> _Blocks = new List<Block>();

        /// <summary>
        /// Quoted blocks. Never null.
        /// </summary>
        public List<Block> Blocks
        {
            get => _Blocks;
            set => _Blocks = value ?? new List<Block>();
        }

        /// <summary>
        /// Instantiate an empty quote.
        /// </summary>
        public QuoteBlock()
        {
        }
    }
}
