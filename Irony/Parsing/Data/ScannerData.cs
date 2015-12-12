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
	/// <see cref="ScannerData"/> is a container for all detailed info needed by scanner to read input.
	/// </summary>
	public class ScannerData
	{
		public readonly LanguageData Language;

		public readonly TerminalList MultilineTerminals = new TerminalList();

		/// <summary>
		/// Hash table for fast lookup of non-grammar terminals by input char
		/// </summary>
		public readonly TerminalLookupTable NonGrammarTerminalsLookup = new TerminalLookupTable();

		/// <summary>
		/// Hash table for fast terminal lookup by input char
		/// </summary>
		public readonly TerminalLookupTable TerminalsLookup = new TerminalLookupTable();

		/// <summary>
		/// Terminals with no limited set of prefixes, copied from <see cref="GrammarData"/>.
		/// </summary>
		public TerminalList NoPrefixTerminals = new TerminalList();

		public ScannerData(LanguageData language)
		{
			this.Language = language;
		}
	}

	public class TerminalLookupTable : Dictionary<char, TerminalList> { }
}
