namespace Test.NetFramework
{
    using System.Threading.Tasks;

    /// <summary>
    /// Smoke harness entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Exit code.</returns>
        public static Task<int> Main(string[] args)
        {
            return Task.FromResult(0);
        }
    }
}
