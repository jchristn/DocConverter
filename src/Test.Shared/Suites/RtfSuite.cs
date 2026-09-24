namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// RTF reader tests.
    /// </summary>
    public static class RtfSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Rtf",
                displayName: "RTF reader",
                cases: new List<TestCaseDescriptor>());
        }
    }
}
