namespace DocConverter.Exceptions
{
    using System;

    /// <summary>
    /// Thrown when the input, or a decompressed part of it, exceeds a configured size limit.
    /// </summary>
    public class InputTooLargeException : DocConverterException
    {
        /// <summary>
        /// The limit that was exceeded, in bytes.
        /// </summary>
        public long LimitBytes { get; }

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        /// <param name="limitBytes">The limit that was exceeded, in bytes.</param>
        public InputTooLargeException(string message, long limitBytes)
            : base(message)
        {
            LimitBytes = limitBytes;
        }

        /// <summary>
        /// Instantiate the exception with a message.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        public InputTooLargeException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Instantiate the exception with a message and the exception that caused it.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        /// <param name="innerException">Exception that caused this one.</param>
        public InputTooLargeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
