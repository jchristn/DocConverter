namespace Test.Shared.Fixtures.Builders
{
    /// <summary>
    /// A date cell value (serial number with a date style).
    /// </summary>
    public sealed class XlsxDateCell
    {
        /// <summary>Serial value.</summary>
        public double Serial { get; }

        /// <summary>Instantiate.</summary>
        /// <param name="serial">Serial value.</param>
        public XlsxDateCell(double serial)
        {
            Serial = serial;
        }
    }
}
