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

using System.Linq;

/// <summary>
/// Original implementation is contributed by Alexey Yakovlev (yallie)
/// </summary>
namespace Irony.Parsing
{
	using ConditionalEntry = ConditionalParserAction.ConditionalEntry;

	public class TokenPreviewHint : GrammarHint
	{
		public int MaxPreviewTokens = 1000;

		private PreferredActionType actionType;
		private StringSet beforeStrings = new StringSet();
		private TerminalSet beforeTerminals = new TerminalSet();
		private string description;
		private string firstString;
		private Terminal firstTerminal;

		public TokenPreviewHint(PreferredActionType actionType, string thisSymbol, params string[] comesBefore)
		{
			this.actionType = actionType;
			this.firstString = thisSymbol;
			this.beforeStrings.AddRange(comesBefore);
		}

		public TokenPreviewHint(PreferredActionType actionType, Terminal thisTerm, params Terminal[] comesBefore)
		{
			this.actionType = actionType;
			this.firstTerminal = thisTerm;
			this.beforeTerminals.UnionWith(comesBefore);
		}

		public override void Apply(LanguageData language, Construction.LRItem owner)
		{
			var state = owner.State;
			if (!state.BuilderData.IsInadequate)
				// The state is adequate, we don't need to do anything
				return;

			var conflicts = state.BuilderData.Conflicts;

			// Note that we remove lookaheads from the state conflicts set at the end of this method - to let parser builder know
			// that this conflict is taken care of.
			// On the other hand we may call this method multiple times for different LRItems if we have multiple hints in the same state.
			// Since we remove lookahead from conflicts on the first call, on the consequitive calls it will not be a conflict -
			// but we still need to add a new conditional entry to a conditional parser action for this lookahead.
			// Thus we process the lookahead anyway, even if it is not a conflict.
			// if (conflicts.Count == 0) return; -- this is a wrong thing to do
			switch (this.actionType)
			{
				case PreferredActionType.Reduce:
					{
						if (!owner.Core.IsFinal)
							return;

						// It is reduce action; find lookaheads in conflict
						var lkhs = owner.Lookaheads;
						if (lkhs.Count == 0)
							// If no conflicts then nothing to do
							return;

						var reduceAction = new ReduceParserAction(owner.Core.Production);
						var reduceCondEntry = new ConditionalEntry(this.CheckCondition, reduceAction, this.description);

						foreach (var lkh in lkhs)
						{
							this.AddConditionalEntry(state, lkh, reduceCondEntry);
							if (conflicts.Contains(lkh))
								conflicts.Remove(lkh);
						}
					}
					break;

				case PreferredActionType.Shift:
					{
						var curr = owner.Core.Current as Terminal;
						if (curr == null)
							// It is either reduce item, or curr is a NonTerminal - we cannot shift it
							return;

						var shiftAction = new ShiftParserAction(owner);
						var shiftCondEntry = new ConditionalEntry(this.CheckCondition, shiftAction, this.description);
						this.AddConditionalEntry(state, curr, shiftCondEntry);

						if (conflicts.Contains(curr))
							conflicts.Remove(curr);
					}
					break;
			}
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);

			// Convert strings to terminals, if needed
			this.firstTerminal = this.firstTerminal ?? this.Grammar.ToTerm(this.firstString);

			if (this.beforeStrings.Count > 0)
			{
				// SL pukes here, it does not support co/contravariance in full, we have to do it long way
				foreach (var s in this.beforeStrings)
				{
					this.beforeTerminals.Add(this.Grammar.ToTerm(s));
				}
			}

			// Build description
			var beforeTerms = string.Join(" ", this.beforeTerminals.Select(t => t.Name));
			this.description = string.Format("{0} if {1} comes before {2}.", this.actionType, this.firstTerminal.Name, beforeTerms);
		}

		public override string ToString()
		{
			if (this.description == null)
				this.description = this.actionType.ToString() + " if ...";

			return this.description;
		}

		/// <summary>
		/// Check if there is an action already in state for this term; if yes, and it is Conditional action,
		/// then simply add an extra conditional entry to it. If an action does not exist, or it is not conditional,
		/// create new conditional action for this term.
		/// </summary>
		/// <param name="state"></param>
		/// <param name="term"></param>
		/// <param name="entry"></param>
		private void AddConditionalEntry(ParserState state, BnfTerm term, ConditionalEntry entry)
		{
			ParserAction oldAction;
			ConditionalParserAction condAction = null;

			if (state.Actions.TryGetValue(term, out oldAction))
				condAction = oldAction as ConditionalParserAction;

			if (condAction == null)
			{
				// There's no old action, or it is not conditional; create new conditional action
				condAction = new ConditionalParserAction();
				condAction.DefaultAction = oldAction;
				state.Actions[term] = condAction;
			}

			condAction.ConditionalEntries.Add(entry);

			if (condAction.DefaultAction == null)
				condAction.DefaultAction = this.FindDefaultAction(state, term);

			if (condAction.DefaultAction == null)
				// If still no action, then use the cond. action as default.
				condAction.DefaultAction = entry.Action;
		}

		private bool CheckCondition(ParsingContext context)
		{
			var scanner = context.Parser.Scanner;

			try
			{
				var eof = this.Grammar.Eof;
				var count = 0;
				scanner.BeginPreview();
				var token = scanner.GetToken();

				while (token != null && token.Terminal != eof)
				{
					if (token.Terminal == this.firstTerminal)
						// Found!
						return true;

					if (this.beforeTerminals.Contains(token.Terminal))
						return false;

					if (++count > this.MaxPreviewTokens && this.MaxPreviewTokens > 0)
						return false;

					token = scanner.GetToken();
				}

				return false;
			}
			finally
			{
				scanner.EndPreview(true);
			}
		}

		/// <summary>
		/// Find an LR item without hints compatible with term (either shift on term or reduce with term as lookahead);
		/// this item without hints would become our default. We assume that other items have hints, and when conditions
		/// on all these hints fail, we chose this remaining item without hints.
		/// </summary>
		/// <param name="state"></param>
		/// <param name="term"></param>
		/// <returns></returns>
		private ParserAction FindDefaultAction(ParserState state, BnfTerm term)
		{
			// First check reduce items
			var reduceItems = state.BuilderData.ReduceItems.SelectByLookahead(term as Terminal);
			foreach (var item in reduceItems)
			{
				if (item.Core.Hints.Count == 0)
					return ReduceParserAction.Create(item.Core.Production);
			}

			var shiftItem = state.BuilderData.ShiftItems.SelectByCurrent(term).FirstOrDefault();
			if (shiftItem != null)
				return new ShiftParserAction(shiftItem);

			// If everything failed, returned first reduce item
			return null;
		}
	}
}
