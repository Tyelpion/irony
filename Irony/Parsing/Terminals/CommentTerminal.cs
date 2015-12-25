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
	public class CommentTerminal : Terminal
	{
		public StringList EndSymbols;

		public string StartSymbol;

		private char[] endSymbolsFirsts;

		/// <summary>
		/// True if NewLine is one of EndSymbols; if yes, EOF is also considered a valid end symbol
		/// </summary>
		private bool isLineComment;

		public CommentTerminal(string name, string startSymbol, params string[] endSymbols) : base(name, TokenCategory.Comment)
		{
			this.StartSymbol = startSymbol;
			this.EndSymbols = new StringList();
			this.EndSymbols.AddRange(endSymbols);

			// Assign max priority
			this.Priority = TerminalPriority.High;
		}

		#region overrides

		public override IList<string> GetFirsts()
		{
			return new string[] { StartSymbol };
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);

			// endSymbolsFirsts char array is used for fast search for end symbols using String's method IndexOfAny(...)
			this.endSymbolsFirsts = new char[this.EndSymbols.Count];

			for (int i = 0; i < this.EndSymbols.Count; i++)
			{
				string sym = this.EndSymbols[i];
				this.endSymbolsFirsts[i] = sym[0];
				this.isLineComment |= sym.Contains("\n");

				if (!this.isLineComment)
					this.SetFlag(TermFlags.IsMultiline);
			}

			if (this.EditorInfo == null)
			{
				var ttype = this.isLineComment ? TokenType.LineComment : TokenType.Comment;
				this.EditorInfo = new TokenEditorInfo(ttype, TokenColor.Comment, TokenTriggers.None);
			}
		}

		public override Token TryMatch(ParsingContext context, ISourceStream source)
		{
			Token result;
			if (context.VsLineScanState.Value != 0)
			{
				// We are continuing in line mode - restore internal env (none in this case)
				context.VsLineScanState.Value = 0;
			}
			else
			{
				// We are starting from scratch
				if (!this.BeginMatch(context, source))
					return null;
			}
			result = CompleteMatch(context, source);
			if (result != null)
				return result;

			// If it is LineComment, it is ok to hit EOF without final line-break; just return all until end.
			if (this.isLineComment)
				return source.CreateToken(this.OutputTerminal);

			if (context.Mode == ParseMode.VsLineScan)
				return CreateIncompleteToken(context, source);

			return context.CreateErrorToken(Resources.ErrUnclosedComment);
		}

		private bool BeginMatch(ParsingContext context, ISourceStream source)
		{
			// Check starting symbol
			if (!source.MatchSymbol(this.StartSymbol))
				return false;

			source.PreviewPosition += this.StartSymbol.Length;
			return true;
		}

		private Token CompleteMatch(ParsingContext context, ISourceStream source)
		{
			// Find end symbol
			while (!source.EOF())
			{
				int firstCharPos;
				if (this.EndSymbols.Count == 1)
					firstCharPos = source.Text.IndexOf(this.EndSymbols[0], source.PreviewPosition);
				else
					firstCharPos = source.Text.IndexOfAny(this.endSymbolsFirsts, source.PreviewPosition);

				if (firstCharPos < 0)
				{
					source.PreviewPosition = source.Text.Length;
					// Indicating error
					return null;
				}

				// We found a character that might start an end symbol; let's see if it is true.
				source.PreviewPosition = firstCharPos;
				foreach (string endSymbol in this.EndSymbols)
				{
					if (source.MatchSymbol(endSymbol))
					{
						// We found end symbol; eat end symbol only if it is not line comment.
						// For line comment, leave LF symbol there, it might be important to have a separate LF token
						if (!this.isLineComment)
							source.PreviewPosition += endSymbol.Length;

						return source.CreateToken(this.OutputTerminal);
					}
				}

				// Move to the next char and try again
				source.PreviewPosition++;
			}

			// Might happen if we found a start char of end symbol, but not the full endSymbol
			return null;
		}

		private Token CreateIncompleteToken(ParsingContext context, ISourceStream source)
		{
			source.PreviewPosition = source.Text.Length;

			var result = source.CreateToken(this.OutputTerminal);
			result.Flags |= TokenFlags.IsIncomplete;

			context.VsLineScanState.TerminalIndex = this.MultilineIndex;

			return result;
		}

		#endregion overrides
	}
}
