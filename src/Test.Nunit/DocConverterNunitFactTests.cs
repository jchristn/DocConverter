namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs every DocConverter suite descriptor through the Touchstone executor as a single NUnit test.
    /// </summary>
    [TestFixture]
    public sealed class DocConverterNunitFactTests : TouchstoneNunitBase
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
        [Test]
        public async Task RunAll()
        {
            await RunAllAsync().ConfigureAwait(false);
        }
    }
}
