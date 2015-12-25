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
using System.Linq;
using System.Text;

namespace Irony.Parsing
{
	[Flags]
	public enum FreeTextOptions
	{
		None = 0x0,

		/// <summary>
		/// Move source pointer beyond terminator (so token "consumes" it from input), but don't include it in token text
		/// </summary>
		ConsumeTerminator = 0x01,

		/// <summary>
		/// Include terminator into token text/value
		/// </summary>
		IncludeTerminator = 0x02,

		/// <summary>
		/// Treat EOF as legitimate terminator
		/// </summary>
		AllowEof = 0x04,

		AllowEmpty = 0x08,
	}

	/// <summary>
	/// Sometimes language definition includes tokens that have no specific format, but are just "all text until some terminator character(s)";
	/// FreeTextTerminal allows easy implementation of such language element.
	/// </summary>
	public class FreeTextLiteral : Terminal
	{
		public StringDictionary Escapes = new StringDictionary();
		public StringSet Firsts = new StringSet();
		public FreeTextOptions FreeTextOptions;
		public StringSet Terminators = new StringSet();

		/// <summary>
		/// True if we have a single Terminator and no escapes
		/// </summary>
		private bool isSimple;

		private string singleTerminator;
		private char[] stopChars;

		public FreeTextLiteral(string name, params string[] terminators) : this(name, FreeTextOptions.None, terminators)
		{ }

		public FreeTextLiteral(string name, FreeTextOptions freeTextOptions, params string[] terminators) : base(name)
		{
			this.FreeTextOptions = freeTextOptions;
			this.Terminators.UnionWith(terminators);
			this.SetFlag(TermFlags.IsLiteral);
		}

		public override IList<string> GetFirsts()
		{
			var result = new StringList();
			result.AddRange(Firsts);
			return result;
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);
			this.isSimple = this.Terminators.Count == 1 && this.Escapes.Count == 0;

			if (this.isSimple)
			{
				this.singleTerminator = this.Terminators.First();
				return;
			}

			var stopChars = new CharHashSet();

			foreach (var key in this.Escapes.Keys)
			{
				stopChars.Add(key[0]);
			}

			foreach (var t in this.Terminators)
			{
				stopChars.Add(t[0]);
			}

			this.stopChars = stopChars.ToArray();
		}

		public override Token TryMatch(ParsingContext context, ISourceStream source)
		{
			if (!this.TryMatchPrefixes(context, source))
				return null;

			return this.isSimple ? this.TryMatchContentSimple(context, source) : this.TryMatchContentExtended(context, source);
		}

		private bool CheckEscape(ISourceStream source, StringBuilder tokenText)
		{
			foreach (var dictEntry in this.Escapes)
			{
				if (source.MatchSymbol(dictEntry.Key))
				{
					source.PreviewPosition += dictEntry.Key.Length;
					tokenText.Append(dictEntry.Value);
					return true;
				}
			}

			return false;
		}

		private bool CheckTerminators(ISourceStream source, StringBuilder tokenText)
		{
			foreach (var term in this.Terminators)
			{
				if (source.MatchSymbol(term))
				{
					if (this.IsSet(FreeTextOptions.IncludeTerminator))
						tokenText.Append(term);

					if (IsSet(FreeTextOptions.ConsumeTerminator | FreeTextOptions.IncludeTerminator))
						source.PreviewPosition += term.Length;

					return true;
				}
			}

			return false;
		}

		private bool IsSet(FreeTextOptions option)
		{
			return (this.FreeTextOptions & option) != 0;
		}

		private Token TryMatchContentExtended(ParsingContext context, ISourceStream source)
		{
			var tokenText = new StringBuilder();

			while (true)
			{
				// Find next position of one of stop chars
				var nextPos = source.Text.IndexOfAny(this.stopChars, source.PreviewPosition);
				if (nextPos == -1)
				{
					if (this.IsSet(FreeTextOptions.AllowEof))
					{
						source.PreviewPosition = source.Text.Length;
						return source.CreateToken(this.OutputTerminal);
					}
					else
						return null;
				}

				var newText = source.Text.Substring(source.PreviewPosition, nextPos - source.PreviewPosition);
				tokenText.Append(newText);

				source.PreviewPosition = nextPos;

				// If it is escape, add escaped text and continue search
				if (this.CheckEscape(source, tokenText))
					continue;

				// Check terminators
				if (this.CheckTerminators(source, tokenText))
					// From while (true); we reached
					break;

				// The current stop is not at escape or terminator; add this char to token text and move on
				tokenText.Append(source.PreviewChar);

				source.PreviewPosition++;

			}

			var text = tokenText.ToString();
			if (string.IsNullOrEmpty(text) && (this.FreeTextOptions & Parsing.FreeTextOptions.AllowEmpty) == 0)
				return null;

			return source.CreateToken(this.OutputTerminal, text);
		}

		private Token TryMatchContentSimple(ParsingContext context, ISourceStream source)
		{
			var startPos = source.PreviewPosition;
			var termLen = this.singleTerminator.Length;
			var stringComp = this.Grammar.CaseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;
			int termPos = source.Text.IndexOf(this.singleTerminator, startPos, stringComp);

			if (termPos < 0 && this.IsSet(FreeTextOptions.AllowEof))
				termPos = source.Text.Length;

			if (termPos < 0)
				return context.CreateErrorToken(Resources.ErrFreeTextNoEndTag, this.singleTerminator);

			var textEnd = termPos;
			if (this.IsSet(FreeTextOptions.IncludeTerminator))
				textEnd += termLen;

			var tokenText = source.Text.Substring(startPos, textEnd - startPos);
			if (string.IsNullOrEmpty(tokenText) && (this.FreeTextOptions & Parsing.FreeTextOptions.AllowEmpty) == 0)
				return null;

			// The following line is a fix submitted by user rmcase
			source.PreviewPosition = IsSet(FreeTextOptions.ConsumeTerminator) ? termPos + termLen : termPos;

			return source.CreateToken(this.OutputTerminal, tokenText);
		}

		private bool TryMatchPrefixes(ParsingContext context, ISourceStream source)
		{
			if (this.Firsts.Count == 0)
				return true;

			foreach (var first in this.Firsts)
			{
				if (source.MatchSymbol(first))
				{
					source.PreviewPosition += first.Length;
					return true;
				}
			}

			return false;
		}
	}
}
