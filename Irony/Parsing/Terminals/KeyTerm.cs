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

namespace Irony.Parsing
{
	/// <summary>
	/// Keyterm is a keyword or a special symbol used in grammar rules, for example: begin, end, while, =, *, etc.
	/// So "key" comes from the Keyword.
	/// </summary>
	public class KeyTerm : Terminal
	{
		/// <summary>
		/// Normally false, meaning keywords (symbols in grammar consisting of letters) cannot be followed by a letter or digit
		/// </summary>
		public bool AllowAlphaAfterKeyword = false;

		public KeyTerm(string text, string name) : base(name)
		{
			this.Text = text;
			this.ErrorAlias = name;
			this.Flags |= TermFlags.NoAstNode;
		}

		public string Text { get; private set; }

		#region overrides: TryMatch, Init, GetPrefixes(), ToString()

		public override IList<string> GetFirsts()
		{
			return new string[] { this.Text };
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);

			#region comments about keyterms priority

			// Priority - determines the order in which multiple terminals try to match input for a given current char in the input.
			// For a given input char the scanner looks up the collection of terminals that may match this input symbol. It is the order
			// in this collection that is determined by Priority value - the higher the priority, the earlier the terminal gets a chance
			// to check the input.
			// Keywords found in grammar by default have lowest priority to allow other terminals (like identifiers)to check the input first.
			// Additionally, longer symbols have higher priority, so symbols like "+=" should have higher priority value than "+" symbol.
			// As a result, Scanner would first try to match "+=", longer symbol, and if it fails, it will try "+".
			// Reserved words are the opposite - they have the highest priority

			#endregion comments about keyterms priority

			if (this.Flags.IsSet(TermFlags.IsReservedWord))
				// The longer the word, the higher is the priority
				this.Priority = TerminalPriority.ReservedWords + this.Text.Length;
			else
				this.Priority = TerminalPriority.Low + this.Text.Length;

			// Setup editor info
			if (this.EditorInfo != null)
				return;

			var tknType = TokenType.Identifier;
			if (this.Flags.IsSet(TermFlags.IsOperator))
				tknType |= TokenType.Operator;
			else if (this.Flags.IsSet(TermFlags.IsDelimiter | TermFlags.IsPunctuation))
				tknType |= TokenType.Delimiter;

			var triggers = TokenTriggers.None;
			if (this.Flags.IsSet(TermFlags.IsBrace))
				triggers |= TokenTriggers.MatchBraces;

			if (this.Flags.IsSet(TermFlags.IsMemberSelect))
				triggers |= TokenTriggers.MemberSelect;

			var color = TokenColor.Text;
			if (this.Flags.IsSet(TermFlags.IsKeyword))
				color = TokenColor.Keyword;

			this.EditorInfo = new TokenEditorInfo(tknType, color, triggers);
		}

		public override string TokenToString(Token token)
		{
			// "(Keyword)" : "(Key symbol)"
			var keyw = this.Flags.IsSet(TermFlags.IsKeyword) ? Resources.LabelKeyword : Resources.LabelKeySymbol;
			var result = (token.ValueString ?? token.Text) + " " + keyw;
			return result;
		}

		public override string ToString()
		{
			if (!this.Name.Equals(this.Text))
				return this.Name;

			return this.Text;
		}

		public override Token TryMatch(ParsingContext context, ISourceStream source)
		{
			if (!source.MatchSymbol(this.Text))
				return null;

			source.PreviewPosition += this.Text.Length;

			// In case of keywords, check that it is not followed by letter or digit
			if (this.Flags.IsSet(TermFlags.IsKeyword) && !this.AllowAlphaAfterKeyword)
			{
				var previewChar = source.PreviewChar;
				if (char.IsLetterOrDigit(previewChar) || previewChar == '_')
					// Reject
					return null;
			}

			var token = source.CreateToken(this.OutputTerminal, this.Text);
			return token;
		}

		#endregion overrides: TryMatch, Init, GetPrefixes(), ToString()

		[System.Diagnostics.DebuggerStepThrough]
		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}

		[System.Diagnostics.DebuggerStepThrough]
		public override int GetHashCode()
		{
			return this.Text.GetHashCode();
		}
	}

	public class KeyTermList : List<KeyTerm> { }

	public class KeyTermTable : Dictionary<string, KeyTerm>
	{
		public KeyTermTable(StringComparer comparer) : base(100, comparer)
		{ }
	}
}
