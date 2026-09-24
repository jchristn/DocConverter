namespace Test.Shared.Suites
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Model;
    using DocConverter.Options;
    using DocConverter.Results;
    using Test.Shared.Fixtures;
    using Test.Shared.Inspection;
    using Touchstone.Core;

    /// <summary>
    /// The public API contract: argument validation on every overload, stream ownership and position semantics, string
    /// input rules (text versus base64), size limits, result fields, and ConvertFileAsync.
    /// </summary>
    public static class ApiContractSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            SuiteBuilder s = new SuiteBuilder("ApiContract", "Public API contract");
            Converter c = new Converter();
            byte[] md = TextFixtures.Reference(DocumentFormatEnum.Markdown);

            s.Add("NullArguments", "Every overload rejects null input, output and document with ArgumentNullException", async ct =>
            {
                using (MemoryStream o = new MemoryStream())
                {
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ConvertAsync((string)null!, DocumentFormatEnum.Text, DocumentFormatEnum.Html, o), "string input").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ConvertAsync((byte[])null!, DocumentFormatEnum.Text, DocumentFormatEnum.Html, o), "bytes input").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ConvertAsync((Stream)null!, DocumentFormatEnum.Text, DocumentFormatEnum.Html, o), "stream input").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ConvertAsync("x", DocumentFormatEnum.Text, DocumentFormatEnum.Html, null!), "null output").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ConvertAsync(new byte[1], DocumentFormatEnum.Text, DocumentFormatEnum.Html, null!), "null output bytes").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ConvertToStringAsync((string)null!, DocumentFormatEnum.Text, DocumentFormatEnum.Html), "to string").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ConvertToStringAsync((byte[])null!, DocumentFormatEnum.Text, DocumentFormatEnum.Html), "to string bytes").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ConvertToStringAsync((Stream)null!, DocumentFormatEnum.Text, DocumentFormatEnum.Html), "to string stream").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ConvertToBytesAsync((string)null!, DocumentFormatEnum.Text, DocumentFormatEnum.Html), "to bytes").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ConvertToBytesAsync((byte[])null!, DocumentFormatEnum.Text, DocumentFormatEnum.Html), "to bytes bytes").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ConvertToBytesAsync((Stream)null!, DocumentFormatEnum.Text, DocumentFormatEnum.Html), "to bytes stream").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ReadAsync((string)null!, DocumentFormatEnum.Text), "read string").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ReadAsync((byte[])null!, DocumentFormatEnum.Text), "read bytes").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ReadAsync((Stream)null!, DocumentFormatEnum.Text), "read stream").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.WriteAsync(null!, DocumentFormatEnum.Html, o), "write doc").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.WriteAsync(new DocumentModel(), DocumentFormatEnum.Html, null!), "write output").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.WriteToStringAsync(null!, DocumentFormatEnum.Html), "write to string").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.WriteToBytesAsync(null!, DocumentFormatEnum.Html), "write to bytes").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.DetectFormatAsync((byte[])null!), "detect bytes").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.DetectFormatAsync((Stream)null!), "detect stream").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.DetectFormatAsync((string)null!), "detect string").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ConvertFileAsync(null!, "x.md"), "file in").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentNullException>(() => c.ConvertFileAsync("x.md", null!), "file out").ConfigureAwait(false);
                }

                TestSupport.ExpectThrows<ArgumentNullException>(() => c.RegisterReader(null!), "register reader");
                TestSupport.ExpectThrows<ArgumentNullException>(() => c.RegisterWriter(null!), "register writer");
            });

            s.Add("AutoTarget", "Auto as a target is rejected with ArgumentException on every overload", async ct =>
            {
                using (MemoryStream o = new MemoryStream())
                {
                    await TestSupport.ExpectThrowsAsync<ArgumentException>(() => c.ConvertAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Auto, o), "stream out").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentException>(() => c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Auto), "string out").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentException>(() => c.ConvertToBytesAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Auto), "bytes out").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentException>(() => c.WriteToStringAsync(new DocumentModel(), DocumentFormatEnum.Auto), "write").ConfigureAwait(false);
                }

                TestSupport.Assert(!c.CanConvert(DocumentFormatEnum.Markdown, DocumentFormatEnum.Auto), "CanConvert false for Auto target");
                TestSupport.Assert(c.CanConvert(DocumentFormatEnum.Auto, DocumentFormatEnum.Html), "CanConvert true for Auto source");
            });

            s.Add("StreamCapabilities", "A read-only output stream and a write-only input stream are rejected with ArgumentException", async ct =>
            {
                using (MemoryStream readOnly = new MemoryStream(new byte[10], false))
                    await TestSupport.ExpectThrowsAsync<ArgumentException>(() => c.ConvertAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, readOnly), "read-only output").ConfigureAwait(false);
                using (WriteOnlyStream writeOnly = new WriteOnlyStream())
                    await TestSupport.ExpectThrowsAsync<ArgumentException>(() => c.ConvertToStringAsync(writeOnly, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html), "write-only input").ConfigureAwait(false);
            });

            s.Add("OutputStreamSemantics", "Output is written at the current position, flushed, left open and positioned after the data", async ct =>
            {
                using (MemoryStream o = new MemoryStream())
                {
                    byte[] prefix = Encoding.ASCII.GetBytes("PREFIX:");
                    o.Write(prefix, 0, prefix.Length);
                    ConversionResult r = await c.ConvertAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Text, o, null, ct).ConfigureAwait(false);
                    TestSupport.Assert(o.CanWrite, "stream still open");
                    TestSupport.AssertEqual(prefix.Length + r.BytesWritten, o.Position, "positioned after data");
                    string all = Encoding.UTF8.GetString(o.ToArray());
                    TestSupport.Assert(all.StartsWith("PREFIX:Reference Document", StringComparison.Ordinal), "appended after prefix");
                }
            });

            s.Add("NonSeekableOutput", "Output can be a write-only non-seekable stream", async ct =>
            {
                using (WriteOnlyStream o = new WriteOnlyStream())
                {
                    ConversionResult r = await c.ConvertAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, o, null, ct).ConfigureAwait(false);
                    TestSupport.AssertEqual(r.BytesWritten, (long)o.Written.Length, "every byte delivered");
                }
            });

            s.Add("InputStreamSemantics", "Input streams are read from their current position and never closed", async ct =>
            {
                using (MemoryStream i = new MemoryStream())
                {
                    byte[] junk = Encoding.ASCII.GetBytes("JUNKJUNK");
                    i.Write(junk, 0, junk.Length);
                    i.Write(md, 0, md.Length);
                    i.Position = junk.Length;
                    StringConversionResult r = await c.ConvertToStringAsync(i, DocumentFormatEnum.Markdown, DocumentFormatEnum.Text, null, ct).ConfigureAwait(false);
                    TestSupport.Assert(r.Output.StartsWith("Reference Document", StringComparison.Ordinal), "read from position");
                    TestSupport.Assert(i.CanRead, "input not closed");
                    TestSupport.AssertEqual((long)md.Length, r.BytesRead, "bytes read excludes skipped prefix");
                }

                using (NonSeekableStream ns = new NonSeekableStream(md, 7))
                {
                    StringConversionResult r = await c.ConvertToStringAsync(ns, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                    TestSupport.AssertContains(r.Output, "<h1>Reference Document</h1>", "non-seekable 7 bytes per read");
                    TestSupport.Assert(!ns.IsDisposed, "non-seekable input not disposed");
                }
            });

            s.Add("StringInputRules", "Strings are documents for text formats and base64 for binary formats; bad base64 is a read error", async ct =>
            {
                StringConversionResult text = await c.ConvertToStringAsync("# Hi", DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                TestSupport.AssertContains(text.Output, "<h1>Hi</h1>", "text format string");
                byte[] docx = (await c.WriteToBytesAsync(ReferenceContent.ToModel(), DocumentFormatEnum.Docx, null, ct).ConfigureAwait(false)).Output;
                DocumentModel viaB64 = await c.ReadAsync(Convert.ToBase64String(docx), DocumentFormatEnum.Docx, null, ct).ConfigureAwait(false);
                TestSupport.Assert(ModelInspector.Inspect(viaB64).Headings.Contains(ReferenceContent.Heading1), "base64 docx string read");
                DocumentModel viaDataUri = await c.ReadAsync("data:application/octet-stream;base64," + Convert.ToBase64String(docx), DocumentFormatEnum.Docx, null, ct).ConfigureAwait(false);
                TestSupport.Assert(viaDataUri.Blocks.Count > 0, "data URI prefix accepted");
                DocumentModel autoB64 = await c.ReadAsync(Convert.ToBase64String(docx), DocumentFormatEnum.Auto, null, ct).ConfigureAwait(false);
                TestSupport.Assert(autoB64.Blocks.Count > 0, "Auto recognizes base64 docx");
                DocumentReadException bad = await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => c.ReadAsync("not base64 !!!", DocumentFormatEnum.Png, null, ct), "invalid base64").ConfigureAwait(false);
                TestSupport.AssertContains(bad.Message, "base64", "message explains base64");
            });

            s.Add("StringOutputRules", "Text targets return text; binary targets return base64 with IsBase64", async ct =>
            {
                StringConversionResult html = await c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                TestSupport.Assert(!html.IsBase64, "html is text");
                StringConversionResult docx = await c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Docx, null, ct).ConfigureAwait(false);
                TestSupport.Assert(docx.IsBase64, "docx is base64");
                byte[] bytes = Convert.FromBase64String(docx.Output);
                TestSupport.Assert(bytes[0] == (byte)'P' && bytes[1] == (byte)'K', "decodes to a zip");
                TestSupport.AssertEqual(docx.BytesWritten, (long)bytes.Length, "BytesWritten matches decoded length");
            });

            s.Add("ResultFields", "Results report formats, byte counts, timings, statistics and metadata consistently", async ct =>
            {
                BytesConversionResult r = await c.ConvertToBytesAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual(DocumentFormatEnum.Markdown, r.SourceFormat, "source");
                TestSupport.AssertEqual(DocumentFormatEnum.Html, r.TargetFormat, "target");
                TestSupport.AssertEqual((long)md.Length, r.BytesRead, "bytes read");
                TestSupport.AssertEqual((long)r.Output.Length, r.BytesWritten, "bytes written");
                TestSupport.Assert(r.TotalMs >= r.ReadMs && r.TotalMs >= r.WriteMs, "timings");
                TestSupport.Assert(r.CompletedUtc >= r.StartedUtc, "timestamps");
                TestSupport.AssertEqual(4, r.Statistics.Headings, "headings");
                TestSupport.AssertEqual(1, r.Statistics.Tables, "tables");
                TestSupport.AssertEqual(4, r.Statistics.TableRows, "rows");
                TestSupport.AssertEqual(12, r.Statistics.TableCells, "cells");
                TestSupport.AssertEqual(1, r.Statistics.Images, "images");
                TestSupport.AssertEqual(1, r.Statistics.Links, "links");
                TestSupport.AssertEqual(4, r.Statistics.Lists, "bullets, two nested lists and the steps");
                TestSupport.AssertEqual(ReferenceContent.Title, r.Metadata.Title, "metadata title");
                TestSupport.Assert(r.Statistics.Characters > 300, "characters counted");
                TestSupport.Assert(r.DetectedSource == null, "no detection when source given");
            });

            s.Add("MaxInputBytes", "MaxInputBytes is enforced at the limit for bytes, strings and streams", async ct =>
            {
                byte[] data = Encoding.UTF8.GetBytes(new string('a', 1000));
                Converter limited = new Converter(new ConverterSettings { MaxInputBytes = 1000 });
                await limited.ConvertToStringAsync(data, DocumentFormatEnum.Text, DocumentFormatEnum.Text, null, ct).ConfigureAwait(false);
                Converter tighter = new Converter(new ConverterSettings { MaxInputBytes = 999 });
                InputTooLargeException ex = await TestSupport.ExpectThrowsAsync<InputTooLargeException>(() => tighter.ConvertToStringAsync(data, DocumentFormatEnum.Text, DocumentFormatEnum.Text, null, ct), "bytes").ConfigureAwait(false);
                TestSupport.AssertEqual(999L, ex.LimitBytes, "limit reported");
                await TestSupport.ExpectThrowsAsync<InputTooLargeException>(() => tighter.ConvertToStringAsync(new string('a', 1000), DocumentFormatEnum.Text, DocumentFormatEnum.Text, null, ct), "string").ConfigureAwait(false);
                using (NonSeekableStream ns = new NonSeekableStream(data, 100))
                    await TestSupport.ExpectThrowsAsync<InputTooLargeException>(() => tighter.ConvertToStringAsync(ns, DocumentFormatEnum.Text, DocumentFormatEnum.Text, null, ct), "non-seekable stream").ConfigureAwait(false);
                using (MemoryStream ms = new MemoryStream(data))
                    await TestSupport.ExpectThrowsAsync<InputTooLargeException>(() => tighter.ConvertToStringAsync(ms, DocumentFormatEnum.Text, DocumentFormatEnum.Text, null, ct), "seekable stream").ConfigureAwait(false);
            });

            s.Add("EmptyInput", "Empty input converts for text formats and is a read error for binary formats", async ct =>
            {
                foreach (DocumentFormatEnum f in new[] { DocumentFormatEnum.Text, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, DocumentFormatEnum.Json, DocumentFormatEnum.Xml, DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv })
                {
                    StringConversionResult r = await c.ConvertToStringAsync(new byte[0], f, DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                    TestSupport.AssertEqual("", r.Output, f + " empty to markdown");
                }

                foreach (DocumentFormatEnum f in new[] { DocumentFormatEnum.Docx, DocumentFormatEnum.Png })
                    await TestSupport.ExpectThrowsAsync<DocumentReadException>(() => c.ConvertToStringAsync(new byte[0], f, DocumentFormatEnum.Markdown, null, ct), f + " empty").ConfigureAwait(false);
            });

            s.Add("ConvertFile", "ConvertFileAsync infers formats, refuses to overwrite, and honors overwrite", async ct =>
            {
                string dir = Path.Combine(Path.GetTempPath(), "docconverter-tests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(dir);
                try
                {
                    string input = Path.Combine(dir, "in.md");
                    string output = Path.Combine(dir, "out.html");
                    File.WriteAllBytes(input, md);
                    ConversionResult r = await c.ConvertFileAsync(input, output, token: ct).ConfigureAwait(false);
                    TestSupport.AssertEqual(DocumentFormatEnum.Markdown, r.SourceFormat, "inferred source");
                    TestSupport.AssertEqual(DocumentFormatEnum.Html, r.TargetFormat, "inferred target");
                    TestSupport.AssertContains(File.ReadAllText(output), "<h1>Reference Document</h1>", "html written");
                    await TestSupport.ExpectThrowsAsync<IOException>(() => c.ConvertFileAsync(input, output, token: ct), "exists").ConfigureAwait(false);
                    await c.ConvertFileAsync(input, output, DocumentFormatEnum.Auto, DocumentFormatEnum.Text, true, null, ct).ConfigureAwait(false);
                    TestSupport.Assert(File.ReadAllText(output).StartsWith("Reference Document", StringComparison.Ordinal), "overwritten with text");
                    await TestSupport.ExpectThrowsAsync<FileNotFoundException>(() => c.ConvertFileAsync(Path.Combine(dir, "missing.md"), Path.Combine(dir, "x.html"), token: ct), "missing").ConfigureAwait(false);
                    await TestSupport.ExpectThrowsAsync<ArgumentException>(() => c.ConvertFileAsync(input, Path.Combine(dir, "out.unknown"), token: ct), "unknown target extension").ConfigureAwait(false);
                }
                finally
                {
                    Directory.Delete(dir, true);
                }
            });

            s.Add("ReadWriteTwoStep", "ReadAsync then WriteAsync equals a one step conversion, and WriteAsync does not modify the caller's model", async ct =>
            {
                ConversionOptions o = new ConversionOptions { IncludeImages = false, Title = "Changed" };
                StringConversionResult one = await c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, o, ct).ConfigureAwait(false);
                DocumentModel model = await c.ReadAsync(md, DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                string before = ModelComparer.Canonical(model);
                StringConversionResult two = await c.WriteToStringAsync(model, DocumentFormatEnum.Html, o, ct).ConfigureAwait(false);
                TestSupport.AssertEqual(one.Output, two.Output, "same output");
                TestSupport.AssertEqual(before, ModelComparer.Canonical(model), "caller's model unchanged");
                TestSupport.AssertEqual(DocumentFormatEnum.Auto, two.SourceFormat, "write-only source is Auto");
            });

            s.Add("Capabilities", "Supported conversions cover the full built-in matrix with fidelity and notes", ct =>
            {
                TestSupport.AssertEqual(18, c.GetInputFormats().Count, "input formats");
                TestSupport.AssertEqual(11, c.GetOutputFormats().Count, "output formats");
                TestSupport.AssertEqual(198, c.GetSupportedConversions().Count, "pairs");
                foreach (SupportedConversion p in c.GetSupportedConversions())
                {
                    TestSupport.Assert(c.CanConvert(p.From, p.To), "CanConvert " + p);
                    if (p.Fidelity == FidelityEnum.Projection) TestSupport.Assert(p.Notes.Length > 0, "projection has notes: " + p);
                }

                TestSupport.Assert(!c.CanConvert(DocumentFormatEnum.Markdown, DocumentFormatEnum.Rtf), "no RTF writer");
                TestSupport.Assert(!c.CanConvert(DocumentFormatEnum.Markdown, DocumentFormatEnum.Png), "no image writer");
                return Task.CompletedTask;
            });

            s.Add("Dispose", "Converter disposes cleanly and twice", ct =>
            {
                Converter d = new Converter();
                d.Dispose();
                d.Dispose();
                return Task.CompletedTask;
            });

            return s.Build();
        }
    }
}
