namespace Test.Shared.Suites
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Detection;
    using DocConverter.Enums;
    using DocConverter.Options;
    using DocConverter.Results;
    using Test.Shared.Inspection;
    using Test.Shared.Matrix;
    using Touchstone.Core;

    /// <summary>
    /// The full conversion matrix: every source variant times every built-in target times every output shape (stream,
    /// string, byte array). Each case validates the output with an independent inspector, checks what must survive for
    /// the pair, and checks the result fields are consistent with the output.
    /// </summary>
    public static class ConversionMatrixSuite
    {
        private static readonly string[] _Shapes = new string[] { "Stream", "String", "Bytes" };

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            SuiteBuilder s = new SuiteBuilder("Matrix", "Conversion matrix");
            Converter c = new Converter();
            ConversionOptions options = new ConversionOptions { Deterministic = true };

            foreach (SourceSpec source in MatrixCatalog.Sources)
            {
                foreach (DocumentFormatEnum target in MatrixCatalog.Targets)
                {
                    foreach (string shape in _Shapes)
                    {
                        SourceSpec src = source;
                        DocumentFormatEnum to = target;
                        string how = shape;
                        s.Add(src.Id + "_" + to + "_" + how, src.Id + " to " + to + " via " + how, async ct =>
                        {
                            byte[] input = src.Bytes();
                            ConversionResult result;
                            byte[] output;
                            if (how == "Stream")
                            {
                                using (MemoryStream ms = new MemoryStream())
                                {
                                    result = await c.ConvertAsync(input, src.Format, to, ms, options, ct).ConfigureAwait(false);
                                    output = ms.ToArray();
                                }
                            }
                            else if (how == "String")
                            {
                                StringConversionResult r = await c.ConvertToStringAsync(input, src.Format, to, options, ct).ConfigureAwait(false);
                                TestSupport.AssertEqual(!DocumentFormatParser.IsTextBased(to), r.IsBase64, "IsBase64 for " + to);
                                output = r.IsBase64 ? Convert.FromBase64String(r.Output) : options.OutputEncoding.GetBytes(r.Output);
                                result = r;
                            }
                            else
                            {
                                BytesConversionResult r = await c.ConvertToBytesAsync(input, src.Format, to, options, ct).ConfigureAwait(false);
                                output = r.Output;
                                result = r;
                            }

                            TestSupport.AssertEqual(src.Format, result.SourceFormat, "result source format");
                            TestSupport.AssertEqual(to, result.TargetFormat, "result target format");
                            TestSupport.AssertEqual((long)input.Length, result.BytesRead, "bytes read");
                            TestSupport.AssertEqual((long)output.Length, result.BytesWritten, "bytes written equals output length");
                            ContentSnapshot snap = OutputInspector.Inspect(to, output);
                            MatrixExpectations.Check(src, to, snap, result);
                        });
                    }
                }
            }

            return s.Build();
        }
    }
}
