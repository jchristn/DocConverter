namespace Test.Shared.Suites.Docx
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Model;
    using Test.Shared.Fixtures;
    using Test.Shared.Fixtures.Builders;

    /// <summary>
    /// DOCX failure cases: corrupt, encrypted, legacy, oversized and cancelled input.
    /// </summary>
    public static class DocxNegativeCases
    {
        /// <summary>A truncated zip throws DocumentReadException.</summary>
        public static async Task Truncated(CancellationToken token)
        {
            await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => DocxReaderCases.ReadAsync(DocxEdgeFixtures.Truncated()), "truncated zip").ConfigureAwait(false);
        }

        /// <summary>A zip without word/document.xml throws DocumentReadException.</summary>
        public static async Task MissingMainPart(CancellationToken token)
        {
            await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => DocxReaderCases.ReadAsync(DocxEdgeFixtures.MissingMainPart()), "missing main part").ConfigureAwait(false);
        }

        /// <summary>An encrypted (password protected) package throws DocumentReadException mentioning the password.</summary>
        public static async Task PasswordProtected(CancellationToken token)
        {
            DocumentReadException ex = await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => DocxReaderCases.ReadAsync(DocxEdgeFixtures.PasswordProtected()), "encrypted").ConfigureAwait(false);
            TestSupport.AssertContains(ex.Message, "password", "message mentions password");
        }

        /// <summary>A legacy .doc passed as DOCX throws DocumentReadException naming the format.</summary>
        public static async Task LegacyDoc(CancellationToken token)
        {
            DocumentReadException ex = await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => DocxReaderCases.ReadAsync(DocxEdgeFixtures.LegacyDoc()), "legacy doc").ConfigureAwait(false);
            TestSupport.AssertContains(ex.Message, ".doc", "message names .doc");
        }

        /// <summary>Parts that decompress beyond MaxDecompressedBytes throw InputTooLargeException.</summary>
        public static async Task ZipBomb(CancellationToken token)
        {
            ConverterSettings settings = new ConverterSettings();
            settings.MaxDecompressedBytes = 2048;
            using (Converter converter = new Converter(settings))
            {
                await TestSupport.ExpectThrowsAsync<InputTooLargeException>(() => converter.ReadAsync(DocxReaderCases.Reference, DocumentFormatEnum.Docx), "zip bomb guard").ConfigureAwait(false);
            }
        }

        /// <summary>Empty input throws DocumentReadException.</summary>
        public static async Task Empty(CancellationToken token)
        {
            await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => DocxReaderCases.ReadAsync(new byte[0]), "empty").ConfigureAwait(false);
        }

        /// <summary>Random bytes throw DocumentReadException.</summary>
        public static async Task NotAZip(CancellationToken token)
        {
            byte[] junk = new byte[4096];
            new Random(42).NextBytes(junk);
            await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => DocxReaderCases.ReadAsync(junk), "random bytes").ConfigureAwait(false);
        }

        /// <summary>A cancelled token stops reading and writing.</summary>
        public static async Task Cancelled(CancellationToken token)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            using (Converter converter = new Converter())
            {
                cts.Cancel();
                await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => converter.ReadAsync(DocxReaderCases.Reference, DocumentFormatEnum.Docx, null, cts.Token), "read cancelled").ConfigureAwait(false);
                await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => converter.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Docx, null, cts.Token), "write cancelled").ConfigureAwait(false);
                await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => converter.ConvertToBytesAsync(DocxReaderCases.Reference, DocumentFormatEnum.Docx, DocumentFormatEnum.Docx, null, cts.Token), "convert cancelled").ConfigureAwait(false);
            }
        }

        /// <summary>The reader does not modify or close the caller's stream and the converter reads from its position.</summary>
        public static async Task StreamInput(CancellationToken token)
        {
            byte[] prefix = new byte[] { 1, 2, 3 };
            byte[] docx = DocxReaderCases.Reference;
            byte[] combined = new byte[prefix.Length + docx.Length];
            Buffer.BlockCopy(prefix, 0, combined, 0, prefix.Length);
            Buffer.BlockCopy(docx, 0, combined, prefix.Length, docx.Length);
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream(combined))
            using (Converter converter = new Converter())
            {
                ms.Position = prefix.Length;
                DocumentModel doc = await converter.ReadAsync(ms, DocumentFormatEnum.Docx).ConfigureAwait(false);
                TestSupport.Assert(doc.Blocks.Count > 5, "read from offset");
                TestSupport.Assert(ms.CanRead, "stream still open");
            }
        }
    }
}
