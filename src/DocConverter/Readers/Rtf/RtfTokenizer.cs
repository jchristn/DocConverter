namespace DocConverter.Readers.Rtf
{
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Splits RTF source into tokens. Unlike a whitespace-skipping tokenizer, spaces inside text are preserved; only the
    /// single delimiter space after a control word and raw line breaks are dropped, as the RTF specification requires.
    /// </summary>
    internal static class RtfTokenizer
    {
        internal static List<RtfToken> Tokenize(string rtf)
        {
            List<RtfToken> tokens = new List<RtfToken>();
            StringBuilder text = new StringBuilder();
            int pos = 0;
            while (pos < rtf.Length)
            {
                char c = rtf[pos];
                if (c == '{' || c == '}')
                {
                    FlushText(tokens, text);
                    tokens.Add(new RtfToken(c == '{' ? RtfTokenType.GroupStart : RtfTokenType.GroupEnd, c.ToString(), null, null));
                    pos++;
                }
                else if (c == '\\')
                {
                    if (pos + 1 >= rtf.Length)
                    {
                        pos++;
                        continue;
                    }

                    char next = rtf[pos + 1];
                    if (next == '\\' || next == '{' || next == '}')
                    {
                        text.Append(next);
                        pos += 2;
                        continue;
                    }

                    FlushText(tokens, text);
                    if (IsAsciiLetter(next))
                    {
                        pos = ReadControlWord(rtf, pos + 1, tokens);
                    }
                    else if (next == '\'')
                    {
                        if (IsHex(rtf, pos + 2) && IsHex(rtf, pos + 3))
                        {
                            int value = HexValue(rtf[pos + 2]) * 16 + HexValue(rtf[pos + 3]);
                            tokens.Add(new RtfToken(RtfTokenType.HexByte, "'", value, null));
                            pos += 4;
                        }
                        else
                        {
                            pos += 2;
                        }
                    }
                    else if (next == '\r' || next == '\n')
                    {
                        tokens.Add(new RtfToken(RtfTokenType.ControlWord, "par", null, null));
                        pos += 2;
                    }
                    else
                    {
                        tokens.Add(new RtfToken(RtfTokenType.ControlSymbol, next.ToString(), null, null));
                        pos += 2;
                    }
                }
                else if (c == '\r' || c == '\n')
                {
                    pos++;
                }
                else
                {
                    text.Append(c);
                    pos++;
                }
            }

            FlushText(tokens, text);
            return tokens;
        }

        private static int ReadControlWord(string rtf, int pos, List<RtfToken> tokens)
        {
            int start = pos;
            while (pos < rtf.Length && IsAsciiLetter(rtf[pos]) && pos - start < 32) pos++;
            string word = rtf.Substring(start, pos - start);

            int? parameter = null;
            if (pos < rtf.Length && (rtf[pos] == '-' || char.IsDigit(rtf[pos])))
            {
                bool negative = rtf[pos] == '-';
                if (negative) pos++;
                long value = 0;
                int digits = 0;
                while (pos < rtf.Length && char.IsDigit(rtf[pos]) && digits < 10)
                {
                    value = value * 10 + (rtf[pos] - '0');
                    pos++;
                    digits++;
                }

                if (value > int.MaxValue) value = int.MaxValue;
                parameter = negative ? -(int)value : (int)value;
            }

            if (pos < rtf.Length && rtf[pos] == ' ') pos++;

            if (word == "bin" && parameter.HasValue && parameter.Value > 0)
            {
                int length = parameter.Value;
                if (pos + length > rtf.Length) length = rtf.Length - pos;
                byte[] data = new byte[length];
                for (int i = 0; i < length; i++) data[i] = (byte)rtf[pos + i];
                tokens.Add(new RtfToken(RtfTokenType.Binary, "bin", length, data));
                return pos + length;
            }

            tokens.Add(new RtfToken(RtfTokenType.ControlWord, word, parameter, null));
            return pos;
        }

        private static void FlushText(List<RtfToken> tokens, StringBuilder text)
        {
            if (text.Length == 0) return;
            tokens.Add(new RtfToken(RtfTokenType.Text, text.ToString(), null, null));
            text.Clear();
        }

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        private static bool IsHex(string s, int index)
        {
            if (index >= s.Length) return false;
            char c = s[index];
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private static int HexValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            return c - 'A' + 10;
        }
    }
}
