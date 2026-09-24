namespace Test.Xunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using Xunit;

    /// <summary>
    /// Runs every DocConverter suite descriptor through the Touchstone executor as a single fact.
    /// </summary>
    public sealed class DocConverterFactTests : TouchstoneFactBase
    {
        /// <summary>
        /// Suites under test.
        /// </summary>
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return DocConverterSuites.All; }
        }

        /// <summary>
        /// Execute all suites.
        /// </summary>
        [Fact]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
