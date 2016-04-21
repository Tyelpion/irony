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
using Irony.Ast;

namespace Irony.Parsing
{
	[Flags]
	public enum StringOptions : short
	{
		None = 0,
		IsChar = 0x01,

		/// <summary>
		/// Convert doubled start/end symbol to a single symbol; for ex. in SQL, '' -> '
		/// </summary>
		AllowsDoubledQuote = 0x02,

		AllowsLineBreak = 0x04,

		/// <summary>
		/// Can include embedded expressions that should be evaluated on the fly; ex in Ruby: "hello #{name}"
		/// </summary>
		IsTemplate = 0x08,

		NoEscapes = 0x10,
		AllowsUEscapes = 0x20,
		AllowsXEscapes = 0x40,
		AllowsOctalEscapes = 0x80,
		AllowsAllEscapes = AllowsUEscapes | AllowsXEscapes | AllowsOctalEscapes,
	}

	public class StringLiteral : CompoundTerminalBase
	{
		public enum StringFlagsInternal : short
		{
			HasEscapes = 0x100,
		}

		#region StringSubType

		private class StringSubType
		{
			internal readonly StringOptions Flags;
			internal readonly byte Index;
			internal readonly string Start, End;

			internal StringSubType(string start, string end, StringOptions flags, byte index)
			{
				this.Start = start;
				this.End = end;
				this.Flags = flags;
				this.Index = index;
			}

			internal static int LongerStartFirst(StringSubType x, StringSubType y)
			{
				try
				{
					// In case any of them is null
					if (x.Start.Length > y.Start.Length)
						return -1;
				}
				catch
				{ }

				return 0;
			}
		}

		private class StringSubTypeList : List<StringSubType>
		{
			internal void Add(string start, string end, StringOptions flags)
			{
				this.Add(new StringSubType(start, end, flags, (byte) this.Count));
			}
		}

		#endregion StringSubType

		#region constructors and initialization

		public StringLiteral(string name) : base(name)
		{
			this.SetFlag(TermFlags.IsLiteral);
		}

		public StringLiteral(string name, string startEndSymbol, StringOptions options) : this(name)
		{
			this.subtypes.Add(startEndSymbol, startEndSymbol, options);
		}

		public StringLiteral(string name, string startEndSymbol) : this(name, startEndSymbol, StringOptions.None)
		{
		}

		public StringLiteral(string name, string startEndSymbol, StringOptions options, Type astNodeType) : this(name, startEndSymbol, options)
		{
			this.AstConfig.NodeType = astNodeType;
		}

		public StringLiteral(string name, string startEndSymbol, StringOptions options, AstNodeCreator astNodeCreator) : this(name, startEndSymbol, options)
		{
			this.AstConfig.NodeCreator = astNodeCreator;
		}

		public void AddPrefix(string prefix, StringOptions flags)
		{
			this.AddPrefixFlag(prefix, (short) flags);
		}

		public void AddStartEnd(string startEndSymbol, StringOptions stringOptions)
		{
			this.AddStartEnd(startEndSymbol, startEndSymbol, stringOptions);
		}

		public void AddStartEnd(string startSymbol, string endSymbol, StringOptions stringOptions)
		{
			this.subtypes.Add(startSymbol, endSymbol, stringOptions);
		}

		#endregion constructors and initialization

		#region Properties/Fields

		private readonly StringSubTypeList subtypes = new StringSubTypeList();

		/// <summary>
		/// First chars  of start-end symbols
		/// </summary>
		private string startSymbolsFirsts;

		#endregion Properties/Fields

		#region overrides: Init, GetFirsts, ReadBody, etc...

		public override IList<string> GetFirsts()
		{
			var result = new StringList();
			result.AddRange(Prefixes);

			// We assume that prefix is always optional, so string can start with start-end symbol
			foreach (char ch in this.startSymbolsFirsts)
			{
				result.Add(ch.ToString());
			}

			return result;
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);

			this.startSymbolsFirsts = string.Empty;
			if (this.subtypes.Count == 0)
			{
				// "Error in string literal [{0}]: No start/end symbols specified."
				grammarData.Language.Errors.Add(GrammarErrorLevel.Error, null, Resources.ErrInvStrDef, this.Name);
				return;
			}

			// Collect all start-end symbols in lists and create strings of first chars
			// To detect duplicate start symbols
			var allStartSymbols = new StringSet();
			this.subtypes.Sort(StringSubType.LongerStartFirst);
			var isTemplate = false;

			foreach (StringSubType subType in this.subtypes)
			{
				if (allStartSymbols.Contains(subType.Start))
					// "Duplicate start symbol {0} in string literal [{1}]."
					grammarData.Language.Errors.Add(GrammarErrorLevel.Error, null, Resources.ErrDupStartSymbolStr, subType.Start, this.Name);

				allStartSymbols.Add(subType.Start);
				this.startSymbolsFirsts += subType.Start[0].ToString();

				isTemplate |= (subType.Flags & StringOptions.IsTemplate) != 0;
			}

			if (!this.CaseSensitivePrefixesSuffixes)
				this.startSymbolsFirsts = this.startSymbolsFirsts.ToLower() + this.startSymbolsFirsts.ToUpper();

			// Set multiline flag
			foreach (StringSubType info in this.subtypes)
			{
				if ((info.Flags & StringOptions.AllowsLineBreak) != 0)
				{
					this.SetFlag(TermFlags.IsMultiline);
					break;
				}
			}

			// For templates only
			if (isTemplate)
			{
				// Check that template settings object is provided
				var templateSettings = this.AstConfig.Data as StringTemplateSettings;
				if (templateSettings == null)
					// "Error in string literal [{0}]: IsTemplate flag is set, but TemplateSettings is not provided."
					grammarData.Language.Errors.Add(GrammarErrorLevel.Error, null, Resources.ErrTemplNoSettings, this.Name);
				else if (templateSettings.ExpressionRoot == null)
					// ""
					grammarData.Language.Errors.Add(GrammarErrorLevel.Error, null, Resources.ErrTemplMissingExprRoot, this.Name);
				else if (!Grammar.SnippetRoots.Contains(templateSettings.ExpressionRoot))
					// ""
					grammarData.Language.Errors.Add(GrammarErrorLevel.Error, null, Resources.ErrTemplExprNotRoot, this.Name);
			}

			// Create editor info
			if (this.EditorInfo == null)
				this.EditorInfo = new TokenEditorInfo(TokenType.String, TokenColor.String, TokenTriggers.None);
		}

		/// <summary>
		/// Extract the string content from lexeme, adjusts the escaped and double-end symbols
		/// </summary>
		/// <param name="details"></param>
		/// <returns></returns>
		protected override bool ConvertValue(CompoundTokenDetails details)
		{
			string value = details.Body;
			var escapeEnabled = !details.IsSet((short) StringOptions.NoEscapes);

			// Fix all escapes
			if (escapeEnabled && value.IndexOf(EscapeChar) >= 0)
			{
				details.Flags |= (int) StringFlagsInternal.HasEscapes;
				var arr = value.Split(EscapeChar);
				var ignoreNext = false;

				// We skip the 0 element as it is not preceeded by "\"
				for (int i = 1; i < arr.Length; i++)
				{
					if (ignoreNext)
					{
						ignoreNext = false;
						continue;
					}

					string s = arr[i];
					if (string.IsNullOrEmpty(s))
					{
						// It is "\\" - escaped escape symbol.
						arr[i] = @"\";
						ignoreNext = true;
						continue;
					}

					// The char is being escaped is the first one; replace it with char in Escapes table
					char first = s[0];
					char newFirst;

					if (Escapes.TryGetValue(first, out newFirst))
						arr[i] = newFirst + s.Substring(1);
					else
						arr[i] = HandleSpecialEscape(arr[i], details);
				}
				value = string.Join(string.Empty, arr);
			}

			// Check for doubled end symbol
			string endSymbol = details.EndSymbol;
			if (details.IsSet((short) StringOptions.AllowsDoubledQuote) && value.IndexOf(endSymbol) >= 0)
				value = value.Replace(endSymbol + endSymbol, endSymbol);

			if (details.IsSet((short) StringOptions.IsChar))
			{
				if (value.Length != 1)
				{
					// "Invalid length of char literal - should be a single character.";
					details.Error = Resources.ErrBadChar;
					return false;
				}

				details.Value = value[0];
			}
			else
			{
				details.TypeCodes = new TypeCode[] { TypeCode.String };
				details.Value = value;
			}

			return true;
		}

		/// <summary>
		/// Should support:  \Udddddddd, \udddd, \xdddd, \N{name}, \0, \ddd (octal),
		/// </summary>
		/// <param name="segment"></param>
		/// <param name="details"></param>
		/// <returns></returns>
		protected virtual string HandleSpecialEscape(string segment, CompoundTokenDetails details)
		{
			if (string.IsNullOrEmpty(segment))
				return string.Empty;

			int len, p;
			string digits;
			char ch;
			string result;
			char first = segment[0];

			switch (first)
			{
				case 'u':
				case 'U':
					if (details.IsSet((short) StringOptions.AllowsUEscapes))
					{
						len = (first == 'u' ? 4 : 8);
						if (segment.Length < len + 1)
						{
							// "Invalid unicode escape ({0}), expected {1} hex digits."
							details.Error = string.Format(Resources.ErrBadUnEscape, segment.Substring(len + 1), len);
							return segment;
						}

						digits = segment.Substring(1, len);
						ch = (char) Convert.ToUInt32(digits, 16);
						result = ch + segment.Substring(len + 1);

						return result;
					}
					break;

				case 'x':
					if (details.IsSet((short) StringOptions.AllowsXEscapes))
					{
						// x-escape allows variable number of digits, from one to 4; let's count them
						// current position
						p = 1;

						while (p < 5 && p < segment.Length)
						{
							if (Strings.HexDigits.IndexOf(segment[p]) < 0)
								break;

							p++;
						}

						// p now point to char right after the last digit
						if (p <= 1)
						{
							// @"Invalid \x escape, at least one digit expected.";
							details.Error = Resources.ErrBadXEscape;
							return segment;
						}

						digits = segment.Substring(1, p - 1);
						ch = (char) Convert.ToUInt32(digits, 16);
						result = ch + segment.Substring(p);

						return result;
					}
					break;

				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
					if (details.IsSet((short) StringOptions.AllowsOctalEscapes))
					{
						// Octal escape allows variable number of digits, from one to 3; let's count them
						// Current position
						p = 0;
						while (p < 3 && p < segment.Length)
						{
							if (Strings.OctalDigits.IndexOf(segment[p]) < 0)
								break;

							p++;
						}

						// p now point to char right after the last digit
						digits = segment.Substring(0, p);
						ch = (char) Convert.ToUInt32(digits, 8);
						result = ch + segment.Substring(p);

						return result;
					}
					break;
			}

			// "Invalid escape sequence: \{0}"
			details.Error = string.Format(Resources.ErrInvEscape, segment);
			return segment;
		}

		protected override void InitDetails(ParsingContext context, CompoundTerminalBase.CompoundTokenDetails details)
		{
			base.InitDetails(context, details);
			if (context.VsLineScanState.Value != 0)
			{
				// We are continuing partial string on the next line
				details.Flags = context.VsLineScanState.TerminalFlags;
				details.SubTypeIndex = context.VsLineScanState.TokenSubType;

				var stringInfo = this.subtypes[context.VsLineScanState.TokenSubType];
				details.StartSymbol = stringInfo.Start;
				details.EndSymbol = stringInfo.End;
			}
		}

		protected override bool ReadBody(ISourceStream source, CompoundTokenDetails details)
		{
			if (!details.PartialContinues)
			{
				if (!ReadStartSymbol(source, details))
					return false;
			}

			return CompleteReadBody(source, details);
		}

		protected override void ReadSuffix(ISourceStream source, CompoundTerminalBase.CompoundTokenDetails details)
		{
			base.ReadSuffix(source, details);

			// "char" type can be identified by suffix (like VB where c suffix identifies char)
			// in this case we have details.TypeCodes[0] == char  and we need to set the IsChar flag
			if (details.TypeCodes != null && details.TypeCodes[0] == TypeCode.Char)
				details.Flags |= (int) StringOptions.IsChar;
			else
			  // We may have IsChar flag set (from startEndSymbol, like in c# single quote identifies char)
			  // in this case set type code
			  if (details.IsSet((short) StringOptions.IsChar))
				details.TypeCodes = new TypeCode[] { TypeCode.Char };
		}

		private bool CompleteReadBody(ISourceStream source, CompoundTokenDetails details)
		{
			var escapeEnabled = !details.IsSet((short) StringOptions.NoEscapes);
			var start = source.PreviewPosition;
			var endQuoteSymbol = details.EndSymbol;

			// Doubled quote symbol
			var endQuoteDoubled = endQuoteSymbol + endQuoteSymbol;

			var lineBreakAllowed = details.IsSet((short) StringOptions.AllowsLineBreak);

			// 1. Find the string end
			// first get the position of the next line break; we are interested in it to detect malformed string,
			// therefore do it only if linebreak is NOT allowed; if linebreak is allowed, set it to -1 (we don't care).
			var nlPos = lineBreakAllowed ? -1 : source.Text.IndexOf('\n', source.PreviewPosition);

			// Fix by ashmind for EOF right after opening symbol
			while (true)
			{
				var endPos = source.Text.IndexOf(endQuoteSymbol, source.PreviewPosition);

				// Check for partial token in line-scanning mode
				if (endPos < 0 && details.PartialOk && lineBreakAllowed)
				{
					this.ProcessPartialBody(source, details);
					return true;
				}

				// Check for malformed string: either EndSymbol not found, or LineBreak is found before EndSymbol
				var malformed = endPos < 0 || nlPos >= 0 && nlPos < endPos;
				if (malformed)
				{
					// Set source position for recovery: move to the next line if linebreak is not allowed.
					if (nlPos > 0) endPos = nlPos;
					if (endPos > 0) source.PreviewPosition = endPos + 1;

					// "Mal-formed  string literal - cannot find termination symbol.";
					details.Error = Resources.ErrBadStrLiteral;

					// We did find start symbol, so it is definitely string, only malformed
					return true;
				}

				if (source.EOF())
					return true;

				// We found EndSymbol - check if it is escaped; if yes, skip it and continue search
				if (escapeEnabled && this.IsEndQuoteEscaped(source.Text, endPos))
				{
					source.PreviewPosition = endPos + endQuoteSymbol.Length;

					// Searching for end symbol
					continue;
				}

				// Check if it is doubled end symbol
				source.PreviewPosition = endPos;
				if (details.IsSet((short) StringOptions.AllowsDoubledQuote) && source.MatchSymbol(endQuoteDoubled))
				{
					source.PreviewPosition = endPos + endQuoteDoubled.Length;
					continue;
				}

				// Ok, this is normal endSymbol that terminates the string.
				// Advance source position and get out from the loop
				details.Body = source.Text.Substring(start, endPos - start);
				source.PreviewPosition = endPos + endQuoteSymbol.Length;

				// If we come here it means we're done - we found string end.
				return true;
			}
		}

		private bool IsEndQuoteEscaped(string text, int quotePosition)
		{
			var escaped = false;
			var p = quotePosition - 1;

			while (p > 0 && text[p] == this.EscapeChar)
			{
				escaped = !escaped;
				p--;
			}

			return escaped;
		}

		private void ProcessPartialBody(ISourceStream source, CompoundTokenDetails details)
		{
			int from = source.PreviewPosition;
			source.PreviewPosition = source.Text.Length;
			details.Body = source.Text.Substring(from, source.PreviewPosition - from);
			details.IsPartial = true;
		}

		private bool ReadStartSymbol(ISourceStream source, CompoundTokenDetails details)
		{
			if (this.startSymbolsFirsts.IndexOf(source.PreviewChar) < 0)
				return false;

			foreach (StringSubType subType in this.subtypes)
			{
				if (!source.MatchSymbol(subType.Start))
					continue;

				// We found start symbol
				details.StartSymbol = subType.Start;
				details.EndSymbol = subType.End;
				details.Flags |= (short) subType.Flags;
				details.SubTypeIndex = subType.Index;
				source.PreviewPosition += subType.Start.Length;

				return true;
			}

			return false;
		}

		#endregion overrides: Init, GetFirsts, ReadBody, etc...
	}

	/// <summary>
	/// Container for settings of tempate string parser, to interpet strings having embedded values or expressions
	/// like in Ruby:
	/// "Hello, #{name}"
	/// Default values match settings for Ruby strings
	/// </summary>
	public class StringTemplateSettings
	{
		public string EndTag = "}";
		public NonTerminal ExpressionRoot;
		public string StartTag = "#{";
	}
}
