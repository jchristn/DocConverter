namespace DocConverter.Writers.Docx
{
    using System.Collections.Generic;
    using DocConverter.Enums;
    using DocumentFormat.OpenXml;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Builds the numbering part: one bulleted and one decimal abstract definition (nine levels each), and one numbering
    /// instance per written list so every list restarts at its own start value.
    /// </summary>
    internal sealed class DocxNumberingBuilder
    {
        internal const int BulletAbstractId = 1;
        internal const int DecimalAbstractId = 2;
        internal const int MaxLevel = 8;

        private static readonly string[] _Bullets = new string[] { "•", "◦", "▪" };

        private readonly List<W.NumberingInstance> _Instances = new List<W.NumberingInstance>();
        private int _NextNumId = 1;

        internal bool HasLists
        {
            get => _Instances.Count > 0;
        }

        internal int AddList(ListKindEnum kind, int level, int start)
        {
            int numId = _NextNumId++;
            bool ordered = kind == ListKindEnum.Ordered;
            W.NumberingInstance instance = new W.NumberingInstance(new W.AbstractNumId { Val = ordered ? DecimalAbstractId : BulletAbstractId });
            instance.NumberID = numId;
            if (ordered)
            {
                W.LevelOverride levelOverride = new W.LevelOverride(new W.StartOverrideNumberingValue { Val = start });
                levelOverride.LevelIndex = level;
                instance.Append(levelOverride);
            }

            _Instances.Add(instance);
            return numId;
        }

        internal W.Numbering Build()
        {
            W.Numbering numbering = new W.Numbering();
            numbering.Append(BuildAbstract(BulletAbstractId, false));
            numbering.Append(BuildAbstract(DecimalAbstractId, true));
            foreach (W.NumberingInstance instance in _Instances) numbering.Append(instance);
            return numbering;
        }

        private static W.AbstractNum BuildAbstract(int id, bool ordered)
        {
            W.AbstractNum abs = new W.AbstractNum();
            abs.AbstractNumberId = id;
            abs.Append(new W.MultiLevelType { Val = W.MultiLevelValues.HybridMultilevel });
            for (int level = 0; level <= MaxLevel; level++)
            {
                W.Level lvl = new W.Level();
                lvl.LevelIndex = level;
                lvl.Append(new W.StartNumberingValue { Val = 1 });
                lvl.Append(new W.NumberingFormat { Val = ordered ? W.NumberFormatValues.Decimal : W.NumberFormatValues.Bullet });
                lvl.Append(new W.LevelText { Val = ordered ? "%" + (level + 1) + "." : _Bullets[level % _Bullets.Length] });
                lvl.Append(new W.LevelJustification { Val = W.LevelJustificationValues.Left });
                lvl.Append(new W.PreviousParagraphProperties(new W.Indentation { Left = ((level + 1) * 720).ToString(), Hanging = "360" }));
                abs.Append(lvl);
            }

            return abs;
        }
    }
}
