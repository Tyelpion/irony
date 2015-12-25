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
using System.Linq;
using System.Text.RegularExpressions;

namespace Irony.Parsing
{
	[Flags]
	public enum RegexTermOptions
	{
		None = 0,

		/// <summary>
		/// If not set (default) then any following letter (after legal switches) is reported as invalid switch
		/// </summary>
		AllowLetterAfter = 0x01,

		/// <summary>
		/// If set, token.Value contains Regex object; otherwise, it contains a pattern (string)
		/// </summary>
		CreateRegExObject = 0x02,

		/// <summary>
		/// Require unique switches
		/// </summary>
		UniqueSwitches = 0x04,

		Default = CreateRegExObject | UniqueSwitches,
	}

	/// <summary>
	/// Regular expression literal, like javascript literal:   /abc?/i
	/// Allows optional switches
	/// example:
	///  regex = /abc\\\/de/
	///  matches fragments like  "abc\/de"
	/// Note: switches are returned in token.Details field. Unlike in StringLiteral, we don't need to unescape the escaped chars,
	/// (this is the job of regex engine), we only need to correctly recognize the end of expression
	/// </summary>
	public class RegexLiteral : Terminal
	{
		public RegexOptions DefaultOptions = RegexOptions.None;

		public Char EndSymbol = '/';

		public Char EscapeSymbol = '\\';

		public RegexTermOptions Options = RegexTermOptions.Default;

		public Char StartSymbol = '/';

		public RegexSwitchTable Switches = new RegexSwitchTable();

		private char[] stopChars;

		public RegexLiteral(string name) : base(name)
		{
			this.Switches.Add('i', RegexOptions.IgnoreCase);
			this.Switches.Add('g', RegexOptions.None); // Not sure what to do with this flag? anybody, any advice?
			this.Switches.Add('m', RegexOptions.Multiline);
			this.SetFlag(TermFlags.IsLiteral);
		}

		public RegexLiteral(string name, char startEndSymbol, char escapeSymbol) : base(name)
		{
			this.StartSymbol = startEndSymbol;
			this.EndSymbol = startEndSymbol;
			this.EscapeSymbol = escapeSymbol;
		}

		public override IList<string> GetFirsts()
		{
			var result = new StringList();
			result.Add(this.StartSymbol.ToString());

			return result;
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);
			this.stopChars = new char[] { this.EndSymbol, '\r', '\n' };
		}

		public bool IsSet(RegexTermOptions option)
		{
			return (this.Options & option) != 0;
		}

		public override Token TryMatch(ParsingContext context, ISourceStream source)
		{
			while (true)
			{
				// Find next position
				var newPos = source.Text.IndexOfAny(this.stopChars, source.PreviewPosition + 1);

				// We either didn't find it
				if (newPos == -1)
					// "No end symbol for regex literal."
					return context.CreateErrorToken(Resources.ErrNoEndForRegex);

				source.PreviewPosition = newPos;
				if (source.PreviewChar != this.EndSymbol)
					// We hit CR or LF, this is an error
					return context.CreateErrorToken(Resources.ErrNoEndForRegex);

				if (!this.CheckEscaped(source))
					break;
			}

			// Move after end symbol
			source.PreviewPosition++;

			// Save pattern length, we will need it
			// Exclude start and end symbol
			var patternLen = source.PreviewPosition - source.Location.Position - 2;

			// Read switches and turn them into options
			var options = RegexOptions.None;
			var switches = string.Empty;

			while (this.ReadSwitch(source, ref options))
			{
				if (this.IsSet(RegexTermOptions.UniqueSwitches) && switches.Contains(source.PreviewChar))
					// "Duplicate switch '{0}' for regular expression"
					return context.CreateErrorToken(Resources.ErrDupRegexSwitch, source.PreviewChar);

				switches += source.PreviewChar.ToString();
				source.PreviewPosition++;
			}

			// Check following symbol
			if (!this.IsSet(RegexTermOptions.AllowLetterAfter))
			{
				var currChar = source.PreviewChar;
				if (char.IsLetter(currChar) || currChar == '_')
					// "Invalid switch '{0}' for regular expression"
					return context.CreateErrorToken(Resources.ErrInvRegexSwitch, currChar);
			}

			var token = source.CreateToken(this.OutputTerminal);

			// We have token, now what's left is to set its Value field. It is either pattern itself, or Regex instance
			// Exclude start and end symbol
			var pattern = token.Text.Substring(1, patternLen);
			object value = pattern;

			if (this.IsSet(RegexTermOptions.CreateRegExObject))
			{
				value = new Regex(pattern, options);
			}

			token.Value = value;

			// Save switches in token.Details
			token.Details = switches;

			return token;
		}

		private bool CheckEscaped(ISourceStream source)
		{
			var savePos = source.PreviewPosition;
			var escaped = false;
			source.PreviewPosition--;

			while (source.PreviewChar == this.EscapeSymbol)
			{
				escaped = !escaped;
				source.PreviewPosition--;
			}

			source.PreviewPosition = savePos;
			return escaped;
		}

		private bool ReadSwitch(ISourceStream source, ref RegexOptions options)
		{
			RegexOptions option;
			var result = this.Switches.TryGetValue(source.PreviewChar, out option);

			if (result)
				options |= option;

			return result;
		}

		public class RegexSwitchTable : Dictionary<char, RegexOptions> { }
	}
}
