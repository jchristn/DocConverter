namespace Test.Shared.Doubles
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Model;
    using DocConverter.Ocr;
    using DocConverter.Options;
    using DocConverter.Readers;
    using DocConverter.Writers;
    using Test.Shared.Inspection;

    /// <summary>
    /// An OCR provider that overrides nothing, so every member throws NotImplementedException.
    /// </summary>
    public sealed class StubOcr : OcrProviderBase
    {
    }
}
