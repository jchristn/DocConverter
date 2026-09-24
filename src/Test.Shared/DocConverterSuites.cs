namespace Test.Shared
{
    using System.Collections.Generic;
    using Test.Shared.Suites;
    using Touchstone.Core;

    /// <summary>
    /// Registry of every DocConverter test suite, consumed by all runners.
    /// </summary>
    public static class DocConverterSuites
    {
        /// <summary>
        /// All registered test suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                List<TestSuiteDescriptor> suites = new List<TestSuiteDescriptor>
                {
                    DocxSuite.Build(),
                    XlsxSuite.Build(),
                    PptxSuite.Build(),
                    PdfSuite.Build(),
                    RtfSuite.Build(),
                    ImageSuite.Build()
                };
#if !DOCCONVERTER_NETSTANDARD_TEST
                suites.Add(CliSuite.Build());
#endif
                return suites;
            }
        }
    }
}
