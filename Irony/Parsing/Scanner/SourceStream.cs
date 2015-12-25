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

namespace Irony.Parsing
{
	public class SourceStream : ISourceStream
	{
		private char[] chars;
		private StringComparison stringComparison;
		private int tabWidth;
		private int textLength;

		public SourceStream(string text, bool caseSensitive, int tabWidth) : this(text, caseSensitive, tabWidth, new SourceLocation())
		{
		}

		public SourceStream(string text, bool caseSensitive, int tabWidth, SourceLocation initialLocation)
		{
			this.text = text;
			this.textLength = this.text.Length;
			this.chars = this.text.ToCharArray();
			this.stringComparison = caseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;
			this.tabWidth = tabWidth;
			this.location = initialLocation;
			this.previewPosition = this.location.Position;

			if (this.tabWidth <= 1)
				this.tabWidth = 8;
		}

		#region ISourceStream Members

		private SourceLocation location;

		private int previewPosition;

		private string text;

		public SourceLocation Location
		{
			[System.Diagnostics.DebuggerStepThrough]
			get { return this.location; }

			set { this.location = value; }
		}

		public char NextPreviewChar
		{
			[System.Diagnostics.DebuggerStepThrough]
			get
			{
				if (this.previewPosition + 1 >= this.textLength)
					return '\0';

				return this.chars[this.previewPosition + 1];
			}
		}

		public int Position
		{
			get
			{
				return this.location.Position;
			}
			set
			{
				if (this.location.Position != value)
					this.SetNewPosition(value);
			}
		}

		public char PreviewChar
		{
			[System.Diagnostics.DebuggerStepThrough]
			get
			{
				if (this.previewPosition >= this.textLength)
					return '\0';

				return this.chars[this.previewPosition];
			}
		}

		public int PreviewPosition
		{
			get { return this.previewPosition; }

			set { this.previewPosition = value; }
		}

		public string Text
		{
			get { return this.text; }
		}

		public Token CreateToken(Terminal terminal)
		{
			var tokenText = this.GetPreviewText();
			return new Token(terminal, this.Location, tokenText, tokenText);
		}

		public Token CreateToken(Terminal terminal, object value)
		{
			var tokenText = this.GetPreviewText();
			return new Token(terminal, this.Location, tokenText, value);
		}

		[System.Diagnostics.DebuggerStepThrough]
		public bool EOF()
		{
			return this.previewPosition >= this.textLength;
		}

		public bool MatchSymbol(string symbol)
		{
			try
			{
				int cmp = string.Compare(this.text, this.PreviewPosition, symbol, 0, symbol.Length, this.stringComparison);
				return cmp == 0;
			}
			catch
			{
				// Exception may be thrown if Position + symbol.length > text.Length;
				// this happens not often, only at the very end of the file, so we don't check this explicitly
				// but simply catch the exception and return false. Again, try/catch block has no overhead
				// if exception is not thrown.
				return false;
			}
		}

		#endregion ISourceStream Members

		/// <summary>
		/// To make debugging easier: show 20 chars from current position
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			string result;
			try
			{
				var p = this.Location.Position;
				if (p + 20 < this.textLength)
					// " ..."
					result = this.text.Substring(p, 20) + Resources.LabelSrcHaveMore;
				else
					// "(EOF)"
					result = this.text.Substring(p) + Resources.LabelEofMark;
			}
			catch (Exception)
			{
				result = this.PreviewChar + Resources.LabelSrcHaveMore;
			}

			// "[{0}], at {1}"
			return string.Format(Resources.MsgSrcPosToString, result, this.Location);
		}

		/// <summary>
		/// returns substring from Location.Position till (PreviewPosition - 1)
		/// </summary>
		/// <returns></returns>
		private string GetPreviewText()
		{
			var until = this.previewPosition;
			if (until > this.textLength)
				until = this.textLength;

			var p = this.location.Position;
			var text = this.text.Substring(p, until - p);

			return text;
		}

		/// <summary>
		/// Computes the Location info (line, col) for a new source position.
		/// </summary>
		/// <param name="newPosition"></param>
		private void SetNewPosition(int newPosition)
		{
			if (newPosition < Position)
				throw new Exception(Resources.ErrCannotMoveBackInSource);

			int p = this.Position;
			int col = this.Location.Column;
			int line = this.Location.Line;

			while (p < newPosition)
			{
				var curr = this.chars[p];
				switch (curr)
				{
					case '\n': line++; col = 0; break;
					case '\r': break;
					case '\t': col = (col / this.tabWidth + 1) * this.tabWidth; break;
					default: col++; break;
				}

				p++;
			}

			this.Location = new SourceLocation(p, line, col);
		}
	}
}
