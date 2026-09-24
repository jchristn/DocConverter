namespace DocConverter.Ocr
{
    using System;
    using DocConverter.Enums;
    using DocConverter.Observability;
    using DocConverter.Options;

    /// <summary>
    /// Pipeline stage where OCR will run between reading and writing. In this release it only guards against a
    /// configured provider being silently ignored.
    /// </summary>
    internal static class OcrStage
    {
        internal static void EnsureSupported(IOcrProvider? provider, ConversionOptions options)
        {
            if (provider == null) return;
            if (options.OcrMode == OcrModeEnum.Off) return;
            throw new NotImplementedException(
                "OCR integration is not implemented in DocConverter " + DocConverterDiagnostics.Version + ". An OCR provider ('"
                + provider.GetType().Name + "') is configured and OcrMode is " + options.OcrMode
                + ". Set OcrMode to Off or remove the provider.");
        }
    }
}
