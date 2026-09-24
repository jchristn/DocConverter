namespace DocConverter.Readers.Rtf
{
    /// <summary>
    /// One RTF token. For control words Text is the word and Parameter the optional numeric parameter; for control
    /// symbols Text is the symbol character; for text tokens Text is the literal text; for hex bytes Parameter is the byte
    /// value; for binary tokens Data holds the raw bytes.
    /// </summary>
    internal sealed class RtfToken
    {
        internal RtfTokenType Type { get; }

        internal string Text { get; }

        internal int? Parameter { get; }

        internal byte[]? Data { get; }

        internal RtfToken(RtfTokenType type, string text, int? parameter, byte[]? data)
        {
            Type = type;
            Text = text ?? "";
            Parameter = parameter;
            Data = data;
        }
    }
}
