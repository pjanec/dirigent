// https://github.com/sinairv/WinFormsSyntaxHighlighter
// commit 4b2576613452969e8c20f4f8bb41089db8ad710f

using System;
using System.Drawing;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinFormsSyntaxHighlighter
{
    public static class StringExtensions
    {
        public static string NormalizeLineBreaks(this string instance, string preferredLineBreak)
        {
            return instance.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", preferredLineBreak);
        }

        public static string NormalizeLineBreaks(this string instance)
        {
            return NormalizeLineBreaks(instance, Environment.NewLine);
        }
    }

    public class ColorUtils
    {
        public static string ColorToRtfTableEntry(Color color)
        {
            return String.Format(@"\red{0}\green{1}\blue{2}", color.R, color.G, color.B);
        }
    }

    /// <summary>
    /// Enumerates the type of the parsed content
    /// </summary>
    public enum ExpressionType
    {
        None = 0,
        Identifier, // i.e. a word which is neither keyword nor inside any word-group
        Operator,
        Number,
        Whitespace,
        Newline,
        Keyword,
        Comment,
        CommentLine,
        String,
        DelimitedGroup,       // needs extra argument
        WordGroup             // needs extra argument
    }

    public class Expression
    {
        public ExpressionType Type { get; private set; }
        public string Content { get; private set; }
        public string Group { get; private set; }

        public Expression(string content, ExpressionType type, string group)
        {
            if (content == null)
                throw new ArgumentNullException("content");
            if (group == null)
                throw new ArgumentNullException("group");

            Type = type;
            Content = content;
            Group = group;
        }

        public Expression(string content, ExpressionType type)
            : this(content, type, String.Empty)
        {
        }

        public override string ToString()
        {
            if (Type == ExpressionType.Newline)
                return String.Format("({0})", Type);

            return String.Format("({0} --> {1}{2})", Content, Type, Group.Length > 0 ? " --> " + Group : String.Empty);
        }
    }

    public class PatternDefinition
    {
        private readonly Regex _regex;
        private ExpressionType _expressionType = ExpressionType.Identifier;
        private readonly bool _isCaseSensitive = true;

        public PatternDefinition(Regex regularExpression)
        {
            if (regularExpression == null)
                throw new ArgumentNullException("regularExpression");
            _regex = regularExpression;
        }

        public PatternDefinition(string regexPattern)
        {
            if (String.IsNullOrEmpty(regexPattern))
                throw new ArgumentException("regex pattern must not be null or empty", "regexPattern");

            _regex = new Regex(regexPattern, RegexOptions.Compiled);
        }

        public PatternDefinition(params string[] tokens)
            : this(true, tokens)
        {
        }

        public PatternDefinition(IEnumerable<string> tokens)
            : this(true, tokens)
        {
        }

        internal PatternDefinition(bool caseSensitive, IEnumerable<string> tokens)
        {
            if (tokens == null)
                throw new ArgumentNullException("tokens");

            _isCaseSensitive = caseSensitive;

            var regexTokens = new List<string>();

            foreach (var token in tokens)
            {
                var escaptedToken = Regex.Escape(token.Trim());

                if (escaptedToken.Length > 0)
                {
                    if (Char.IsLetterOrDigit(escaptedToken[0]))
                        regexTokens.Add(String.Format(@"\b{0}\b", escaptedToken));
                    else
                        regexTokens.Add(escaptedToken);
                }
            }

            string pattern = String.Join("|", regexTokens);
            var regexOptions = RegexOptions.Compiled;
            if (!caseSensitive)
                regexOptions = regexOptions | RegexOptions.IgnoreCase;
            _regex = new Regex(pattern, regexOptions);
        }

        internal ExpressionType ExpressionType 
        {
            get { return _expressionType; }
            set { _expressionType = value; }
        }

        internal bool IsCaseSensitive 
        {
            get { return _isCaseSensitive; }
        }

        internal Regex Regex
        {
            get { return _regex; }
        }
    }

    public class CaseInsensitivePatternDefinition : PatternDefinition
    {
        public CaseInsensitivePatternDefinition(IEnumerable<string> tokens)
            : base(false, tokens)
        {
        }

        public CaseInsensitivePatternDefinition(params string[] tokens)
            : base(false, tokens)
        {
        }
    }

    public class SyntaxStyle
    {
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public Color Color { get; set; }

        public SyntaxStyle(Color color, bool bold, bool italic)
        {
            Color = color;
            Bold = bold;
            Italic = italic;
        }

        public SyntaxStyle(Color color)
            : this(color, false, false)
        {
        }
    }

    internal class PatternStyleMap
    {
        public string Name { get; set; }
        public PatternDefinition PatternDefinition { get; set; }
        public SyntaxStyle SyntaxStyle { get; set; }

        public PatternStyleMap(string name, PatternDefinition patternDefinition, SyntaxStyle syntaxStyle)
        {
            if (patternDefinition == null)
                throw new ArgumentNullException("patternDefinition");
            if (syntaxStyle == null)
                throw new ArgumentNullException("syntaxStyle");
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException("name must not be null or empty", "name");

            Name = name;
            PatternDefinition = patternDefinition;
            SyntaxStyle = syntaxStyle;
        }
    }

    internal class StyleGroupPair
    {
        public int Index { get; set; }
        public SyntaxStyle SyntaxStyle { get; set; }
        public string GroupName { get; set; }

        public StyleGroupPair(SyntaxStyle syntaxStyle, string groupName)
        {
            if (syntaxStyle == null)
                throw new ArgumentNullException("syntaxStyle");
            if (groupName == null)
                throw new ArgumentNullException("groupName");

            SyntaxStyle = syntaxStyle;
            GroupName = groupName;
        }
    }

    public class SyntaxHighlighter
    {
        /// <summary>
        /// Reference to the RichTextBox instance, for which 
        /// the syntax highlighting is going to occur.
        /// </summary>
        private readonly RichTextBox _richTextBox;

        private readonly int _fontSizeFactor;

        private readonly string _fontName;

        /// <summary>
        /// Determines whether the program is busy creating rtf for the previous
        /// modification of the text-box. It is necessary to avoid blinks when the 
        /// user is typing fast.
        /// </summary>
        private bool _isDuringHighlight;

        private List<StyleGroupPair> _styleGroupPairs;

        private readonly List<PatternStyleMap> _patternStyles = new List<PatternStyleMap>(); 

        public SyntaxHighlighter(RichTextBox richTextBox)
        {
            if (richTextBox == null)
                throw new ArgumentNullException("richTextBox");

            _richTextBox = richTextBox;

            _fontSizeFactor = Convert.ToInt32(_richTextBox.Font.Size * 2);
            _fontName = _richTextBox.Font.Name;

            DisableHighlighting = false;

            _richTextBox.TextChanged += RichTextBox_TextChanged;
        }


        /// <summary>
        /// Gets or sets a value indicating whether highlighting should be disabled or not.
        /// If true, the user input will remain intact. If false, the rich content will be
        /// modified to match the syntax of the currently selected language.
        /// </summary>
        public bool DisableHighlighting { get; set; }

        public void AddPattern(PatternDefinition patternDefinition, SyntaxStyle syntaxStyle)
        {
            AddPattern((_patternStyles.Count + 1).ToString(CultureInfo.InvariantCulture), patternDefinition, syntaxStyle);
        }

        public void AddPattern(string name, PatternDefinition patternDefinition, SyntaxStyle syntaxStyle)
        {
            if (patternDefinition == null)
                throw new ArgumentNullException("patternDefinition");
            if (syntaxStyle == null)
                throw new ArgumentNullException("syntaxStyle");
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException("name must not be null or empty", "name");

            var existingPatternStyle = FindPatternStyle(name);

            if (existingPatternStyle != null)
                throw new ArgumentException("A pattern style pair with the same name already exists");

            _patternStyles.Add(new PatternStyleMap(name, patternDefinition, syntaxStyle));
        }

        protected SyntaxStyle GetDefaultStyle()
        {
            return new SyntaxStyle(_richTextBox.ForeColor, _richTextBox.Font.Bold, _richTextBox.Font.Italic);
        }

        private PatternStyleMap FindPatternStyle(string name)
        {
            var patternStyle = _patternStyles.FirstOrDefault(p => String.Equals(p.Name, name, StringComparison.Ordinal));
            return patternStyle;
        }

        /// <summary>
        /// Rehighlights the text-box content.
        /// </summary>
        public void ReHighlight()
        {
            if (!DisableHighlighting)
            {
                if (_isDuringHighlight) 
                    return;

                _richTextBox.DisableThenDoThenEnable(HighlighTextBase);
            }
        }

        private void RichTextBox_TextChanged(object sender, EventArgs e)
        {
            ReHighlight();
        }

        // TODO: make abstact
        internal IEnumerable<Expression> Parse(string text)
        {
            text = text.NormalizeLineBreaks("\n");
            var parsedExpressions = new List<Expression> { new Expression(text, ExpressionType.None, String.Empty) };

            foreach (var patternStyleMap in _patternStyles)
            {
                parsedExpressions = ParsePattern(patternStyleMap, parsedExpressions);
            }

            parsedExpressions = ProcessLineBreaks(parsedExpressions);
            return parsedExpressions;
        }

        // TODO: move to child
        private Regex _lineBreakRegex;

        // TODO: move to child
        private Regex GetLineBreakRegex()
        {
            if (_lineBreakRegex == null)
                _lineBreakRegex = new Regex(Regex.Escape("\n"), RegexOptions.Compiled);

            return _lineBreakRegex;
        }

        private List<Expression> ProcessLineBreaks(List<Expression> expressions)
        {
            var parsedExpressions = new List<Expression>();

            var regex = GetLineBreakRegex();

            foreach (var inputExpression in expressions)
            {
                int lastProcessedIndex = -1;

                foreach (var match in regex.Matches(inputExpression.Content).Cast<Match>().OrderBy(m => m.Index))
                {
                    if (match.Success)
                    {
                        if (match.Index > lastProcessedIndex + 1)
                        {
                            string nonMatchedContent = inputExpression.Content.Substring(lastProcessedIndex + 1,
                                match.Index - lastProcessedIndex - 1);
                            var nonMatchedExpression = new Expression(nonMatchedContent, inputExpression.Type,
                                inputExpression.Group);
                            parsedExpressions.Add(nonMatchedExpression);
                            //lastProcessedIndex = match.Index + match.Length - 1;
                        }

                        string matchedContent = inputExpression.Content.Substring(match.Index, match.Length);
                        var matchedExpression = new Expression(matchedContent,
                            ExpressionType.Newline, "line-break");
                        parsedExpressions.Add(matchedExpression);
                        lastProcessedIndex = match.Index + match.Length - 1;
                    }
                }

                if (lastProcessedIndex < inputExpression.Content.Length - 1)
                {
                    string nonMatchedContent = inputExpression.Content.Substring(lastProcessedIndex + 1,
                        inputExpression.Content.Length - lastProcessedIndex - 1);
                    var nonMatchedExpression = new Expression(nonMatchedContent, inputExpression.Type, inputExpression.Group);
                    parsedExpressions.Add(nonMatchedExpression);
                }
            }

            return parsedExpressions;
        }

        // TODO: move to relevant child class
        private List<Expression> ParsePattern(PatternStyleMap patternStyleMap, List<Expression> expressions)
        {
            var parsedExpressions = new List<Expression>();

            foreach (var inputExpression in expressions)
            {
                if (inputExpression.Type != ExpressionType.None)
                {
                    parsedExpressions.Add(inputExpression);
                }
                else
                {
                    var regex = patternStyleMap.PatternDefinition.Regex;

                    int lastProcessedIndex = -1;

                    foreach (var match in regex.Matches(inputExpression.Content).Cast<Match>().OrderBy(m => m.Index))
                    {
                        if (match.Success)
                        {
                            if (match.Index > lastProcessedIndex + 1)
                            {
                                string nonMatchedContent = inputExpression.Content.Substring(lastProcessedIndex + 1, match.Index - lastProcessedIndex - 1);
                                var nonMatchedExpression = new Expression(nonMatchedContent, ExpressionType.None, String.Empty);
                                parsedExpressions.Add(nonMatchedExpression);
                                //lastProcessedIndex = match.Index + match.Length - 1;
                            }

                            string matchedContent = inputExpression.Content.Substring(match.Index, match.Length);
                            var matchedExpression = new Expression(matchedContent, patternStyleMap.PatternDefinition.ExpressionType, patternStyleMap.Name);
                            parsedExpressions.Add(matchedExpression);
                            lastProcessedIndex = match.Index + match.Length - 1;
                        }
                    }

                    if (lastProcessedIndex < inputExpression.Content.Length - 1)
                    {
                        string nonMatchedContent = inputExpression.Content.Substring(lastProcessedIndex + 1, inputExpression.Content.Length - lastProcessedIndex - 1);
                        var nonMatchedExpression = new Expression(nonMatchedContent, ExpressionType.None, String.Empty);
                        parsedExpressions.Add(nonMatchedExpression);
                    }
                }
            }

            return parsedExpressions;
        }

        // TODO: make abstract
        internal IEnumerable<StyleGroupPair> GetStyles()
        {
            yield return new StyleGroupPair(GetDefaultStyle(), String.Empty);

            foreach (var patternStyle in _patternStyles)
            {
                var style = patternStyle.SyntaxStyle;
                yield return new StyleGroupPair(new SyntaxStyle(style.Color, style.Bold, style.Italic), patternStyle.Name);
            }
        }

        // TODO: make virtual
        internal virtual string GetGroupName(Expression expression)
        {
            return expression.Group;
        }

        private List<StyleGroupPair> GetStyleGroupPairs()
        {
            if (_styleGroupPairs == null)
            {
                _styleGroupPairs = GetStyles().ToList();

                for (int i = 0; i < _styleGroupPairs.Count; i++)
                {
                    _styleGroupPairs[i].Index = i + 1;
                }
            }

            return _styleGroupPairs;
        }

        #region RTF Stuff
        /// <summary>
        /// The base method that highlights the text-box content.
        /// </summary>
        private void HighlighTextBase()
        {
            _isDuringHighlight = true;

            try
            {
                var sb = new StringBuilder();

                sb.AppendLine(RTFHeader());
                sb.AppendLine(RTFColorTable());
                sb.Append(@"\viewkind4\uc1\pard\f0\fs").Append(_fontSizeFactor).Append(" ");

                foreach (var exp in Parse(_richTextBox.Text))
                {
                    if (exp.Type == ExpressionType.Whitespace)
                    {
                        string wsContent = exp.Content;
                        sb.Append(wsContent);
                    }
                    else if (exp.Type == ExpressionType.Newline)
                    {
                        sb.AppendLine(@"\par");
                    }
                    else
                    {
                        string content = exp.Content.Replace("\\", "\\\\").Replace("{", @"\{").Replace("}", @"\}");

                        var styleGroups = GetStyleGroupPairs();

                        string groupName = GetGroupName(exp);

                        var styleToApply = styleGroups.FirstOrDefault(s => String.Equals(s.GroupName, groupName, StringComparison.Ordinal));

                        if (styleToApply != null)
                        {
                            string opening = String.Empty, cloing = String.Empty;

                            if (styleToApply.SyntaxStyle.Bold)
                            {
                                opening += @"\b";
                                cloing += @"\b0";
                            }

                            if (styleToApply.SyntaxStyle.Italic)
                            {
                                opening += @"\i";
                                cloing += @"\i0";
                            }

                            sb.AppendFormat(@"\cf{0}{2} {1}\cf0{3} ", styleToApply.Index,
                                content, opening, cloing);
                        }
                        else
                        {
                            sb.AppendFormat(@"\cf{0} {1}\cf0 ", 1, content);
                        }
                    }
                }

                sb.Append(@"\par }");

                _richTextBox.Rtf = sb.ToString();
            }
            finally
            {
                _isDuringHighlight = false;
            }
        }

        private string RTFColorTable()
        {
            var styleGroupPairs = GetStyleGroupPairs();

            if (styleGroupPairs.Count <= 0)
                styleGroupPairs.Add(new StyleGroupPair(GetDefaultStyle(), String.Empty));

            var sbRtfColorTable = new StringBuilder();
            sbRtfColorTable.Append(@"{\colortbl ;");

            foreach (var styleGroup in styleGroupPairs)
            {
                sbRtfColorTable.AppendFormat("{0};", ColorUtils.ColorToRtfTableEntry(styleGroup.SyntaxStyle.Color));
            }

            sbRtfColorTable.Append("}");

            return sbRtfColorTable.ToString();
        }

        private string RTFHeader()
        {
            return String.Concat(@"{\rtf1\ansi\ansicpg1252\deff0\deflang1033{\fonttbl{\f0\fnil\fcharset0 ", _fontName, @";}}");
        }

        #endregion

    }


   public static class TextBoxBaseExtensions
    {
        /// <summary>
        /// In order to make flicker free changes to the TextBox's text, it will 
        /// first disable the TextBox using some Win32 API stuff, applies the changes
        /// passed through the <c>Action</c> argument, and then re-enables the TextBox.
        /// </summary>
        /// <param name="textBox"></param>
        /// <param name="action"></param>
        public static void DisableThenDoThenEnable(this TextBoxBase textBox, Action action)
        {
            IntPtr stateLocked = IntPtr.Zero;

            Lock(textBox, ref stateLocked);

            int hscroll = GetHScrollPos(textBox);
            int vscroll = GetVScrollPos(textBox);

            int selstart = textBox.SelectionStart;

            action();

            textBox.Select(selstart, 0);

            SetHScrollPos(textBox, hscroll);
            SetVScrollPos(textBox, vscroll);

            Unlock(textBox, ref stateLocked);
        }

        public static int GetHScrollPos(this TextBoxBase textBox)
        {
            return GetScrollPos((int)textBox.Handle, SB_HORZ);
        }

        public static void SetHScrollPos(this TextBoxBase textBox, int value)
        {
            SetScrollPos(textBox.Handle, SB_HORZ, value, true);
            PostMessageA(textBox.Handle, WM_HSCROLL, SB_THUMBPOSITION + 0x10000 * value, 0);
        }

        public static int GetVScrollPos(this TextBoxBase textBox)
        {
            return GetScrollPos((int)textBox.Handle, SB_VERT);
        }

        public static void SetVScrollPos(this TextBoxBase textBox, int value)
        {
            SetScrollPos(textBox.Handle, SB_VERT, value, true);
            PostMessageA(textBox.Handle, WM_VSCROLL, SB_THUMBPOSITION + 0x10000 * value, 0);
        }

        private static void Lock(this TextBoxBase textBox, ref IntPtr stateLocked)
        {
            // Stop redrawing:  
            SendMessage(textBox.Handle, WM_SETREDRAW, 0, IntPtr.Zero);
            // Stop sending of events:  
            stateLocked = SendMessage(textBox.Handle, EM_GETEVENTMASK, 0, IntPtr.Zero);
            // change colors and stuff in the RichTextBox  
        }

        private static void Unlock(this TextBoxBase textBox, ref IntPtr stateLocked)
        {
            // turn on events  
            SendMessage(textBox.Handle, EM_SETEVENTMASK, 0, stateLocked);
            // turn on redrawing  
            SendMessage(textBox.Handle, WM_SETREDRAW, 1, IntPtr.Zero);

            stateLocked = IntPtr.Zero;
            textBox.Invalidate();
        }

        #region Win API Stuff

        // Windows APIs
        [DllImport("user32", CharSet = CharSet.Auto)]
        private extern static IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool PostMessageA(IntPtr hWnd, int nBar, int wParam, int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetScrollPos(int hWnd, int nBar);

        [DllImport("user32.dll")]
        private static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

        private const int WM_SETREDRAW = 0x000B;
        private const int WM_USER = 0x400;
        private const int EM_GETEVENTMASK = (WM_USER + 59);
        private const int EM_SETEVENTMASK = (WM_USER + 69);
        private const int SB_HORZ = 0x0;
        private const int SB_VERT = 0x1;
        private const int WM_HSCROLL = 0x114;
        private const int WM_VSCROLL = 0x115;
        private const int SB_THUMBPOSITION = 4;
        private const int UNDO_BUFFER = 100;

        #endregion
    }

    public static class JsonPatternsPreset
    {
        public static void ApplyTo( SyntaxHighlighter sh )
        {
            //// multi-line comments
            //syntaxHighlighter.AddPattern(new PatternDefinition(new Regex(@"/\*(.|[\r\n])*?\*/", RegexOptions.Multiline | RegexOptions.Compiled)), new SyntaxStyle(Color.DarkSeaGreen, false, true));
            //// singlie-line comments
            //syntaxHighlighter.AddPattern(new PatternDefinition(new Regex(@"//.*?$", RegexOptions.Multiline | RegexOptions.Compiled)), new SyntaxStyle(Color.Green, false, true));
            // fields
            sh.AddPattern(new PatternDefinition(@"\""([^""]|\""\"")*\"":"), new SyntaxStyle(Color.Brown));
            // double quote strings
            sh.AddPattern(new PatternDefinition(@"\""([^""]|\""\"")*\"""), new SyntaxStyle(Color.Red));
            // single quote strings
            sh.AddPattern(new PatternDefinition(@"\'([^']|\'\')*\'"), new SyntaxStyle(Color.Salmon));
            // operators
            sh.AddPattern(new PatternDefinition("=", "[", "]", "{", "}"), new SyntaxStyle(Color.Black));
            // numbers
            sh.AddPattern(new PatternDefinition(@"\d+\.\d+|\d+"), new SyntaxStyle(Color.Black));
            // keywords1
            sh.AddPattern(new PatternDefinition("null", "false", "true"), new SyntaxStyle(Color.Blue));
            //// keywords2
            //syntaxHighlighter.AddPattern(new CaseInsensitivePatternDefinition("public", "partial", "class", "void"), new SyntaxStyle(Color.Navy, true, false));
        }
    }
}
