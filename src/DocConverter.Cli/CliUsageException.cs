namespace DocConverter.Cli
{
    using System;

    /// <summary>
    /// Thrown when the command line is invalid: unknown option, missing argument or invalid value.
    /// </summary>
    public class CliUsageException : Exception
    {
        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="message">Message describing the problem.</param>
        public CliUsageException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Instantiate the exception with a cause.
        /// </summary>
        /// <param name="message">Message describing the problem.</param>
        /// <param name="innerException">Cause.</param>
        public CliUsageException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
