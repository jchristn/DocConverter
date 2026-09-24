namespace DocConverter.Internal
{
    using System.IO;
    using DocConverter.Results;

    /// <summary>
    /// The written document (in memory) and the result describing the conversion.
    /// </summary>
    internal sealed class PipelineOutput
    {
        internal ConversionResult Result { get; }

        internal MemoryStream Output { get; }

        internal PipelineOutput(ConversionResult result, MemoryStream output)
        {
            Result = result;
            Output = output;
        }
    }
}
