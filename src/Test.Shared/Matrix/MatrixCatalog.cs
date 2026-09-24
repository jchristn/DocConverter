namespace Test.Shared.Matrix
{
    using System.Collections.Generic;
    using System.Text;
    using DocConverter.Enums;
    using Test.Shared.Fixtures;
    using Test.Shared.Fixtures.Builders;

    /// <summary>
    /// Every source variant of the conversion matrix with the content it carries, and the built-in targets.
    /// </summary>
    public static class MatrixCatalog
    {
        private static readonly object _Lock = new object();
        private static List<SourceSpec>? _Sources = null;

        /// <summary>
        /// Built-in targets, in plan order.
        /// </summary>
        public static readonly DocumentFormatEnum[] Targets = new DocumentFormatEnum[]
        {
            DocumentFormatEnum.Markdown, DocumentFormatEnum.Html, DocumentFormatEnum.Text, DocumentFormatEnum.Json,
            DocumentFormatEnum.Xml, DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv, DocumentFormatEnum.Docx,
            DocumentFormatEnum.Xlsx, DocumentFormatEnum.Pptx, DocumentFormatEnum.Pdf
        };

        /// <summary>
        /// Every source variant: 14 documents (JSON and XML in canonical and generic form) and 6 image formats.
        /// </summary>
        public static IReadOnlyList<SourceSpec> Sources
        {
            get
            {
                lock (_Lock)
                {
                    if (_Sources == null) _Sources = Build();
                    return _Sources;
                }
            }
        }

        private static List<SourceSpec> Build()
        {
            List<SourceSpec> list = new List<SourceSpec>();

            list.Add(Rich("Markdown", DocumentFormatEnum.Markdown, () => TextFixtures.Reference(DocumentFormatEnum.Markdown)));
            list.Add(Rich("Html", DocumentFormatEnum.Html, () => TextFixtures.Reference(DocumentFormatEnum.Html)));
            list.Add(Rich("JsonCanonical", DocumentFormatEnum.Json, TextFixtures.CanonicalJson));
            list.Add(Rich("XmlCanonical", DocumentFormatEnum.Xml, TextFixtures.CanonicalXml));
            list.Add(Rich("Docx", DocumentFormatEnum.Docx, DocxFixtureBuilder.BuildReference));

            SourceSpec text = new SourceSpec("Text", DocumentFormatEnum.Text, () => TextFixtures.Reference(DocumentFormatEnum.Text));
            text.Snippets.AddRange(ReferenceContent.CoreTextSnippets);
            list.Add(text);

            foreach (DocumentFormatEnum f in new[] { DocumentFormatEnum.Csv, DocumentFormatEnum.Tsv })
            {
                DocumentFormatEnum format = f;
                SourceSpec delimited = new SourceSpec(format.ToString(), format, () => TextFixtures.Reference(format));
                delimited.Snippets.Add("Grace Hopper");
                delimited.Snippets.Add("Mathematician");
                delimited.TableRows.AddRange(ReferenceContent.TableRows);
                list.Add(delimited);
            }

            foreach (DocumentFormatEnum f in new[] { DocumentFormatEnum.Json, DocumentFormatEnum.Xml })
            {
                DocumentFormatEnum format = f;
                SourceSpec generic = new SourceSpec(format + "Generic", format, () => TextFixtures.Reference(format));
                generic.Snippets.AddRange(new[] { ReferenceContent.Title, "Grace Hopper", "Step two", ReferenceContent.QuoteText, ReferenceContent.InternationalLatin, ReferenceContent.Closing });
                generic.TableRows.AddRange(ReferenceContent.TableRows);
                generic.FirstTableRows.Add(new[] { format == DocumentFormatEnum.Json ? "title" : "@title", ReferenceContent.Title });
                list.Add(generic);
            }

            SourceSpec rtf = new SourceSpec("Rtf", DocumentFormatEnum.Rtf, () => Encoding.ASCII.GetBytes(RtfFixtureBuilder.BuildReference()));
            rtf.Snippets.AddRange(new[] { ReferenceContent.Heading1, ReferenceContent.BoldText, ReferenceContent.ItalicText, "Grace Hopper", ReferenceContent.InternationalLatin });
            rtf.TableRows.AddRange(ReferenceContent.TableRows);
            rtf.Headings.Add(ReferenceContent.Heading1);
            rtf.HasImage = true;
            list.Add(rtf);

            SourceSpec xlsx = new SourceSpec("Xlsx", DocumentFormatEnum.Xlsx, XlsxFixtureBuilder.BuildReference);
            xlsx.Snippets.AddRange(new[] { "Grace Hopper", "Admiral", ReferenceContent.InternationalLatin });
            xlsx.TableRows.AddRange(ReferenceContent.TableRows);
            list.Add(xlsx);

            SourceSpec pptx = new SourceSpec("Pptx", DocumentFormatEnum.Pptx, PptxFixtureBuilder.BuildReference);
            pptx.Snippets.AddRange(new[] { ReferenceContent.Heading1, "Second bullet", ReferenceContent.NestedBullet, "Step two", "Grace Hopper", ReferenceContent.InternationalLatin, ReferenceContent.QuoteText });
            pptx.TableRows.AddRange(ReferenceContent.TableRows);
            pptx.Headings.Add(ReferenceContent.Heading1);
            pptx.HasImage = true;
            list.Add(pptx);

            SourceSpec pdf = new SourceSpec("Pdf", DocumentFormatEnum.Pdf, PdfFixtureBuilder.BuildReference);
            pdf.Snippets.AddRange(new[] { ReferenceContent.Heading1, ReferenceContent.BoldText, "Grace Hopper", ReferenceContent.InternationalLatin, ReferenceContent.Closing });
            pdf.TableRows.AddRange(ReferenceContent.TableRows);
            pdf.HasImage = true;
            list.Add(pdf);

            string[][] images = new string[][]
            {
                new[] { "Png", "sample.png", "true" }, new[] { "Jpeg", "sample.jpg", "true" }, new[] { "Gif", "sample.gif", "false" },
                new[] { "Bmp", "sample.bmp", "true" }, new[] { "Tiff", "sample.tiff", "false" }, new[] { "WebP", "sample.webp", "false" }
            };
            foreach (string[] image in images)
            {
                string file = image[1];
                DocumentFormatEnum format = (DocumentFormatEnum)System.Enum.Parse(typeof(DocumentFormatEnum), image[0]);
                SourceSpec spec = new SourceSpec(image[0], format, () => TestImages.Sample(file));
                spec.HasImage = true;
                spec.ImageOnly = true;
                spec.ImageEmbeddableInPdf = image[2] == "true";
                list.Add(spec);
            }

            return list;
        }

        private static SourceSpec Rich(string id, DocumentFormatEnum format, System.Func<byte[]> bytes)
        {
            SourceSpec spec = new SourceSpec(id, format, bytes);
            spec.Snippets.AddRange(ReferenceContent.CoreTextSnippets);
            spec.TableRows.AddRange(ReferenceContent.TableRows);
            spec.Headings.AddRange(new[] { ReferenceContent.Heading1, ReferenceContent.HeadingLists, ReferenceContent.HeadingTable, ReferenceContent.HeadingCode });
            spec.HasImage = true;
            return spec;
        }
    }
}
