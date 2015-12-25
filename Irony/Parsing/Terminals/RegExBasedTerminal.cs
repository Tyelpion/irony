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
using System.Text.RegularExpressions;

namespace Irony.Parsing
{
	/// <summary>
	/// Note: this class was not tested at all
	/// Based on contributions by CodePlex user sakana280
	/// 12.09.2008 - breaking change! added "name" parameter to the constructor
	/// </summary>
	public class RegexBasedTerminal : Terminal
	{
		public RegexBasedTerminal(string pattern, params string[] prefixes) : base("name")
		{
			this.Pattern = pattern;

			if (prefixes != null)
				this.Prefixes.AddRange(prefixes);
		}

		public RegexBasedTerminal(string name, string pattern, params string[] prefixes) : base(name)
		{
			this.Pattern = pattern;

			if (prefixes != null)
				this.Prefixes.AddRange(prefixes);
		}

		#region public properties

		public readonly string Pattern;
		public readonly StringList Prefixes = new StringList();

		private Regex expression;

		public Regex Expression
		{
			get { return this.expression; }
		}

		#endregion public properties

		public override IList<string> GetFirsts()
		{
			return this.Prefixes;
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);

			var workPattern = @"\G(" + Pattern + ")";
			var options = (Grammar.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
			this.expression = new Regex(workPattern, options);

			if (this.EditorInfo == null)
				this.EditorInfo = new TokenEditorInfo(TokenType.Unknown, TokenColor.Text, TokenTriggers.None);
		}

		public override Token TryMatch(ParsingContext context, ISourceStream source)
		{
			var m = this.expression.Match(source.Text, source.PreviewPosition);
			if (!m.Success || m.Index != source.PreviewPosition)
				return null;

			source.PreviewPosition += m.Length;
			return source.CreateToken(this.OutputTerminal);
		}
	}
}
