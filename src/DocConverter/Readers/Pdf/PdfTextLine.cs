namespace DocConverter.Readers.Pdf
{
    using System.Collections.Generic;
    using System.Text;
    using UglyToad.PdfPig.Content;

    /// <summary>
    /// One visual line of words on a PDF page, with the font facts used to classify it.
    /// </summary>
    internal sealed class PdfTextLine
    {
        internal List<Word> Words { get; } = new List<Word>();

        internal double Baseline { get; set; }

        internal double Left { get; set; }

        internal double Top { get; set; }

        internal double Bottom { get; set; }

        internal double FontSize { get; set; }

        internal bool Bold { get; set; }

        internal bool Mono { get; set; }

        internal double AverageCharWidth { get; set; }

        internal string Text
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (Word word in Words)
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(word.Text);
                }

                return sb.ToString();
            }
        }
    }
}
