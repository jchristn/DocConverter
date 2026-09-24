namespace DocConverter.Readers.Rtf
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Threading;
    using DocConverter.Enums;
    using DocConverter.Internal;
    using DocConverter.Model;

    /// <summary>
    /// Turns RTF tokens into a DocumentModel. Handles group scoped character formatting (bold, italic, underline, strike,
    /// superscript, subscript, hidden), code pages and Unicode escapes, the font table (monospace fonts become code),
    /// the style sheet (heading N styles and outline levels become headings), lists (\ls, \listtext, \pntext), tables with
    /// merged cells and header rows, pictures (PNG and JPEG), HYPERLINK fields and document information.
    /// </summary>
    internal sealed class RtfParser
    {
        #region Private-Members

        private static readonly HashSet<string> _SkipDestinations = new HashSet<string>(StringComparer.Ordinal)
        {
            "colortbl", "header", "headerl", "headerr", "headerf", "footer", "footerl", "footerr", "footerf",
            "footnote", "annotation", "atnid", "atnauthor", "atnref", "xe", "tc", "bkmkstart", "bkmkend", "nonshppict",
            "pn", "listtable", "listoverridetable", "rsidtbl", "generator", "themedata", "colorschememapping",
            "latentstyles", "datastore", "xmlnstbl", "mmathPr", "pgdsctbl", "revtbl", "filetbl", "userprops", "docvar",
            "wgrffmtfilter", "pntxta", "pntxtb", "object", "objdata", "blipuid", "fldtype", "ftnsep", "ftnsepc",
            "aftnsep", "aftnsepc", "template", "shpinst", "sp", "sn", "sv", "picprop", "defchp", "defpap", "passwordhash"
        };

        private static readonly HashSet<string> _InfoFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "title", "author", "subject", "keywords", "doccomm", "operator", "company", "category", "manager", "comment"
        };

        private readonly ConversionContext _Context;
        private readonly CancellationToken _Token;
        private readonly bool _RawBytes;
        private readonly DocumentModel _Model = new DocumentModel();
        private readonly List<RtfGroupState> _Stack = new List<RtfGroupState>();
        private RtfGroupState _State = new RtfGroupState();
        private int _CodePage = 1252;
        private int _DefaultFont = -1;
        private readonly List<byte> _PendingBytes = new List<byte>();
        private int _SkipChars = 0;
        private bool _GroupFirstToken = false;
        private bool _IgnorableNext = false;

        private readonly Dictionary<int, bool> _MonoFonts = new Dictionary<int, bool>();
        private int _FontDefIndex = -1;
        private bool _FontDefFixed = false;
        private readonly StringBuilder _FontDefName = new StringBuilder();

        private readonly Dictionary<int, int> _StyleHeadingLevels = new Dictionary<int, int>();
        private int _StyleDefIndex = 0;
        private int _StyleDefOutline = -1;
        private bool _StyleDefIsParagraph = true;
        private readonly StringBuilder _StyleDefName = new StringBuilder();

        private readonly Dictionary<string, StringBuilder> _Info = new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
        private int _CreatedYear = 0;
        private int _CreatedMonth = 0;
        private int _CreatedDay = 0;
        private int _CreatedHour = 0;
        private int _CreatedMinute = 0;
        private bool _InCreatim = false;

        private List<Inline> _Inlines = new List<Inline>();
        private bool _ParaHasText = false;
        private bool _ParaAllMono = true;
        private bool _ParaHasNonText = false;
        private int _ParaStyle = 0;
        private int _ParaOutline = -1;
        private bool _ParaInTable = false;
        private int _ParaListId = 0;
        private int _ParaListLevel = 0;
        private bool _ParaListMarkerSeen = false;
        private readonly StringBuilder _ListMarker = new StringBuilder();
        private TextAlignmentEnum _ParaAlign = TextAlignmentEnum.Default;

        private readonly List<ListBlock> _ListStack = new List<ListBlock>();
        private int _CurrentListId = -1;
        private CodeBlock? _OpenCode = null;

        private TableBlock? _Table = null;
        private readonly List<RtfCellDefinition> _RowDefs = new List<RtfCellDefinition>();
        private RtfCellDefinition _NextDef = new RtfCellDefinition();
        private bool _RowHeader = false;
        private readonly List<List<Block>> _RowCells = new List<List<Block>>();
        private List<Block> _CellBlocks = new List<Block>();
        private int _HeaderRows = 0;
        private readonly Dictionary<int, TableCell> _ColumnOwners = new Dictionary<int, TableCell>();

        private StringBuilder? _PictHex = null;
        private byte[]? _PictBinary = null;
        private string _PictType = "";
        private int _PictGoalWidth = 0;
        private int _PictGoalHeight = 0;
        private int _PictDepth = -1;
        private readonly Dictionary<string, double[]> _ImageSizes = new Dictionary<string, double[]>(StringComparer.Ordinal);

        private bool _SkippedWarned = false;

        #endregion

        #region Constructors-and-Factories

        internal RtfParser(ConversionContext context, bool rawBytes, CancellationToken token)
        {
            _Context = context;
            _RawBytes = rawBytes;
            _Token = token;
        }

        #endregion

        #region Public-Methods

        internal DocumentModel Parse(List<RtfToken> tokens)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                if ((i & 1023) == 0) _Token.ThrowIfCancellationRequested();
                RtfToken token = tokens[i];
                switch (token.Type)
                {
                    case RtfTokenType.GroupStart:
                        OnGroupStart();
                        break;
                    case RtfTokenType.GroupEnd:
                        OnGroupEnd();
                        break;
                    case RtfTokenType.ControlWord:
                        OnControlWord(token.Text, token.Parameter);
                        _GroupFirstToken = false;
                        break;
                    case RtfTokenType.ControlSymbol:
                        OnControlSymbol(token.Text);
                        break;
                    case RtfTokenType.HexByte:
                        _GroupFirstToken = false;
                        OnHexByte(token.Parameter ?? 0);
                        break;
                    case RtfTokenType.Text:
                        _GroupFirstToken = false;
                        OnText(token.Text);
                        break;
                    case RtfTokenType.Binary:
                        if (_State.Destination == RtfDestination.Picture) _PictBinary = token.Data;
                        break;
                }
            }

            FlushPendingBytes();
            EndParagraph(true);
            FinishTable();
            CloseStructures();
            ApplyInfo();
            return _Model;
        }

        #endregion

        #region Private-Methods

        private void OnGroupStart()
        {
            FlushPendingBytes();
            _Stack.Add(_State);
            _State = _State.Clone();
            _GroupFirstToken = true;
            _IgnorableNext = false;
            _SkipChars = 0;
        }

        private void OnGroupEnd()
        {
            FlushPendingBytes();
            if (_State.Destination == RtfDestination.Picture && _Stack.Count == _PictDepth) FinishPicture();
            if (_State.Destination == RtfDestination.FontTable) FinishFont();
            if (_State.Destination == RtfDestination.StyleSheet) FinishStyle();
            if (_State.Field != null && _State.Field.Depth == _Stack.Count) FinishField(_State.Field);
            if (_InCreatim && _State.Destination == RtfDestination.Info) _InCreatim = false;

            if (_Stack.Count == 0) return;
            _State = _Stack[_Stack.Count - 1];
            _Stack.RemoveAt(_Stack.Count - 1);
            _GroupFirstToken = false;
            _SkipChars = 0;
        }

        private void OnControlSymbol(string symbol)
        {
            if (symbol == "*")
            {
                _IgnorableNext = true;
                return;
            }

            _GroupFirstToken = false;
            if (_SkipChars > 0)
            {
                _SkipChars--;
                return;
            }

            if (symbol == "~") Emit("\u00A0");
            else if (symbol == "_") Emit("\u2011");
        }

        private void OnHexByte(int value)
        {
            if (_SkipChars > 0)
            {
                _SkipChars--;
                return;
            }

            if (_State.Destination == RtfDestination.Picture) return;
            _PendingBytes.Add((byte)value);
        }

        private void OnText(string text)
        {
            if (_State.Destination == RtfDestination.Skip) return;
            if (_State.Destination == RtfDestination.Picture)
            {
                if (_PictHex != null)
                {
                    foreach (char c in text)
                        if (IsHexDigit(c)) _PictHex.Append(c);
                }

                return;
            }

            StringBuilder plain = new StringBuilder();
            foreach (char c in text)
            {
                if (_SkipChars > 0)
                {
                    _SkipChars--;
                    continue;
                }

                if (_RawBytes && c >= '\u0080' && c <= '\u00FF')
                {
                    if (plain.Length > 0)
                    {
                        FlushPendingBytes();
                        Emit(plain.ToString());
                        plain.Clear();
                    }

                    _PendingBytes.Add((byte)c);
                    continue;
                }

                if (_PendingBytes.Count > 0)
                {
                    if (plain.Length > 0)
                    {
                        Emit(plain.ToString());
                        plain.Clear();
                    }

                    FlushPendingBytes();
                }

                plain.Append(c);
            }

            if (plain.Length > 0)
            {
                FlushPendingBytes();
                Emit(plain.ToString());
            }
        }

        private void FlushPendingBytes()
        {
            if (_PendingBytes.Count == 0) return;
            string decoded = RtfCodePage.Decode(_PendingBytes, _CodePage);
            _PendingBytes.Clear();
            Emit(decoded);
        }

        private void OnControlWord(string word, int? parameter)
        {
            int p = parameter ?? 1;
            bool first = _GroupFirstToken;
            bool ignorable = _IgnorableNext;
            _IgnorableNext = false;

            if (first || ignorable)
            {
                if (HandleDestination(word, parameter, ignorable)) return;
            }

            if (_State.Destination == RtfDestination.Skip) return;
            FlushPendingBytes();

            if ((word == "u" || word == "uc") && _State.Destination != RtfDestination.Picture)
            {
                OnBodyWord(word, parameter, p);
                return;
            }

            switch (_State.Destination)
            {
                case RtfDestination.FontTable:
                    if (word == "f")
                    {
                        FinishFont();
                        _FontDefIndex = p;
                    }
                    else if (word == "fmodern") _FontDefFixed = true;
                    else if (word == "fprq" && p == 1) _FontDefFixed = true;
                    return;
                case RtfDestination.StyleSheet:
                    if (word == "s") _StyleDefIndex = p;
                    else if (word == "cs" || word == "ds" || word == "ts") _StyleDefIsParagraph = false;
                    else if (word == "outlinelevel") _StyleDefOutline = p;
                    return;
                case RtfDestination.Info:
                    if (_InCreatim)
                    {
                        if (word == "yr") _CreatedYear = p;
                        else if (word == "mo") _CreatedMonth = p;
                        else if (word == "dy") _CreatedDay = p;
                        else if (word == "hr") _CreatedHour = p;
                        else if (word == "min") _CreatedMinute = p;
                    }

                    return;
                case RtfDestination.Picture:
                    OnPictureWord(word, p);
                    return;
                case RtfDestination.InfoText:
                case RtfDestination.FieldInstruction:
                    return;
            }

            OnBodyWord(word, parameter, p);
        }

        private bool HandleDestination(string word, int? parameter, bool ignorable)
        {
            switch (word)
            {
                case "fonttbl":
                    _State.Destination = RtfDestination.FontTable;
                    return true;
                case "stylesheet":
                    _State.Destination = RtfDestination.StyleSheet;
                    return true;
                case "info":
                    _State.Destination = RtfDestination.Info;
                    return true;
                case "creatim":
                    _State.Destination = RtfDestination.Info;
                    _InCreatim = true;
                    return true;
                case "pict":
                    _State.Destination = RtfDestination.Picture;
                    _PictHex = new StringBuilder();
                    _PictBinary = null;
                    _PictType = "";
                    _PictGoalWidth = 0;
                    _PictGoalHeight = 0;
                    _PictDepth = _Stack.Count;
                    return true;
                case "shppict":
                    return true;
                case "field":
                    _State.Field = new RtfField(_Stack.Count);
                    _State.FieldResult = false;
                    return true;
                case "fldinst":
                    _State.Destination = RtfDestination.FieldInstruction;
                    return true;
                case "fldrslt":
                    _State.FieldResult = true;
                    StartFieldResult();
                    return true;
                case "listtext":
                case "pntext":
                    _State.Destination = RtfDestination.ListText;
                    _ParaListMarkerSeen = true;
                    _ListMarker.Clear();
                    return true;
            }

            if (_State.Destination == RtfDestination.Info && _InfoFields.Contains(word))
            {
                _State.Destination = RtfDestination.InfoText;
                _State.InfoField = word;
                if (!_Info.ContainsKey(word)) _Info[word] = new StringBuilder();
                return true;
            }

            if (_SkipDestinations.Contains(word) || ignorable)
            {
                if ((word == "footnote" || word == "annotation") && !_SkippedWarned)
                {
                    _SkippedWarned = true;
                    _Context.AddWarning(WarningCodeEnum.FormattingLost, "RTF footnotes and annotations were skipped.");
                }

                _State.Destination = RtfDestination.Skip;
                return true;
            }

            return false;
        }

        private void OnPictureWord(string word, int p)
        {
            switch (word)
            {
                case "pngblip":
                case "jpegblip":
                case "emfblip":
                case "wmetafile":
                case "dibitmap":
                case "wbitmap":
                case "macpict":
                case "pmmetafile":
                    _PictType = word;
                    break;
                case "picwgoal":
                    _PictGoalWidth = p;
                    break;
                case "pichgoal":
                    _PictGoalHeight = p;
                    break;
            }
        }

        private void OnBodyWord(string word, int? parameter, int p)
        {
            bool on = !parameter.HasValue || parameter.Value != 0;
            switch (word)
            {
                case "ansicpg":
                    if (parameter.HasValue) _CodePage = parameter.Value;
                    break;
                case "deff":
                    _DefaultFont = p;
                    if (_Stack.Count <= 1) _State.FontIndex = p;
                    break;
                case "b":
                    _State.Bold = on;
                    break;
                case "i":
                    _State.Italic = on;
                    break;
                case "ul":
                case "uld":
                case "uldb":
                case "ulw":
                case "uldash":
                case "ulth":
                case "ulwave":
                    _State.Underline = on;
                    break;
                case "ulnone":
                    _State.Underline = false;
                    break;
                case "strike":
                case "striked":
                    _State.Strike = on;
                    break;
                case "super":
                    _State.Superscript = on;
                    _State.Subscript = false;
                    break;
                case "sub":
                    _State.Subscript = on;
                    _State.Superscript = false;
                    break;
                case "nosupersub":
                    _State.Superscript = false;
                    _State.Subscript = false;
                    break;
                case "plain":
                    _State.ResetCharacter();
                    _State.FontIndex = _DefaultFont;
                    break;
                case "v":
                    _State.Hidden = on;
                    break;
                case "f":
                    _State.FontIndex = p;
                    break;
                case "uc":
                    _State.UnicodeSkip = p < 0 ? 0 : p;
                    break;
                case "u":
                    if (parameter.HasValue)
                    {
                        int code = parameter.Value < 0 ? parameter.Value + 65536 : parameter.Value;
                        Emit(((char)code).ToString());
                        _SkipChars = _State.UnicodeSkip;
                    }

                    break;
                case "par":
                    EndParagraph(false);
                    break;
                case "sect":
                    EndParagraph(false);
                    break;
                case "page":
                    EndParagraph(false);
                    FinishTable();
                    CloseStructures();
                    _Model.Blocks.Add(new PageBreakBlock());
                    break;
                case "line":
                    AppendInline(new LineBreakInline());
                    break;
                case "tab":
                    Emit("\t");
                    break;
                case "pard":
                    _ParaStyle = 0;
                    _ParaOutline = -1;
                    _ParaInTable = false;
                    _ParaListId = 0;
                    _ParaListLevel = 0;
                    _ParaAlign = TextAlignmentEnum.Default;
                    break;
                case "s":
                    _ParaStyle = p;
                    break;
                case "outlinelevel":
                    _ParaOutline = p;
                    break;
                case "intbl":
                    _ParaInTable = true;
                    break;
                case "ls":
                    _ParaListId = p;
                    break;
                case "ilvl":
                    _ParaListLevel = p;
                    break;
                case "ql":
                    _ParaAlign = TextAlignmentEnum.Left;
                    break;
                case "qc":
                    _ParaAlign = TextAlignmentEnum.Center;
                    break;
                case "qr":
                    _ParaAlign = TextAlignmentEnum.Right;
                    break;
                case "qj":
                    _ParaAlign = TextAlignmentEnum.Justify;
                    break;
                case "trowd":
                    _RowDefs.Clear();
                    _NextDef = new RtfCellDefinition();
                    _RowHeader = false;
                    break;
                case "trhdr":
                    _RowHeader = true;
                    break;
                case "clmgf":
                    _NextDef.MergeFirst = true;
                    break;
                case "clmrg":
                    _NextDef.MergeContinue = true;
                    break;
                case "clvmgf":
                    _NextDef.VerticalMergeFirst = true;
                    break;
                case "clvmrg":
                    _NextDef.VerticalMergeContinue = true;
                    break;
                case "cellx":
                    _RowDefs.Add(_NextDef);
                    _NextDef = new RtfCellDefinition();
                    break;
                case "cell":
                case "nestcell":
                    EndCell();
                    break;
                case "row":
                case "nestrow":
                    EndRow();
                    break;
                case "emdash":
                    Emit("\u2014");
                    break;
                case "endash":
                    Emit("\u2013");
                    break;
                case "bullet":
                    Emit("\u2022");
                    break;
                case "lquote":
                    Emit("\u2018");
                    break;
                case "rquote":
                    Emit("\u2019");
                    break;
                case "ldblquote":
                    Emit("\u201C");
                    break;
                case "rdblquote":
                    Emit("\u201D");
                    break;
                case "emspace":
                    Emit("\u2003");
                    break;
                case "enspace":
                    Emit("\u2002");
                    break;
                case "qmspace":
                    Emit("\u2005");
                    break;
            }
        }

        private void Emit(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            switch (_State.Destination)
            {
                case RtfDestination.FontTable:
                    foreach (char c in text)
                    {
                        if (c == ';') FinishFont();
                        else _FontDefName.Append(c);
                    }

                    return;
                case RtfDestination.StyleSheet:
                    foreach (char c in text)
                    {
                        if (c == ';') FinishStyle();
                        else _StyleDefName.Append(c);
                    }

                    return;
                case RtfDestination.InfoText:
                    if (_Info.TryGetValue(_State.InfoField, out StringBuilder? info)) info.Append(text);
                    return;
                case RtfDestination.FieldInstruction:
                    if (_State.Field != null) _State.Field.Instruction.Append(text);
                    return;
                case RtfDestination.ListText:
                    _ListMarker.Append(text);
                    return;
                case RtfDestination.Body:
                    if (_State.Hidden) return;
                    AppendText(text);
                    return;
                default:
                    return;
            }
        }

        private List<Inline> InlineTarget()
        {
            if (_State.FieldResult && _State.Field != null && _State.Field.Link != null) return _State.Field.Link.Inlines;
            return _Inlines;
        }

        private void AppendText(string text)
        {
            bool mono = _State.FontIndex >= 0 && _MonoFonts.TryGetValue(_State.FontIndex, out bool isMono) && isMono;
            InlineStyleEnum style = InlineStyleEnum.None;
            if (_State.Bold) style |= InlineStyleEnum.Bold;
            if (_State.Italic) style |= InlineStyleEnum.Italic;
            if (_State.Underline) style |= InlineStyleEnum.Underline;
            if (_State.Strike) style |= InlineStyleEnum.Strikethrough;
            if (_State.Superscript) style |= InlineStyleEnum.Superscript;
            if (_State.Subscript) style |= InlineStyleEnum.Subscript;
            if (mono) style |= InlineStyleEnum.Code;

            if (text.Trim().Length > 0)
            {
                _ParaHasText = true;
                if (!mono) _ParaAllMono = false;
            }

            List<Inline> target = InlineTarget();
            if (target.Count > 0 && target[target.Count - 1] is TextInline last && last.Style == style)
            {
                last.Text += text;
                return;
            }

            target.Add(new TextInline(text, style));
        }

        private void AppendInline(Inline inline)
        {
            if (!(inline is TextInline)) _ParaHasNonText = true;
            InlineTarget().Add(inline);
        }

        private void StartFieldResult()
        {
            RtfField? field = _State.Field;
            if (field == null || field.Link != null) return;
            string? url = field.HyperlinkUrl;
            if (url == null) return;
            LinkInline link = new LinkInline();
            link.Url = url;
            field.Link = link;
            _Inlines.Add(link);
            _ParaHasNonText = true;
        }

        private void FinishField(RtfField field)
        {
            if (field.Link != null && field.Link.Inlines.Count == 0) _Inlines.Remove(field.Link);
            field.Link = null;
        }

        private void FinishFont()
        {
            if (_FontDefIndex >= 0)
            {
                string name = _FontDefName.ToString().Trim().ToLowerInvariant();
                bool mono = _FontDefFixed
                    || name.IndexOf("courier", StringComparison.Ordinal) >= 0
                    || name.IndexOf("mono", StringComparison.Ordinal) >= 0
                    || name.IndexOf("consolas", StringComparison.Ordinal) >= 0
                    || name.IndexOf("menlo", StringComparison.Ordinal) >= 0
                    || name.IndexOf("lucida console", StringComparison.Ordinal) >= 0;
                _MonoFonts[_FontDefIndex] = mono;
            }

            _FontDefIndex = -1;
            _FontDefFixed = false;
            _FontDefName.Clear();
        }

        private void FinishStyle()
        {
            string name = _StyleDefName.ToString().Trim().ToLowerInvariant();
            if (_StyleDefIsParagraph && (name.Length > 0 || _StyleDefOutline >= 0))
            {
                int level = 0;
                if (_StyleDefOutline >= 0 && _StyleDefOutline <= 8) level = _StyleDefOutline + 1;
                else if (name.StartsWith("heading ", StringComparison.Ordinal))
                {
                    if (int.TryParse(name.Substring(8).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) level = n;
                }
                else if (name == "title")
                {
                    level = 1;
                }

                if (level > 0) _StyleHeadingLevels[_StyleDefIndex] = level > 6 ? 6 : level;
            }

            _StyleDefIndex = 0;
            _StyleDefOutline = -1;
            _StyleDefIsParagraph = true;
            _StyleDefName.Clear();
        }

        private void FinishPicture()
        {
            byte[]? data = _PictBinary;
            if (data == null && _PictHex != null && _PictHex.Length >= 2) data = HexToBytes(_PictHex.ToString());
            _PictHex = null;
            _PictBinary = null;
            _PictDepth = -1;

            if (_PictType != "pngblip" && _PictType != "jpegblip")
            {
                _Context.AddWarning(WarningCodeEnum.UnknownElementSkipped, "An embedded RTF picture in a format other than PNG or JPEG (" + (_PictType.Length > 0 ? _PictType : "unknown") + ") was skipped.");
                return;
            }

            if (data == null || data.Length == 0) return;
            ImageInfo? info = ImageHeaderReader.Read(data);
            if (info == null)
            {
                _Context.AddWarning(WarningCodeEnum.UnknownElementSkipped, "An embedded RTF picture could not be decoded and was skipped.");
                return;
            }

            BinaryResource resource = new BinaryResource
            {
                MediaType = info.MediaType,
                Data = data,
                PixelWidth = info.Width,
                PixelHeight = info.Height,
                FileName = "image" + (_Model.Resources.Count + 1) + "." + ImageHeaderReader.ExtensionFor(info.Format)
            };

            string id = _Model.AddResource(resource);
            if (_PictGoalWidth > 0 && _PictGoalHeight > 0)
                _ImageSizes[id] = new double[] { _PictGoalWidth / 20.0, _PictGoalHeight / 20.0 };
            AppendInline(new ImageInline(id, null));
        }

        private static byte[] HexToBytes(string hex)
        {
            int length = hex.Length / 2;
            byte[] bytes = new byte[length];
            for (int i = 0; i < length; i++)
                bytes[i] = (byte)((HexValue(hex[i * 2]) << 4) | HexValue(hex[i * 2 + 1]));
            return bytes;
        }

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private static int HexValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            return c - 'A' + 10;
        }

        private void ResetParagraphContent()
        {
            _Inlines = new List<Inline>();
            _ParaHasText = false;
            _ParaAllMono = true;
            _ParaHasNonText = false;
            _ParaListMarkerSeen = false;
            _ListMarker.Clear();

            RtfField? field = _State.Field;
            if (field != null && field.Link != null)
            {
                LinkInline continued = new LinkInline();
                continued.Url = field.Link.Url;
                field.Link = continued;
                _Inlines.Add(continued);
            }
        }

        private Block? ParagraphContentBlock()
        {
            if (_Inlines.Count == 0) return null;
            if (!_ParaHasText && _Inlines.Count == 1 && _Inlines[0] is ImageInline image)
            {
                ImageBlock block = new ImageBlock(image.ResourceId, image.AltText);
                if (_ImageSizes.TryGetValue(image.ResourceId, out double[]? size))
                {
                    block.Width = size[0];
                    block.Height = size[1];
                }

                return block;
            }

            if (!_ParaHasText && !_ParaHasNonText) return null;
            ParagraphBlock paragraph = new ParagraphBlock(_Inlines);
            paragraph.Alignment = _ParaAlign;
            return paragraph;
        }

        private void EndParagraph(bool final)
        {
            FlushPendingBytes();

            if (_ParaInTable)
            {
                Block? cellBlock = ParagraphContentBlock();
                EnsureTable();
                if (cellBlock != null) _CellBlocks.Add(cellBlock);
                ResetParagraphContent();
                return;
            }

            if (_Table != null && (_Inlines.Count > 0 || !final)) FinishTable();

            int headingLevel = 0;
            if (_ParaOutline >= 0 && _ParaOutline <= 8) headingLevel = _ParaOutline + 1;
            else if (_StyleHeadingLevels.TryGetValue(_ParaStyle, out int styleLevel)) headingLevel = styleLevel;
            if (headingLevel > 6) headingLevel = 6;

            bool isList = _ParaListId > 0 || _ParaListMarkerSeen;
            Block? content = ParagraphContentBlock();

            if (content == null)
            {
                if (_OpenCode != null && !final && !isList && headingLevel == 0) _OpenCode.Text += "\n";
                ResetParagraphContent();
                return;
            }

            if (headingLevel > 0 && content is ParagraphBlock headingSource)
            {
                CloseStructures();
                HeadingBlock heading = new HeadingBlock();
                heading.Level = headingLevel;
                heading.Inlines = headingSource.Inlines;
                _Model.Blocks.Add(heading);
            }
            else if (isList)
            {
                CloseCode();
                AddListItem(content);
            }
            else if (content is ParagraphBlock codeSource && _ParaAllMono && _ParaHasText && !_ParaHasNonText)
            {
                CloseLists();
                string text = ModelText.Inlines(codeSource.Inlines);
                if (_OpenCode == null)
                {
                    _OpenCode = new CodeBlock(text, null);
                    _Model.Blocks.Add(_OpenCode);
                }
                else
                {
                    _OpenCode.Text += "\n" + text;
                }
            }
            else
            {
                CloseStructures();
                _Model.Blocks.Add(content);
            }

            ResetParagraphContent();
        }

        private void AddListItem(Block content)
        {
            string marker = _ListMarker.ToString().Replace("\t", "").Trim();
            bool ordered = IsOrderedMarker(marker, out int number);
            int level = _ParaListLevel < 0 ? 0 : _ParaListLevel;
            int maxDepth = _Context.MaxNestingDepth > 1 ? _Context.MaxNestingDepth - 1 : 0;
            if (level > maxDepth) level = maxDepth;

            if (_ListStack.Count > 0 && _ParaListId != _CurrentListId) CloseLists();
            if (_ListStack.Count > 0 && level == 0 && (_ListStack[0].Kind == ListKindEnum.Ordered) != ordered) CloseLists();
            _CurrentListId = _ParaListId;

            if (_ListStack.Count == 0)
            {
                ListBlock root = new ListBlock(ordered ? ListKindEnum.Ordered : ListKindEnum.Unordered);
                root.Start = ordered ? number : 1;
                _Model.Blocks.Add(root);
                _ListStack.Add(root);
            }

            while (_ListStack.Count - 1 > level) _ListStack.RemoveAt(_ListStack.Count - 1);
            while (_ListStack.Count - 1 < level)
            {
                ListBlock parent = _ListStack[_ListStack.Count - 1];
                if (parent.Items.Count == 0) parent.Items.Add(new ListItemBlock());
                ListBlock nested = new ListBlock(ordered ? ListKindEnum.Ordered : ListKindEnum.Unordered);
                nested.Start = ordered ? number : 1;
                parent.Items[parent.Items.Count - 1].Blocks.Add(nested);
                _ListStack.Add(nested);
            }

            ListItemBlock item = new ListItemBlock();
            item.Blocks.Add(content);
            _ListStack[_ListStack.Count - 1].Items.Add(item);
        }

        private static bool IsOrderedMarker(string marker, out int number)
        {
            number = 1;
            if (marker.Length == 0) return false;
            string core = marker.TrimEnd('.', ')').TrimStart('(');
            if (core.Length == 0 || core.Length == marker.Length && !char.IsDigit(marker[0])) return false;
            if (int.TryParse(core, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                number = parsed;
                return true;
            }

            return core.Length == 1 && char.IsLetter(core[0]) && core.Length < marker.Length;
        }

        private void EnsureTable()
        {
            if (_Table != null) return;
            CloseStructures();
            _Table = new TableBlock();
            _HeaderRows = 0;
            _ColumnOwners.Clear();
            _Model.Blocks.Add(_Table);
        }

        private void EndCell()
        {
            FlushPendingBytes();
            EnsureTable();
            Block? content = ParagraphContentBlock();
            if (content != null) _CellBlocks.Add(content);
            ResetParagraphContent();
            _RowCells.Add(_CellBlocks);
            _CellBlocks = new List<Block>();
        }

        private void EndRow()
        {
            if (_Table == null) return;
            if (_CellBlocks.Count > 0)
            {
                _RowCells.Add(_CellBlocks);
                _CellBlocks = new List<Block>();
            }

            TableRow row = new TableRow();
            TableCell? previous = null;
            int column = 0;
            for (int i = 0; i < _RowCells.Count; i++)
            {
                RtfCellDefinition? def = i < _RowDefs.Count ? _RowDefs[i] : null;
                if (def != null && def.MergeContinue && previous != null)
                {
                    previous.ColumnSpan++;
                    column++;
                    continue;
                }

                if (def != null && def.VerticalMergeContinue && _ColumnOwners.TryGetValue(column, out TableCell? owner))
                {
                    owner.RowSpan++;
                    column += owner.ColumnSpan;
                    continue;
                }

                TableCell cell = new TableCell();
                cell.Blocks = _RowCells[i];
                cell.IsHeader = _RowHeader;
                row.Cells.Add(cell);
                if (def != null && def.VerticalMergeFirst) _ColumnOwners[column] = cell;
                else _ColumnOwners.Remove(column);
                previous = cell;
                column++;
            }

            _RowCells.Clear();
            if (_RowHeader && _Table.Rows.Count == _HeaderRows) _HeaderRows++;
            _Table.Rows.Add(row);
        }

        private void FinishTable()
        {
            if (_Table == null) return;
            if (_RowCells.Count > 0 || _CellBlocks.Count > 0) EndRow();
            _Table.HeaderRowCount = _HeaderRows;
            if (_Table.Rows.Count == 0) _Model.Blocks.Remove(_Table);
            _Table = null;
            _ColumnOwners.Clear();
        }

        private void CloseLists()
        {
            _ListStack.Clear();
        }

        private void CloseCode()
        {
            if (_OpenCode != null) _OpenCode.Text = _OpenCode.Text.TrimEnd('\n');
            _OpenCode = null;
        }

        private void CloseStructures()
        {
            CloseLists();
            CloseCode();
        }

        private void ApplyInfo()
        {
            _Model.Metadata.Title = InfoValue("title");
            _Model.Metadata.Author = InfoValue("author");
            _Model.Metadata.Subject = InfoValue("subject");
            _Model.Metadata.Keywords = InfoValue("keywords");
            _Model.Metadata.Description = InfoValue("doccomm");
            if (_CreatedYear > 0 && _CreatedMonth >= 1 && _CreatedMonth <= 12 && _CreatedDay >= 1 && _CreatedDay <= 31)
            {
                try
                {
                    _Model.Metadata.CreatedUtc = new DateTime(_CreatedYear, _CreatedMonth, _CreatedDay, Clamp(_CreatedHour, 0, 23), Clamp(_CreatedMinute, 0, 59), 0, DateTimeKind.Utc);
                }
                catch (ArgumentOutOfRangeException)
                {
                    _Model.Metadata.CreatedUtc = null;
                }
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        private string? InfoValue(string key)
        {
            if (!_Info.TryGetValue(key, out StringBuilder? sb)) return null;
            string value = sb.ToString().Trim();
            return value.Length == 0 ? null : value;
        }

        #endregion
    }
}
