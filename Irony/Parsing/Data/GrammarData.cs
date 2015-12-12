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

namespace Irony.Parsing
{
	/// <summary>
	/// <see cref="GrammarData"/> is a container for all basic info about the grammar
	/// <para />
	/// <see cref="GrammarData"/> is a field in LanguageData object.
	/// </summary>
	public class GrammarData
	{
		public readonly BnfTermSet AllTerms = new BnfTermSet();

		public readonly Grammar Grammar;

		public readonly LanguageData Language;

		public readonly NonTerminalSet NonTerminals = new NonTerminalSet();

		public readonly TerminalSet Terminals = new TerminalSet();

		public NonTerminal AugmentedRoot;

		public NonTerminalSet AugmentedSnippetRoots = new NonTerminalSet();

		/// <summary>
		/// Terminals that have no limited set of prefixes
		/// </summary>
		public TerminalSet NoPrefixTerminals = new TerminalSet();

		public GrammarData(LanguageData language)
		{
			this.Language = language;
			this.Grammar = language.Grammar;
		}
	}
}
