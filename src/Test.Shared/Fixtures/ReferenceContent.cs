namespace Test.Shared.Fixtures
{
    using System.Collections.Generic;
    using DocConverter.Enums;
    using DocConverter.Model;

    /// <summary>
    /// The single reference document every fixture expresses (plan Section 10.2). Fixture builders reproduce this content
    /// in each source format without using DocConverter's writers, and inspectors check it survives each conversion.
    /// </summary>
    public static class ReferenceContent
    {
        /// <summary>Document title (metadata).</summary>
        public const string Title = "DocConverter Reference Document";

        /// <summary>Author (metadata).</summary>
        public const string Author = "DocConverter Tests";

        /// <summary>Subject (metadata).</summary>
        public const string Subject = "Reference content";

        /// <summary>Level 1 heading.</summary>
        public const string Heading1 = "Reference Document";

        /// <summary>Level 2 heading before the lists.</summary>
        public const string HeadingLists = "Lists";

        /// <summary>Level 2 heading before the table.</summary>
        public const string HeadingTable = "Table";

        /// <summary>Level 3 heading before the code block.</summary>
        public const string HeadingCode = "Code";

        /// <summary>Opening words of the styled paragraph.</summary>
        public const string StyledLead = "This paragraph has ";

        /// <summary>Bold run.</summary>
        public const string BoldText = "bold text";

        /// <summary>Italic run.</summary>
        public const string ItalicText = "italic text";

        /// <summary>Underlined run.</summary>
        public const string UnderlineText = "underlined text";

        /// <summary>Struck through run.</summary>
        public const string StrikeText = "struck text";

        /// <summary>Inline code run.</summary>
        public const string InlineCode = "inline code";

        /// <summary>Link text.</summary>
        public const string LinkText = "link to the docs";

        /// <summary>Link URL.</summary>
        public const string LinkUrl = "https://example.com/docs";

        /// <summary>Top level bullets.</summary>
        public static readonly string[] Bullets = new string[] { "First bullet", "Second bullet", "Third bullet" };

        /// <summary>Bullet nested under the second bullet.</summary>
        public const string NestedBullet = "Nested bullet A";

        /// <summary>Bullet nested under the nested bullet.</summary>
        public const string DeepBullet = "Deep bullet";

        /// <summary>Ordered list items.</summary>
        public static readonly string[] Steps = new string[] { "Step one", "Step two", "Step three" };

        /// <summary>Table rows, header first. Four rows by three columns; the last column is numeric.</summary>
        public static readonly string[][] TableRows = new string[][]
        {
            new string[] { "Name", "Role", "Years" },
            new string[] { "Ada Lovelace", "Engineer", "7" },
            new string[] { "Grace Hopper", "Admiral", "40" },
            new string[] { "Alan Turing", "Mathematician", "12" }
        };

        /// <summary>Code block text.</summary>
        public const string CodeText = "int total = 40 + 2;\nConsole.WriteLine(total);";

        /// <summary>Code block language.</summary>
        public const string CodeLanguage = "csharp";

        /// <summary>Quotation text.</summary>
        public const string QuoteText = "Simplicity is prerequisite for reliability.";

        /// <summary>Image alternative text.</summary>
        public const string ImageAlt = "Reference image";

        /// <summary>International paragraph.</summary>
        public const string International = "Grüße aus Zürich. 你好，世界。 مرحبا بالعالم. Emoji 🚀✅.";

        /// <summary>Latin, Greek and Cyrillic portion of the international paragraph that every target, PDF included, must keep.</summary>
        public const string InternationalLatin = "Grüße aus Zürich.";

        /// <summary>Paragraph full of characters that need escaping somewhere.</summary>
        public const string Special = "Special characters: <tag> & \"quotes\" | pipes * stars _ underscores [brackets] #hash";

        /// <summary>Closing paragraph.</summary>
        public const string Closing = "End of reference document.";

        /// <summary>
        /// Width and height of the reference image in pixels.
        /// </summary>
        public const int ImageSize = 16;

        /// <summary>
        /// The reference image as PNG bytes.
        /// </summary>
        public static byte[] ImagePng()
        {
            return TestImages.SolidPng(ImageSize, ImageSize, 0x1F, 0x4E, 0x8C);
        }

        /// <summary>
        /// Plain text snippets that every target able to hold text must contain after conversion.
        /// </summary>
        public static IReadOnlyList<string> CoreTextSnippets
        {
            get
            {
                return new List<string>
                {
                    Heading1, BoldText, ItalicText, LinkText, "Second bullet", NestedBullet, "Step two",
                    "Grace Hopper", "Admiral", QuoteText, InternationalLatin, Closing
                };
            }
        }

        /// <summary>
        /// Build the reference content directly as a document model.
        /// </summary>
        /// <returns>A new document model.</returns>
        public static DocumentModel ToModel()
        {
            DocumentModel doc = new DocumentModel();
            doc.Metadata.Title = Title;
            doc.Metadata.Author = Author;
            doc.Metadata.Subject = Subject;

            doc.Blocks.Add(new HeadingBlock(1, Heading1));

            ParagraphBlock styled = new ParagraphBlock();
            styled.Inlines.Add(new TextInline(StyledLead));
            styled.Inlines.Add(new TextInline(BoldText, InlineStyleEnum.Bold));
            styled.Inlines.Add(new TextInline(", "));
            styled.Inlines.Add(new TextInline(ItalicText, InlineStyleEnum.Italic));
            styled.Inlines.Add(new TextInline(", "));
            styled.Inlines.Add(new TextInline(UnderlineText, InlineStyleEnum.Underline));
            styled.Inlines.Add(new TextInline(", "));
            styled.Inlines.Add(new TextInline(StrikeText, InlineStyleEnum.Strikethrough));
            styled.Inlines.Add(new TextInline(", "));
            styled.Inlines.Add(new TextInline(InlineCode, InlineStyleEnum.Code));
            styled.Inlines.Add(new TextInline(" and a "));
            styled.Inlines.Add(new LinkInline(LinkUrl, LinkText));
            styled.Inlines.Add(new TextInline("."));
            doc.Blocks.Add(styled);

            doc.Blocks.Add(new HeadingBlock(2, HeadingLists));
            ListBlock bullets = new ListBlock(ListKindEnum.Unordered);
            bullets.Items.Add(new ListItemBlock(Bullets[0]));
            ListItemBlock second = new ListItemBlock(Bullets[1]);
            ListBlock nested = new ListBlock(ListKindEnum.Unordered);
            ListItemBlock nestedItem = new ListItemBlock(NestedBullet);
            ListBlock deep = new ListBlock(ListKindEnum.Unordered);
            deep.Items.Add(new ListItemBlock(DeepBullet));
            nestedItem.Blocks.Add(deep);
            nested.Items.Add(nestedItem);
            second.Blocks.Add(nested);
            bullets.Items.Add(second);
            bullets.Items.Add(new ListItemBlock(Bullets[2]));
            doc.Blocks.Add(bullets);

            ListBlock steps = new ListBlock(ListKindEnum.Ordered);
            foreach (string step in Steps) steps.Items.Add(new ListItemBlock(step));
            doc.Blocks.Add(steps);

            doc.Blocks.Add(new HeadingBlock(2, HeadingTable));
            TableBlock table = new TableBlock();
            table.HeaderRowCount = 1;
            for (int r = 0; r < TableRows.Length; r++)
            {
                TableRow row = new TableRow(TableRows[r]);
                if (r == 0) foreach (TableCell cell in row.Cells) cell.IsHeader = true;
                table.Rows.Add(row);
            }

            doc.Blocks.Add(table);

            doc.Blocks.Add(new HeadingBlock(3, HeadingCode));
            doc.Blocks.Add(new CodeBlock(CodeText, CodeLanguage));

            QuoteBlock quote = new QuoteBlock();
            quote.Blocks.Add(new ParagraphBlock(QuoteText));
            doc.Blocks.Add(quote);

            BinaryResource image = new BinaryResource
            {
                Id = "img1",
                MediaType = "image/png",
                Data = ImagePng(),
                FileName = "reference.png",
                PixelWidth = ImageSize,
                PixelHeight = ImageSize
            };
            doc.AddResource(image);
            doc.Blocks.Add(new ImageBlock("img1", ImageAlt));

            doc.Blocks.Add(new ParagraphBlock(International));
            doc.Blocks.Add(new ParagraphBlock(Special));
            doc.Blocks.Add(new ParagraphBlock(Closing));
            return doc;
        }
    }
}
