namespace DocConverter.Readers.Docx
{
    using System.Collections.Generic;
    using System.Text;
    using DocConverter.Model;

    /// <summary>
    /// Accumulation state while turning a sequence of Word body elements into blocks: the output, the open lists, and a
    /// pending code block or quote that consecutive paragraphs extend.
    /// </summary>
    internal sealed class DocxBlockState
    {
        internal List<Block> Output { get; } = new List<Block>();

        internal List<DocxListFrame> Lists { get; } = new List<DocxListFrame>();

        internal StringBuilder? Code { get; set; } = null;

        internal QuoteBlock? Quote { get; set; } = null;

        internal int Depth { get; }

        internal DocxBlockState(int depth)
        {
            Depth = depth;
        }
    }
}
