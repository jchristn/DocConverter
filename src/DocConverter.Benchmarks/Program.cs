namespace DocConverter.Benchmarks
{
    using BenchmarkDotNet.Running;

    /// <summary>
    /// Benchmark entry point. Run with: dotnet run -c Release --project src/DocConverter.Benchmarks -- --filter *
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">BenchmarkDotNet arguments.</param>
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
