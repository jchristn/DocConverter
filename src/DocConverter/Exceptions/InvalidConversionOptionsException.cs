namespace DocConverter.Exceptions
{
    using System;

    /// <summary>
    /// Thrown when an option value or a combination of option values is invalid.
    /// </summary>
    public class InvalidConversionOptionsException : DocConverterException
    {
        /// <summary>
        /// Instantiate the exception with a message.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        public InvalidConversionOptionsException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Instantiate the exception with a message and the exception that caused it.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        /// <param name="innerException">Exception that caused this one.</param>
        public InvalidConversionOptionsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
