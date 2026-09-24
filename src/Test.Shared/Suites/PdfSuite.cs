namespace Test.Shared.Suites
{
    using System.Collections.Generic;
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
            return new TestSuiteDescriptor(
                suiteId: "Pdf",
                displayName: "PDF reader and writer",
                cases: new List<TestCaseDescriptor>());
        }
    }
}
