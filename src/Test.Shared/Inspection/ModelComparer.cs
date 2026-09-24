namespace Test.Shared.Inspection
{
    using System;
    using System.Text;
    using System.Text.Json;
    using DocConverter.Model;
    using DocConverter.Model.Serialization;

    /// <summary>
    /// Compares documents by their canonical form (including binary data), which covers every model member.
    /// </summary>
    public static class ModelComparer
    {
        /// <summary>
        /// Canonical JSON text of a document, indented, used for comparison and diagnostics.
        /// </summary>
        /// <param name="document">Document.</param>
        /// <param name="includeMetadata">Include metadata.</param>
        /// <returns>JSON.</returns>
        public static string Canonical(DocumentModel document, bool includeMetadata = true)
        {
            CanonicalDocument dto = CanonicalMapper.ToDto(document, includeMetadata, true);
            return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true, MaxDepth = 1024, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        }

        /// <summary>
        /// Throw TestAssertionException when the documents differ, pointing at the first differing line.
        /// </summary>
        /// <param name="expected">Expected document.</param>
        /// <param name="actual">Actual document.</param>
        /// <param name="message">Failure message.</param>
        /// <param name="includeMetadata">Compare metadata too.</param>
        public static void AssertEqual(DocumentModel expected, DocumentModel actual, string message, bool includeMetadata = true)
        {
            string a = Canonical(expected, includeMetadata);
            string b = Canonical(actual, includeMetadata);
            if (string.Equals(a, b, StringComparison.Ordinal)) return;

            string[] la = a.Split('\n');
            string[] lb = b.Split('\n');
            int n = Math.Min(la.Length, lb.Length);
            int line = n;
            for (int i = 0; i < n; i++)
            {
                if (la[i] != lb[i])
                {
                    line = i;
                    break;
                }
            }

            StringBuilder sb = new StringBuilder(message);
            sb.Append(" (documents differ at canonical line ").Append(line + 1).Append(": expected '");
            sb.Append(line < la.Length ? la[line].Trim() : "<end>").Append("', got '").Append(line < lb.Length ? lb[line].Trim() : "<end>").Append("')");
            throw new TestAssertionException(sb.ToString());
        }
    }
}
