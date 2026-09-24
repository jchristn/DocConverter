namespace DocConverter.Readers.Docx
{
    using System.Collections.Generic;
    using DocConverter.Enums;
    using DocumentFormat.OpenXml.Packaging;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Resolves numbering instances to list kinds and start values.
    /// </summary>
    internal sealed class DocxNumberingResolver
    {
        private readonly Dictionary<int, W.NumberingInstance> _Nums = new Dictionary<int, W.NumberingInstance>();
        private readonly Dictionary<int, W.AbstractNum> _Abstracts = new Dictionary<int, W.AbstractNum>();

        internal DocxNumberingResolver(NumberingDefinitionsPart? part)
        {
            if (part == null || part.Numbering == null) return;
            foreach (W.AbstractNum abs in part.Numbering.Elements<W.AbstractNum>())
            {
                if (abs.AbstractNumberId != null && !_Abstracts.ContainsKey(abs.AbstractNumberId.Value)) _Abstracts[abs.AbstractNumberId.Value] = abs;
            }

            foreach (W.NumberingInstance num in part.Numbering.Elements<W.NumberingInstance>())
            {
                if (num.NumberID != null && !_Nums.ContainsKey(num.NumberID.Value)) _Nums[num.NumberID.Value] = num;
            }
        }

        internal ListKindEnum Kind(int numId, int level)
        {
            W.Level? lvl = FindLevel(numId, level);
            if (lvl == null || lvl.NumberingFormat == null || lvl.NumberingFormat.Val == null) return ListKindEnum.Unordered;
            W.NumberFormatValues format = lvl.NumberingFormat.Val.Value;
            if (format == W.NumberFormatValues.Bullet || format == W.NumberFormatValues.None) return ListKindEnum.Unordered;
            return ListKindEnum.Ordered;
        }

        internal int Start(int numId, int level)
        {
            if (_Nums.TryGetValue(numId, out W.NumberingInstance? num))
            {
                foreach (W.LevelOverride ov in num.Elements<W.LevelOverride>())
                {
                    if (ov.LevelIndex != null && ov.LevelIndex.Value == level && ov.StartOverrideNumberingValue?.Val != null)
                        return ov.StartOverrideNumberingValue.Val.Value;
                }
            }

            W.Level? lvl = FindLevel(numId, level);
            if (lvl != null && lvl.StartNumberingValue?.Val != null) return lvl.StartNumberingValue.Val.Value;
            return 1;
        }

        private W.Level? FindLevel(int numId, int level)
        {
            if (!_Nums.TryGetValue(numId, out W.NumberingInstance? num)) return null;
            foreach (W.LevelOverride ov in num.Elements<W.LevelOverride>())
            {
                if (ov.LevelIndex != null && ov.LevelIndex.Value == level && ov.Level != null) return ov.Level;
            }

            int? abstractId = num.AbstractNumId?.Val?.Value;
            if (!abstractId.HasValue || !_Abstracts.TryGetValue(abstractId.Value, out W.AbstractNum? abs)) return null;
            W.Level? fallback = null;
            foreach (W.Level lvl in abs.Elements<W.Level>())
            {
                if (lvl.LevelIndex != null && lvl.LevelIndex.Value == level) return lvl;
                if (fallback == null) fallback = lvl;
            }

            return fallback;
        }
    }
}
