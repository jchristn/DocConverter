namespace DocConverter.Readers.Rtf
{
    /// <summary>
    /// Kinds of RTF tokens.
    /// </summary>
    internal enum RtfTokenType
    {
        GroupStart,
        GroupEnd,
        ControlWord,
        ControlSymbol,
        Text,
        HexByte,
        Binary
    }
}
