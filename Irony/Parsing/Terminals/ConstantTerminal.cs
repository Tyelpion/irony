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
	/// This terminal allows to declare a set of constants in the input language
	/// It should be used when constant symbols do not look like normal identifiers; e.g. in Scheme, #t, #f are true/false
	/// constants, and they don't fit into Scheme identifier pattern.
	/// </summary>
	public class ConstantsTable : Dictionary<string, object> { }

	public class ConstantTerminal : Terminal
	{
		public readonly ConstantsTable Constants = new ConstantsTable();

		public ConstantTerminal(string name, Type nodeType = null) : base(name)
		{
			this.SetFlag(TermFlags.IsConstant);

			if (nodeType != null)
				this.AstConfig.NodeType = nodeType;

			// Constants have priority over normal identifiers
			this.Priority = TerminalPriority.High;
		}

		public void Add(string lexeme, object value)
		{
			this.Constants[lexeme] = value;
		}

		public override IList<string> GetFirsts()
		{
			var array = new string[this.Constants.Count];
			this.Constants.Keys.CopyTo(array, 0);
			return array;
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);
			if (this.EditorInfo == null)
				this.EditorInfo = new TokenEditorInfo(TokenType.Unknown, TokenColor.Text, TokenTriggers.None);
		}

		public override Token TryMatch(ParsingContext context, ISourceStream source)
		{
			var text = source.Text;
			foreach (var entry in this.Constants)
			{
				source.PreviewPosition = source.Position;

				var constant = entry.Key;
				if (source.PreviewPosition + constant.Length > text.Length)
					continue;

				if (source.MatchSymbol(constant))
				{
					source.PreviewPosition += constant.Length;
					if (!this.Grammar.IsWhitespaceOrDelimiter(source.PreviewChar))
						// Make sure it is delimiter
						continue;

					return source.CreateToken(this.OutputTerminal, entry.Value);
				}
			}

			return null;
		}
	}
}
