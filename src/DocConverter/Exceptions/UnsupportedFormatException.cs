namespace DocConverter.Exceptions
{
    using System;

    /// <summary>
    /// Thrown when the input format cannot be detected, or is recognized but not supported (for example legacy .doc files).
    /// </summary>
    public class UnsupportedFormatException : DocConverterException
    {
        /// <summary>
        /// Instantiate the exception with a message.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        public UnsupportedFormatException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Instantiate the exception with a message and the exception that caused it.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        /// <param name="innerException">Exception that caused this one.</param>
        public UnsupportedFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
