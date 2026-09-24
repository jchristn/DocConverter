#if !DOCCONVERTER_NETSTANDARD_TEST
namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using Test.Shared.Suites.Cli;
    using Touchstone.Core;

    /// <summary>
    /// docconv command line tests, run in-process with memory streams and temporary directories.
    /// </summary>
    public static class CliSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(CliParsingCases.Cases());
            cases.AddRange(CliConversionCases.Cases());
            cases.AddRange(CliReportCases.Cases());
            cases.AddRange(CliOptionCases.Cases());
            return new TestSuiteDescriptor(
                suiteId: "Cli",
                displayName: "docconv command line",
                cases: cases);
        }
    }
}
#endif
