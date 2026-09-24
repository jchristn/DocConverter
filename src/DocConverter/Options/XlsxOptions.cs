namespace DocConverter.Options
{
    using DocConverter.Enums;
    using DocConverter.Exceptions;

    /// <summary>
    /// Options for reading and writing XLSX.
    /// </summary>
    public class XlsxOptions
    {
        /// <summary>
        /// When reading, include hidden worksheets. Default false.
        /// </summary>
        public bool IncludeHiddenSheets { get; set; } = false;

        /// <summary>
        /// When reading, score rows to find the header row. Default true.
        /// </summary>
        public bool DetectHeaderRow { get; set; } = true;

        /// <summary>
        /// When writing, put non-table blocks on a leading \"Document\" sheet. Default true.
        /// </summary>
        public bool IncludeNonTableContent { get; set; } = true;

        /// <summary>
        /// When writing, store invariant-culture numbers, booleans and ISO dates as typed cells. Default true.
        /// </summary>
        public bool InferCellTypes { get; set; } = true;
    }
}
