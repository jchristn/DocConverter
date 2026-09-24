namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;

    /// <summary>
    /// Collects test cases for one suite with less ceremony than constructing descriptors by hand.
    /// </summary>
    public sealed class SuiteBuilder
    {
        private readonly string _SuiteId;
        private readonly string _DisplayName;
        private readonly List<TestCaseDescriptor> _Cases = new List<TestCaseDescriptor>();
        private readonly HashSet<string> _Ids = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Instantiate a builder.
        /// </summary>
        /// <param name="suiteId">Suite identifier.</param>
        /// <param name="displayName">Suite display name.</param>
        public SuiteBuilder(string suiteId, string displayName)
        {
            _SuiteId = suiteId;
            _DisplayName = displayName;
        }

        /// <summary>
        /// Number of cases added.
        /// </summary>
        public int Count
        {
            get => _Cases.Count;
        }

        /// <summary>
        /// Add an asynchronous case.
        /// </summary>
        /// <param name="caseId">Case identifier, unique within the suite.</param>
        /// <param name="displayName">What the case proves.</param>
        /// <param name="body">Case body.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentException">Thrown when the case identifier is already used.</exception>
        public SuiteBuilder Add(string caseId, string displayName, Func<CancellationToken, Task> body)
        {
            if (!_Ids.Add(caseId)) throw new ArgumentException("Duplicate case id '" + caseId + "' in suite " + _SuiteId + ".", nameof(caseId));
            _Cases.Add(new TestCaseDescriptor(_SuiteId, caseId, displayName, executeAsync: body));
            return this;
        }

        /// <summary>
        /// Add a synchronous case.
        /// </summary>
        /// <param name="caseId">Case identifier, unique within the suite.</param>
        /// <param name="displayName">What the case proves.</param>
        /// <param name="body">Case body.</param>
        /// <returns>This builder.</returns>
        public SuiteBuilder AddSync(string caseId, string displayName, Action body)
        {
            return Add(caseId, displayName, ct =>
            {
                body();
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Build the suite descriptor.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(suiteId: _SuiteId, displayName: _DisplayName, cases: _Cases);
        }
    }
}
