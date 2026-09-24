namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Model;
    using Test.Shared.Suites.Pdf;
    using Touchstone.Core;

    /// <summary>
    /// Image reader tests.
    /// </summary>
    public static class ImageSuite
    {
        private static readonly string[][] _Samples = new string[][]
        {
            new string[] { "sample.png", "Png", "image/png" },
            new string[] { "sample-alpha.png", "Png", "image/png" },
            new string[] { "sample-palette.png", "Png", "image/png" },
            new string[] { "sample.jpg", "Jpeg", "image/jpeg" },
            new string[] { "sample-progressive.jpg", "Jpeg", "image/jpeg" },
            new string[] { "sample-cmyk.jpg", "Jpeg", "image/jpeg" },
            new string[] { "sample.gif", "Gif", "image/gif" },
            new string[] { "sample.bmp", "Bmp", "image/bmp" },
            new string[] { "sample.tiff", "Tiff", "image/tiff" },
            new string[] { "sample.webp", "WebP", "image/webp" },
            new string[] { "sample-lossless.webp", "WebP", "image/webp" }
        };

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            foreach (string[] sample in _Samples)
            {
                string file = sample[0];
                DocumentFormatEnum format = (DocumentFormatEnum)Enum.Parse(typeof(DocumentFormatEnum), sample[1]);
                string mediaType = sample[2];
                cases.Add(Case("Read_" + file.Replace('.', '_').Replace('-', '_'), "Reads " + file + " as one 96x64 " + format + " image", async ct =>
                {
                    byte[] data = FormatFixtures.Load("Images/" + file);
                    DocumentModel model = await Read(data, format).ConfigureAwait(false);
                    TestSupport.AssertEqual(1, model.Blocks.Count, "one block");
                    ImageBlock image = (ImageBlock)model.Blocks[0];
                    BinaryResource resource = model.Resources[image.ResourceId];
                    TestSupport.AssertEqual(mediaType, resource.MediaType, "media type");
                    TestSupport.AssertEqual(96, resource.PixelWidth ?? 0, "pixel width");
                    TestSupport.AssertEqual(64, resource.PixelHeight ?? 0, "pixel height");
                    TestSupport.AssertEqual(data.Length, resource.Data.Length, "bytes kept");
                    TestSupport.Assert(!string.IsNullOrEmpty(image.AltText), "alt text");
                    TestSupport.Assert(!string.IsNullOrEmpty(resource.FileName), "file name");
                }));
                cases.Add(Case("Auto_" + file.Replace('.', '_').Replace('-', '_'), "Auto detection reads " + file + " as " + format, async ct =>
                {
                    using (Converter converter = new Converter())
                    {
                        DocumentModel model = await converter.ReadAsync(FormatFixtures.Load("Images/" + file), DocumentFormatEnum.Auto).ConfigureAwait(false);
                        TestSupport.AssertEqual(mediaType, model.Resources[((ImageBlock)model.Blocks[0]).ResourceId].MediaType, "detected media type");
                    }
                }));
            }

            cases.Add(Case("FormatMismatch", "PNG bytes declared as JPEG throw DocumentReadException", async ct =>
            {
                await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(FormatFixtures.Load("Images/sample.png"), DocumentFormatEnum.Jpeg), "mismatch").ConfigureAwait(false);
            }));
            cases.Add(Case("TruncatedPng", "A PNG cut before its IHDR dimensions throws DocumentReadException", async ct =>
            {
                byte[] png = FormatFixtures.Load("Images/sample.png");
                byte[] cut = new byte[12];
                Buffer.BlockCopy(png, 0, cut, 0, cut.Length);
                await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(cut, DocumentFormatEnum.Png), "truncated png").ConfigureAwait(false);
            }));
            cases.Add(Case("TruncatedJpeg", "A JPEG cut before its frame header throws DocumentReadException", async ct =>
            {
                byte[] jpg = FormatFixtures.Load("Images/sample.jpg");
                byte[] cut = new byte[20];
                Buffer.BlockCopy(jpg, 0, cut, 0, cut.Length);
                await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(cut, DocumentFormatEnum.Jpeg), "truncated jpeg").ConfigureAwait(false);
            }));
            cases.Add(Case("GarbageInput", "Random bytes declared as GIF throw DocumentReadException", async ct =>
            {
                byte[] junk = new byte[256];
                new Random(3).NextBytes(junk);
                junk[0] = 0x00;
                await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(junk, DocumentFormatEnum.Gif), "garbage").ConfigureAwait(false);
            }));
            cases.Add(Case("EmptyInput", "Empty input declared as PNG throws DocumentReadException", async ct =>
            {
                await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => Read(new byte[0], DocumentFormatEnum.Png), "empty").ConfigureAwait(false);
            }));
            cases.Add(Case("Base64StringInput", "A base64 string declared as PNG reads the image", async ct =>
            {
                using (Converter converter = new Converter())
                {
                    string b64 = Convert.ToBase64String(FormatFixtures.Load("Images/sample.png"));
                    DocumentModel model = await converter.ReadAsync(b64, DocumentFormatEnum.Png).ConfigureAwait(false);
                    TestSupport.AssertEqual(96, model.Resources[((ImageBlock)model.Blocks[0]).ResourceId].PixelWidth ?? 0, "width from base64");
                }
            }));
            cases.Add(Case("PreCancelled", "A cancelled token throws OperationCanceledException", async ct =>
            {
                using (CancellationTokenSource cts = new CancellationTokenSource())
                using (Converter converter = new Converter())
                {
                    cts.Cancel();
                    await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => converter.ReadAsync(FormatFixtures.Load("Images/sample.png"), DocumentFormatEnum.Png, null, cts.Token), "cancelled").ConfigureAwait(false);
                }
            }));

            return new TestSuiteDescriptor(
                suiteId: "Image",
                displayName: "Image reader",
                cases: cases);
        }

        private static async Task<DocumentModel> Read(byte[] data, DocumentFormatEnum format)
        {
            using (Converter converter = new Converter())
            {
                return await converter.ReadAsync(data, format).ConfigureAwait(false);
            }
        }

        private static TestCaseDescriptor Case(string id, string name, Func<CancellationToken, Task> body)
        {
            return new TestCaseDescriptor("Image", id, name, executeAsync: body);
        }
    }
}
