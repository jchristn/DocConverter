namespace Test.Shared
{
    /// <summary>
    /// Controls golden file handling. When UpdateDirectory is set (by Test.Automated --update-golden &lt;dir&gt;),
    /// golden suites write fresh golden files into that directory instead of comparing. Test.Shared itself never decides
    /// to write files.
    /// </summary>
    public static class GoldenSettings
    {
        /// <summary>
        /// Directory to write golden files into, normally src/Test.Shared/Fixtures/Golden. Null (default) compares.
        /// </summary>
        public static string? UpdateDirectory { get; set; } = null;
    }
}
