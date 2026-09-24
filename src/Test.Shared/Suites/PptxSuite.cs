namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// PPTX reader and writer tests.
    /// </summary>
    public static class PptxSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Pptx",
                displayName: "PPTX reader and writer",
                cases: new List<TestCaseDescriptor>());
        }
    }
}
