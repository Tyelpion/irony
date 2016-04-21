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
	/// Terminal based on custom method; allows creating custom match without creating new class derived from Terminal
	/// </summary>
	/// <param name="terminal"></param>
	/// <param name="context"></param>
	/// <param name="source"></param>
	/// <returns></returns>
	public delegate Token MatchHandler(Terminal terminal, ParsingContext context, ISourceStream source);

	public class CustomTerminal : Terminal
	{
		public readonly StringList Prefixes = new StringList();

		private readonly MatchHandler handler;

		public CustomTerminal(string name, MatchHandler handler, params string[] prefixes) : base(name)
		{
			this.handler = handler;
			if (prefixes != null)
				this.Prefixes.AddRange(prefixes);

			this.EditorInfo = new TokenEditorInfo(TokenType.Unknown, TokenColor.Text, TokenTriggers.None);
		}

		public MatchHandler Handler
		{
			[System.Diagnostics.DebuggerStepThrough]
			get { return this.handler; }
		}

		[System.Diagnostics.DebuggerStepThrough]
		public override IList<string> GetFirsts()
		{
			return this.Prefixes;
		}

		public override Token TryMatch(ParsingContext context, ISourceStream source)
		{
			return this.handler(this, context, source);
		}
	}
}
