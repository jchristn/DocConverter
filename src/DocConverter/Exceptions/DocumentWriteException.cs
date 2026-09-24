namespace DocConverter.Exceptions
{
    using System;

    /// <summary>
    /// Thrown when the output cannot be produced.
    /// </summary>
    public class DocumentWriteException : DocConverterException
    {
        /// <summary>
        /// Instantiate the exception with a message.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        public DocumentWriteException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Instantiate the exception with a message and the exception that caused it.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        /// <param name="innerException">Exception that caused this one.</param>
        public DocumentWriteException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
