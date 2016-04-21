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
	#region About compound terminals

	/*
		As  it turns out, many terminal types in real-world languages have 3-part structure: prefix-body-suffix
		The body is essentially the terminal "value", while prefix and suffix are used to specify additional
		information (options), while not  being a part of the terminal itself.
		For example:
		1. c# numbers, may have 0x prefix for hex representation, and suffixes specifying
			the exact data type of the literal (f, l, m, etc)
		2. c# string may have "@" prefix which disables escaping inside the string
		3. c# identifiers may have "@" prefix and escape sequences inside - just like strings
		4. Python string may have "u" and "r" prefixes, "r" working the same way as @ in c# strings
		5. VB string literals may have "c" suffix identifying that the literal is a character, not a string
		6. VB number literals and identifiers may have suffixes identifying data type

		So it seems like all these terminals have the format "prefix-body-suffix".
		The CompoundTerminalBase base class implements base functionality supporting this multi-part structure.
		The IdentifierTerminal, NumberLiteral and StringLiteral classes inherit from this base class.
		The methods in TerminalFactory static class demonstrate that with this architecture we can define the whole
		variety of terminals for c#, Python and VB.NET languages.
	*/

	#endregion About compound terminals

	public abstract class CompoundTerminalBase : Terminal
	{
		#region Nested classes

		public class CompoundTokenDetails
		{
			public string Body;
			public string EndSymbol;
			public string Error;

			/// <summary>
			/// Exponent symbol for Number literal
			/// </summary>
			public string ExponentSymbol;

			/// <summary>
			/// Need to be short, because we need to save it in Scanner state for Vs integration
			/// </summary>
			public short Flags;

			public bool IsPartial;
			public bool PartialContinues;

			/// <summary>
			/// Partial token info, used by VS integration
			/// </summary>
			public bool PartialOk;

			public string Prefix;
			public string Sign;

			/// <summary>
			/// String start and end symbols
			/// </summary>
			public string StartSymbol;

			/// <summary>
			/// Used for string literal kind
			/// </summary>
			public byte SubTypeIndex;

			public string Suffix;
			public TypeCode[] TypeCodes;
			public object Value;

			public string Text { get { return this.Prefix + this.Body + this.Suffix; } }

			/// <summary>
			/// Flags helper method
			/// </summary>
			/// <param name="flag"></param>
			/// <returns></returns>
			public bool IsSet(short flag)
			{
				return (this.Flags & flag) != 0;
			}
		}

		protected class ScanFlagTable : Dictionary<string, short> { }

		protected class TypeCodeTable : Dictionary<string, TypeCode[]> { }

		#endregion Nested classes

		#region constructors and initialization

		protected CompoundTerminalBase(string name) : this(name, TermFlags.None)
		{
		}

		protected CompoundTerminalBase(string name, TermFlags flags) : base(name)
		{
			this.SetFlag(flags);
			this.Escapes = GetDefaultEscapes();
		}

		public void AddSuffix(string suffix, params TypeCode[] typeCodes)
		{
			this.SuffixTypeCodes.Add(suffix, typeCodes);
			this.Suffixes.Add(suffix);
		}

		protected void AddPrefixFlag(string prefix, short flags)
		{
			this.PrefixFlags.Add(prefix, flags);
			this.Prefixes.Add(prefix);
		}

		#endregion constructors and initialization

		#region public Properties/Fields

		/// <summary>
		/// Case sensitivity for prefixes and suffixes
		/// </summary>
		public bool CaseSensitivePrefixesSuffixes;

		public Char EscapeChar = '\\';
		public EscapeTable Escapes = new EscapeTable();

		#endregion public Properties/Fields

		#region private fields

		protected readonly ScanFlagTable PrefixFlags = new ScanFlagTable();
		protected readonly TypeCodeTable SuffixTypeCodes = new TypeCodeTable();
		protected StringList Prefixes = new StringList();
		protected StringList Suffixes = new StringList();

		/// <summary>
		/// First chars of all prefixes, for fast prefix detection
		/// </summary>
		private CharHashSet prefixesFirsts;

		/// <summary>
		/// First chars of all suffixes, for fast suffix detection
		/// </summary>
		private CharHashSet suffixesFirsts;

		#endregion private fields

		#region overrides: Init, TryMatch

		public override IList<string> GetFirsts()
		{
			return this.Prefixes;
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);

			// Collect all suffixes, prefixes in lists and create sets of first chars for both
			this.Prefixes.Sort(StringList.LongerFirst);
			this.Suffixes.Sort(StringList.LongerFirst);

			this.prefixesFirsts = new CharHashSet(this.CaseSensitivePrefixesSuffixes);
			this.suffixesFirsts = new CharHashSet(this.CaseSensitivePrefixesSuffixes);

			foreach (string pfx in Prefixes)
			{
				this.prefixesFirsts.Add(pfx[0]);
			}

			foreach (string sfx in Suffixes)
			{
				this.suffixesFirsts.Add(sfx[0]);
			}
		}

		public override Token TryMatch(ParsingContext context, ISourceStream source)
		{
			Token token;

			// Try quick parse first, but only if we're not continuing
			if (context.VsLineScanState.Value == 0)
			{
				token = QuickParse(context, source);
				if (token != null)
					return token;

				// Revert the position
				source.PreviewPosition = source.Position;
			}

			var details = new CompoundTokenDetails();
			this.InitDetails(context, details);

			if (context.VsLineScanState.Value == 0)
				this.ReadPrefix(source, details);

			if (!this.ReadBody(source, details))
				return null;

			if (details.Error != null)
				return context.CreateErrorToken(details.Error);

			if (details.IsPartial)
			{
				details.Value = details.Body;
			}
			else
			{
				this.ReadSuffix(source, details);

				if (!this.ConvertValue(details))
				{
					if (string.IsNullOrEmpty(details.Error))
						details.Error = Resources.ErrInvNumber;

					// "Failed to convert the value: {0}"
					return context.CreateErrorToken(details.Error);
				}
			}

			token = CreateToken(context, source, details);

			if (details.IsPartial)
			{
				// Save terminal state so we can continue
				context.VsLineScanState.TokenSubType = (byte) details.SubTypeIndex;
				context.VsLineScanState.TerminalFlags = (short) details.Flags;
				context.VsLineScanState.TerminalIndex = this.MultilineIndex;
			}
			else
				context.VsLineScanState.Value = 0;

			return token;
		}

		protected virtual bool ConvertValue(CompoundTokenDetails details)
		{
			details.Value = details.Body;
			return false;
		}

		protected virtual Token CreateToken(ParsingContext context, ISourceStream source, CompoundTokenDetails details)
		{
			var token = source.CreateToken(this.OutputTerminal, details.Value);
			token.Details = details;
			if (details.IsPartial)
				token.Flags |= TokenFlags.IsIncomplete;

			return token;
		}

		protected virtual void InitDetails(ParsingContext context, CompoundTokenDetails details)
		{
			details.PartialOk = (context.Mode == ParseMode.VsLineScan);
			details.PartialContinues = (context.VsLineScanState.Value != 0);
		}

		protected virtual Token QuickParse(ParsingContext context, ISourceStream source)
		{
			return null;
		}

		protected virtual bool ReadBody(ISourceStream source, CompoundTokenDetails details)
		{
			return false;
		}

		protected virtual void ReadPrefix(ISourceStream source, CompoundTokenDetails details)
		{
			if (!this.prefixesFirsts.Contains(source.PreviewChar))
				return;

			var comparisonType = CaseSensitivePrefixesSuffixes ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;
			foreach (string pfx in Prefixes)
			{
				// Prefixes are usually case insensitive, even if language is case-sensitive. So we cannot use source.MatchSymbol here,
				// we need case-specific comparison
				if (string.Compare(source.Text, source.PreviewPosition, pfx, 0, pfx.Length, comparisonType) != 0)
					continue;

				// We found prefix
				details.Prefix = pfx;
				source.PreviewPosition += pfx.Length;

				// Set flag from prefix
				short pfxFlags;
				if (!string.IsNullOrEmpty(details.Prefix) && this.PrefixFlags.TryGetValue(details.Prefix, out pfxFlags))
					details.Flags |= (short) pfxFlags;

				return;
			}
		}

		protected virtual void ReadSuffix(ISourceStream source, CompoundTokenDetails details)
		{
			if (!this.suffixesFirsts.Contains(source.PreviewChar))
				return;

			var comparisonType = CaseSensitivePrefixesSuffixes ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;
			foreach (string sfx in Suffixes)
			{
				// Suffixes are usually case insensitive, even if language is case-sensitive. So we cannot use source.MatchSymbol here,
				// we need case-specific comparison
				if (string.Compare(source.Text, source.PreviewPosition, sfx, 0, sfx.Length, comparisonType) != 0)
					continue;

				// We found suffix
				details.Suffix = sfx;
				source.PreviewPosition += sfx.Length;

				// Set TypeCode from suffix
				TypeCode[] codes;
				if (!string.IsNullOrEmpty(details.Suffix) && this.SuffixTypeCodes.TryGetValue(details.Suffix, out codes))
					details.TypeCodes = codes;

				return;
			}
		}

		#endregion overrides: Init, TryMatch

		#region utils: GetDefaultEscapes

		public static EscapeTable GetDefaultEscapes()
		{
			var escapes = new EscapeTable();
			escapes.Add('a', '\u0007');
			escapes.Add('b', '\b');
			escapes.Add('t', '\t');
			escapes.Add('n', '\n');
			escapes.Add('v', '\v');
			escapes.Add('f', '\f');
			escapes.Add('r', '\r');
			escapes.Add('"', '"');
			escapes.Add('\'', '\'');
			escapes.Add('\\', '\\');
			escapes.Add(' ', ' ');

			// This is a special escape of the linebreak itself,
			// when string ends with "\" char and continues on the next line
			escapes.Add('\n', '\n');

			return escapes;
		}

		#endregion utils: GetDefaultEscapes
	}

	public class EscapeTable : Dictionary<char, char> { }
}
