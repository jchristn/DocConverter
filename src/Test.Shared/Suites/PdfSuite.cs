namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using Test.Shared.Suites.Pdf;
    using Touchstone.Core;

    /// <summary>
    /// PDF reader and writer tests.
    /// </summary>
    public static class PdfSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(PdfReaderCases.Build());
            cases.AddRange(PdfWriterCases.Build());
            return new TestSuiteDescriptor(
                suiteId: "Pdf",
                displayName: "PDF reader and writer",
                cases: cases);
        }
    }
}
