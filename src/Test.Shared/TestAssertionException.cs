namespace Test.Shared
{
    using System;

    /// <summary>
    /// Thrown by test assertions when a condition does not hold.
    /// </summary>
    public class TestAssertionException : Exception
    {
        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="message">Failure message with context.</param>
        public TestAssertionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Instantiate the exception with an inner exception.
        /// </summary>
        /// <param name="message">Failure message with context.</param>
        /// <param name="innerException">Cause.</param>
        public TestAssertionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
