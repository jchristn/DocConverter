namespace DocConverter.Exceptions
{
    using System;

    /// <summary>
    /// Thrown when no registered reader or writer covers a conversion pair. Every built-in pair is supported, so this only occurs with caller-registered formats.
    /// </summary>
    public class ConversionNotSupportedException : DocConverterException
    {
        /// <summary>
        /// Instantiate the exception with a message.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        public ConversionNotSupportedException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Instantiate the exception with a message and the exception that caused it.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        /// <param name="innerException">Exception that caused this one.</param>
        public ConversionNotSupportedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
