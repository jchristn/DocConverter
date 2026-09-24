namespace DocConverter.Results
{
    using DocConverter.Enums;

    /// <summary>
    /// A warning raised during a conversion. Warnings of the same code are merged: Message is the first occurrence and
    /// Count is the number of occurrences.
    /// </summary>
    public class ConversionWarning
    {
        /// <summary>
        /// Warning code.
        /// </summary>
        public WarningCodeEnum Code { get; }

        /// <summary>
        /// Message describing the first occurrence. Never null.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Number of occurrences. At least 1.
        /// </summary>
        public int Count { get; internal set; }

        /// <summary>
        /// Instantiate a warning.
        /// </summary>
        /// <param name="code">Warning code.</param>
        /// <param name="message">Message. Null is stored as empty.</param>
        /// <param name="count">Number of occurrences. Values below 1 are stored as 1.</param>
        public ConversionWarning(WarningCodeEnum code, string? message, int count = 1)
        {
            Code = code;
            Message = message ?? "";
            Count = count < 1 ? 1 : count;
        }

        /// <summary>
        /// Code, count and message, for logs.
        /// </summary>
        /// <returns>String form.</returns>
        public override string ToString()
        {
            return Code + (Count > 1 ? " (x" + Count + ")" : "") + ": " + Message;
        }
    }
}
