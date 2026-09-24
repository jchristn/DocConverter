namespace Test.Shared.Inspection
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using UglyToad.PdfPig;
    using UglyToad.PdfPig.Content;
    using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

    /// <summary>
    /// Inspects PDF output with PdfPig, independently of DocConverter's PDF reader.
    /// </summary>
    public static class PdfInspector
    {
        /// <summary>
        /// Open the PDF, require at least one page, and extract text, image count, links and title. Fails when MigraDoc's
        /// silent "Image has no valid type." fallback text appears.
        /// </summary>
        /// <param name="pdf">PDF bytes.</param>
        /// <returns>Snapshot.</returns>
        /// <exception cref="TestAssertionException">Thrown when the PDF is invalid, empty, or shows MigraDoc's error text.</exception>
        public static ContentSnapshot Inspect(byte[] pdf)
        {
            ContentSnapshot snapshot = new ContentSnapshot();
            StringBuilder text = new StringBuilder();
            try
            {
                using (PdfDocument document = PdfDocument.Open(pdf))
                {
                    TestSupport.Assert(document.NumberOfPages >= 1, "PDF has no pages");
                    snapshot.Title = document.Information.Title;
                    foreach (Page page in document.GetPages())
                    {
                        text.Append(ContentOrderTextExtractor.GetText(page)).Append('\n');
                        foreach (IPdfImage image in page.GetImages()) snapshot.ImageCount++;
                        foreach (Hyperlink link in page.GetHyperlinks())
                            if (!string.IsNullOrEmpty(link.Uri)) snapshot.LinkUrls.Add(link.Uri!);
                    }
                }
            }
            catch (TestAssertionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new TestAssertionException("PdfPig could not open the PDF: " + ex.Message, ex);
            }

            string all = text.ToString();
            if (all.IndexOf("Image has no valid type", StringComparison.Ordinal) >= 0)
                throw new TestAssertionException("The PDF contains MigraDoc's fallback text 'Image has no valid type.'");
            snapshot.AllText = ContentSnapshot.Normalize(all);
            return snapshot;
        }

        /// <summary>
        /// Page sizes in points (width, height pairs flattened: w0, h0, w1, h1, ...).
        /// </summary>
        /// <param name="pdf">PDF bytes.</param>
        /// <returns>Sizes.</returns>
        public static List<double> PageSizes(byte[] pdf)
        {
            List<double> sizes = new List<double>();
            using (PdfDocument document = PdfDocument.Open(pdf))
            {
                foreach (Page page in document.GetPages())
                {
                    sizes.Add(page.Width);
                    sizes.Add(page.Height);
                }
            }

            return sizes;
        }

        /// <summary>
        /// Smallest x coordinate of any letter on the first page, used to check margins.
        /// </summary>
        /// <param name="pdf">PDF bytes.</param>
        /// <returns>Left edge in points.</returns>
        public static double FirstPageTextLeft(byte[] pdf)
        {
            double min = double.MaxValue;
            using (PdfDocument document = PdfDocument.Open(pdf))
            {
                Page page = document.GetPage(1);
                foreach (Letter letter in page.Letters)
                    if (!string.IsNullOrWhiteSpace(letter.Value) && letter.BoundingBox.Left < min) min = letter.BoundingBox.Left;
            }

            return min;
        }

        /// <summary>
        /// Most common letter point size on the first page.
        /// </summary>
        /// <param name="pdf">PDF bytes.</param>
        /// <returns>Size in points.</returns>
        public static double DominantFontSize(byte[] pdf)
        {
            Dictionary<double, int> counts = new Dictionary<double, int>();
            using (PdfDocument document = PdfDocument.Open(pdf))
            {
                Page page = document.GetPage(1);
                foreach (Letter letter in page.Letters)
                {
                    if (string.IsNullOrWhiteSpace(letter.Value)) continue;
                    double size = Math.Round(letter.PointSize, 1);
                    counts.TryGetValue(size, out int n);
                    counts[size] = n + 1;
                }
            }

            double best = 0;
            int bestCount = -1;
            foreach (KeyValuePair<double, int> pair in counts)
            {
                if (pair.Value > bestCount)
                {
                    best = pair.Key;
                    bestCount = pair.Value;
                }
            }

            return best;
        }
    }
}
