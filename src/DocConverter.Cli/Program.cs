namespace DocConverter.Cli
{
    using System.Threading.Tasks;

    /// <summary>
    /// Entry point for the docconv tool.
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
