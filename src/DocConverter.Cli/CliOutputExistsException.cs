namespace DocConverter.Cli
{
    using System;
    using System.IO;

    /// <summary>
    /// Thrown when an output file already exists and --overwrite was not given.
    /// </summary>
    public class CliOutputExistsException : IOException
    {
        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="message">Message naming the file.</param>
        public CliOutputExistsException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Instantiate the exception with a cause.
        /// </summary>
        /// <param name="message">Message naming the file.</param>
        /// <param name="innerException">Cause.</param>
        public CliOutputExistsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
