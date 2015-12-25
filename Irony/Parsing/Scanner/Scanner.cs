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

using System.Collections.Generic;

namespace Irony.Parsing
{
	/// <summary>
	/// The <see cref="Scanner"/>'s function is to transform a stream of characters into aggregates/words or lexemes,
	/// like identifier, number, literal, etc.
	/// </summary>
	public class Scanner
	{
		#region Properties and Fields: Data, _source

		public readonly ScannerData Data;
		public readonly Parser Parser;
		private Grammar grammar;

		private ParsingContext Context
		{
			get { return Parser.Context; }
		}

		#endregion Properties and Fields: Data, _source

		public Scanner(Parser parser)
		{
			this.Parser = parser;
			this.Data = parser.Language.ScannerData;
			this.grammar = parser.Language.Grammar;

			// Create token streams
			var tokenStream = this.GetUnfilteredTokens();

			// Chain all token filters
			this.Context.TokenFilters.Clear();

			this.grammar.CreateTokenFilters(this.Data.Language, this.Context.TokenFilters);

			foreach (TokenFilter filter in this.Context.TokenFilters)
			{
				tokenStream = filter.BeginFiltering(this.Context, tokenStream);
			}

			this.Context.FilteredTokens = tokenStream.GetEnumerator();
		}

		public Token GetToken()
		{
			// Get new token from pipeline
			if (!this.Context.FilteredTokens.MoveNext())
				return null;

			var token = this.Context.FilteredTokens.Current;
			if (this.Context.Status == ParserStatus.Previewing)
				this.Context.PreviewTokens.Push(token);
			else
				this.Context.CurrentParseTree.Tokens.Add(token);
			return token;
		}

		internal void Reset()
		{
		}

		/// <summary>
		/// This is iterator method, so it returns immediately when called directly
		/// returns unfiltered, "raw" token stream
		/// </summary>
		/// <returns></returns>
		private IEnumerable<Token> GetUnfilteredTokens()
		{
			// We don't do "while(!_source.EOF())... because on EOF() we need to continue and produce EOF token
			while (true)
			{
				this.Context.PreviousToken = this.Context.CurrentToken;
				this.Context.CurrentToken = null;

				this.NextToken();
				this.Context.OnTokenCreated();

				yield return Context.CurrentToken;
				// Don't yield break, continue returning EOF
			}
		}

		#region Scanning tokens

		private void ComputeCurrentTerminals()
		{
			this.Context.CurrentTerminals.Clear();

			TerminalList termsForCurrentChar;

			if (!this.Data.TerminalsLookup.TryGetValue(this.Context.Source.PreviewChar, out termsForCurrentChar))
				termsForCurrentChar = this.Data.NoPrefixTerminals;

			// If we are recovering, previewing or there's no parser state, then return list as is
			if (this.Context.Status == ParserStatus.Recovering ||
				this.Context.Status == ParserStatus.Previewing ||
				this.Context.CurrentParserState == null ||
				this.grammar.LanguageFlags.IsSet(LanguageFlags.DisableScannerParserLink) ||
				this.Context.Mode == ParseMode.VsLineScan)
			{
				this.Context.CurrentTerminals.AddRange(termsForCurrentChar);
				return;
			}

			// Try filtering terms by checking with parser which terms it expects;
			var parserState = this.Context.CurrentParserState;
			foreach (var term in termsForCurrentChar)
			{
				// Note that we check the OutputTerminal with parser, not the term itself;
				// in most cases it is the same as term, but not always
				if (parserState.ExpectedTerminals.Contains(term.OutputTerminal) || this.grammar.NonGrammarTerminals.Contains(term))
					this.Context.CurrentTerminals.Add(term);
			}
		}

		/// <summary>
		/// This method is a last attempt by scanner to match ANY terminal, after regular matching (by input char) had failed.
		/// Likely this will produce some token which is invalid for current parser state (for ex, identifier where a number
		/// is expected); in this case the parser will report an error as "Error: expected number".
		/// if this matching fails, the scanner will produce an error as "unexpected character."
		/// </summary>
		/// <returns></returns>
		private bool MatchAllTerminals()
		{
			this.Context.CurrentTerminals.Clear();
			this.Context.CurrentTerminals.AddRange(this.Data.Language.GrammarData.Terminals);
			this.MatchTerminals();

			if (Context.CurrentToken is MultiToken)
				this.UnpackMultiToken();

			return this.Context.CurrentToken != null;
		}

		private bool MatchNonGrammarTerminals()
		{
			TerminalList terms;

			if (!this.Data.NonGrammarTerminalsLookup.TryGetValue(this.Context.Source.PreviewChar, out terms))
				return false;

			foreach (var term in terms)
			{
				this.Context.Source.PreviewPosition = this.Context.Source.Location.Position;
				this.Context.CurrentToken = term.TryMatch(this.Context, this.Context.Source);

				if (this.Context.CurrentToken != null)
					term.OnValidateToken(this.Context);

				if (this.Context.CurrentToken != null)
				{
					// Check if we need to fire LineStart token before this token;
					// We do it only if the token is not a comment; comments should be ignored by the outline logic
					var token = this.Context.CurrentToken;
					if (token.Category == TokenCategory.Content && this.NeedLineStartToken(token.Location))
					{
						// Buffer current token; we'll eject LineStart instead
						this.Context.BufferedTokens.Push(token);

						// Set it back to the start of the token
						this.Context.Source.Location = token.Location;

						// Generate LineStart
						this.Context.CurrentToken = this.Context.Source.CreateToken(this.grammar.LineStartTerminal);

						// Update LineStart
						this.Context.PreviousLineStart = this.Context.Source.Location;
					}
					return true;
				}
			}

			this.Context.Source.PreviewPosition = this.Context.Source.Location.Position;
			return false;
		}

		private bool MatchRegularTerminals()
		{
			// We need to eject LineStart BEFORE we try to produce a real token; this LineStart token should reach
			// the parser, make it change the state and with it to change the set of expected tokens. So when we
			// finally move to scan the real token, the expected terminal set is correct.
			if (this.NeedLineStartToken(this.Context.Source.Location))
			{
				this.Context.CurrentToken = this.Context.Source.CreateToken(this.grammar.LineStartTerminal);
				this.Context.PreviousLineStart = this.Context.Source.Location;

				return true;
			}

			// Find matching terminal
			// First, try terminals with explicit "first-char" prefixes, selected by current char in source
			this.ComputeCurrentTerminals();

			// If we have more than one candidate; let grammar method select
			if (this.Context.CurrentTerminals.Count > 1)
				this.grammar.OnScannerSelectTerminal(this.Context);

			this.MatchTerminals();

			// If we don't have a token from terminals, try Grammar's method
			if (this.Context.CurrentToken == null)
				this.Context.CurrentToken = this.grammar.TryMatch(this.Context, this.Context.Source);

			if (this.Context.CurrentToken is MultiToken)
				this.UnpackMultiToken();

			return this.Context.CurrentToken != null;
		}

		private void MatchTerminals()
		{
			Token priorToken = null;

			for (int i = 0; i < this.Context.CurrentTerminals.Count; i++)
			{
				var term = this.Context.CurrentTerminals[i];

				// If we have priorToken from prior term in the list, check if prior term has higher priority than this term;
				// if term.Priority is lower then we don't need to check anymore, higher priority (in prior token) wins
				// Note that terminals in the list are sorted in descending priority order
				if (priorToken != null && priorToken.Terminal.Priority > term.Priority)
					return;

				// Reset source position and try to match
				this.Context.Source.PreviewPosition = this.Context.Source.Location.Position;

				var token = term.TryMatch(this.Context, this.Context.Source);
				if (token == null)
					continue;

				// Skip it if it is shorter than previous token
				if (priorToken != null && !priorToken.IsError() && (token.Length < priorToken.Length))
					continue;

				// Now it becomes current token
				this.Context.CurrentToken = token;

				// Validate it
				term.OnValidateToken(this.Context);

				if (this.Context.CurrentToken != null)
					priorToken = this.Context.CurrentToken;
			}
		}

		private bool NeedLineStartToken(SourceLocation forLocation)
		{
			return this.grammar.LanguageFlags.IsSet(LanguageFlags.EmitLineStartToken) && forLocation.Line > this.Context.PreviousLineStart.Line;
		}

		private void NextToken()
		{
			// 1. Check if there are buffered tokens
			if (this.Context.BufferedTokens.Count > 0)
			{
				this.Context.CurrentToken = this.Context.BufferedTokens.Pop();
				return;
			}

			// 2. Skip whitespace.
			this.grammar.SkipWhitespace(this.Context.Source);

			// 3. That's the token start, calc location (line and column)
			this.Context.Source.Position = this.Context.Source.PreviewPosition;

			// 4. Check for EOF
			if (this.Context.Source.EOF())
			{
				this.Context.CurrentToken = new Token(this.grammar.Eof, this.Context.Source.Location, string.Empty, this.grammar.Eof.Name); ;
				return;
			}

			// 5. Actually scan the source text and construct a new token
			this.ScanToken();
		}

		/// <summary>
		/// Scans the source text and constructs a new token
		/// </summary>
		private void ScanToken()
		{
			if (!this.MatchNonGrammarTerminals() && !this.MatchRegularTerminals())
			{
				// We are in error already; try to match ANY terminal and let the parser report an error
				this.MatchAllTerminals(); //try to match any terminal out there
			}

			var token = this.Context.CurrentToken;

			// If we have normal token then return it
			if (token != null && !token.IsError())
			{
				var src = this.Context.Source;

				// Set position to point after the result token
				src.PreviewPosition = src.Position + token.Length;
				src.Position = src.PreviewPosition;

				return;
			}

			// We have an error: either error token or no token at all
			if (token == null)   //if no token then create error token
				this.Context.CurrentToken = this.Context.CreateErrorToken(Resources.ErrInvalidChar, this.Context.Source.PreviewChar);

			this.Recover();
		}

		/// <summary>
		/// If token is MultiToken then push all its child tokens into _bufferdTokens and return the first token in buffer
		/// </summary>
		private void UnpackMultiToken()
		{
			var token = Context.CurrentToken as MultiToken;

			if (token == null)
				return;

			for (int i = token.ChildTokens.Count - 1; i >= 0; i--)
			{
				this.Context.BufferedTokens.Push(token.ChildTokens[i]);
			}

			this.Context.CurrentToken = this.Context.BufferedTokens.Pop();
		}

		#endregion Scanning tokens

		#region VS Integration methods

		/// <summary>
		/// Use this method for VS integration; VS language package requires scanner that returns tokens one-by-one.
		/// Start and End positions required by this scanner may be derived from Token :
		/// start=token.Location.Position; end=start + token.Length;
		/// </summary>
		/// <param name="state"></param>
		/// <returns></returns>
		public Token VsReadToken(ref int state)
		{
			this.Context.VsLineScanState.Value = state;

			if (this.Context.Source.EOF())
				return null;

			if (state == 0)
				this.NextToken();
			else
			{
				Terminal term = this.Data.MultilineTerminals[this.Context.VsLineScanState.TerminalIndex - 1];
				this.Context.CurrentToken = term.TryMatch(this.Context, this.Context.Source);
			}

			// Set state value from context
			state = this.Context.VsLineScanState.Value;

			if (this.Context.CurrentToken != null && this.Context.CurrentToken.Terminal == this.grammar.Eof)
				return null;

			return this.Context.CurrentToken;
		}

		public void VsSetSource(string text, int offset)
		{
			var line = this.Context.Source == null ? 0 : this.Context.Source.Location.Line;
			var newLoc = new SourceLocation(offset, line + 1, 0);
			this.Context.Source = new SourceStream(text, this.Context.Language.Grammar.CaseSensitive, this.Context.TabWidth, newLoc);
		}

		#endregion VS Integration methods

		#region Error recovery

		/// <summary>
		/// Simply skip until whitespace or delimiter character
		/// </summary>
		/// <returns></returns>
		private bool Recover()
		{
			var src = this.Context.Source;
			src.PreviewPosition++;

			while (!this.Context.Source.EOF())
			{
				if (this.grammar.IsWhitespaceOrDelimiter(src.PreviewChar))
				{
					src.Position = src.PreviewPosition;
					return true;
				}

				src.PreviewPosition++;
			}

			return false;
		}

		#endregion Error recovery

		#region TokenPreview

		/// <summary>
		/// Preview mode allows custom code in grammar to help parser decide on appropriate action in case of conflict
		/// Preview process is simply searching for particular tokens in "preview set", and finding out which of the
		/// tokens will come first.
		/// In preview mode, tokens returned by FetchToken are collected in _previewTokens list; after finishing preview
		///  the scanner "rolls back" to original position - either by directly restoring the position, or moving the preview
		///  tokens into _bufferedTokens list, so that they will read again by parser in normal mode.
		/// See c# grammar sample for an example of using preview methods
		/// </summary>
		private SourceLocation previewStartLocation;

		/// <summary>
		/// Switches Scanner into preview mode
		/// </summary>
		public void BeginPreview()
		{
			this.Context.Status = ParserStatus.Previewing;
			this.previewStartLocation = this.Context.Source.Location;
			this.Context.PreviewTokens.Clear();
		}

		/// <summary>
		/// Ends preview mode
		/// </summary>
		/// <param name="keepPreviewTokens"></param>
		public void EndPreview(bool keepPreviewTokens)
		{
			if (keepPreviewTokens)
			{
				// Insert previewed tokens into buffered list, so we don't recreate them again
				while (this.Context.PreviewTokens.Count > 0)
				{
					this.Context.BufferedTokens.Push(this.Context.PreviewTokens.Pop());
				}
			}
			else
				this.Context.SetSourceLocation(this.previewStartLocation);

			this.Context.PreviewTokens.Clear();
			this.Context.Status = ParserStatus.Parsing;
		}

		#endregion TokenPreview
	}
}
