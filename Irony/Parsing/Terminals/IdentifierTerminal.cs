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

namespace Irony.Parsing
{
	public enum CaseRestriction
	{
		None,
		FirstUpper,
		FirstLower,
		AllUpper,
		AllLower
	}

	[Flags]
	public enum IdOptions : short
	{
		None = 0,
		AllowsEscapes = 0x01,
		CanStartWithEscape = 0x03,

		IsNotKeyword = 0x10,
		NameIncludesPrefix = 0x20,
	}

	/// <summary>
	/// Identifier terminal. Matches alpha-numeric sequences that usually represent identifiers and keywords.
	/// c#: @ prefix signals to not interpret as a keyword; allows \u escapes
	/// </summary>
	public class IdentifierTerminal : CompoundTerminalBase
	{
		/// <summary>
		/// Id flags for internal use
		/// </summary>
		internal enum IdFlagsInternal : short
		{
			HasEscapes = 0x100,
		}

		#region constructors and initialization

		public IdentifierTerminal(string name) : this(name, IdOptions.None)
		{
		}

		public IdentifierTerminal(string name, IdOptions options) : this(name, "_", "_")
		{
			this.Options = options;
		}

		public IdentifierTerminal(string name, string extraChars, string extraFirstChars = "") : base(name)
		{
			this.AllFirstChars = Strings.AllLatinLetters + extraFirstChars;
			this.AllChars = Strings.AllLatinLetters + Strings.DecimalDigits + extraChars;
		}

		public void AddPrefix(string prefix, IdOptions options)
		{
			this.AddPrefixFlag(prefix, (short) options);
		}

		#endregion constructors and initialization

		#region properties: AllChars, AllFirstChars

		/// <summary>
		/// Categories of all other chars
		/// </summary>
		public readonly UnicodeCategoryList CharCategories = new UnicodeCategoryList();

		/// <summary>
		/// Categories of chars to remove from final id, usually formatting category
		/// </summary>
		public readonly UnicodeCategoryList CharsToRemoveCategories = new UnicodeCategoryList();

		/// <summary>
		/// Categories of first char
		/// </summary>
		public readonly UnicodeCategoryList StartCharCategories = new UnicodeCategoryList();

		public string AllChars;
		public string AllFirstChars;
		public CaseRestriction CaseRestriction;
		public TokenEditorInfo KeywordEditorInfo = new TokenEditorInfo(TokenType.Keyword, TokenColor.Keyword, TokenTriggers.None);

		/// <summary>
		/// Flags for the case when there are no prefixes
		/// </summary>
		public IdOptions Options;

		private CharHashSet allCharsSet;
		private CharHashSet allFirstCharsSet;

		#endregion properties: AllChars, AllFirstChars

		#region overrides

		public override IList<string> GetFirsts()
		{
			// New scanner: identifier has no prefixes
			return null;
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);

			this.allCharsSet = new CharHashSet(this.Grammar.CaseSensitive);
			this.allCharsSet.UnionWith(this.AllChars.ToCharArray());

			// Adjust case restriction. We adjust only first chars; if first char is ok, we will scan the rest without restriction
			// and then check casing for entire identifier
			switch (this.CaseRestriction)
			{
				case CaseRestriction.AllLower:
				case CaseRestriction.FirstLower:
					this.allFirstCharsSet = new CharHashSet(true);
					this.allFirstCharsSet.UnionWith(this.AllFirstChars.ToLowerInvariant().ToCharArray());
					break;

				case CaseRestriction.AllUpper:
				case CaseRestriction.FirstUpper:
					this.allFirstCharsSet = new CharHashSet(true);
					this.allFirstCharsSet.UnionWith(this.AllFirstChars.ToUpperInvariant().ToCharArray());
					break;

				default: // None
					this.allFirstCharsSet = new CharHashSet(Grammar.CaseSensitive);
					this.allFirstCharsSet.UnionWith(this.AllFirstChars.ToCharArray());
					break;
			}

			// If there are "first" chars defined by categories, add the terminal to FallbackTerminals
			if (this.StartCharCategories.Count > 0)
				grammarData.NoPrefixTerminals.Add(this);

			if (this.EditorInfo == null)
				this.EditorInfo = new TokenEditorInfo(TokenType.Identifier, TokenColor.Identifier, TokenTriggers.None);
		}

		protected override bool ConvertValue(CompoundTokenDetails details)
		{
			if (details.IsSet((short) IdOptions.NameIncludesPrefix))
				details.Value = details.Prefix + details.Body;
			else
				details.Value = details.Body;

			return true;
		}

		/// <summary>
		/// Override to assign IsKeyword flag to keyword tokens
		/// </summary>
		/// <param name="context"></param>
		/// <param name="source"></param>
		/// <param name="details"></param>
		/// <returns></returns>
		protected override Token CreateToken(ParsingContext context, ISourceStream source, CompoundTokenDetails details)
		{
			var token = base.CreateToken(context, source, details);
			if (details.IsSet((short) IdOptions.IsNotKeyword))
				return token;

			// Check if it is keyword
			this.CheckReservedWord(token);
			return token;
		}

		protected override void InitDetails(ParsingContext context, CompoundTokenDetails details)
		{
			base.InitDetails(context, details);
			details.Flags = (short) Options;
		}

		protected override Token QuickParse(ParsingContext context, ISourceStream source)
		{
			if (!this.allFirstCharsSet.Contains(source.PreviewChar))
				return null;

			source.PreviewPosition++;

			while (this.allCharsSet.Contains(source.PreviewChar) && !source.EOF())
				source.PreviewPosition++;

			// If it is not a terminator then cancel; we need to go through full algorithm
			if (!this.Grammar.IsWhitespaceOrDelimiter(source.PreviewChar))
				return null;

			var token = source.CreateToken(this.OutputTerminal);
			if (this.CaseRestriction != CaseRestriction.None && !CheckCaseRestriction(token.ValueString))
				return null;

			//!!! Do not convert to common case (all-lower) for case-insensitive grammar. Let identifiers remain as is,
			//  it is responsibility of interpreter to provide case-insensitive read/write operations for identifiers
			// if (!this.GrammarData.Grammar.CaseSensitive)
			//    token.Value = token.Text.ToLower(CultureInfo.InvariantCulture);
			this.CheckReservedWord(token);

			return token;
		}

		protected override bool ReadBody(ISourceStream source, CompoundTokenDetails details)
		{
			var start = source.PreviewPosition;
			var allowEscapes = details.IsSet((short) IdOptions.AllowsEscapes);
			var outputChars = new CharList();

			while (!source.EOF())
			{
				var current = source.PreviewChar;
				if (this.Grammar.IsWhitespaceOrDelimiter(current))
					break;

				if (allowEscapes && current == this.EscapeChar)
				{
					current = this.ReadUnicodeEscape(source, details);

					// We  need to back off the position. ReadUnicodeEscape sets the position to symbol right after escape digits.
					// This is the char that we should process in next iteration, so we must backup one char, to pretend the escaped
					// char is at position of last digit of escape sequence.
					source.PreviewPosition--;

					if (details.Error != null)
						return false;
				}

				// Check if current character is OK
				if (!this.CharOk(current, source.PreviewPosition == start))
					break;

				// Check if we need to skip this char
				var currCat = char.GetUnicodeCategory(current); //I know, it suxx, we do it twice, fix it later

				if (!this.CharsToRemoveCategories.Contains(currCat))
					// Add it to output (identifier)
					outputChars.Add(current);

				source.PreviewPosition++;
			}

			if (outputChars.Count == 0)
				return false;

			// Convert collected chars to string
			details.Body = new string(outputChars.ToArray());
			if (!this.CheckCaseRestriction(details.Body))
				return false;

			return !string.IsNullOrEmpty(details.Body);
		}

		private bool CharOk(char ch, bool first)
		{
			// First check char lists, then categories
			var charSet = first ? this.allFirstCharsSet : this.allCharsSet;
			if (charSet.Contains(ch))
				return true;

			// Check categories
			if (this.CharCategories.Count > 0)
			{
				var chCat = char.GetUnicodeCategory(ch);
				var catList = first ? this.StartCharCategories : this.CharCategories;
				if (catList.Contains(chCat))
					return true;
			}

			return false;
		}

		private bool CheckCaseRestriction(string body)
		{
			switch (this.CaseRestriction)
			{
				case CaseRestriction.FirstLower:
					return Char.IsLower(body, 0);

				case CaseRestriction.FirstUpper:
					return Char.IsUpper(body, 0);

				case CaseRestriction.AllLower:
					return body.ToLower() == body;

				case CaseRestriction.AllUpper:
					return body.ToUpper() == body;

				default:
					return true;
			}
		}

		private void CheckReservedWord(Token token)
		{
			KeyTerm keyTerm;
			if (this.Grammar.KeyTerms.TryGetValue(token.Text, out keyTerm))
			{
				token.KeyTerm = keyTerm;

				// If it is reserved word, then overwrite terminal
				if (keyTerm.Flags.IsSet(TermFlags.IsReservedWord))
					token.SetTerminal(keyTerm);
			}
		}

		private char ReadUnicodeEscape(ISourceStream source, CompoundTokenDetails details)
		{
			// Position is currently at "\" symbol
			// Move to U/u char
			source.PreviewPosition++;
			int len;

			switch (source.PreviewChar)
			{
				case 'u':
					len = 4;
					break;

				case 'U':
					len = 8;
					break;

				default:
					// "Invalid escape symbol, expected 'u' or 'U' only."
					details.Error = Resources.ErrInvEscSymbol;
					return '\0';
			}

			if (source.PreviewPosition + len > source.Text.Length)
			{
				// "Invalid escape sequence";
				details.Error = Resources.ErrInvEscSeq;
				return '\0';
			}

			// Move to the first digit
			source.PreviewPosition++;

			var digits = source.Text.Substring(source.PreviewPosition, len);
			var result = (char) Convert.ToUInt32(digits, 16);

			source.PreviewPosition += len;
			details.Flags |= (int) IdFlagsInternal.HasEscapes;

			return result;
		}

		#endregion overrides
	}

	public class UnicodeCategoryList : List<UnicodeCategory> { }
}
