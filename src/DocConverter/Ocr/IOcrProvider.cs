namespace DocConverter.Ocr
{
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Model;

    /// <summary>
    /// Recognizes text in images. Reserved extension point: DocConverter 0.1.0 ships no implementation and does not
    /// call providers yet. Implementations must be thread safe, because one provider serves concurrent conversions.
    /// </summary>
    public interface IOcrProvider
    {
        /// <summary>
        /// Provider name, for example "Tesseract".
        /// </summary>
        string Name { get; }

        /// <summary>
        /// True when the provider can read images of the given media type, for example "image/png".
        /// </summary>
        /// <param name="mediaType">Media type.</param>
        /// <returns>True when supported.</returns>
        bool Supports(string mediaType);

        /// <summary>
        /// Recognize the content of an image.
        /// </summary>
        /// <param name="image">Image resource.</param>
        /// <param name="options">OCR options.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Recognized content as document model blocks.</returns>
        Task<OcrResult> RecognizeAsync(BinaryResource image, OcrOptions options, CancellationToken token = default);
    }
}
