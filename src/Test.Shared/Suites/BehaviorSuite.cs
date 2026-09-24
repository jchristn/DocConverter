namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter;
    using DocConverter.DependencyInjection;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Model;
    using DocConverter.Observability;
    using DocConverter.Ocr;
    using DocConverter.Options;
    using DocConverter.Readers;
    using DocConverter.Results;
    using DocConverter.Writers;
    using Microsoft.Extensions.DependencyInjection;
    using Test.Shared.Doubles;
    using Test.Shared.Fixtures;
    using Test.Shared.Inspection;
    using Touchstone.Core;

    /// <summary>
    /// Cross-cutting behavior: encodings, line endings, cancellation, concurrency, OCR stubs, diagnostics,
    /// extensibility and dependency injection, option validation and presets, and strict mode.
    /// </summary>
    public static class BehaviorSuite
    {
        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            SuiteBuilder s = new SuiteBuilder("Behavior", "Cross-cutting behavior");
            Converter c = new Converter();
            byte[] md = TextFixtures.Reference(DocumentFormatEnum.Markdown);

            // Encodings and line endings.
            foreach (Encoding e in new Encoding[] { new UTF8Encoding(true), new UTF8Encoding(false), new UnicodeEncoding(false, true), new UnicodeEncoding(true, true), new UTF32Encoding(false, true) })
            {
                Encoding enc = e;
                string label = enc.WebName + (enc.GetPreamble().Length > 0 ? "-bom" : "");
                s.Add("InputEncoding_" + label, "Markdown in " + label + " reads with every character intact", async ct =>
                {
                    string text = TestSupport.FixtureText("Text/reference.md");
                    byte[] bytes = enc.GetPreamble().Concat(enc.GetBytes(text)).ToArray();
                    DocumentModel m = await c.ReadAsync(bytes, DocumentFormatEnum.Markdown, null, ct).ConfigureAwait(false);
                    TestSupport.Assert(ModelInspector.Inspect(m).ContainsText(ReferenceContent.International), "international text survives " + label);
                });
            }

            s.Add("InputEncodingOverride", "InputEncoding decodes BOM-less legacy text (Windows-1252 style Latin-1)", async ct =>
            {
                byte[] latin1 = new byte[] { (byte)'c', (byte)'a', (byte)'f', 0xE9 };
                ConversionOptions o = new ConversionOptions { InputEncoding = Encoding.GetEncoding("iso-8859-1") };
                StringConversionResult r = await c.ConvertToStringAsync(latin1, DocumentFormatEnum.Text, DocumentFormatEnum.Text, o, ct).ConfigureAwait(false);
                TestSupport.AssertEqual("café\n", r.Output, "decoded with override");
            });

            s.Add("OutputEncodings", "OutputEncoding controls bytes and byte order marks", async ct =>
            {
                ConversionOptions bom = new ConversionOptions { OutputEncoding = new UTF8Encoding(true) };
                BytesConversionResult a = await c.ConvertToBytesAsync("x", DocumentFormatEnum.Text, DocumentFormatEnum.Text, bom, ct).ConfigureAwait(false);
                TestSupport.Assert(a.Output.Length >= 3 && a.Output[0] == 0xEF && a.Output[1] == 0xBB && a.Output[2] == 0xBF, "utf-8 bom");
                BytesConversionResult b = await c.ConvertToBytesAsync("x", DocumentFormatEnum.Text, DocumentFormatEnum.Text, null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual(0x78, (int)b.Output[0], "default has no bom");
                ConversionOptions utf16 = new ConversionOptions { OutputEncoding = new UnicodeEncoding(false, false) };
                BytesConversionResult u = await c.ConvertToBytesAsync("é", DocumentFormatEnum.Text, DocumentFormatEnum.Text, utf16, ct).ConfigureAwait(false);
                TestSupport.AssertEqual("é\n", Encoding.Unicode.GetString(u.Output), "utf-16 output");
                StringConversionResult s16 = await c.ConvertToStringAsync("é", DocumentFormatEnum.Text, DocumentFormatEnum.Text, utf16, ct).ConfigureAwait(false);
                TestSupport.AssertEqual("é\n", s16.Output, "string output decoded with the output encoding");
            });

            s.Add("LineEndings", "LineEnding Lf, CrLf and Platform are applied to every text writer", async ct =>
            {
                foreach (DocumentFormatEnum f in new[] { DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, DocumentFormatEnum.Text, DocumentFormatEnum.Json, DocumentFormatEnum.Xml, DocumentFormatEnum.Csv })
                {
                    ConversionOptions crlf = new ConversionOptions { LineEnding = LineEndingEnum.CrLf };
                    string a = (await c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, f, crlf, ct).ConfigureAwait(false)).Output;
                    TestSupport.Assert(a.Contains("\r\n") && !a.Replace("\r\n", "").Contains("\n"), f + " all CRLF");
                    string b = (await c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, f, null, ct).ConfigureAwait(false)).Output;
                    TestSupport.Assert(!b.Contains("\r"), f + " default LF");
                    ConversionOptions platform = new ConversionOptions { LineEnding = LineEndingEnum.Platform };
                    string p = (await c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, f, platform, ct).ConfigureAwait(false)).Output;
                    TestSupport.Assert(p.Contains(Environment.NewLine), f + " platform newline");
                }
            });

            // Cancellation.
            s.Add("PreCancelled", "A token cancelled up front throws OperationCanceledException on every overload", async ct =>
            {
                using (CancellationTokenSource cts = new CancellationTokenSource())
                {
                    cts.Cancel();
                    using (MemoryStream o = new MemoryStream())
                    {
                        await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => c.ConvertAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, o, null, cts.Token), "bytes to stream").ConfigureAwait(false);
                        await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => c.ConvertToStringAsync("x", DocumentFormatEnum.Text, DocumentFormatEnum.Html, null, cts.Token), "string").ConfigureAwait(false);
                        using (MemoryStream i = new MemoryStream(md))
                            await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => c.ConvertToBytesAsync(i, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, null, cts.Token), "stream").ConfigureAwait(false);
                        await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => c.ReadAsync(md, DocumentFormatEnum.Markdown, null, cts.Token), "read").ConfigureAwait(false);
                        await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => c.WriteToStringAsync(new DocumentModel(), DocumentFormatEnum.Html, null, cts.Token), "write").ConfigureAwait(false);
                        await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => c.DetectFormatAsync(md, null, cts.Token), "detect").ConfigureAwait(false);
                    }
                }
            });

            s.Add("MidReadCancellation", "Cancelling while a slow stream is being read stops the conversion promptly", async ct =>
            {
                using (CancellationTokenSource cts = new CancellationTokenSource())
                using (NonSeekableStream slow = new NonSeekableStream(md, 16, cts, 5))
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => c.ConvertToStringAsync(slow, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, null, cts.Token), "mid read").ConfigureAwait(false);
                    TestSupport.Assert(slow.Reads < 20, "stopped soon after cancellation (" + slow.Reads + " reads)");
                    TestSupport.Assert(sw.ElapsedMilliseconds < 5000, "promptly");
                }
            });

            s.Add("CancellationDuringWrite", "A writer observing cancellation mid-document stops with OperationCanceledException", async ct =>
            {
                Converter slowConverter = new Converter();
                using (CancellationTokenSource cts = new CancellationTokenSource())
                {
                    slowConverter.RegisterWriter(new CancellingWriter(cts));
                    await TestSupport.ExpectThrowsAsync<OperationCanceledException>(() => slowConverter.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Text, null, cts.Token), "writer cancel").ConfigureAwait(false);
                }
            });

            // Concurrency.
            s.Add("ConcurrentConversions", "32 parallel conversions on one Converter match sequential results", async ct =>
            {
                DocumentFormatEnum[] targets = { DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, DocumentFormatEnum.Text, DocumentFormatEnum.Json, DocumentFormatEnum.Xml, DocumentFormatEnum.Csv, DocumentFormatEnum.Docx, DocumentFormatEnum.Tsv };
                ConversionOptions o = new ConversionOptions { Deterministic = true };
                Dictionary<DocumentFormatEnum, string> expected = new Dictionary<DocumentFormatEnum, string>();
                foreach (DocumentFormatEnum t in targets) expected[t] = Convert.ToBase64String((await c.ConvertToBytesAsync(md, DocumentFormatEnum.Markdown, t, o, ct).ConfigureAwait(false)).Output);
                List<Task<bool>> tasks = new List<Task<bool>>();
                for (int i = 0; i < 32; i++)
                {
                    DocumentFormatEnum t = targets[i % targets.Length];
                    tasks.Add(Task.Run(async () => Convert.ToBase64String((await c.ConvertToBytesAsync(md, DocumentFormatEnum.Markdown, t, o, ct).ConfigureAwait(false)).Output) == expected[t]));
                }

                bool[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
                TestSupport.Assert(results.All(x => x), "every parallel result identical to sequential");
            });

            s.Add("ConcurrentRegistration", "Registering writers while conversions run is safe", async ct =>
            {
                Converter shared = new Converter();
                List<Task> tasks = new List<Task>();
                for (int i = 0; i < 16; i++)
                {
                    tasks.Add(Task.Run(async () => await shared.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, null, ct).ConfigureAwait(false)));
                    tasks.Add(Task.Run(() => shared.RegisterWriter(new DocConverter.Writers.Html.HtmlDocumentWriter())));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            });

            // OCR stubs.
            s.AddSync("OcrBaseThrows", "Every OcrProviderBase member throws NotImplementedException until overridden", () =>
            {
                StubOcr ocr = new StubOcr();
                TestSupport.ExpectThrows<NotImplementedException>(() => { string n = ocr.Name; }, "Name");
                TestSupport.ExpectThrows<NotImplementedException>(() => ocr.Supports("image/png"), "Supports");
                TestSupport.ExpectThrows<NotImplementedException>(() => ocr.RecognizeAsync(new BinaryResource(), new OcrOptions()), "RecognizeAsync");
            });

            s.Add("OcrModeOffIgnoresProvider", "A configured provider with OcrMode Off converts normally", async ct =>
            {
                Converter withOcr = new Converter(new ConverterSettings { OcrProvider = new StubOcr() });
                StringConversionResult r = await withOcr.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, null, ct).ConfigureAwait(false);
                TestSupport.AssertContains(r.Output, "<h1>", "converted");
            });

            s.Add("OcrModeOnThrows", "A configured provider with any other OcrMode throws NotImplementedException before reading", async ct =>
            {
                Converter withOcr = new Converter(new ConverterSettings { OcrProvider = new StubOcr() });
                foreach (OcrModeEnum mode in new[] { OcrModeEnum.ImagesOnly, OcrModeEnum.PagesWithoutText, OcrModeEnum.All })
                {
                    ConversionOptions o = new ConversionOptions { OcrMode = mode };
                    NotImplementedException ex = await TestSupport.ExpectThrowsAsync<NotImplementedException>(() => withOcr.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, o, ct), mode.ToString()).ConfigureAwait(false);
                    TestSupport.AssertContains(ex.Message, "DocConverter " + DocConverter.Observability.DocConverterDiagnostics.Version, "message names the release");
                    await TestSupport.ExpectThrowsAsync<NotImplementedException>(() => withOcr.ReadAsync(md, DocumentFormatEnum.Markdown, o, ct), "read " + mode).ConfigureAwait(false);
                }

                ConversionOptions noProvider = new ConversionOptions { OcrMode = OcrModeEnum.All };
                await c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, noProvider, ct).ConfigureAwait(false);
            });

            s.AddSync("OcrOptionsValidation", "OcrOptions validates confidence and never holds a null language list", () =>
            {
                OcrOptions o = new OcrOptions();
                TestSupport.AssertEqual("eng", o.Languages.Single(), "default language");
                o.Languages = null!;
                TestSupport.AssertEqual(0, o.Languages.Count, "null coalesced");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => o.MinimumConfidence = 1.5, "confidence above 1");
                OcrResult r = new OcrResult { Confidence = 7 };
                TestSupport.AssertEqual(1.0, r.Confidence, "confidence clamped");
            });

            // Diagnostics.
            s.Add("Diagnostics", "One activity and the expected instruments with low-cardinality tags per conversion", async ct =>
            {
                List<Activity> activities = new List<Activity>();
                List<string> instruments = new List<string>();
                using (ActivityListener listener = new ActivityListener
                {
                    ShouldListenTo = source => source.Name == DocConverterDiagnostics.Name,
                    Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                    ActivityStopped = a => { lock (activities) activities.Add(a); }
                })
                using (MeterListener meters = new MeterListener())
                {
                    ActivitySource.AddActivityListener(listener);
                    meters.InstrumentPublished = (instrument, l) => { if (instrument.Meter.Name == DocConverterDiagnostics.Name) l.EnableMeasurementEvents(instrument); };
                    meters.SetMeasurementEventCallback<long>((instrument, value, tags, state) => { lock (instruments) instruments.Add(instrument.Name + ":" + string.Join(",", tags.ToArray().Select(t => t.Key + "=" + t.Value))); });
                    meters.SetMeasurementEventCallback<double>((instrument, value, tags, state) => { lock (instruments) instruments.Add(instrument.Name); });
                    meters.Start();
                    await c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Text, null, ct).ConfigureAwait(false);
                }

                TestSupport.Assert(activities.Any(a => a.OperationName == "docconverter.convert" && (string?)a.GetTagItem("docconverter.from") == "Markdown" && (string?)a.GetTagItem("docconverter.to") == "Text"), "activity with tags");
                TestSupport.Assert(instruments.Any(i => i.StartsWith("docconverter.conversions:", StringComparison.Ordinal) && i.Contains("outcome=success") && i.Contains("from=Markdown")), "conversions counter: " + string.Join(" | ", instruments));
                TestSupport.Assert(instruments.Contains("docconverter.conversion.duration"), "duration histogram");
                TestSupport.Assert(instruments.Any(i => i.StartsWith("docconverter.input.bytes", StringComparison.Ordinal)), "input bytes histogram");
                TestSupport.Assert(instruments.Any(i => i.StartsWith("docconverter.warnings:", StringComparison.Ordinal) && i.Contains("code=FormattingLost")), "warnings counter");
            });

            s.Add("FaultyLoggerIgnored", "A logger that throws never breaks a conversion", async ct =>
            {
                Converter noisy = new Converter(new ConverterSettings { Logger = (sev, msg) => throw new InvalidOperationException("logger boom") });
                StringConversionResult r = await noisy.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Text, null, ct).ConfigureAwait(false);
                TestSupport.Assert(r.Output.Length > 0, "converted");
            });

            s.Add("LoggerReceivesMessages", "The logger receives prefixed messages including warnings", async ct =>
            {
                List<string> lines = new List<string>();
                Converter logged = new Converter(new ConverterSettings { Logger = (sev, msg) => { lock (lines) lines.Add(sev + " " + msg); } });
                await logged.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Text, null, ct).ConfigureAwait(false);
                TestSupport.Assert(lines.All(l => l.Contains("[DocConverter]")), "prefixed");
                TestSupport.Assert(lines.Any(l => l.StartsWith("Warn", StringComparison.Ordinal) && l.Contains("FormattingLost")), "warning logged");
            });

            // Extensibility.
            s.Add("CustomReaderWriter", "A registered custom reader and writer are used, and replacing a built-in works", async ct =>
            {
                Converter ext = new Converter();
                ext.RegisterReader(new UpperTextReader());
                ext.RegisterWriter(new ReverseTextWriter());
                StringConversionResult r = await ext.ConvertToStringAsync("hello", DocumentFormatEnum.Text, DocumentFormatEnum.Text, null, ct).ConfigureAwait(false);
                TestSupport.AssertEqual("OLLEH", r.Output, "custom reader upper-cases, custom writer reverses");
                TestSupport.ExpectThrows<ArgumentException>(() => ext.RegisterReader(new AutoReader()), "Auto registration rejected");
            });

            s.Add("RegistryGap", "A caller format with a reader but no writer throws ConversionNotSupportedException before reading", async ct =>
            {
                Converter gap = new Converter();
                CountingReader reader = new CountingReader();
                gap.RegisterReader(reader);
                TestSupport.Assert(gap.CanConvert(DocumentFormatEnum.Rtf, DocumentFormatEnum.Text), "rtf reader exists");
                TestSupport.Assert(!gap.CanConvert(DocumentFormatEnum.Text, DocumentFormatEnum.Rtf), "no rtf writer");
                await TestSupport.ExpectThrowsAsync<ConversionNotSupportedException>(() => gap.ConvertToStringAsync("x", DocumentFormatEnum.Rtf, DocumentFormatEnum.Png, null, ct), "no png writer").ConfigureAwait(false);
                TestSupport.AssertEqual(0, reader.Calls, "reader never called");
            });

            s.AddSync("DependencyInjection", "AddDocConverter registers one configured singleton for IConverter and Converter", () =>
            {
                ServiceCollection services = new ServiceCollection();
                services.AddDocConverter(settings => settings.MaxNestingDepth = 7);
                using (ServiceProvider provider = services.BuildServiceProvider())
                {
                    IConverter a = provider.GetRequiredService<IConverter>();
                    Converter b = provider.GetRequiredService<Converter>();
                    TestSupport.Assert(ReferenceEquals(a, b), "same instance");
                    TestSupport.AssertEqual(7, a.Settings.MaxNestingDepth, "configured");
                }

                TestSupport.ExpectThrows<ArgumentNullException>(() => ServiceCollectionExtensions.AddDocConverter(null!), "null services");
            });

            // Options.
            s.AddSync("OptionRanges", "Every ranged option rejects values outside its documented range and accepts the edges", () =>
            {
                ConversionOptions o = new ConversionOptions();
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => o.Text.WrapColumn = 19, "wrap 19");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => o.Text.WrapColumn = 1001, "wrap 1001");
                o.Text.WrapColumn = 0;
                o.Text.WrapColumn = 20;
                o.Text.WrapColumn = 1000;
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => o.Pptx.SlideSplitHeadingLevel = 0, "slide split 0");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => o.Pptx.SlideSplitHeadingLevel = 7, "slide split 7");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => o.Pptx.MaxBlocksPerSlide = 0, "blocks 0");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => o.Pptx.MaxTableRowsPerSlide = 1, "rows 1");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => o.Pdf.MarginPoints = -1, "margin -1");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => o.Pdf.MarginPoints = 216.1, "margin 216.1");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => o.Pdf.BaseFontSize = 5, "font 5");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => o.Pdf.HeadingSizeRatio = 1.0, "ratio 1.0");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => o.Pdf.HeadingSizeRatio = double.NaN, "ratio NaN");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => o.Docx.MarginPoints = 300, "docx margin");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => o.Csv.TableIndex = -1, "table index");
                ConverterSettings st = new ConverterSettings();
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => st.MaxInputBytes = 0, "max input 0");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => st.MaxInputBytes = (long)int.MaxValue + 1, "max input too big");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => st.MaxDecompressedBytes = 0, "decompressed 0");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => st.MaxNestingDepth = 0, "depth 0");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => st.MaxNestingDepth = 1025, "depth 1025");
                TestSupport.ExpectThrows<InvalidConversionOptionsException>(() => st.DetectionBufferBytes = 511, "detection 511");
            });

            s.AddSync("OptionNullCoalescing", "Null assignments to nested options and collections store defaults, never null", () =>
            {
                ConversionOptions o = new ConversionOptions();
                o.Markdown = null!;
                o.Html = null!;
                o.Pdf = null!;
                o.OutputEncoding = null!;
                TestSupport.Assert(o.Markdown != null && o.Html != null && o.Pdf != null && o.OutputEncoding != null, "defaults restored");
                ConverterSettings st = new ConverterSettings { DefaultOptions = null! };
                TestSupport.Assert(st.DefaultOptions != null, "default options restored");
                DocumentModel m = new DocumentModel { Blocks = null!, Resources = null!, Metadata = null! };
                TestSupport.Assert(m.Blocks != null && m.Resources != null && m.Metadata != null, "model collections restored");
                HeadingBlock h = new HeadingBlock { Level = 99 };
                TestSupport.AssertEqual(6, h.Level, "heading level clamped high");
                h.Level = -3;
                TestSupport.AssertEqual(1, h.Level, "heading level clamped low");
            });

            s.Add("Presets", "Presets configure what they promise", async ct =>
            {
                ConversionOptions llm = ConversionOptions.ForLlmIngestion();
                StringConversionResult a = await c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Markdown, llm, ct).ConfigureAwait(false);
                TestSupport.AssertNotContains(a.Output, "data:image", "no images for LLMs");
                TestSupport.Assert(a.Warnings.Any(w => w.Code == WarningCodeEnum.ImagesOmitted), "omission reported");
                ConversionOptions archival = ConversionOptions.ForArchival();
                TestSupport.Assert(archival.Deterministic && archival.IncludeImages, "archival");
                ConversionOptions minimal = ConversionOptions.Minimal();
                StringConversionResult m = await c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, minimal, ct).ConfigureAwait(false);
                TestSupport.AssertNotContains(m.Output, "<!DOCTYPE", "minimal html is a fragment");
                TestSupport.AssertNotContains(m.Output, "<img", "minimal has no images");
            });

            s.Add("MetadataAndTitle", "IncludeMetadata false clears metadata; Title overrides it", async ct =>
            {
                ConversionOptions none = new ConversionOptions { IncludeMetadata = false };
                StringConversionResult a = await c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, none, ct).ConfigureAwait(false);
                TestSupport.AssertNotContains(a.Output, "DocConverter Tests", "author dropped");
                ConversionOptions titled = new ConversionOptions { Title = "Override" };
                StringConversionResult b = await c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, titled, ct).ConfigureAwait(false);
                TestSupport.AssertContains(b.Output, "<title>Override</title>", "title override");
                TestSupport.AssertEqual("Override", b.Metadata.Title, "result metadata");
            });

            s.Add("StrictMode", "TreatWarningsAsErrors throws ConversionWarningException carrying the result; stream output is already written", async ct =>
            {
                ConversionOptions strict = new ConversionOptions { TreatWarningsAsErrors = true };
                ConversionWarningException ex = await TestSupport.ExpectThrowsAsync<ConversionWarningException>(() => c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Text, strict, ct), "text loses styles").ConfigureAwait(false);
                TestSupport.Assert(ex.Result != null && ex.Result.Warnings.Count > 0, "result attached");
                using (MemoryStream o = new MemoryStream())
                {
                    await TestSupport.ExpectThrowsAsync<ConversionWarningException>(() => c.ConvertAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Text, o, strict, ct), "stream").ConfigureAwait(false);
                    TestSupport.Assert(o.Length > 0, "output written before the exception");
                }

                StringConversionResult clean = await c.ConvertToStringAsync(md, DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, strict, ct).ConfigureAwait(false);
                TestSupport.AssertEqual(0, clean.Warnings.Count, "lossless pair passes strict mode");
            });

            s.Add("WarningsMerged", "Repeated warnings of one code are merged with a count", async ct =>
            {
                DocumentModel m = new DocumentModel();
                for (int i = 0; i < 5; i++) m.Blocks.Add(new ParagraphBlock(new List<Inline> { new TextInline("b" + i, InlineStyleEnum.Bold) }));
                StringConversionResult r = await c.WriteToStringAsync(m, DocumentFormatEnum.Text, null, ct).ConfigureAwait(false);
                ConversionWarning w = r.Warnings.Single(x => x.Code == WarningCodeEnum.FormattingLost);
                TestSupport.AssertEqual(5, w.Count, "count");
                TestSupport.AssertEqual(1, r.Warnings.Count(x => x.Code == WarningCodeEnum.FormattingLost), "one entry per code");
            });

            return s.Build();
        }
    }
}
