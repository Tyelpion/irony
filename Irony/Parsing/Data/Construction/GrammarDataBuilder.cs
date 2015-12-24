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

namespace Irony.Parsing.Construction
{
	internal class GrammarDataBuilder
	{
		/// <summary>
		/// Each LR0Item gets its unique ID, last assigned (max) Id is kept in this field
		/// </summary>
		internal int lastItemId;

		private Grammar grammar;
		private GrammarData grammarData;
		private LanguageData language;

		/// <summary>
		/// Internal counter for generating names for unnamed non-terminals
		/// </summary>
		private int unnamedCount;

		internal GrammarDataBuilder(LanguageData language)
		{
			this.language = language;
			this.grammar = this.language.Grammar;
		}

		internal void Build()
		{
			this.grammarData = this.language.GrammarData;

			this.CreateAugmentedRoots();
			this.CollectTermsFromGrammar();
			this.InitTermLists();
			this.FillOperatorReportGroup();
			this.CreateProductions();

			ComputeNonTerminalsNullability(this.grammarData);
			ComputeTailsNullability(this.grammarData);

			this.ValidateGrammar();
		}

		private static void ComputeNonTerminalsNullability(GrammarData data)
		{
			var undecided = data.NonTerminals;
			while (undecided.Count > 0)
			{
				var newUndecided = new NonTerminalSet();

				foreach (NonTerminal nt in undecided)
				{
					if (!ComputeNullability(nt))
						newUndecided.Add(nt);
				}

				// We didn't decide on any new, so we're done
				if (undecided.Count == newUndecided.Count) return;

				undecided = newUndecided;
			}
		}

		private static bool ComputeNullability(NonTerminal nonTerminal)
		{
			foreach (Production prod in nonTerminal.Productions)
			{
				if (prod.RValues.Count == 0)
				{
					nonTerminal.SetFlag(TermFlags.IsNullable);

					// Decided, Nullable
					return true;
				}

				// If production has terminals, it is not nullable and cannot contribute to nullability
				if (prod.Flags.IsSet(ProductionFlags.HasTerminals))
					continue;

				// Go thru all elements of production and check nullability
				var allNullable = true;

				foreach (BnfTerm child in prod.RValues)
				{
					allNullable &= child.Flags.IsSet(TermFlags.IsNullable);
				}

				if (allNullable)
				{
					nonTerminal.SetFlag(TermFlags.IsNullable);
					return true;
				}
			}

			// Cannot decide
			return false;
		}

		private static void ComputeTailsNullability(GrammarData data)
		{
			foreach (var nt in data.NonTerminals)
			{
				foreach (var prod in nt.Productions)
				{
					var count = prod.LR0Items.Count;
					for (int i = count - 1; i >= 0; i--)
					{
						var item = prod.LR0Items[i];
						item.TailIsNullable = true;

						if (item.Current == null)
							continue;

						if (!item.Current.Flags.IsSet(TermFlags.IsNullable))
							break;
					}
				}
			}
		}

		private void CollectTermsFromGrammar()
		{
			this.unnamedCount = 0;
			this.grammarData.AllTerms.Clear();

			// Start with NonGrammarTerminals, and set IsNonGrammar flag
			foreach (Terminal t in this.grammarData.Grammar.NonGrammarTerminals)
			{
				t.SetFlag(TermFlags.IsNonGrammar);
				this.grammarData.AllTerms.Add(t);
			}

			// Add main root
			this.CollectTermsRecursive(this.grammarData.AugmentedRoot);

			foreach (var augmRoot in this.grammarData.AugmentedSnippetRoots)
			{
				this.CollectTermsRecursive(augmRoot);
			}

			// Add syntax error explicitly
			this.grammarData.AllTerms.Add(this.grammar.SyntaxError);
		}

		private void CollectTermsRecursive(BnfTerm term)
		{
			if (this.grammarData.AllTerms.Contains(term))
				return;

			this.grammarData.AllTerms.Add(term);

			var nt = term as NonTerminal;
			if (nt == null)
				return;

			if (string.IsNullOrEmpty(nt.Name))
			{
				if (nt.Rule != null && !string.IsNullOrEmpty(nt.Rule.Name))
					nt.Name = nt.Rule.Name;
				else
					nt.Name = "Unnamed" + (this.unnamedCount++);
			}

			if (nt.Rule == null)
				this.language.Errors.AddAndThrow(GrammarErrorLevel.Error, null, Resources.ErrNtRuleIsNull, nt.Name);

			// Check all child elements
			foreach (BnfTermList elemList in nt.Rule.Data)
			{
				for (int i = 0; i < elemList.Count; i++)
				{
					BnfTerm child = elemList[i];
					if (child == null)
					{
						this.language.Errors.Add(GrammarErrorLevel.Error, null, Resources.ErrRuleContainsNull, nt.Name, i);
						continue;
					}

					// Check for nested expression - convert to non-terminal
					var expr = child as BnfExpression;
					if (expr != null)
					{
						child = new NonTerminal(null, expr);
						elemList[i] = child;
					}

					this.CollectTermsRecursive(child);
				}
			}
		}

		private void ComputeProductionFlags(Production production)
		{
			production.Flags = ProductionFlags.None;

			foreach (var rv in production.RValues)
			{
				// Check if it is a Terminal or Error element
				var t = rv as Terminal;
				if (t != null)
				{
					production.Flags |= ProductionFlags.HasTerminals;
					if (t.Category == TokenCategory.Error) production.Flags |= ProductionFlags.IsError;
				}

				if (rv.Flags.IsSet(TermFlags.IsPunctuation))
					continue;
			}
		}

		private NonTerminal CreateAugmentedRoot(NonTerminal root)
		{
			var result = new NonTerminal(root.Name + "'", root + this.grammar.Eof);

			// Mark that we don't need AST node here
			result.SetFlag(TermFlags.NoAstNode);

			return result;
		}

		private void CreateAugmentedRoots()
		{
			this.grammarData.AugmentedRoot = this.CreateAugmentedRoot(this.grammar.Root);

			foreach (var snippetRoot in this.grammar.SnippetRoots)
			{
				this.grammarData.AugmentedSnippetRoots.Add(this.CreateAugmentedRoot(snippetRoot));
			}
		}

		private Production CreateProduction(NonTerminal lvalue, BnfTermList operands)
		{
			var prod = new Production(lvalue);
			GrammarHintList hints = null;

			// Create RValues list skipping Empty terminal and collecting grammar hints
			foreach (BnfTerm operand in operands)
			{
				if (operand == this.grammar.Empty)
					continue;

				// Collect hints as we go - they will be added to the next non-hint element
				var hint = operand as GrammarHint;
				if (hint != null)
				{
					if (hints == null)
						hints = new GrammarHintList();

					hints.Add(hint);
					continue;
				}

				// Add the operand and create LR0 Item
				prod.RValues.Add(operand);
				prod.LR0Items.Add(new LR0Item(this.lastItemId++, prod, prod.RValues.Count - 1, hints));
				hints = null;
			}

			// Set the flags
			if (prod.RValues.Count == 0)
				prod.Flags |= ProductionFlags.IsEmpty;

			// Add final LRItem
			this.ComputeProductionFlags(prod);
			prod.LR0Items.Add(new LR0Item(this.lastItemId++, prod, prod.RValues.Count, hints));

			return prod;
		}

		private void CreateProductions()
		{
			this.lastItemId = 0;

			// CheckWrapTailHints() method may add non-terminals on the fly, so we have to use for loop here (not foreach)
			foreach (var nt in this.grammarData.NonTerminals)
			{
				nt.Productions.Clear();

				// Get data (sequences) from both Rule and ErrorRule
				var allData = new BnfExpressionData();
				allData.AddRange(nt.Rule.Data);

				if (nt.ErrorRule != null)
					allData.AddRange(nt.ErrorRule.Data);

				// Actually create productions for each sequence
				foreach (BnfTermList prodOperands in allData)
				{
					var prod = this.CreateProduction(nt, prodOperands);
					nt.Productions.Add(prod);
				}
			}
		}

		private void FillOperatorReportGroup()
		{
			foreach (var group in this.grammar.TermReportGroups)
			{
				if (group.GroupType == TermReportGroupType.Operator)
				{
					foreach (var term in this.grammarData.Terminals)
					{
						if (term.Flags.IsSet(TermFlags.IsOperator))
							group.Terminals.Add(term);
					}

					return;
				}
			}
		}

		private void InitTermLists()
		{
			// Collect terminals and NonTerminals
			var empty = this.grammar.Empty;

			foreach (BnfTerm term in this.grammarData.AllTerms)
			{
				// Remember - we may have hints, so it's not only terminals and non-terminals
				if (term is NonTerminal)
					this.grammarData.NonTerminals.Add((NonTerminal) term);

				if (term is Terminal && term != empty)
					this.grammarData.Terminals.Add((Terminal) term);
			}

			// Mark keywords - any "word" symbol directly mentioned in the grammar
			foreach (var term in this.grammarData.Terminals)
			{
				var symTerm = term as KeyTerm;
				if (symTerm == null)
					continue;

				if (!string.IsNullOrEmpty(symTerm.Text) && char.IsLetter(symTerm.Text[0]))
					symTerm.SetFlag(TermFlags.IsKeyword);
			}

			// Init all terms
			foreach (var term in this.grammarData.AllTerms)
			{
				term.Init(this.grammarData);
			}
		}

		#region Grammar Validation

		private int CountNonPunctuationTerms(Production production)
		{
			var count = 0;

			foreach (var rvalue in production.RValues)
			{
				if (!rvalue.Flags.IsSet(TermFlags.IsPunctuation))
					count++;
			}

			return count;
		}

		private void ValidateGrammar()
		{
			var createAst = this.grammar.LanguageFlags.IsSet(LanguageFlags.CreateAst);
			var invalidTransSet = new NonTerminalSet();

			foreach (var nt in this.grammarData.NonTerminals)
			{
				if (nt.Flags.IsSet(TermFlags.IsTransient))
				{
					// List non-terminals cannot be marked transient - otherwise there may be some ambiguities and inconsistencies
					if (nt.Flags.IsSet(TermFlags.IsList))
						this.language.Errors.Add(GrammarErrorLevel.Error, null, Resources.ErrListCannotBeTransient, nt.Name);

					// Count number of non-punctuation child nodes in each production
					foreach (var prod in nt.Productions)
					{
						if (this.CountNonPunctuationTerms(prod) > 1)
							invalidTransSet.Add(nt);
					}
				}

				// Validate error productions
				foreach (var prod in nt.Productions)
				{
					if (prod.Flags.IsSet(ProductionFlags.IsError))
					{
						var lastTerm = prod.RValues[prod.RValues.Count - 1];

						if (!(lastTerm is Terminal) || lastTerm == this.grammar.SyntaxError)
							this.language.Errors.Add(GrammarErrorLevel.Warning, null, Resources.ErrLastTermOfErrorProd, nt.Name);

						// "The last term of error production must be a terminal. NonTerminal: {0}"
					}
				}
			}

			if (invalidTransSet.Count > 0)
				this.language.Errors.Add(GrammarErrorLevel.Error, null, Resources.ErrTransientNtMustHaveOneTerm, invalidTransSet.ToString());
		}

		#endregion Grammar Validation
	}
}
