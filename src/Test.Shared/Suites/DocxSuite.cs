namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// DOCX reader and writer tests.
    /// </summary>
    public static class DocxSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Docx",
                displayName: "DOCX reader and writer",
                cases: new List<TestCaseDescriptor>());
        }
    }
}
