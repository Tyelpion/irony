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
	public enum TokenCategory
	{
		Content,

		/// <summary>
		/// NewLine, indent, dedent
		/// </summary>
		Outline,

		Comment,
		Directive,
		Error,
	}

	public enum TokenFlags
	{
		IsIncomplete = 0x01,
	}

	/// <summary>
	/// Some terminals may need to return a bunch of tokens in one call to TryMatch;
	/// <see cref="MultiToken"/> is a container for these tokens
	/// </summary>
	public class MultiToken : Token
	{
		public TokenList ChildTokens;

		public MultiToken(params Token[] tokens) : this(tokens[0].Terminal, tokens[0].Location, new TokenList())
		{
			this.ChildTokens.AddRange(tokens);
		}

		public MultiToken(Terminal term, SourceLocation location, TokenList childTokens) : base(term, location, string.Empty, null)
		{
			this.ChildTokens = childTokens;
		}
	}

	/// <summary>
	/// Tokens are produced by scanner and fed to parser, optionally passing through Token filters in between.
	/// </summary>
	public partial class Token
	{
		public readonly SourceLocation Location;

		public readonly string Text;

		public object Details;

		public TokenEditorInfo EditorInfo;

		public TokenFlags Flags;

		public KeyTerm KeyTerm;

		/// <summary>
		/// Matching opening/closing brace
		/// </summary>
		public Token OtherBrace;

		/// <summary>
		/// Scanner state after producing token
		/// </summary>
		public short ScannerState;

		public object Value;

		public Token(Terminal term, SourceLocation location, string text, object value)
		{
			this.SetTerminal(term);
			this.KeyTerm = term as KeyTerm;
			this.Location = location;
			this.Text = text;
			this.Value = value;
		}

		public TokenCategory Category
		{
			get { return this.Terminal.Category; }
		}

		public int Length
		{
			get { return this.Text == null ? 0 : this.Text.Length; }
		}

		public SourceLocation EndLocation
		{
			get
			{
				var length = this.Length;
				if (length == 0)
					return this.Location;

				var pos = this.Location.Position + length;
				var lines = this.Text.Split('\n');
				var lineCount = lines.Length - 1;
				var column = lineCount > 0 ? lines[lines.Length - 1].Length : this.Location.Column + length;

				return new SourceLocation(pos, this.Location.Line + lineCount, column);
			}
		}

		public Terminal Terminal { get; private set; }

		public string ValueString
		{
			get { return (this.Value == null ? string.Empty : this.Value.ToString()); }
		}

		public bool IsError()
		{
			return this.Category == TokenCategory.Error;
		}

		public bool IsSet(TokenFlags flag)
		{
			return (this.Flags & flag) != 0;
		}

		public void SetTerminal(Terminal terminal)
		{
			this.Terminal = terminal;

			// Set to term's EditorInfo by default
			this.EditorInfo = this.Terminal.EditorInfo;
		}

		[System.Diagnostics.DebuggerStepThrough]
		public override string ToString()
		{
			return this.Terminal.TokenToString(this);
		}
	}

	public class TokenList : List<Token> { }

	public class TokenStack : Stack<Token> { }
}
