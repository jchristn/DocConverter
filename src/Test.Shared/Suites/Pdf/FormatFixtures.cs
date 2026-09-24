namespace Test.Shared.Suites.Pdf
{
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Loads embedded fixtures by their path under Fixtures/, tolerating resource names that contain backslashes.
    /// </summary>
    public static class FormatFixtures
    {
        /// <summary>
        /// Load a fixture, for example "Images/sample.gif".
        /// </summary>
        /// <param name="relativePath">Path under Fixtures/, forward slashes.</param>
        /// <returns>Bytes.</returns>
        /// <exception cref="FileNotFoundException">Thrown when missing.</exception>
        public static byte[] Load(string relativePath)
        {
            string wanted = "Fixtures/" + relativePath.Replace('\\', '/');
            Assembly assembly = typeof(TestSupport).Assembly;
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (name.Replace('\\', '/') != wanted) continue;
                using (Stream? stream = assembly.GetManifestResourceStream(name))
                {
                    if (stream == null) break;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
            }

            throw new FileNotFoundException("Embedded fixture not found: " + wanted);
        }
    }
}
