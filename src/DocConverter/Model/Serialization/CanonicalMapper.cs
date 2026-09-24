namespace DocConverter.Model.Serialization
{
    using System;
    using System.Collections.Generic;
    using DocConverter.Enums;
    using DocConverter.Exceptions;

    /// <summary>
    /// Maps between DocumentModel and its canonical DTOs. The mapping is lossless: FromDto(ToDto(model)) equals model.
    /// </summary>
    public static class CanonicalMapper
    {
        private static readonly InlineStyleEnum[] _StyleFlags = new InlineStyleEnum[]
        {
            InlineStyleEnum.Bold, InlineStyleEnum.Italic, InlineStyleEnum.Underline, InlineStyleEnum.Strikethrough,
            InlineStyleEnum.Code, InlineStyleEnum.Superscript, InlineStyleEnum.Subscript
        };

        /// <summary>
        /// Map a document to its canonical form.
        /// </summary>
        /// <param name="document">Document.</param>
        /// <param name="includeMetadata">When false, metadata is omitted.</param>
        /// <param name="includeBinary">When false, resource data is omitted (metadata about resources is kept).</param>
        /// <returns>Canonical document.</returns>
        /// <exception cref="ArgumentNullException">Thrown when document is null.</exception>
        public static CanonicalDocument ToDto(DocumentModel document, bool includeMetadata = true, bool includeBinary = true)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            CanonicalDocument dto = new CanonicalDocument();
            if (includeMetadata)
            {
                DocumentMetadata m = document.Metadata;
                dto.Metadata = new CanonicalMetadata
                {
                    Title = m.Title,
                    Subject = m.Subject,
                    Author = m.Author,
                    Keywords = m.Keywords,
                    Description = m.Description,
                    Language = m.Language,
                    Created = m.CreatedUtc,
                    Modified = m.ModifiedUtc
                };
            }

            foreach (Block block in document.Blocks) dto.Blocks.Add(BlockToDto(block));

            List<string> ids = new List<string>(document.Resources.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                BinaryResource r = document.Resources[id];
                dto.Resources.Add(new CanonicalResource
                {
                    Id = r.Id,
                    MediaType = r.MediaType,
                    FileName = r.FileName,
                    PixelWidth = r.PixelWidth,
                    PixelHeight = r.PixelHeight,
                    Size = r.Data.LongLength,
                    Data = includeBinary ? Convert.ToBase64String(r.Data) : null
                });
            }

            return dto;
        }

        /// <summary>
        /// Map a canonical document back to the model.
        /// </summary>
        /// <param name="dto">Canonical document.</param>
        /// <returns>Document.</returns>
        /// <exception cref="ArgumentNullException">Thrown when dto is null.</exception>
        /// <exception cref="DocumentReadException">Thrown when the canonical content is invalid (unknown types, bad base64).</exception>
        public static DocumentModel FromDto(CanonicalDocument dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            DocumentModel document = new DocumentModel();
            if (dto.Metadata != null)
            {
                document.Metadata = new DocumentMetadata
                {
                    Title = dto.Metadata.Title,
                    Subject = dto.Metadata.Subject,
                    Author = dto.Metadata.Author,
                    Keywords = dto.Metadata.Keywords,
                    Description = dto.Metadata.Description,
                    Language = dto.Metadata.Language,
                    CreatedUtc = dto.Metadata.Created,
                    ModifiedUtc = dto.Metadata.Modified
                };
            }

            if (dto.Blocks != null)
                foreach (CanonicalBlock block in dto.Blocks) document.Blocks.Add(BlockFromDto(block));

            if (dto.Resources != null)
            {
                foreach (CanonicalResource r in dto.Resources)
                {
                    if (r == null) continue;
                    byte[] data;
                    try
                    {
                        data = string.IsNullOrEmpty(r.Data) ? Array.Empty<byte>() : Convert.FromBase64String(r.Data);
                    }
                    catch (FormatException ex)
                    {
                        throw new DocumentReadException("Resource '" + r.Id + "' has invalid base64 data.", ex);
                    }

                    document.Resources[r.Id ?? ""] = new BinaryResource
                    {
                        Id = r.Id ?? "",
                        MediaType = r.MediaType,
                        FileName = r.FileName,
                        PixelWidth = r.PixelWidth,
                        PixelHeight = r.PixelHeight,
                        Data = data
                    };
                }
            }

            return document;
        }

        /// <summary>
        /// Deep copy of a document, made through the canonical form.
        /// </summary>
        /// <param name="document">Document.</param>
        /// <returns>Independent copy.</returns>
        /// <exception cref="ArgumentNullException">Thrown when document is null.</exception>
        public static DocumentModel Clone(DocumentModel document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            DocumentModel copy = FromDto(ToDto(document, true, false));
            foreach (KeyValuePair<string, BinaryResource> pair in document.Resources)
            {
                if (copy.Resources.TryGetValue(pair.Key, out BinaryResource? target)) target.Data = pair.Value.Data;
            }

            return copy;
        }

        private static CanonicalBlock BlockToDto(Block block)
        {
            CanonicalBlock dto = new CanonicalBlock
            {
                Id = block.Id,
                SourcePage = block.SourcePage,
                SourceSheet = block.SourceSheet,
                SourceSlide = block.SourceSlide
            };

            switch (block)
            {
                case SectionBlock section:
                    dto.Type = "section";
                    dto.Kind = section.Kind.ToString();
                    dto.Title = section.Title;
                    dto.Blocks = BlocksToDto(section.Blocks);
                    break;
                case HeadingBlock heading:
                    dto.Type = "heading";
                    dto.Level = heading.Level;
                    dto.Inlines = InlinesToDto(heading.Inlines);
                    break;
                case ParagraphBlock paragraph:
                    dto.Type = "paragraph";
                    dto.Inlines = InlinesToDto(paragraph.Inlines);
                    if (paragraph.Alignment != TextAlignmentEnum.Default) dto.Alignment = paragraph.Alignment.ToString();
                    break;
                case ListBlock list:
                    dto.Type = "list";
                    dto.Kind = list.Kind.ToString();
                    if (list.Kind == ListKindEnum.Ordered) dto.Start = list.Start;
                    dto.Items = new List<CanonicalListItem>();
                    foreach (ListItemBlock item in list.Items)
                        dto.Items.Add(new CanonicalListItem { Blocks = BlocksToDto(item.Blocks), Checked = item.Checked });
                    break;
                case ListItemBlock looseItem:
                    dto.Type = "list";
                    dto.Kind = ListKindEnum.Unordered.ToString();
                    dto.Items = new List<CanonicalListItem> { new CanonicalListItem { Blocks = BlocksToDto(looseItem.Blocks), Checked = looseItem.Checked } };
                    break;
                case TableBlock table:
                    dto.Type = "table";
                    dto.HeaderRowCount = table.HeaderRowCount;
                    dto.Caption = table.Caption;
                    if (table.ColumnAlignments.Count > 0)
                    {
                        dto.ColumnAlignments = new List<string>();
                        foreach (TextAlignmentEnum a in table.ColumnAlignments) dto.ColumnAlignments.Add(a.ToString());
                    }

                    dto.Rows = new List<CanonicalTableRow>();
                    foreach (TableRow row in table.Rows)
                    {
                        CanonicalTableRow r = new CanonicalTableRow();
                        foreach (TableCell cell in row.Cells)
                        {
                            r.Cells.Add(new CanonicalTableCell
                            {
                                Blocks = BlocksToDto(cell.Blocks),
                                ColumnSpan = cell.ColumnSpan > 1 ? cell.ColumnSpan : (int?)null,
                                RowSpan = cell.RowSpan > 1 ? cell.RowSpan : (int?)null,
                                IsHeader = cell.IsHeader ? true : (bool?)null
                            });
                        }

                        dto.Rows.Add(r);
                    }

                    break;
                case CodeBlock code:
                    dto.Type = "code";
                    dto.Language = code.Language;
                    dto.Text = code.Text;
                    break;
                case QuoteBlock quote:
                    dto.Type = "quote";
                    dto.Blocks = BlocksToDto(quote.Blocks);
                    break;
                case ImageBlock image:
                    dto.Type = "image";
                    dto.ResourceId = image.ResourceId;
                    dto.AltText = image.AltText;
                    dto.Caption = image.Caption;
                    dto.Width = image.Width;
                    dto.Height = image.Height;
                    break;
                case ThematicBreakBlock _:
                    dto.Type = "thematicBreak";
                    break;
                case PageBreakBlock _:
                    dto.Type = "pageBreak";
                    break;
                default:
                    dto.Type = "unknown";
                    break;
            }

            return dto;
        }

        private static List<CanonicalBlock> BlocksToDto(List<Block> blocks)
        {
            List<CanonicalBlock> list = new List<CanonicalBlock>();
            foreach (Block b in blocks) list.Add(BlockToDto(b));
            return list;
        }

        private static List<CanonicalInline> InlinesToDto(List<Inline> inlines)
        {
            List<CanonicalInline> list = new List<CanonicalInline>();
            foreach (Inline inline in inlines)
            {
                switch (inline)
                {
                    case TextInline text:
                        CanonicalInline t = new CanonicalInline { Type = "text", Text = text.Text };
                        if (text.Style != InlineStyleEnum.None)
                        {
                            t.Style = new List<string>();
                            foreach (InlineStyleEnum flag in _StyleFlags)
                                if ((text.Style & flag) == flag) t.Style.Add(flag.ToString());
                        }

                        list.Add(t);
                        break;
                    case LinkInline link:
                        list.Add(new CanonicalInline { Type = "link", Url = link.Url, Title = link.Title, Inlines = InlinesToDto(link.Inlines) });
                        break;
                    case ImageInline image:
                        list.Add(new CanonicalInline { Type = "image", ResourceId = image.ResourceId, AltText = image.AltText });
                        break;
                    case LineBreakInline _:
                        list.Add(new CanonicalInline { Type = "lineBreak" });
                        break;
                }
            }

            return list;
        }

        private static Block BlockFromDto(CanonicalBlock dto)
        {
            if (dto == null) throw new DocumentReadException("The canonical document contains a null block.");
            Block block;
            switch (dto.Type)
            {
                case "section":
                    SectionBlock section = new SectionBlock(ParseEnum(dto.Kind, SectionKindEnum.Generic), dto.Title);
                    section.Blocks = BlocksFromDto(dto.Blocks);
                    block = section;
                    break;
                case "heading":
                    HeadingBlock heading = new HeadingBlock();
                    heading.Level = dto.Level ?? 1;
                    heading.Inlines = InlinesFromDto(dto.Inlines);
                    block = heading;
                    break;
                case "paragraph":
                    ParagraphBlock paragraph = new ParagraphBlock();
                    paragraph.Inlines = InlinesFromDto(dto.Inlines);
                    paragraph.Alignment = ParseEnum(dto.Alignment, TextAlignmentEnum.Default);
                    block = paragraph;
                    break;
                case "list":
                    ListBlock list = new ListBlock(ParseEnum(dto.Kind, ListKindEnum.Unordered));
                    list.Start = dto.Start ?? 1;
                    if (dto.Items != null)
                    {
                        foreach (CanonicalListItem item in dto.Items)
                        {
                            if (item == null) continue;
                            ListItemBlock li = new ListItemBlock();
                            li.Blocks = BlocksFromDto(item.Blocks);
                            li.Checked = item.Checked;
                            list.Items.Add(li);
                        }
                    }

                    block = list;
                    break;
                case "table":
                    TableBlock table = new TableBlock();
                    table.HeaderRowCount = dto.HeaderRowCount ?? 0;
                    table.Caption = dto.Caption;
                    if (dto.ColumnAlignments != null)
                        foreach (string a in dto.ColumnAlignments) table.ColumnAlignments.Add(ParseEnum(a, TextAlignmentEnum.Default));
                    if (dto.Rows != null)
                    {
                        foreach (CanonicalTableRow r in dto.Rows)
                        {
                            if (r == null) continue;
                            TableRow row = new TableRow();
                            if (r.Cells != null)
                            {
                                foreach (CanonicalTableCell c in r.Cells)
                                {
                                    if (c == null) continue;
                                    TableCell cell = new TableCell();
                                    cell.Blocks = BlocksFromDto(c.Blocks);
                                    cell.ColumnSpan = c.ColumnSpan ?? 1;
                                    cell.RowSpan = c.RowSpan ?? 1;
                                    cell.IsHeader = c.IsHeader ?? false;
                                    row.Cells.Add(cell);
                                }
                            }

                            table.Rows.Add(row);
                        }
                    }

                    block = table;
                    break;
                case "code":
                    block = new CodeBlock(dto.Text, dto.Language);
                    break;
                case "quote":
                    QuoteBlock quote = new QuoteBlock();
                    quote.Blocks = BlocksFromDto(dto.Blocks);
                    block = quote;
                    break;
                case "image":
                    ImageBlock image = new ImageBlock(dto.ResourceId ?? "", dto.AltText);
                    image.Caption = dto.Caption;
                    image.Width = dto.Width;
                    image.Height = dto.Height;
                    block = image;
                    break;
                case "thematicBreak":
                    block = new ThematicBreakBlock();
                    break;
                case "pageBreak":
                    block = new PageBreakBlock();
                    break;
                default:
                    throw new DocumentReadException("The canonical document contains an unknown block type '" + dto.Type + "'.");
            }

            block.Id = dto.Id;
            block.SourcePage = dto.SourcePage;
            block.SourceSheet = dto.SourceSheet;
            block.SourceSlide = dto.SourceSlide;
            return block;
        }

        private static List<Block> BlocksFromDto(List<CanonicalBlock>? blocks)
        {
            List<Block> list = new List<Block>();
            if (blocks == null) return list;
            foreach (CanonicalBlock b in blocks) list.Add(BlockFromDto(b));
            return list;
        }

        private static List<Inline> InlinesFromDto(List<CanonicalInline>? inlines)
        {
            List<Inline> list = new List<Inline>();
            if (inlines == null) return list;
            foreach (CanonicalInline dto in inlines)
            {
                if (dto == null) continue;
                switch (dto.Type)
                {
                    case "text":
                        InlineStyleEnum style = InlineStyleEnum.None;
                        if (dto.Style != null)
                            foreach (string s in dto.Style) style |= ParseEnum(s, InlineStyleEnum.None);
                        list.Add(new TextInline(dto.Text ?? "", style));
                        break;
                    case "link":
                        LinkInline link = new LinkInline();
                        link.Url = dto.Url ?? "";
                        link.Title = dto.Title;
                        link.Inlines = InlinesFromDto(dto.Inlines);
                        list.Add(link);
                        break;
                    case "image":
                        list.Add(new ImageInline(dto.ResourceId ?? "", dto.AltText));
                        break;
                    case "lineBreak":
                        list.Add(new LineBreakInline());
                        break;
                    default:
                        throw new DocumentReadException("The canonical document contains an unknown inline type '" + dto.Type + "'.");
                }
            }

            return list;
        }

        private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            if (Enum.TryParse(value, true, out TEnum parsed)) return parsed;
            throw new DocumentReadException("The canonical document contains an unknown " + typeof(TEnum).Name + " value '" + value + "'.");
        }
    }
}
