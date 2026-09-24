namespace DocConverter.Model
{
    using System.Collections.Generic;
    using DocConverter.Enums;

    /// <summary>
    /// An ordered, unordered or task list.
    /// </summary>
    public class ListBlock : Block
    {
        private int _Start = 1;
        private List<ListItemBlock> _Items = new List<ListItemBlock>();

        /// <summary>
        /// Kind of list. Default Unordered.
        /// </summary>
        public ListKindEnum Kind { get; set; } = ListKindEnum.Unordered;

        /// <summary>
        /// First number of an ordered list. Default 1. Values below 0 are stored as 0.
        /// </summary>
        public int Start
        {
            get => _Start;
            set => _Start = value < 0 ? 0 : value;
        }

        /// <summary>
        /// List items. Never null.
        /// </summary>
        public List<ListItemBlock> Items
        {
            get => _Items;
            set => _Items = value ?? new List<ListItemBlock>();
        }

        /// <summary>
        /// Instantiate an empty unordered list.
        /// </summary>
        public ListBlock()
        {
        }

        /// <summary>
        /// Instantiate an empty list of the given kind.
        /// </summary>
        /// <param name="kind">Kind of list.</param>
        public ListBlock(ListKindEnum kind)
        {
            Kind = kind;
        }
    }
}
