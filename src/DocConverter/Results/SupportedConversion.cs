namespace DocConverter.Results
{
    using DocConverter.Enums;

    /// <summary>
    /// One supported conversion pair and its fidelity.
    /// </summary>
    public class SupportedConversion
    {
        /// <summary>
        /// Source format.
        /// </summary>
        public DocumentFormatEnum From { get; }

        /// <summary>
        /// Target format.
        /// </summary>
        public DocumentFormatEnum To { get; }

        /// <summary>
        /// Fidelity of the pair.
        /// </summary>
        public FidelityEnum Fidelity { get; }

        /// <summary>
        /// Short note on what is lost for Projection pairs. Empty for Full pairs. Never null.
        /// </summary>
        public string Notes { get; }

        /// <summary>
        /// Instantiate a pair.
        /// </summary>
        /// <param name="from">Source format.</param>
        /// <param name="to">Target format.</param>
        /// <param name="fidelity">Fidelity.</param>
        /// <param name="notes">Notes. Null is stored as empty.</param>
        public SupportedConversion(DocumentFormatEnum from, DocumentFormatEnum to, FidelityEnum fidelity, string? notes)
        {
            From = from;
            To = to;
            Fidelity = fidelity;
            Notes = notes ?? "";
        }

        /// <summary>
        /// "From -> To (Fidelity)".
        /// </summary>
        /// <returns>String form.</returns>
        public override string ToString()
        {
            return From + " -> " + To + " (" + Fidelity + ")";
        }
    }
}
