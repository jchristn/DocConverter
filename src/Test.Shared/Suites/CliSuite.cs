namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// docconv command line tests.
    /// </summary>
    public static class CliSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Cli",
                displayName: "docconv command line",
                cases: new List<TestCaseDescriptor>());
        }
    }
}
