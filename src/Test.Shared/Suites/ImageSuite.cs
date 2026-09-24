namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Image reader tests.
    /// </summary>
    public static class ImageSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Image",
                displayName: "Image reader",
                cases: new List<TestCaseDescriptor>());
        }
    }
}
