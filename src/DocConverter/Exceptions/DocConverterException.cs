namespace DocConverter.Exceptions
{
    using System;

    /// <summary>
    /// Base type for every exception DocConverter throws for domain errors.
    /// </summary>
    public class DocConverterException : Exception
    {
        /// <summary>
        /// Instantiate the exception with a message.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        public DocConverterException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Instantiate the exception with a message and the exception that caused it.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        /// <param name="innerException">Exception that caused this one.</param>
        public DocConverterException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
