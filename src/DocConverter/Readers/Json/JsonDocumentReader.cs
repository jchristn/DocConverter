namespace DocConverter.Readers.Json
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using DocConverter.Enums;
    using DocConverter.Exceptions;
    using DocConverter.Internal;
    using DocConverter.Model;
    using DocConverter.Model.Serialization;
    using DocConverter.Options;

    /// <summary>
    /// Reads JSON. Input carrying the top level "docconverter" marker is the canonical document form and is read
    /// losslessly. Any other JSON is mapped structurally: an object's scalar members become a key/value table, an array of
    /// objects becomes a table with one column per key, an array of scalars becomes a list, and nested objects or arrays
    /// become sections titled with their key. Nesting deeper than ConverterSettings.MaxNestingDepth is written as compact
    /// JSON text with the NestedDepthLimited warning. Stateless and thread safe.
    /// </summary>
    public sealed class JsonDocumentReader : IDocumentReader
    {
        private static readonly IReadOnlyList<DocumentFormatEnum> _Formats = new DocumentFormatEnum[] { DocumentFormatEnum.Json };

        /// <inheritdoc />
        public IReadOnlyList<DocumentFormatEnum> Formats
        {
            get => _Formats;
        }

        /// <inheritdoc />
        public async Task<DocumentModel> ReadAsync(Stream input, DocumentFormatEnum format, ConversionOptions options, ConversionContext context, CancellationToken token = default)
        {
            string text = await TextIO.ReadAllTextAsync(input, options, context, token).ConfigureAwait(false);
            byte[] utf8 = new UTF8Encoding(false).GetBytes(text.TrimStart('﻿'));
            token.ThrowIfCancellationRequested();

            if (CanonicalJson.IsCanonical(utf8, 0, utf8.Length))
                return CanonicalMapper.FromDto(CanonicalJson.Deserialize(utf8, 0, utf8.Length));

            if (utf8.Length == 0 || text.Trim().Length == 0) return new DocumentModel();

            JsonDocumentOptions parseOptions = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = Math.Max(64, (context.MaxNestingDepth * 2) + 16)
            };

            // Arbitrary JSON has no fixed contract, so this is the one place the library walks JsonElement directly.
            try
            {
                using (JsonDocument json = JsonDocument.Parse(utf8, parseOptions))
                {
                    DocumentModel document = new DocumentModel();
                    MapValue(json.RootElement, null, document.Blocks, 0, context, token);
                    return document;
                }
            }
            catch (JsonException ex)
            {
                throw new DocumentReadException("The JSON input is malformed or nested too deeply: " + ex.Message, ex);
            }
        }

        private static void MapValue(JsonElement value, string? key, List<Block> target, int depth, ConversionContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (depth >= context.MaxNestingDepth)
            {
                context.AddWarning(WarningCodeEnum.NestedDepthLimited, "JSON nested deeper than " + context.MaxNestingDepth + " levels was written as compact JSON text.");
                target.Add(new ParagraphBlock((key != null ? key + ": " : "") + Compact(value)));
                return;
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    MapObject(value, key, target, depth, context, token);
                    break;
                case JsonValueKind.Array:
                    MapArray(value, key, target, depth, context, token);
                    break;
                default:
                    target.Add(new ParagraphBlock((key != null ? key + ": " : "") + Scalar(value)));
                    break;
            }
        }

        private static void MapObject(JsonElement obj, string? key, List<Block> target, int depth, ConversionContext context, CancellationToken token)
        {
            List<Block> blocks = target;
            if (key != null)
            {
                SectionBlock section = new SectionBlock(SectionKindEnum.Generic, key);
                target.Add(section);
                blocks = section.Blocks;
            }

            TableBlock? pairs = null;
            foreach (JsonProperty property in obj.EnumerateObject())
            {
                if (IsScalar(property.Value))
                {
                    if (pairs == null)
                    {
                        pairs = new TableBlock();
                        pairs.HeaderRowCount = 1;
                        TableRow header = new TableRow(new string[] { "Key", "Value" });
                        foreach (TableCell cell in header.Cells) cell.IsHeader = true;
                        pairs.Rows.Add(header);
                        blocks.Add(pairs);
                    }

                    pairs.Rows.Add(new TableRow(new string[] { property.Name, Scalar(property.Value) }));
                }
                else
                {
                    MapValue(property.Value, property.Name, blocks, depth + 1, context, token);
                }
            }
        }

        private static void MapArray(JsonElement array, string? key, List<Block> target, int depth, ConversionContext context, CancellationToken token)
        {
            List<Block> blocks = target;
            if (key != null)
            {
                SectionBlock section = new SectionBlock(SectionKindEnum.Generic, key);
                target.Add(section);
                blocks = section.Blocks;
            }

            bool allScalar = true;
            bool allObjects = true;
            int count = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                count++;
                if (!IsScalar(item)) allScalar = false;
                if (item.ValueKind != JsonValueKind.Object) allObjects = false;
            }

            if (count == 0) return;

            if (allScalar)
            {
                ListBlock list = new ListBlock(ListKindEnum.Unordered);
                foreach (JsonElement item in array.EnumerateArray()) list.Items.Add(new ListItemBlock(Scalar(item)));
                blocks.Add(list);
                return;
            }

            if (allObjects)
            {
                List<string> columns = new List<string>();
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonElement item in array.EnumerateArray())
                    foreach (JsonProperty p in item.EnumerateObject())
                        if (seen.Add(p.Name)) columns.Add(p.Name);

                TableBlock table = new TableBlock();
                table.HeaderRowCount = 1;
                TableRow header = new TableRow(columns);
                foreach (TableCell cell in header.Cells) cell.IsHeader = true;
                table.Rows.Add(header);
                int rowIndex = 0;
                foreach (JsonElement item in array.EnumerateArray())
                {
                    if ((rowIndex++ & 255) == 0) token.ThrowIfCancellationRequested();
                    TableRow row = new TableRow();
                    foreach (string column in columns)
                    {
                        string cellText = "";
                        if (item.TryGetProperty(column, out JsonElement v))
                            cellText = IsScalar(v) ? Scalar(v) : Compact(v);
                        row.Cells.Add(new TableCell(cellText));
                    }

                    table.Rows.Add(row);
                }

                blocks.Add(table);
                return;
            }

            int index = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                MapValue(item, "[" + index.ToString(CultureInfo.InvariantCulture) + "]", blocks, depth + 1, context, token);
                index++;
            }
        }

        private static bool IsScalar(JsonElement value)
        {
            return value.ValueKind != JsonValueKind.Object && value.ValueKind != JsonValueKind.Array;
        }

        private static string Compact(JsonElement value)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (Utf8JsonWriter writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
                {
                    value.WriteTo(writer);
                }

                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private static string Scalar(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String: return value.GetString() ?? "";
                case JsonValueKind.True: return "true";
                case JsonValueKind.False: return "false";
                case JsonValueKind.Null: return "";
                default: return value.GetRawText();
            }
        }
    }
}
