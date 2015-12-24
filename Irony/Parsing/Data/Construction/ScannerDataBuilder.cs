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

namespace Irony.Parsing.Construction
{
	internal class ScannerDataBuilder
	{
		private ScannerData data;
		private Grammar grammar;
		private GrammarData grammarData;
		private LanguageData language;

		internal ScannerDataBuilder(LanguageData language)
		{
			this.language = language;
			this.grammar = this.language.Grammar;
			this.grammarData = language.GrammarData;
		}

		internal void Build()
		{
			this.data = this.language.ScannerData;

			this.InitMultilineTerminalsList();
			this.ProcessNonGrammarTerminals();
			this.BuildTerminalsLookupTable();
		}

		private void AddTerminalToLookup(TerminalLookupTable lookup, Terminal term, IList<string> firsts)
		{
			foreach (string prefix in firsts)
			{
				if (string.IsNullOrEmpty(prefix))
				{
					this.language.Errors.Add(GrammarErrorLevel.Error, null, Resources.ErrTerminalHasEmptyPrefix, term.Name);
					continue;
				}

				// Calculate hash key for the prefix
				char firstChar = prefix[0];

				if (this.grammar.CaseSensitive)
					this.AddTerminalToLookupByFirstChar(lookup, term, firstChar);
				else
				{
					this.AddTerminalToLookupByFirstChar(lookup, term, char.ToLower(firstChar));
					this.AddTerminalToLookupByFirstChar(lookup, term, char.ToUpper(firstChar));
				}
			}
		}

		private void AddTerminalToLookupByFirstChar(TerminalLookupTable lookup, Terminal term, char firstChar)
		{
			TerminalList currentList;
			if (!lookup.TryGetValue(firstChar, out currentList))
			{
				// If list does not exist yet, create it
				currentList = new TerminalList();
				lookup[firstChar] = currentList;
			}

			// Add terminal to the list
			if (!currentList.Contains(term))
				currentList.Add(term);
		}

		private void BuildTerminalsLookupTable()
		{
			foreach (Terminal term in this.grammarData.Terminals)
			{
				// Non-grammar terminals are scanned in a separate step, before regular terminals; so we don't include them here
				if (term.Flags.IsSet(TermFlags.IsNonScanner | TermFlags.IsNonGrammar))
					continue;

				var firsts = term.GetFirsts();
				if (firsts == null || firsts.Count == 0)
				{
					this.grammarData.NoPrefixTerminals.Add(term);
					continue;
				}

				this.AddTerminalToLookup(this.data.TerminalsLookup, term, firsts);
			}

			if (this.grammarData.NoPrefixTerminals.Count > 0)
			{
				// Copy them to Scanner data
				this.data.NoPrefixTerminals.AddRange(this.grammarData.NoPrefixTerminals);

				// Sort in reverse priority order
				this.data.NoPrefixTerminals.Sort(Terminal.ByPriorityReverse);

				// Now add Fallback terminals to every list, then sort lists by reverse priority
				// so that terminal with higher priority comes first in the list
				foreach (TerminalList list in this.data.TerminalsLookup.Values)
				{
					foreach (var ft in this.data.NoPrefixTerminals)
					{
						if (!list.Contains(ft))
							list.Add(ft);
					}
				}
			}

			// Finally sort every list in terminals lookup table
			foreach (TerminalList list in this.data.TerminalsLookup.Values)
			{
				if (list.Count > 1)
					list.Sort(Terminal.ByPriorityReverse);
			}
		}

		private void InitMultilineTerminalsList()
		{
			foreach (var terminal in this.grammarData.Terminals)
			{
				if (terminal.Flags.IsSet(TermFlags.IsNonScanner))
					continue;

				if (terminal.Flags.IsSet(TermFlags.IsMultiline))
				{
					this.data.MultilineTerminals.Add(terminal);
					terminal.MultilineIndex = (byte) (this.data.MultilineTerminals.Count);
				}
			}
		}

		private void ProcessNonGrammarTerminals()
		{
			foreach (var term in this.grammar.NonGrammarTerminals)
			{
				var firsts = term.GetFirsts();
				if (firsts == null || firsts.Count == 0)
				{
					this.language.Errors.Add(GrammarErrorLevel.Error, null, Resources.ErrTerminalHasEmptyPrefix, term.Name);
					continue;
				}

				this.AddTerminalToLookup(this.data.NonGrammarTerminalsLookup, term, firsts);
			}

			// Sort each list
			foreach (var list in this.data.NonGrammarTerminalsLookup.Values)
			{
				if (list.Count > 1)
					list.Sort(Terminal.ByPriorityReverse);
			}
		}
	}
}
