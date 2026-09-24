namespace DocConverter.Readers.Rtf
{
    /// <summary>
    /// Where the text of the current RTF group goes.
    /// </summary>
    internal enum RtfDestination
    {
        Body,
        Skip,
        FontTable,
        StyleSheet,
        Info,
        InfoText,
        Picture,
        FieldInstruction,
        ListText
    }
}
