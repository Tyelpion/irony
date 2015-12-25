using System;

namespace Irony.Parsing
{
	/// <summary>
	/// Operator associativity types
	/// </summary>
	public enum Associativity
	{
		Left,
		Right,

		/// <summary>
		/// Honestly don't know what that means, but it is mentioned in literature
		/// </summary>
		Neutral
	}

	[Flags]
	public enum LanguageFlags
	{
		None = 0,

		/// <summary>
		/// Compilation options
		/// Be careful - use this flag ONLY if you use NewLine terminal in grammar explicitly!
		/// it happens only in line-based languages like Basic.
		/// </summary>
		NewLineBeforeEOF = 0x01,

		/// <summary>
		/// Emit LineStart token
		/// </summary>
		EmitLineStartToken = 0x02,

		/// <summary>
		/// In grammars that define TokenFilters (like Python) this flag should be set
		/// </summary>
		DisableScannerParserLink = 0x04,

		/// <summary>
		/// Create AST nodes
		/// </summary>
		CreateAst = 0x08,

		SupportsCommandLine = 0x0200,

		/// <summary>
		/// Tail-recursive language - Scheme is one example
		/// </summary>
		TailRecursive = 0x0400,

		SupportsBigInt = 0x01000,
		SupportsComplex = 0x02000,
		SupportsRational = 0x04000,

		/// <summary>
		/// Default value
		/// </summary>
		Default = None,
	}

	/// <summary>
	/// Used by Make-list-rule methods
	/// </summary>
	[Flags]
	public enum TermListOptions
	{
		None = 0,
		AllowEmpty = 0x01,
		AllowTrailingDelimiter = 0x02,

		/// <summary>
		/// In some cases this hint would help to resolve the conflicts that come up when you have two lists separated by a nullable term.
		/// This hint would resolve the conflict, telling the parser to include as many as possible elements in the first list, and the rest,
		/// if any, would go to the second list. By default, this flag is included in Star and Plus lists.
		/// </summary>
		AddPreferShiftHint = 0x04,

		/// <summary>
		/// Combinations - use these
		/// </summary>
		PlusList = AddPreferShiftHint,

		StarList = AllowEmpty | AddPreferShiftHint,
	}
}
