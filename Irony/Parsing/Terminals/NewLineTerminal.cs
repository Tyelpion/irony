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
using System.Linq;

namespace Irony.Parsing
{
	/// <summary>
	/// This is a simple NewLine terminal recognizing line terminators for use in grammars for line-based languages like VB
	/// instead of more complex alternative of using CodeOutlineFilter.
	/// </summary>
	public class NewLineTerminal : Terminal
	{
		public string LineTerminators = "\n\r\v";

		public NewLineTerminal(string name) : base(name, TokenCategory.Outline)
		{
			// "[line break]";
			this.ErrorAlias = Resources.LabelLineBreak;
			this.Flags |= TermFlags.IsPunctuation;
		}

		#region overrides: Init, GetFirsts, TryMatch

		public override IList<string> GetFirsts()
		{
			var firsts = new StringList();
			foreach (char t in this.LineTerminators)
			{
				firsts.Add(t.ToString());
			}

			return firsts;
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);

			// That will prevent SkipWhitespace method from skipping new-line chars
			this.Grammar.UsesNewLine = true;
		}

		public override Token TryMatch(ParsingContext context, ISourceStream source)
		{
			var current = source.PreviewChar;
			if (!this.LineTerminators.Contains(current))
				return null;

			// Treat \r\n as a single terminator
			var doExtraShift = (current == '\r' && source.NextPreviewChar == '\n');

			// main shift
			source.PreviewPosition++;
			if (doExtraShift)
				source.PreviewPosition++;

			var result = source.CreateToken(this.OutputTerminal);
			return result;
		}

		#endregion overrides: Init, GetFirsts, TryMatch
	}
}
