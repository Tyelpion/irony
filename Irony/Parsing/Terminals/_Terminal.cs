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
	public static class TerminalPriority
	{
		public static int High = 1000;
		public static int Low = -1000;
		public static int Normal;
		public static int ReservedWords = 900;
	}

	public class Terminal : BnfTerm
	{
		#region Constructors

		public Terminal(string name) : this(name, TokenCategory.Content, TermFlags.None)
		{ }

		public Terminal(string name, TokenCategory category) : this(name, category, TermFlags.None)
		{ }

		public Terminal(string name, string errorAlias, TokenCategory category, TermFlags flags) : this(name, category, flags)
		{
			this.ErrorAlias = errorAlias;
		}

		public Terminal(string name, TokenCategory category, TermFlags flags) : base(name)
		{
			this.Category = category;
			this.Flags |= flags;

			if (this.Category == TokenCategory.Outline)
				this.SetFlag(TermFlags.IsPunctuation);

			this.OutputTerminal = this;
		}

		#endregion Constructors

		#region fields and properties

		public TokenCategory Category = TokenCategory.Content;
		public TokenEditorInfo EditorInfo;

		public Terminal IsPairFor;

		public byte MultilineIndex;

		/// <summary>
		/// Priority is used when more than one terminal may match the input char.
		/// It determines the order in which terminals will try to match input for a given char in the input.
		/// For a given input char the scanner uses the hash table to look up the collection of terminals that may match this input symbol.
		/// It is the order in this collection that is determined by Priority property - the higher the priority,
		/// the earlier the terminal gets a chance to check the input.
		/// </summary>
		/// <remarks>
		/// Default is 0
		/// </remarks>
		public int Priority = TerminalPriority.Normal;

		/// <summary>
		/// Terminal to attach to the output token. By default is set to the Terminal itself
		/// Use SetOutputTerminal method to change it. For example of use see TerminalFactory.CreateSqlIdentifier and sample SQL grammar
		/// </summary>
		public Terminal OutputTerminal { get; protected set; }

		#endregion fields and properties

		#region virtual methods: GetFirsts(), TryMatch, Init, TokenToString

		/// <summary>
		/// "Firsts" (chars) collections are used for quick search for possible matching terminal(s) using current character in the input stream.
		/// A terminal might declare no firsts. In this case, the terminal is tried for match for any current input character.
		/// </summary>
		/// <returns></returns>
		public virtual IList<string> GetFirsts()
		{
			return null;
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);
		}

		public virtual string TokenToString(Token token)
		{
			if (token.ValueString == this.Name)
				return token.ValueString;
			else
				return (token.ValueString ?? token.Text) + " (" + this.Name + ")";
		}

		public virtual Token TryMatch(ParsingContext context, ISourceStream source)
		{
			return null;
		}

		#endregion virtual methods: GetFirsts(), TryMatch, Init, TokenToString

		#region Events: ValidateToken, ParserInputPreview

		/// <summary>
		/// Invoked when ParseTreeNode is created from the token. This is parser-preview event, when parser
		/// just received the token, wrapped it into ParseTreeNode and is about to look at it.
		/// </summary>
		public event EventHandler<ParsingEventArgs> ParserInputPreview;

		public event EventHandler<ValidateTokenEventArgs> ValidateToken;

		protected internal virtual void OnParserInputPreview(ParsingContext context)
		{
			if (this.ParserInputPreview != null)
				this.ParserInputPreview(this, context.SharedParsingEventArgs);
		}

		protected internal virtual void OnValidateToken(ParsingContext context)
		{
			if (this.ValidateToken != null)
				this.ValidateToken(this, context.SharedValidateTokenEventArgs);
		}

		#endregion Events: ValidateToken, ParserInputPreview

		#region static comparison methods

		public static int ByPriorityReverse(Terminal x, Terminal y)
		{
			if (x.Priority > y.Priority)
				return -1;
			if (x.Priority == y.Priority)
				return 0;
			return 1;
		}

		#endregion static comparison methods

		#region Miscellaneous: SetOutputTerminal

		public void SetOutputTerminal(Grammar grammar, Terminal outputTerminal)
		{
			this.OutputTerminal = outputTerminal;
			grammar.NonGrammarTerminals.Add(this);
		}

		#endregion Miscellaneous: SetOutputTerminal

		[Obsolete("Deprecated: use constants in TerminalPriority class instead")]
		public const int HighestPriority = 1000;

		[Obsolete("Deprecated: use constants in TerminalPriority class instead")]
		public const int LowestPriority = -1000;

		[Obsolete("Deprecated: use constants in TerminalPriority class instead")]
		public const int ReservedWordsPriority = 900;

		public static string TerminalsToString(IEnumerable<Terminal> terminals)
		{
			return string.Join(" ", terminals);
		}
	}

	public class TerminalList : List<Terminal>
	{
		public override string ToString()
		{
			return Terminal.TerminalsToString(this);
		}
	}

	public class TerminalSet : HashSet<Terminal>
	{
		public override string ToString()
		{
			return Terminal.TerminalsToString(this);
		}
	}
}
