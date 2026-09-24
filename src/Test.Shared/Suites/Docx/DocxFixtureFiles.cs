namespace Test.Shared.Suites.Docx
{
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Loads embedded fixtures regardless of whether the resource name uses forward or back slashes.
    /// </summary>
    public static class DocxFixtureFiles
    {
        /// <summary>
        /// Load an embedded fixture by its path under Fixtures/.
        /// </summary>
        /// <param name="relativePath">Path relative to the Fixtures folder, with forward slashes.</param>
        /// <returns>Bytes.</returns>
        /// <exception cref="FileNotFoundException">Thrown when no matching resource exists.</exception>
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
