namespace DocConverter.Exceptions
{
    using System;

    /// <summary>
    /// Thrown when the input cannot be read: corrupt, truncated, encrypted, or malformed content. The parser's exception, when there is one, is the InnerException.
    /// </summary>
    public class DocumentReadException : DocConverterException
    {
        /// <summary>
        /// Instantiate the exception with a message.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        public DocumentReadException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Instantiate the exception with a message and the exception that caused it.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        /// <param name="innerException">Exception that caused this one.</param>
        public DocumentReadException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
