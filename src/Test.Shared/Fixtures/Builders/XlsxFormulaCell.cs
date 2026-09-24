namespace Test.Shared.Fixtures.Builders
{
    /// <summary>
    /// A formula cell with its cached value.
    /// </summary>
    public sealed class XlsxFormulaCell
    {
        /// <summary>Formula text.</summary>
        public string Formula { get; }

        /// <summary>Cached value.</summary>
        public string Cached { get; }

        /// <summary>Instantiate.</summary>
        /// <param name="formula">Formula.</param>
        /// <param name="cached">Cached value.</param>
        public XlsxFormulaCell(string formula, string cached)
        {
            Formula = formula;
            Cached = cached;
        }
    }
}
