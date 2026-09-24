namespace DocConverter.Ocr
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Model;

    /// <summary>
    /// Convenience base class for OCR providers. Every member throws NotImplementedException until a derived class
    /// overrides it. Reserved extension point: DocConverter ships no implementation yet.
    /// </summary>
    public abstract class OcrProviderBase : IOcrProvider
    {
        /// <summary>
        /// Provider name. Throws NotImplementedException unless overridden.
        /// </summary>
        /// <exception cref="NotImplementedException">Thrown unless overridden.</exception>
        public virtual string Name
        {
            get { throw new NotImplementedException("OcrProviderBase.Name must be overridden by the OCR provider."); }
        }

        /// <summary>
        /// Instantiate the provider.
        /// </summary>
        protected OcrProviderBase()
        {
        }

        /// <summary>
        /// True when the provider can read the media type. Throws NotImplementedException unless overridden.
        /// </summary>
        /// <param name="mediaType">Media type.</param>
        /// <returns>True when supported.</returns>
        /// <exception cref="NotImplementedException">Thrown unless overridden.</exception>
        public virtual bool Supports(string mediaType)
        {
            throw new NotImplementedException("OcrProviderBase.Supports must be overridden by the OCR provider.");
        }

        /// <summary>
        /// Recognize the content of an image. Throws NotImplementedException unless overridden.
        /// </summary>
        /// <param name="image">Image resource.</param>
        /// <param name="options">OCR options.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Recognized content.</returns>
        /// <exception cref="NotImplementedException">Thrown unless overridden.</exception>
        public virtual Task<OcrResult> RecognizeAsync(BinaryResource image, OcrOptions options, CancellationToken token = default)
        {
            throw new NotImplementedException("OcrProviderBase.RecognizeAsync must be overridden by the OCR provider.");
        }
    }
}
