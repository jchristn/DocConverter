namespace DocConverter.Model.Serialization
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using DocConverter.Exceptions;

    /// <summary>
    /// Serializes the canonical document form to and from JSON.
    /// </summary>
    internal static class CanonicalJson
    {
        internal const string Marker = "docconverter";

        private static readonly JsonSerializerOptions _Indented = Create(true);
        private static readonly JsonSerializerOptions _Compact = Create(false);

        internal static byte[] Serialize(CanonicalDocument dto, bool indented)
        {
            return JsonSerializer.SerializeToUtf8Bytes(dto, indented ? _Indented : _Compact);
        }

        internal static CanonicalDocument Deserialize(byte[] utf8, int offset, int length)
        {
            try
            {
                CanonicalDocument? dto = JsonSerializer.Deserialize<CanonicalDocument>(new ReadOnlySpan<byte>(utf8, offset, length), _Compact);
                if (dto == null) throw new DocumentReadException("The canonical JSON document is empty.");
                if (dto.DocConverter != "1") throw new DocumentReadException("Unsupported canonical document version '" + dto.DocConverter + "'. This release reads version 1.");
                return dto;
            }
            catch (JsonException ex)
            {
                throw new DocumentReadException("The canonical JSON document is malformed: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// True when the JSON is an object with a top level "docconverter" property.
        /// </summary>
        internal static bool IsCanonical(byte[] utf8, int offset, int length)
        {
            try
            {
                Utf8JsonReader reader = new Utf8JsonReader(new ReadOnlySpan<byte>(utf8, offset, length), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = 4096 });
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return false;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject) return false;
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        if (reader.ValueTextEquals(Marker)) return true;
                        reader.Skip();
                    }
                }

                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static JsonSerializerOptions Create(bool indented)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                MaxDepth = 1024,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return options;
        }
    }
}
