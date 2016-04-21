using System;
using System.Collections.Generic;

namespace Irony.Parsing
{
	[Flags]
	public enum OutlineOptions
	{
		None = 0,
		ProduceIndents = 0x01,
		CheckBraces = 0x02,

		/// <summary>
		/// To implement, auto line joining if line ends with operator
		/// </summary>
		CheckOperator = 0x04,
	}

	public class CodeOutlineFilter : TokenFilter
	{
		public readonly KeyTerm ContinuationTerminal;
		public readonly OutlineOptions Options;
		public Token CurrentToken;
		public Stack<int> Indents = new Stack<int>();
		public TokenStack OutputTokens = new TokenStack();
		public Token PreviousToken;
		public SourceLocation PreviousTokenLocation;

		private readonly Grammar grammar;
		private bool checkBraces, checkOperator;
		private ParsingContext context;
		private bool doubleEof;
		private GrammarData grammarData;
		private bool isContinuation, prevIsContinuation;
		private bool isOperator, prevIsOperator;
		private bool produceIndents;

		#region constructor

		public CodeOutlineFilter(GrammarData grammarData, OutlineOptions options, KeyTerm continuationTerminal)
		{
			this.grammarData = grammarData;
			this.grammar = grammarData.Grammar;
			this.grammar.LanguageFlags |= LanguageFlags.EmitLineStartToken;
			this.Options = options;
			this.ContinuationTerminal = continuationTerminal;

			if (this.ContinuationTerminal != null)
				if (!this.grammar.NonGrammarTerminals.Contains(this.ContinuationTerminal))
					this.grammarData.Language.Errors.Add(GrammarErrorLevel.Warning, null, Resources.ErrOutlineFilterContSymbol, this.ContinuationTerminal.Name);

			// "CodeOutlineFilter: line continuation symbol '{0}' should be added to Grammar.NonGrammarTerminals list.",
			this.produceIndents = this.OptionIsSet(OutlineOptions.ProduceIndents);
			this.checkBraces = this.OptionIsSet(OutlineOptions.CheckBraces);
			this.checkOperator = this.OptionIsSet(OutlineOptions.CheckOperator);

			this.Reset();
		}

		#endregion constructor

		public override IEnumerable<Token> BeginFiltering(ParsingContext context, IEnumerable<Token> tokens)
		{
			this.context = context;

			foreach (Token token in tokens)
			{
				this.ProcessToken(token);

				while (this.OutputTokens.Count > 0)
				{
					yield return this.OutputTokens.Pop();
				}
			}
		}

		public bool OptionIsSet(OutlineOptions option)
		{
			return (this.Options & option) != 0;
		}

		public void ProcessToken(Token token)
		{
			this.SetCurrentToken(token);

			// Quick checks
			if (this.isContinuation)
				return;

			var tokenTerm = token.Terminal;

			// Check EOF
			if (tokenTerm == this.grammar.Eof)
			{
				this.ProcessEofToken();
				return;
			}

			if (tokenTerm != this.grammar.LineStartTerminal)
				return;

			// If we are here, we have LineStart token on new line; first remove it from stream, it should not go to parser
			this.OutputTokens.Pop();

			if (this.PreviousToken == null)
				return;

			// First check if there was continuation symbol before
			// or - if checkBraces flag is set - check if there were open braces
			if (this.prevIsContinuation || this.checkBraces && this.context.OpenBraces.Count > 0)
				// No Eos token in this case
				return;

			if (this.prevIsOperator && this.checkOperator)
				// no Eos token in this case
				return;

			// We need to produce Eos token and indents (if _produceIndents is set).
			// First check indents - they go first into OutputTokens stack, so they will be popped out last
			if (this.produceIndents)
			{
				var currIndent = token.Location.Column;
				var prevIndent = this.Indents.Peek();

				if (currIndent > prevIndent)
				{
					this.Indents.Push(currIndent);
					this.PushOutlineToken(this.grammar.Indent, token.Location);
				}
				else if (currIndent < prevIndent)
				{
					this.PushDedents(currIndent);

					// Check that current indent exactly matches the previous indent
					if (this.Indents.Peek() != currIndent)
					{
						// Fire error
						this.OutputTokens.Push(new Token(this.grammar.SyntaxError, token.Location, string.Empty, Resources.ErrInvDedent));

						// "Invalid dedent level, no previous matching indent found."
					}
				}
			}

			// Finally produce Eos token, but not in command line mode. In command line mode the Eos was already produced
			// when we encountered Eof on previous line
			if (this.context.Mode != ParseMode.CommandLine)
			{
				var eosLocation = this.ComputeEosLocation();
				this.PushOutlineToken(this.grammar.Eos, eosLocation);
			}
		}

		public override void Reset()
		{
			base.Reset();

			this.Indents.Clear();
			this.Indents.Push(0);
			this.OutputTokens.Clear();
			this.PreviousToken = null;
			this.CurrentToken = null;
			this.PreviousTokenLocation = new SourceLocation();
		}

		private SourceLocation ComputeEosLocation()
		{
			if (this.PreviousToken == null)
				return new SourceLocation();

			// Return position at the end of previous token
			var loc = this.PreviousToken.Location;
			var len = this.PreviousToken.Length;

			return new SourceLocation(loc.Position + len, loc.Line, loc.Column + len);
		}

		/// <summary>
		/// Processes Eof token. We should take into account the special case of processing command line input.
		/// In this case we should not automatically dedent all stacked indents if we get EOF.
		/// Note that tokens will be popped from the OutputTokens stack and sent to parser in the reverse order compared to
		/// the order we pushed them into OutputTokens stack. We have Eof already in stack; we first push dedents, then Eos
		/// They will come out to parser in the following order: Eos, Dedents, Eof.
		/// </summary>
		private void ProcessEofToken()
		{
			// First decide whether we need to produce dedents and Eos symbol
			var pushDedents = false;
			var pushEos = true;

			switch (this.context.Mode)
			{
				case ParseMode.File:
					// Do dedents if token filter tracks indents
					pushDedents = this.produceIndents;
					break;

				case ParseMode.CommandLine:
					// Only if user entered empty line, we dedent all
					pushDedents = this.produceIndents && this.doubleEof;

					// If previous symbol is continuation symbol then don't push Eos
					pushEos = !this.prevIsContinuation && !this.doubleEof;
					break;

				case ParseMode.VsLineScan:
					// Do not dedent at all on every line end
					pushDedents = false;
					break;
			}

			// Unindent all buffered indents;
			if (pushDedents)
				this.PushDedents(0);

			// Now push Eos token - it will be popped first, then dedents, then EOF token
			if (pushEos)
			{
				var eosLocation = this.ComputeEosLocation();
				this.PushOutlineToken(this.grammar.Eos, eosLocation);
			}
		}

		private void PushDedents(int untilPosition)
		{
			while (this.Indents.Peek() > untilPosition)
			{
				this.Indents.Pop();
				this.PushOutlineToken(this.grammar.Dedent, this.CurrentToken.Location);
			}
		}

		private void PushOutlineToken(Terminal term, SourceLocation location)
		{
			this.OutputTokens.Push(new Token(term, location, string.Empty, null));
		}

		private void SetCurrentToken(Token token)
		{
			this.doubleEof = this.CurrentToken != null && this.CurrentToken.Terminal == this.grammar.Eof  && token.Terminal == this.grammar.Eof;

			// Copy CurrentToken to PreviousToken
			if (this.CurrentToken != null && CurrentToken.Category == TokenCategory.Content)
			{
				// Remember only content tokens
				this.PreviousToken = CurrentToken;
				this.prevIsContinuation = this.isContinuation;
				this.prevIsOperator = this.isOperator;

				if (this.PreviousToken != null)
					this.PreviousTokenLocation = this.PreviousToken.Location;
			}

			this.CurrentToken = token;
			this.isContinuation = (token.Terminal == this.ContinuationTerminal && this.ContinuationTerminal != null);
			this.isOperator = token.Terminal.Flags.IsSet(TermFlags.IsOperator);

			if (!this.isContinuation)
				// By default input token goes to output, except continuation symbol
				this.OutputTokens.Push(token);
		}
	}
}
