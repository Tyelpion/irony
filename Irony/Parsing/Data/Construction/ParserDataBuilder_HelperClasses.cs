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

/// <summary>
/// Helper data classes for ParserDataBuilder
/// Note about using LRItemSet vs LRItemList.
/// It appears that in many places the LRItemList would be a better (and faster) choice than LRItemSet.
/// Many of the sets are actually lists and don't require hashset's functionality.
/// But surprisingly, using LRItemSet proved to have much better performance (twice faster for lookbacks/lookaheads computation), so LRItemSet
/// is used everywhere.
/// </summary>
namespace Irony.Parsing.Construction
{
	public partial class LR0Item
	{
		public readonly BnfTerm Current;
		public readonly int Position;
		public readonly Production Production;
		public GrammarHintList Hints = new GrammarHintList();
		public bool TailIsNullable;

		/// <summary>
		/// Automatically generated IDs - used for building keys for lists of kernel LR0Items
		/// which in turn are used to quickly lookup parser states in hash
		/// </summary>
		internal readonly int ID;

		private int hashCode;

		public LR0Item(int id, Production production, int position, GrammarHintList hints)
		{
			this.ID = id;
			this.Production = production;
			this.Position = position;
			this.Current = (this.Position < this.Production.RValues.Count) ? this.Production.RValues[this.Position] : null;

			if (hints != null)
				this.Hints.AddRange(hints);

			this.hashCode = this.ID.ToString().GetHashCode();
		}

		public bool IsFinal
		{
			get { return this.Position == this.Production.RValues.Count; }
		}

		public bool IsInitial
		{
			get { return this.Position == 0; }
		}

		public bool IsKernel
		{
			get { return this.Position > 0; }
		}

		public LR0Item ShiftedItem
		{
			get
			{
				if (this.Position >= this.Production.LR0Items.Count - 1)
					return null;
				else
					return this.Production.LR0Items[this.Position + 1];
			}
		}

		public override int GetHashCode()
		{
			return this.hashCode;
		}

		public override string ToString()
		{
			return Production.ProductionToString(this.Production, this.Position);
		}
	}

	public class LR0ItemList : List<LR0Item> { }

	public class LR0ItemSet : HashSet<LR0Item> { }

	public class LRItem
	{
		public readonly LR0Item Core;
		public readonly ParserState State;

		/// <summary>
		/// Lookahead info for reduce items
		/// </summary>
		public TerminalSet Lookaheads = new TerminalSet();

		public TransitionSet Lookbacks = new TransitionSet();

		/// <summary>
		/// Used in lookahead computations
		/// </summary>
		public LRItem ShiftedItem;

		/// <summary>
		/// Used in lookahead computations
		/// </summary>
		public Transition Transition;

		private int hashCode;

		public LRItem(ParserState state, LR0Item core)
		{
			this.State = state;
			this.Core = core;
			this.hashCode = unchecked(state.GetHashCode() + core.GetHashCode());
		}

		public override int GetHashCode()
		{
			return this.hashCode;
		}

		public TerminalSet GetLookaheadsInConflict()
		{
			var lkhc = new TerminalSet();
			lkhc.UnionWith(this.Lookaheads);
			lkhc.IntersectWith(this.State.BuilderData.Conflicts);

			return lkhc;
		}

		public override string ToString()
		{
			return Core.ToString();
		}
	}

	public class LRItemList : List<LRItem> { }

	public class LRItemSet : HashSet<LRItem>
	{
		public LRItem FindByCore(LR0Item core)
		{
			foreach (LRItem item in this)
			{
				if (item.Core == core)
					return item;
			}

			return null;
		}

		public LR0ItemSet GetShiftedCores()
		{
			var result = new LR0ItemSet();
			foreach (var item in this)
			{
				if (item.Core.ShiftedItem != null)
					result.Add(item.Core.ShiftedItem);
			}

			return result;
		}

		public LRItemSet SelectByCurrent(BnfTerm current)
		{
			var result = new LRItemSet();
			foreach (var item in this)
			{
				if (item.Core.Current == current)
					result.Add(item);
			}

			return result;
		}

		public LRItemSet SelectByLookahead(Terminal lookahead)
		{
			var result = new LRItemSet();
			foreach (var item in this)
			{
				if (item.Lookaheads.Contains(lookahead))
					result.Add(item);
			}

			return result;
		}
	}

	public class ParserStateData
	{
		public readonly LRItemSet AllItems = new LRItemSet();
		public readonly TerminalSet Conflicts = new TerminalSet();
		public readonly LRItemSet InitialItems = new LRItemSet();
		public readonly bool IsInadequate;
		public readonly LRItemSet ReduceItems = new LRItemSet();
		public readonly LRItemSet ShiftItems = new LRItemSet();
		public readonly TerminalSet ShiftTerminals = new TerminalSet();
		public readonly BnfTermSet ShiftTerms = new BnfTermSet();
		public readonly ParserState State;
		public LR0ItemSet AllCores = new LR0ItemSet();

		private ParserStateSet readStateSet;

		private TransitionTable transitions;

		/// <summary>
		/// used for creating canonical states from core set
		/// </summary>
		/// <param name="state"></param>
		/// <param name="kernelCores"></param>
		public ParserStateData(ParserState state, LR0ItemSet kernelCores)
		{
			this.State = state;

			foreach (var core in kernelCores)
			{
				this.AddItem(core);
			}

			this.IsInadequate = this.ReduceItems.Count > 1 || this.ReduceItems.Count == 1 && this.ShiftItems.Count > 0;
		}

		/// <summary>
		/// A set of states reachable through shifts over nullable non-terminals. Computed on demand
		/// </summary>
		public ParserStateSet ReadStateSet
		{
			get
			{
				if (this.readStateSet == null)
				{
					this.readStateSet = new ParserStateSet();

					foreach (var shiftTerm in State.BuilderData.ShiftTerms)
					{
						if (shiftTerm.Flags.IsSet(TermFlags.IsNullable))
						{
							var shift = State.Actions[shiftTerm] as ShiftParserAction;
							var targetState = shift.NewState;

							this.readStateSet.Add(targetState);

							// We shouldn't get into loop here, the chain of reads is finite
							this.readStateSet.UnionWith(targetState.BuilderData.ReadStateSet);
						}
					}
				}

				return this.readStateSet;
			}
		}

		public TransitionTable Transitions
		{
			get
			{
				if (this.transitions == null)
					this.transitions = new TransitionTable();

				return this.transitions;
			}
		}

		public void AddItem(LR0Item core)
		{
			// Check if a core had been already added. If yes, simply return
			if (!this.AllCores.Add(core))
				return;

			// Create new item, add it to AllItems, InitialItems, ReduceItems or ShiftItems
			var item = new LRItem(this.State, core);
			this.AllItems.Add(item);

			if (item.Core.IsFinal)
				this.ReduceItems.Add(item);
			else
				this.ShiftItems.Add(item);

			if (item.Core.IsInitial)
				this.InitialItems.Add(item);

			if (core.IsFinal)
				return;

			// Add current term to ShiftTerms
			if (!this.ShiftTerms.Add(core.Current))
				return;

			if (core.Current is Terminal)
				this.ShiftTerminals.Add(core.Current as Terminal);

			// If current term (core.Current) is a new non-terminal, expand it
			var currNt = core.Current as NonTerminal;
			if (currNt == null)
				return;

			foreach (var prod in currNt.Productions)
			{
				this.AddItem(prod.LR0Items[0]);
			}
		}

		public ParserState GetNextState(BnfTerm shiftTerm)
		{
			var shift = this.ShiftItems.FirstOrDefault(item => item.Core.Current == shiftTerm);
			if (shift == null)
				return null;

			return shift.ShiftedItem.State;
		}

		public TerminalSet GetReduceReduceConflicts()
		{
			var result = new TerminalSet();
			result.UnionWith(this.Conflicts);
			result.ExceptWith(this.ShiftTerminals);

			return result;
		}

		public TerminalSet GetShiftReduceConflicts()
		{
			var result = new TerminalSet();
			result.UnionWith(this.Conflicts);
			result.IntersectWith(this.ShiftTerminals);

			return result;
		}
	}

	/// <summary>
	/// An object representing inter-state transitions. Defines Includes, IncludedBy that are used for efficient lookahead computation
	/// </summary>
	public class Transition
	{
		public readonly ParserState FromState;
		public readonly TransitionSet IncludedBy = new TransitionSet();
		public readonly TransitionSet Includes = new TransitionSet();
		public readonly LRItemSet Items;
		public readonly NonTerminal OverNonTerminal;
		public readonly ParserState ToState;
		private int hashCode;

		public Transition(ParserState fromState, NonTerminal overNonTerminal)
		{
			this.FromState = fromState;
			this.OverNonTerminal = overNonTerminal;

			var shiftItem = fromState.BuilderData.ShiftItems.First(item => item.Core.Current == overNonTerminal);

			this.ToState = this.FromState.BuilderData.GetNextState(overNonTerminal);
			this.hashCode = unchecked(this.FromState.GetHashCode() - overNonTerminal.GetHashCode());

			this.FromState.BuilderData.Transitions.Add(overNonTerminal, this);
			this.Items = this.FromState.BuilderData.ShiftItems.SelectByCurrent(overNonTerminal);

			foreach (var item in Items)
			{
				item.Transition = this;
			}
		}

		public override int GetHashCode()
		{
			return this.hashCode;
		}

		public void Include(Transition other)
		{
			if (other == this)
				return;

			if (!this.IncludeTransition(other))
				return;

			// Include children
			foreach (var child in other.Includes)
			{
				this.IncludeTransition(child);
			}
		}

		public override string ToString()
		{
			return this.FromState.Name + " -> (over " + this.OverNonTerminal.Name + ") -> " + this.ToState.Name;
		}

		private bool IncludeTransition(Transition other)
		{
			if (!this.Includes.Add(other))
				return false;

			other.IncludedBy.Add(this);

			// Propagate "up"
			foreach (var incBy in this.IncludedBy)
			{
				incBy.IncludeTransition(other);
			}

			return true;
		}
	}

	public class TransitionList : List<Transition> { }

	public class TransitionSet : HashSet<Transition> { }

	public class TransitionTable : Dictionary<NonTerminal, Transition> { }
}
