namespace DocConverter.Readers.Pptx
{
    using System.Collections.Generic;
    using DocumentFormat.OpenXml.Packaging;
    using A = DocumentFormat.OpenXml.Drawing;
    using P = DocumentFormat.OpenXml.Presentation;

    /// <summary>
    /// Resolves the position of placeholder shapes that inherit their offset from the slide layout or slide master.
    /// </summary>
    internal sealed class PptxPlaceholderPositions
    {
        private readonly Dictionary<string, A.Offset> _ByKey = new Dictionary<string, A.Offset>(System.StringComparer.Ordinal);

        internal PptxPlaceholderPositions(SlidePart slide)
        {
            SlideLayoutPart? layout = slide.SlideLayoutPart;
            SlideMasterPart? master = layout?.SlideMasterPart;
            if (master?.SlideMaster?.CommonSlideData?.ShapeTree != null) Collect(master.SlideMaster.CommonSlideData.ShapeTree);
            if (layout?.SlideLayout?.CommonSlideData?.ShapeTree != null) Collect(layout.SlideLayout.CommonSlideData.ShapeTree);
        }

        internal A.Offset? Find(P.PlaceholderShape? placeholder)
        {
            if (placeholder == null) return null;
            string type = TypeName(placeholder);
            string idx = placeholder.Index != null && placeholder.Index.HasValue ? placeholder.Index.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
            if (idx.Length > 0 && _ByKey.TryGetValue("idx:" + idx, out A.Offset? byIndex)) return byIndex;
            if (_ByKey.TryGetValue("type:" + type, out A.Offset? byType)) return byType;
            return null;
        }

        internal static string TypeName(P.PlaceholderShape placeholder)
        {
            if (placeholder.Type == null || !placeholder.Type.HasValue) return "body";
            P.PlaceholderValues type = placeholder.Type.Value;
            if (type == P.PlaceholderValues.CenteredTitle) return "title";
            if (type == P.PlaceholderValues.Title) return "title";
            if (type == P.PlaceholderValues.SubTitle) return "subTitle";
            if (type == P.PlaceholderValues.Body) return "body";
            return type.ToString();
        }

        private void Collect(P.ShapeTree tree)
        {
            foreach (P.Shape shape in tree.Elements<P.Shape>())
            {
                P.PlaceholderShape? ph = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;
                A.Offset? offset = shape.ShapeProperties?.Transform2D?.Offset;
                if (ph == null || offset == null) continue;
                _ByKey["type:" + TypeName(ph)] = offset;
                if (ph.Index != null && ph.Index.HasValue) _ByKey["idx:" + ph.Index.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)] = offset;
            }
        }
    }
}
