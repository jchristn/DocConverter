namespace DocConverter.Model
{
    using System.Collections.Generic;
    using DocConverter.Enums;

    /// <summary>
    /// A group of blocks: a page, a slide, a worksheet, or a generic grouping of nested data.
    /// </summary>
    public class SectionBlock : Block
    {
        private List<Block> _Blocks = new List<Block>();

        /// <summary>
        /// Kind of section. Default Generic.
        /// </summary>
        public SectionKindEnum Kind { get; set; } = SectionKindEnum.Generic;

        /// <summary>
        /// Section title, for example a worksheet name or a slide title. Null when absent.
        /// </summary>
        public string? Title { get; set; } = null;

        /// <summary>
        /// Child blocks in reading order. Never null.
        /// </summary>
        public List<Block> Blocks
        {
            get => _Blocks;
            set => _Blocks = value ?? new List<Block>();
        }

        /// <summary>
        /// Instantiate an empty generic section.
        /// </summary>
        public SectionBlock()
        {
        }

        /// <summary>
        /// Instantiate a section of the given kind and title.
        /// </summary>
        /// <param name="kind">Kind of section.</param>
        /// <param name="title">Title, or null.</param>
        public SectionBlock(SectionKindEnum kind, string? title)
        {
            Kind = kind;
            Title = title;
        }
    }
}
