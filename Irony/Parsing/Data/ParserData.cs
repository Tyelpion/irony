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
using System.Text;

namespace Irony.Parsing
{
	using Irony.Parsing.Construction;

	[Flags]
	public enum ProductionFlags
	{
		None = 0,

		/// <summary>
		/// contains terminal
		/// </summary>
		HasTerminals = 0x02,

		/// <summary>
		/// contains Error terminal
		/// </summary>
		IsError = 0x04,

		IsEmpty = 0x08,
	}

	/// <summary>
	/// <see cref="ParserData"/> is a container for all information used by CoreParser in input processing.
	/// <para />
	/// <see cref="ParserData"/> is a field in <see cref="LanguageData"/> structure and is used by CoreParser when parsing intput.
	/// <para />
	/// The state graph entry is <see cref="InitialState"/> state; the state graph encodes information usually contained
	/// in what is known in literature as transiton/goto tables.
	/// <para />
	/// The graph is built from the language grammar by <see cref="ParserDataBuilder"/>.
	/// </summary>
	public class ParserData
	{
		public readonly LanguageData Language;

		public readonly ParserStateList States = new ParserStateList();

		public ParserAction ErrorAction;

		/// <summary>
		/// Main initial state.
		/// </summary>
		public ParserState InitialState;

		/// <summary>
		/// Lookup table: AugmRoot => InitialState
		/// </summary>
		public ParserStateTable InitialStates = new ParserStateTable();

		public ParserData(LanguageData language)
		{
			Language = language;
		}
	}

	public partial class ParserState
	{
		public readonly ParserActionTable Actions = new ParserActionTable();

		/// <summary>
		/// Expected terms contains terminals is to be used in Parser-advise-to-Scanner facility
		/// would use it to filter current terminals when <see cref="Scanner"/> has more than one terminal for current char,
		/// it can ask Parser to filter the list using the <see cref="ExpectedTerminals"/> in current Parser state.
		/// </summary>
		public readonly TerminalSet ExpectedTerminals = new TerminalSet();

		public readonly string Name;

		/// <summary>
		/// Custom flags available for use by language/parser authors, to "mark" states in some way
		/// Irony reserves the highest order byte for internal use.
		/// </summary>
		public int CustomFlags;

		/// <summary>
		/// Defined for states with a single reduce item.
		/// <para />
		/// Parser.GetAction returns this action if it is not null.
		/// </summary>
		public ParserAction DefaultAction;

		/// <summary>
		/// Used for error reporting, we would use it to include list of expected terms in error message.
		///<para />
		/// It is reduced compared to ExpectedTerms - some terms are "merged" into other non-terminals (with non-empty DisplayName)
		/// to make message shorter and cleaner. It is computed on-demand in CoreParser
		/// </summary>
		public StringSet ReportedExpectedSet;

		/// <summary>
		/// Transient, used only during automaton construction and may be cleared after that.
		/// </summary>
		internal ParserStateData BuilderData;

		public ParserState(string name)
		{
			this.Name = name;
		}

		public void ClearData()
		{
			this.BuilderData = null;
		}

		public bool CustomFlagIsSet(int flag)
		{
			return (this.CustomFlags & flag) != 0;
		}

		public override int GetHashCode()
		{
			return this.Name.GetHashCode();
		}

		public override string ToString()
		{
			return this.Name;
		}
	}

	public class ParserStateHash : Dictionary<string, ParserState> { }

	public class ParserStateList : List<ParserState> { }

	public class ParserStateSet : HashSet<ParserState> { }

	public class ParserStateTable : Dictionary<NonTerminal, ParserState> { }

	public partial class Production
	{
		/// <summary>
		/// Left-side element
		/// </summary>
		public readonly NonTerminal LValue;

		/// <summary>
		/// The right-side elements sequence
		/// </summary>
		public readonly BnfTermList RValues = new BnfTermList();

		public ProductionFlags Flags;

		/// <summary>
		/// LR0 items based on this production
		/// </summary>
		internal readonly Construction.LR0ItemList LR0Items = new Construction.LR0ItemList();

		public Production(NonTerminal lvalue)
		{
			this.LValue = lvalue;
		}

		public static string ProductionToString(Production production, int dotPosition)
		{
			// dot in the middle of the line
			char dotChar = '\u00B7';

			var bld = new StringBuilder();
			bld.Append(production.LValue.Name);
			bld.Append(" -> ");

			for (int i = 0; i < production.RValues.Count; i++)
			{
				if (i == dotPosition)
					bld.Append(dotChar);

				bld.Append(production.RValues[i].Name);
				bld.Append(" ");
			}

			if (dotPosition == production.RValues.Count)
				bld.Append(dotChar);

			return bld.ToString();
		}

		public override string ToString()
		{
			// no dot
			return ProductionToString(this, -1);
		}

		public string ToStringQuoted()
		{
			return "'" + ToString() + "'";
		}
	}

	public class ProductionList : List<Production> { }
}
