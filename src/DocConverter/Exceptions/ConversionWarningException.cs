namespace DocConverter.Exceptions
{
    using System;
    using DocConverter.Results;

    /// <summary>
    /// Thrown after a conversion completes with warnings while ConversionOptions.TreatWarningsAsErrors is true.
    /// When the conversion wrote into a caller-owned stream, that output has already been written.
    /// </summary>
    public class ConversionWarningException : DocConverterException
    {
        /// <summary>
        /// The result of the conversion, including its warnings. Null only when constructed without one.
        /// </summary>
        public ConversionResult? Result { get; }

        /// <summary>
        /// Instantiate the exception with the result that carried warnings.
        /// </summary>
        /// <param name="message">Message describing the warnings.</param>
        /// <param name="result">Result of the conversion.</param>
        public ConversionWarningException(string message, ConversionResult result)
            : base(message)
        {
            Result = result;
        }

        /// <summary>
        /// Instantiate the exception with a message.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        public ConversionWarningException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Instantiate the exception with a message and the exception that caused it.
        /// </summary>
        /// <param name="message">Message describing the failure, with context.</param>
        /// <param name="innerException">Exception that caused this one.</param>
        public ConversionWarningException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
