namespace DocConverter.Readers.Rtf
{
    /// <summary>
    /// Character formatting and destination scoped to an RTF group. Copied when a group opens and restored when it closes.
    /// </summary>
    internal sealed class RtfGroupState
    {
        internal bool Bold { get; set; }

        internal bool Italic { get; set; }

        internal bool Underline { get; set; }

        internal bool Strike { get; set; }

        internal bool Superscript { get; set; }

        internal bool Subscript { get; set; }

        internal bool Hidden { get; set; }

        internal int FontIndex { get; set; } = -1;

        internal int UnicodeSkip { get; set; } = 1;

        internal RtfDestination Destination { get; set; } = RtfDestination.Body;

        internal string InfoField { get; set; } = "";

        internal RtfField? Field { get; set; }

        internal bool FieldResult { get; set; }

        internal RtfGroupState Clone()
        {
            return new RtfGroupState
            {
                Bold = Bold,
                Italic = Italic,
                Underline = Underline,
                Strike = Strike,
                Superscript = Superscript,
                Subscript = Subscript,
                Hidden = Hidden,
                FontIndex = FontIndex,
                UnicodeSkip = UnicodeSkip,
                Destination = Destination,
                InfoField = InfoField,
                Field = Field,
                FieldResult = FieldResult
            };
        }

        internal void ResetCharacter()
        {
            Bold = false;
            Italic = false;
            Underline = false;
            Strike = false;
            Superscript = false;
            Subscript = false;
            Hidden = false;
        }
    }
}
