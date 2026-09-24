namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// XLSX reader and writer tests.
    /// </summary>
    public static class XlsxSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Xlsx",
                displayName: "XLSX reader and writer",
                cases: new List<TestCaseDescriptor>());
        }
    }
}
