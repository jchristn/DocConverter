#if !DOCCONVERTER_NETSTANDARD_TEST
namespace Test.Shared.Suites.Cli
{
    using System;
    using System.IO;
    using System.Text;

    /// <summary>
    /// A temporary directory under the system temp folder, deleted on dispose.
    /// </summary>
    public sealed class CliTempDirectory : IDisposable
    {
        /// <summary>
        /// Directory path.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Create the directory.
        /// </summary>
        public CliTempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "docconv-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        /// <summary>
        /// Full path of a file in the directory.
        /// </summary>
        /// <param name="name">File name.</param>
        /// <returns>Full path.</returns>
        public string File(string name)
        {
            return System.IO.Path.Combine(Path, name);
        }

        /// <summary>
        /// Write a UTF-8 text file (no byte order mark) and return its path.
        /// </summary>
        /// <param name="name">File name.</param>
        /// <param name="text">Content.</param>
        /// <returns>Full path.</returns>
        public string WriteText(string name, string text)
        {
            string path = File(name);
            System.IO.File.WriteAllText(path, text, new UTF8Encoding(false));
            return path;
        }

        /// <summary>
        /// Write a binary file and return its path.
        /// </summary>
        /// <param name="name">File name.</param>
        /// <param name="bytes">Content.</param>
        /// <returns>Full path.</returns>
        public string WriteBytes(string name, byte[] bytes)
        {
            string path = File(name);
            System.IO.File.WriteAllBytes(path, bytes);
            return path;
        }

        /// <summary>
        /// Read a file as UTF-8 text.
        /// </summary>
        /// <param name="name">File name.</param>
        /// <returns>Content.</returns>
        public string ReadText(string name)
        {
            return System.IO.File.ReadAllText(File(name), new UTF8Encoding(false));
        }

        /// <summary>
        /// Number of files in the directory.
        /// </summary>
        public int FileCount
        {
            get => Directory.GetFiles(Path).Length;
        }

        /// <summary>
        /// Delete the directory.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
            catch (IOException)
            {
                // Best effort cleanup.
            }
            catch (UnauthorizedAccessException)
            {
                // Best effort cleanup.
            }
        }
    }
}
#endif
