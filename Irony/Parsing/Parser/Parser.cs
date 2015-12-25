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
using System.Diagnostics;

namespace Irony.Parsing
{
	/// <summary>
	/// Parser class represents combination of scanner and LALR parser (CoreParser)
	/// </summary>
	public class Parser
	{
		public readonly ParserData Data;
		public readonly LanguageData Language;
		public readonly NonTerminal Root;

		//public readonly CoreParser CoreParser;

		public readonly Scanner Scanner;

		/// <summary>
		/// Either language root or initial state for parsing snippets - like Ruby's expressions in strings : "result= #{x+y}"
		/// </summary>
		internal readonly ParserState InitialState;

		private Grammar grammar;

		public Parser(Grammar grammar) : this(new LanguageData(grammar))
		{ }

		public Parser(LanguageData language) : this(language, null)
		{ }

		public Parser(LanguageData language, NonTerminal root)
		{
			this.Language = language;
			this.Data = Language.ParserData;
			this.grammar = Language.Grammar;
			this.Context = new ParsingContext(this);
			this.Scanner = new Scanner(this);
			this.Root = root;
			if (Root == null)
			{
				this.Root = this.Language.Grammar.Root;
				this.InitialState = this.Language.ParserData.InitialState;
			}
			else
			{
				if (this.Root != this.Language.Grammar.Root && !this.Language.Grammar.SnippetRoots.Contains(this.Root))
					throw new Exception(string.Format(Resources.ErrRootNotRegistered, root.Name));

				this.InitialState = this.Language.ParserData.InitialStates[this.Root];
			}
		}

		public ParsingContext Context { get; internal set; }

		public ParseTree Parse(string sourceText)
		{
			return Parse(sourceText, "Source");
		}

		public ParseTree Parse(string sourceText, string fileName)
		{
			var loc = default(SourceLocation);
			this.Reset();

			/*if (Context.Status == ParserStatus.AcceptedPartial)
			{
				var oldLoc = Context.Source.Location;
				loc = new SourceLocation(oldLoc.Position, oldLoc.Line + 1, 0);
				} else { }
			}*/

			this.Context.Source = new SourceStream(sourceText, this.Language.Grammar.CaseSensitive, this.Context.TabWidth, loc);
			this.Context.CurrentParseTree = new ParseTree(sourceText, fileName);
			this.Context.Status = ParserStatus.Parsing;

			var sw = new Stopwatch();
			sw.Start();

			this.ParseAll();

			// Set Parse status
			var parseTree = Context.CurrentParseTree;
			var hasErrors = parseTree.HasErrors();
			if (hasErrors)
				parseTree.Status = ParseTreeStatus.Error;
			else if (Context.Status == ParserStatus.AcceptedPartial)
				parseTree.Status = ParseTreeStatus.Partial;
			else
				parseTree.Status = ParseTreeStatus.Parsed;

			// Build AST if no errors and AST flag is set
			bool createAst = this.grammar.LanguageFlags.IsSet(LanguageFlags.CreateAst);
			if (createAst && !hasErrors)
				this.Language.Grammar.BuildAst(this.Language, parseTree);

			// Done; record the time
			sw.Stop();

			parseTree.ParseTimeMilliseconds = sw.ElapsedMilliseconds;
			if (parseTree.ParserMessages.Count > 0)
				parseTree.ParserMessages.Sort(LogMessageList.ByLocation);

			return parseTree;
		}

		public ParseTree ScanOnly(string sourceText, string fileName)
		{
			this.Context.CurrentParseTree = new ParseTree(sourceText, fileName);
			this.Context.Source = new SourceStream(sourceText, this.Language.Grammar.CaseSensitive, this.Context.TabWidth);

			while (true)
			{
				var token = this.Scanner.GetToken();
				if (token == null || token.Terminal == this.Language.Grammar.Eof)
					break;
			}

			return this.Context.CurrentParseTree;
		}

		internal void Reset()
		{
			this.Context.Reset();
			Scanner.Reset();
		}

		private void ParseAll()
		{
			// Main loop
			this.Context.Status = ParserStatus.Parsing;
			while (this.Context.Status == ParserStatus.Parsing)
			{
				this.ExecuteNextAction();
			}
		}

		#region Parser Action execution

		internal ParserAction GetNextAction()
		{
			var currState = this.Context.CurrentParserState;
			var currInput = this.Context.CurrentParserInput;

			if (currState.DefaultAction != null)
				return currState.DefaultAction;

			ParserAction action;

			// First try as keyterm/key symbol; for example if token text = "while", then first try it as a keyword "while";
			// if this does not work, try as an identifier that happens to match a keyword but is in fact identifier
			Token inputToken = currInput.Token;

			if (inputToken != null && inputToken.KeyTerm != null)
			{
				var keyTerm = inputToken.KeyTerm;
				if (currState.Actions.TryGetValue(keyTerm, out action))
				{
					#region comments

					// Ok, we found match as a key term (keyword or special symbol)
					// Backpatch the token's term. For example in most cases keywords would be recognized as Identifiers by Scanner.
					// Identifier would also check with SymbolTerms table and set AsSymbol field to SymbolTerminal if there exist
					// one for token content. So we first find action by Symbol if there is one; if we find action, then we
					// patch token's main terminal to AsSymbol value.  This is important for recognizing keywords (for colorizing),
					// and for operator precedence algorithm to work when grammar uses operators like "AND", "OR", etc.
					//TODO: This might be not quite correct action, and we can run into trouble with some languages that have keywords that
					// are not reserved words. But proper implementation would require substantial addition to parser code:
					// when running into errors, we need to check the stack for places where we made this "interpret as Symbol"
					// decision, roll back the stack and try to reinterpret as identifier

					#endregion comments

					inputToken.SetTerminal(keyTerm);
					currInput.Term = keyTerm;
					currInput.Precedence = keyTerm.Precedence;
					currInput.Associativity = keyTerm.Associativity;

					return action;
				}
			}

			// Try to get by main Terminal, only if it is not the same as symbol
			if (currState.Actions.TryGetValue(currInput.Term, out action))
				return action;

			// If input is EOF and NewLineBeforeEof flag is set, try using NewLine to find action
			if (currInput.Term == this.grammar.Eof &&
				this.grammar.LanguageFlags.IsSet(LanguageFlags.NewLineBeforeEOF) &&
				currState.Actions.TryGetValue(this.grammar.NewLine, out action))
			{
				// There's no action for EOF but there's action for NewLine. Let's add newLine token as input, just in case
				// action code wants to check input - it should see NewLine.
				var newLineToken = new Token(this.grammar.NewLine, currInput.Token.Location, "\r\n", null);
				var newLineNode = new ParseTreeNode(newLineToken);

				this.Context.CurrentParserInput = newLineNode;

				return action;
			}
			return null;
		}

		private void ExecuteNextAction()
		{
			// Read input only if DefaultReduceAction is null - in this case the state does not contain ExpectedSet,
			// so parser cannot assist scanner when it needs to select terminal and therefore can fail
			if (this.Context.CurrentParserInput == null && this.Context.CurrentParserState.DefaultAction == null)
				this.ReadInput();

			// Check scanner error
			if (this.Context.CurrentParserInput != null && this.Context.CurrentParserInput.IsError)
			{
				this.RecoverFromError();
				return;
			}

			// Try getting action
			var action = this.GetNextAction();
			if (action == null)
			{
				if (this.CheckPartialInputCompleted())
					return;

				this.RecoverFromError();
				return;
			}

			// We have action. Write trace and execute it
			if (this.Context.TracingEnabled)
				this.Context.AddTrace(action.ToString());

			action.Execute(this.Context);
		}

		#endregion Parser Action execution

		#region reading input

		public void ReadInput()
		{
			Token token;
			Terminal term;

			// Get token from scanner while skipping all comment tokens (but accumulating them in comment block)
			do
			{
				token = Scanner.GetToken();
				term = token.Terminal;
				if (term.Category == TokenCategory.Comment)
					this.Context.CurrentCommentTokens.Add(token);
			}
			while (term.Flags.IsSet(TermFlags.IsNonGrammar) && term != this.grammar.Eof);

			// Check brace token
			if (term.Flags.IsSet(TermFlags.IsBrace) && !CheckBraceToken(token))
				token = new Token(this.grammar.SyntaxError, token.Location, token.Text, string.Format(Resources.ErrUnmatchedCloseBrace, token.Text));

			// Create parser input node
			this.Context.CurrentParserInput = new ParseTreeNode(token);

			// Attach comments if any accumulated to content token
			if (token.Terminal.Category == TokenCategory.Content)
			{
				this.Context.CurrentParserInput.Comments = this.Context.CurrentCommentTokens;
				this.Context.CurrentCommentTokens = new TokenList();
			}

			// Fire event on Terminal
			token.Terminal.OnParserInputPreview(this.Context);
		}

		#endregion reading input

		#region Error Recovery

		public void RecoverFromError()
		{
			this.Data.ErrorAction.Execute(this.Context);
		}

		#endregion Error Recovery

		#region Utilities

		/// <summary>
		/// We assume here that the token is a brace (opening or closing)
		/// </summary>
		/// <param name="token"></param>
		/// <returns></returns>
		private bool CheckBraceToken(Token token)
		{
			if (token.Terminal.Flags.IsSet(TermFlags.IsOpenBrace))
			{
				this.Context.OpenBraces.Push(token);
				return true;
			}

			// It is closing brace; check if we have opening brace in the stack
			var braces = this.Context.OpenBraces;
			var match = (braces.Count > 0 && braces.Peek().Terminal.IsPairFor == token.Terminal);
			if (!match)
				return false;

			// Link both tokens, pop the stack and return true
			var openingBrace = braces.Pop();
			openingBrace.OtherBrace = token;
			token.OtherBrace = openingBrace;

			return true;
		}

		private bool CheckPartialInputCompleted()
		{
			bool partialCompleted = (this.Context.Mode == ParseMode.CommandLine && this.Context.CurrentParserInput.Term == this.grammar.Eof);
			if (!partialCompleted)
				return false;

			this.Context.Status = ParserStatus.AcceptedPartial;

			// clean up EOF in input so we can continue parsing next line
			this.Context.CurrentParserInput = null;

			return true;
		}

		#endregion Utilities
	}
}
