#region License

/* **********************************************************************************
 * Copyright (c) Roman Ivantsov
 * This source code is subject to terms and conditions of the MIT License
 * for Irony. A copy of the license can be found in the License.txt file
 * at the root of this distribution.
 * By using this source code in any fashion, you are agreeing to be bound by the terms of the
 * MIT License.
 * You must not remove this notice from this software.
 * **********************************************************************************/

#endregion License

using System;
using System.Collections.Generic;
using System.Globalization;

using Irony.Ast;

namespace Irony.Parsing
{
	public class Grammar
	{
		#region properties

		/// <summary>
		/// Gets case sensitivity of the grammar. Read-only, true by default.
		/// Can be set to false only through a parameter to grammar constructor.
		/// </summary>
		public readonly bool CaseSensitive;

		/// <summary>
		/// Terminals not present in grammar expressions and not reachable from the Root
		/// (Comment terminal is usually one of them)
		/// Tokens produced by these terminals will be ignored by parser input.
		/// </summary>
		public readonly TerminalSet NonGrammarTerminals = new TerminalSet();

		public CultureInfo DefaultCulture = CultureInfo.InvariantCulture;

		/// <summary>
		/// List of chars that unambigously identify the start of new token.
		/// used in scanner error recovery, and in quick parse path in NumberLiterals, Identifiers
		/// </summary>
		[Obsolete("Use IsWhitespaceOrDelimiter() method instead.")]
		public string Delimiters;

		/// <summary>
		/// Shown in Grammar info tab
		/// </summary>
		public string GrammarComments;

		public LanguageFlags LanguageFlags = LanguageFlags.Default;

		/// <summary>
		/// The main root entry for the grammar.
		/// </summary>
		public NonTerminal Root;

		/// <summary>
		/// Alternative roots for parsing code snippets.
		/// </summary>
		public NonTerminalSet SnippetRoots = new NonTerminalSet();

		public TermReportGroupList TermReportGroups = new TermReportGroupList();

		/// <summary>
		/// Not used anymore
		/// </summary>
		[Obsolete("Override Grammar.SkipWhitespace method instead.")]
		public string WhitespaceChars = " \t\r\n\v";

		#endregion properties

		#region Console-related properties, initialized in grammar constructor

		public string ConsoleGreeting;

		/// <summary>
		/// Default prompt
		/// </summary>
		public string ConsolePrompt;

		/// <summary>
		/// Prompt to show when more input is expected
		/// </summary>
		public string ConsolePromptMoreInput;

		public string ConsoleTitle;

		#endregion Console-related properties, initialized in grammar constructor

		#region constructors

		public Grammar() : this(true) // Case sensitive by default
		{ }

		public Grammar(bool caseSensitive)
		{
			_currentGrammar = this;

			this.CaseSensitive = caseSensitive;
			var ignoreCase = !this.CaseSensitive;
			var stringComparer = StringComparer.Create(CultureInfo.InvariantCulture, ignoreCase);
			this.KeyTerms = new KeyTermTable(stringComparer);

			// Initialize console attributes
			this.ConsoleTitle = Resources.MsgDefaultConsoleTitle;
			this.ConsoleGreeting = string.Format(Resources.MsgDefaultConsoleGreeting, this.GetType().Name);
			this.ConsolePrompt = ">";
			this.ConsolePromptMoreInput = ".";
		}

		#endregion constructors

		#region Reserved words handling

		/// <summary>
		/// Reserved words handling
		/// </summary>
		/// <param name="reservedWords"></param>
		public void MarkReservedWords(params string[] reservedWords)
		{
			foreach (var word in reservedWords)
			{
				var wdTerm = this.ToTerm(word);
				wdTerm.SetFlag(TermFlags.IsReservedWord);
			}
		}

		#endregion Reserved words handling

		#region Register/Mark methods

		/// <summary>
		/// MemberSelect are symbols invoking member list dropdowns in editor; for ex: . (dot), ::
		/// </summary>
		/// <param name="symbols"></param>
		public void MarkMemberSelect(params string[] symbols)
		{
			foreach (var symbol in symbols)
			{
				this.ToTerm(symbol).SetFlag(TermFlags.IsMemberSelect);
			}
		}

		/// <summary>
		/// Sets IsNotReported flag on terminals. As a result the terminal wouldn't appear in expected terminal list
		/// in syntax error messages
		/// </summary>
		/// <param name="terms"></param>
		public void MarkNotReported(params BnfTerm[] terms)
		{
			foreach (var term in terms)
			{
				term.SetFlag(TermFlags.IsNotReported);
			}
		}

		public void MarkNotReported(params string[] symbols)
		{
			foreach (var symbol in symbols)
			{
				this.ToTerm(symbol).SetFlag(TermFlags.IsNotReported);
			}
		}

		public void MarkPunctuation(params string[] symbols)
		{
			foreach (string symbol in symbols)
			{
				KeyTerm term = this.ToTerm(symbol);
				term.SetFlag(TermFlags.IsPunctuation | TermFlags.NoAstNode);
			}
		}

		public void MarkPunctuation(params BnfTerm[] terms)
		{
			foreach (BnfTerm term in terms)
			{
				term.SetFlag(TermFlags.IsPunctuation | TermFlags.NoAstNode);
			}
		}

		public void MarkTransient(params NonTerminal[] nonTerminals)
		{
			foreach (NonTerminal nt in nonTerminals)
			{
				nt.Flags |= TermFlags.IsTransient | TermFlags.NoAstNode;
			}
		}

		public void RegisterBracePair(string openBrace, string closeBrace)
		{
			KeyTerm openS = this.ToTerm(openBrace);
			KeyTerm closeS = this.ToTerm(closeBrace);

			openS.SetFlag(TermFlags.IsOpenBrace);
			openS.IsPairFor = closeS;

			closeS.SetFlag(TermFlags.IsCloseBrace);
			closeS.IsPairFor = openS;
		}

		public void RegisterOperators(int precedence, params string[] opSymbols)
		{
			this.RegisterOperators(precedence, Associativity.Left, opSymbols);
		}

		public void RegisterOperators(int precedence, Associativity associativity, params string[] opSymbols)
		{
			foreach (string op in opSymbols)
			{
				KeyTerm opSymbol = ToTerm(op);
				opSymbol.SetFlag(TermFlags.IsOperator);
				opSymbol.Precedence = precedence;
				opSymbol.Associativity = associativity;
			}
		}

		public void RegisterOperators(int precedence, params BnfTerm[] opTerms)
		{
			this.RegisterOperators(precedence, Associativity.Left, opTerms);
		}

		public void RegisterOperators(int precedence, Associativity associativity, params BnfTerm[] opTerms)
		{
			foreach (var term in opTerms)
			{
				term.SetFlag(TermFlags.IsOperator);
				term.Precedence = precedence;
				term.Associativity = associativity;
			}
		}

		#endregion Register/Mark methods

		#region virtual methods: CreateTokenFilters, TryMatch

		/// <summary>
		/// Constructs the error message in situation when parser has no available action for current input.
		/// override this method if you want to change this message
		/// </summary>
		/// <param name="context"></param>
		/// <param name="expectedTerms"></param>
		/// <returns></returns>
		public virtual string ConstructParserErrorMessage(ParsingContext context, StringSet expectedTerms)
		{
			if (expectedTerms.Count > 0)
				return string.Format(Resources.ErrSyntaxErrorExpected, expectedTerms.ToString(", "));
			else
				return Resources.ErrParserUnexpectedInput;
		}

		public virtual void CreateTokenFilters(LanguageData language, TokenFilterList filters)
		{
		}

		/// <summary>
		/// Gives a way to customize parse tree nodes captions in the tree view.
		/// </summary>
		/// <param name="node"></param>
		/// <returns></returns>
		public virtual string GetParseNodeCaption(ParseTreeNode node)
		{
			if (node.IsError)
				return node.Term.Name + " (Syntax error)";

			if (node.Token != null)
				return node.Token.ToString();

			// Special case for initial node pushed into the stack at parser start
			if (node.Term == null)
				// Resources.LabelInitialState;
				return (node.State != null ? string.Empty : "(State " + node.State.Name + ")");

			var ntTerm = node.Term as NonTerminal;
			if (ntTerm != null && !string.IsNullOrEmpty(ntTerm.NodeCaptionTemplate))
				return ntTerm.GetNodeCaption(node);

			return node.Term.Name;
		}

		/// <summary>Returns true if a character is whitespace or delimiter. Used in quick-scanning versions of some terminals. </summary>
		/// <param name="ch">The character to check.</param>
		/// <returns>True if a character is whitespace or delimiter; otherwise, false.</returns>
		/// <remarks>Does not have to be completely accurate, should recognize most common characters that are special chars by themselves
		/// and may never be part of other multi-character tokens. </remarks>
		public virtual bool IsWhitespaceOrDelimiter(char ch)
		{
			switch (ch)
			{
				case ' ':
				case '\t':
				case '\r':
				case '\n':
				case '\v': //whitespaces
				case '(':
				case ')':
				case ',':
				case ';':
				case '[':
				case ']':
				case '{':
				case '}':
				case (char) 0:
					// EOF
					return true;

				default:
					return false;
			}
		}

		/// <summary>
		/// The method is called after GrammarData is constructed
		/// </summary>
		/// <param name="language"></param>
		public virtual void OnGrammarDataConstructed(LanguageData language)
		{
		}

		public virtual void OnLanguageDataConstructed(LanguageData language)
		{
		}

		/// <summary>
		/// Override this method to help scanner select a terminal to create token when there are more than one candidates
		/// for an input char. context.CurrentTerminals contains candidate terminals; leave a single terminal in this list
		/// as the one to use.
		/// </summary>
		public virtual void OnScannerSelectTerminal(ParsingContext context) { }

		/// <summary>
		/// Override this method to perform custom error processing
		/// </summary>
		/// <param name="context"></param>
		public virtual void ReportParseError(ParsingContext context)
		{
			string error = null;

			if (context.CurrentParserInput.Term == this.SyntaxError)
				// Scanner error
				error = context.CurrentParserInput.Token.Value as string;
			else if (context.CurrentParserInput.Term == this.Indent)
				error = Resources.ErrUnexpIndent;
			else if (context.CurrentParserInput.Term == this.Eof && context.OpenBraces.Count > 0)
			{
				if (context.OpenBraces.Count > 0)
				{
					// Report unclosed braces/parenthesis
					var openBrace = context.OpenBraces.Peek();
					error = string.Format(Resources.ErrNoClosingBrace, openBrace.Text);
				}
				else
					error = Resources.ErrUnexpEof;
			}
			else
			{
				var expectedTerms = context.GetExpectedTermSet();
				error = ConstructParserErrorMessage(context, expectedTerms);
			}

			context.AddParserError(error);
		}

		/// <summary>Skips whitespace characters in the input stream. </summary>
		/// <remarks>Override this method if your language has non-standard whitespace characters.</remarks>
		/// <param name="source">Source stream.</param>
		public virtual void SkipWhitespace(ISourceStream source)
		{
			while (!source.EOF())
			{
				switch (source.PreviewChar)
				{
					case ' ':
					case '\t':
						break;

					case '\r':
					case '\n':
					case '\v':
						// Do not treat as whitespace if language is line-based
						if (this.UsesNewLine)
							return;

						break;

					default:
						return;
				}

				source.PreviewPosition++;
			}
		}

		/// <summary>
		/// This method is called if Scanner fails to produce a token; it offers custom method a chance to produce the token
		/// </summary>
		/// <param name="context"></param>
		/// <param name="source"></param>
		/// <returns></returns>
		public virtual Token TryMatch(ParsingContext context, ISourceStream source)
		{
			return null;
		}

		#endregion virtual methods: CreateTokenFilters, TryMatch

		#region MakePlusRule, MakeStarRule methods

		public BnfExpression MakePlusRule(NonTerminal listNonTerminal, BnfTerm listMember)
		{
			return this.MakeListRule(listNonTerminal, null, listMember);
		}

		public BnfExpression MakePlusRule(NonTerminal listNonTerminal, BnfTerm delimiter, BnfTerm listMember)
		{
			return this.MakeListRule(listNonTerminal, delimiter, listMember);
		}

		public BnfExpression MakeStarRule(NonTerminal listNonTerminal, BnfTerm listMember)
		{
			return this.MakeListRule(listNonTerminal, null, listMember, TermListOptions.StarList);
		}

		public BnfExpression MakeStarRule(NonTerminal listNonTerminal, BnfTerm delimiter, BnfTerm listMember)
		{
			return this.MakeListRule(listNonTerminal, delimiter, listMember, TermListOptions.StarList);
		}

		protected BnfExpression MakeListRule(NonTerminal list, BnfTerm delimiter, BnfTerm listMember, TermListOptions options = TermListOptions.PlusList)
		{
			// If it is a star-list (allows empty), then we first build plus-list
			var isPlusList = !options.IsSet(TermListOptions.AllowEmpty);
			var allowTrailingDelim = options.IsSet(TermListOptions.AllowTrailingDelimiter) && delimiter != null;

			// "plusList" is the list for which we will construct expression - it is either extra plus-list or original list.
			// In the former case (extra plus-list) we will use it later to construct expression for list
			var plusList = isPlusList ? list : new NonTerminal(listMember.Name + "+");
			plusList.SetFlag(TermFlags.IsList);

			// Rule => list
			plusList.Rule = plusList;
			if (delimiter != null)
				// Rule => list + delim
				plusList.Rule += delimiter;

			if (options.IsSet(TermListOptions.AddPreferShiftHint))
				// Rule => list + delim + PreferShiftHere()
				plusList.Rule += this.PreferShiftHere();

			// Rule => list + delim + PreferShiftHere() + elem
			plusList.Rule += listMember;

			// Rule => list + delim + PreferShiftHere() + elem | elem
			plusList.Rule |= listMember;
			if (isPlusList)
			{
				// If we build plus list - we're almost done; plusList == list
				// add trailing delimiter if necessary; for star list we'll add it to final expression
				if (allowTrailingDelim)
					// Rule => list + delim + PreferShiftHere() + elem | elem | list + delim
					plusList.Rule |= list + delimiter;
			}
			else
			{
				// Setup list.Rule using plus-list we just created
				list.Rule = Empty | plusList;
				if (allowTrailingDelim)
					list.Rule |= plusList + delimiter | delimiter;

				plusList.SetFlag(TermFlags.NoAstNode);

				// Indicates that real list is one level lower
				list.SetFlag(TermFlags.IsListContainer);
			}
			return list.Rule;
		}

		#endregion MakePlusRule, MakeStarRule methods

		#region Hint utilities

		protected CustomActionHint CustomActionHere(ExecuteActionMethod executeMethod, PreviewActionMethod previewMethod = null)
		{
			return new CustomActionHint(executeMethod, previewMethod);
		}

		protected GrammarHint ImplyPrecedenceHere(int precedence)
		{
			return ImplyPrecedenceHere(precedence, Associativity.Left);
		}

		protected GrammarHint ImplyPrecedenceHere(int precedence, Associativity associativity)
		{
			return new ImpliedPrecedenceHint(precedence, associativity);
		}

		protected GrammarHint PreferShiftHere()
		{
			return new PreferredActionHint(PreferredActionType.Shift);
		}

		protected GrammarHint ReduceHere()
		{
			return new PreferredActionHint(PreferredActionType.Reduce);
		}

		protected TokenPreviewHint ReduceIf(string thisSymbol, params string[] comesBefore)
		{
			return new TokenPreviewHint(PreferredActionType.Reduce, thisSymbol, comesBefore);
		}

		protected TokenPreviewHint ReduceIf(Terminal thisSymbol, params Terminal[] comesBefore)
		{
			return new TokenPreviewHint(PreferredActionType.Reduce, thisSymbol, comesBefore);
		}

		protected TokenPreviewHint ShiftIf(string thisSymbol, params string[] comesBefore)
		{
			return new TokenPreviewHint(PreferredActionType.Shift, thisSymbol, comesBefore);
		}

		protected TokenPreviewHint ShiftIf(Terminal thisSymbol, params Terminal[] comesBefore)
		{
			return new TokenPreviewHint(PreferredActionType.Shift, thisSymbol, comesBefore);
		}

		#endregion Hint utilities

		#region Term report group methods

		/// <summary>
		/// Adds a group and an alias for all operator symbols used in the grammar.
		/// </summary>
		/// <param name="alias">An alias for operator symbols.</param>
		protected void AddOperatorReportGroup(string alias)
		{
			this.TermReportGroups.Add(new TermReportGroup(alias, TermReportGroupType.Operator, null)); //operators will be filled later
		}

		/// <summary>
		/// Creates a terminal reporting group, so all terminals in the group will be reported as a single "alias" in syntex error messages like
		/// "Syntax error, expected: [list of terms]"
		/// </summary>
		/// <param name="alias">An alias for all terminals in the group.</param>
		/// <param name="symbols">Symbols to be included into the group.</param>
		protected void AddTermsReportGroup(string alias, params string[] symbols)
		{
			this.TermReportGroups.Add(new TermReportGroup(alias, TermReportGroupType.Normal, SymbolsToTerms(symbols)));
		}

		/// <summary>
		/// Creates a terminal reporting group, so all terminals in the group will be reported as a single "alias" in syntex error messages like
		/// "Syntax error, expected: [list of terms]"
		/// </summary>
		/// <param name="alias">An alias for all terminals in the group.</param>
		/// <param name="terminals">Terminals to be included into the group.</param>
		protected void AddTermsReportGroup(string alias, params Terminal[] terminals)
		{
			this.TermReportGroups.Add(new TermReportGroup(alias, TermReportGroupType.Normal, terminals));
		}

		/// <summary>
		/// Adds symbols to a group with no-report type, so symbols will not be shown in expected lists in syntax error messages.
		/// </summary>
		/// <param name="symbols">Symbols to exclude.</param>
		protected void AddToNoReportGroup(params string[] symbols)
		{
			this.TermReportGroups.Add(new TermReportGroup(string.Empty, TermReportGroupType.DoNotReport, SymbolsToTerms(symbols)));
		}

		/// <summary>
		/// Adds symbols to a group with no-report type, so symbols will not be shown in expected lists in syntax error messages.
		/// </summary>
		/// <param name="terminals"></param>
		protected void AddToNoReportGroup(params Terminal[] terminals)
		{
			this.TermReportGroups.Add(new TermReportGroup(string.Empty, TermReportGroupType.DoNotReport, terminals));
		}

		private IEnumerable<Terminal> SymbolsToTerms(IEnumerable<string> symbols)
		{
			var termList = new TerminalList();
			foreach (var symbol in symbols)
			{
				termList.Add(this.ToTerm(symbol));
			}

			return termList;
		}

		#endregion Term report group methods

		#region Standard terminals: EOF, Empty, NewLine, Indent, Dedent

		public readonly Terminal Dedent = new Terminal("DEDENT", TokenCategory.Outline, TermFlags.IsNonScanner);

		/// <summary>
		/// Empty object is used to identify optional element:
		/// term.Rule = term1 | Empty;
		/// </summary>
		public readonly Terminal Empty = new Terminal("EMPTY");

		/// <summary>
		/// Identifies end of file
		/// <para />
		/// Note: using Eof in grammar rules is optional. Parser automatically adds this symbol
		/// as a lookahead to Root non-terminal
		/// </summary>
		public readonly Terminal Eof = new Terminal("EOF", TokenCategory.Outline);

		/// <summary>
		/// End-of-Statement terminal - used in indentation-sensitive language to signal end-of-statement;
		/// it is not always synced with CRLF chars, and CodeOutlineFilter carefully produces Eos tokens
		/// (as well as Indent and Dedent) based on line/col information in incoming content tokens.
		/// </summary>
		public readonly Terminal Eos = new Terminal("EOS", Resources.LabelEosLabel, TokenCategory.Outline, TermFlags.IsNonScanner);

		/// <summary>
		/// The following terminals are used in indent-sensitive languages like Python;
		/// they are not produced by scanner but are produced by CodeOutlineFilter after scanning
		/// </summary>
		public readonly Terminal Indent = new Terminal("INDENT", TokenCategory.Outline, TermFlags.IsNonScanner);

		/// <summary>
		/// Used as a "line-start" indicator
		/// </summary>
		public readonly Terminal LineStartTerminal = new Terminal("LINE_START", TokenCategory.Outline);

		public readonly NewLineTerminal NewLine = new NewLineTerminal("LF");

		/// <summary>
		/// Artificial terminal to use for injected/replaced tokens that must be ignored by parser.
		/// </summary>
		public readonly Terminal Skip = new Terminal("(SKIP)", TokenCategory.Outline, TermFlags.IsNonGrammar);

		/// <summary>
		/// Used for error tokens
		/// </summary>
		public readonly Terminal SyntaxError = new Terminal("SYNTAX_ERROR", TokenCategory.Error, TermFlags.IsNonScanner);

		/// <summary>
		/// Set to true automatically by NewLine terminal; prevents treating new-line characters as whitespaces
		/// </summary>
		public bool UsesNewLine;

		private NonTerminal newLinePlus;

		private NonTerminal newLineStar;

		public NonTerminal NewLinePlus
		{
			get
			{
				if (this.newLinePlus == null)
				{
					this.newLinePlus = new NonTerminal("LF+");

					// We do no use MakePlusRule method;
					// we specify the rule explicitly to add PrefereShiftHere call - this solves some unintended shift-reduce conflicts
					// when using NewLinePlus
					this.newLinePlus.Rule = this.NewLine | this.newLinePlus + this.PreferShiftHere() + this.NewLine;
					this.MarkPunctuation(this.newLinePlus);
					this.newLinePlus.SetFlag(TermFlags.IsList);
				}

				return this.newLinePlus;
			}
		}

		public NonTerminal NewLineStar
		{
			get
			{
				if (this.newLineStar == null)
				{
					this.newLineStar = new NonTerminal("LF*");
					this.MarkPunctuation(this.newLineStar);
					this.newLineStar.Rule = MakeStarRule(this.newLineStar, this.NewLine);
				}

				return this.newLineStar;
			}
		}

		#endregion Standard terminals: EOF, Empty, NewLine, Indent, Dedent

		#region KeyTerms (keywords + special symbols)

		public KeyTermTable KeyTerms;

		public KeyTerm ToTerm(string text)
		{
			return this.ToTerm(text, text);
		}

		public KeyTerm ToTerm(string text, string name)
		{
			KeyTerm term;
			if (KeyTerms.TryGetValue(text, out term))
			{
				// Update name if it was specified now and not before
				if (string.IsNullOrEmpty(term.Name) && !string.IsNullOrEmpty(name))
					term.Name = name;

				return term;
			}

			// Create new term
			if (!CaseSensitive)
				text = text.ToLower(CultureInfo.InvariantCulture);

			string.Intern(text);
			term = new KeyTerm(text, name);
			this.KeyTerms[text] = term;

			return term;
		}

		#endregion KeyTerms (keywords + special symbols)

		#region CurrentGrammar static field

		/// <summary>
		/// Static per-thread instance; Grammar constructor sets it to self (this).
		/// This field/property is used by operator overloads (which are static) to access Grammar's predefined terminals like Empty,
		/// and SymbolTerms dictionary to convert string literals to symbol terminals and add them to the SymbolTerms dictionary
		/// </summary>
		[ThreadStatic]
		private static Grammar _currentGrammar;

		public static Grammar CurrentGrammar
		{
			get { return _currentGrammar; }
		}

		internal static void ClearCurrentGrammar()
		{
			_currentGrammar = null;
		}

		#endregion CurrentGrammar static field

		#region AST construction

		public virtual void BuildAst(LanguageData language, ParseTree parseTree)
		{
			if (!this.LanguageFlags.IsSet(LanguageFlags.CreateAst))
				return;

			var astContext = new AstContext(language);
			var astBuilder = new AstBuilder(astContext);
			astBuilder.BuildAst(parseTree);
		}

		#endregion AST construction
	}
}
