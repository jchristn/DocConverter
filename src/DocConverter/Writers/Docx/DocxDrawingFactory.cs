namespace DocConverter.Writers.Docx
{
    using DocConverter.Internal;
    using DocConverter.Model;
    using A = DocumentFormat.OpenXml.Drawing;
    using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
    using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
    using W = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Builds inline DrawingML pictures sized from the declared point size or the pixel size at 96 DPI, capped to the
    /// text width.
    /// </summary>
    internal static class DocxDrawingFactory
    {
        private const long EmuPerPoint = 12700;
        private const long EmuPerPixel = 9525;

        internal static W.Drawing Create(string relationshipId, BinaryResource resource, string? altText, double? widthPoints, double? heightPoints, uint drawingId, long maxWidthEmu)
        {
            long cx;
            long cy;
            if (widthPoints.HasValue && heightPoints.HasValue && widthPoints.Value > 0 && heightPoints.Value > 0)
            {
                cx = (long)(widthPoints.Value * EmuPerPoint);
                cy = (long)(heightPoints.Value * EmuPerPoint);
            }
            else
            {
                int? pw = resource.PixelWidth;
                int? ph = resource.PixelHeight;
                if (!pw.HasValue || !ph.HasValue)
                {
                    ImageInfo? info = ImageHeaderReader.Read(resource.Data);
                    pw = info?.Width;
                    ph = info?.Height;
                }

                int w = pw.HasValue && pw.Value > 0 ? pw.Value : 96;
                int h = ph.HasValue && ph.Value > 0 ? ph.Value : 96;
                cx = w * EmuPerPixel;
                cy = h * EmuPerPixel;
            }

            if (cx > maxWidthEmu && cx > 0)
            {
                cy = (long)(cy * ((double)maxWidthEmu / cx));
                cx = maxWidthEmu;
            }

            if (cx < 1) cx = 1;
            if (cy < 1) cy = 1;
            string name = "Picture " + drawingId;

            DW.DocProperties docPr = new DW.DocProperties { Id = drawingId, Name = name };
            if (!string.IsNullOrEmpty(altText)) docPr.Description = altText;

            return new W.Drawing(new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                docPr,
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = 0U, Name = resource.FileName ?? name },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U
            });
        }
    }
}
