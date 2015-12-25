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

namespace Irony.Parsing
{
	public enum TokenColor
	{
		Text = 0,
		Keyword = 1,
		Comment = 2,
		Identifier = 3,
		String = 4,
		Number = 5,
	}

	/// <summary>
	/// (Comments are coming from visual studio integration package)
	/// Specifies a set of triggers that can be fired from an Microsoft.VisualStudio.Package.IScanner
	/// language parser.
	/// </summary>
	[Flags]
	public enum TokenTriggers
	{
		/// <summary>
		/// Used when no triggers are set. This is the default.
		/// </summary>
		None = 0,

		/// <summary>
		/// A character that indicates that the start of a member selection has been
		/// parsed. In C#, this could be a period following a class name. In XML, this
		/// could be a &lt; (the member select is a list of possible tags).
		/// </summary>
		MemberSelect = 1,

		/// <summary>
		/// The opening or closing part of a language pair has been parsed. For example,
		/// in C#, a { or } has been parsed. In XML, a < or > has been parsed.
		/// </summary>
		MatchBraces = 2,

		/// <summary>
		/// A character that marks the start of a parameter list has been parsed. For
		/// example, in C#, this could be an open parenthesis, "(".
		/// </summary>
		ParameterStart = 16,

		/// <summary>
		/// A character that separates parameters in a list has been parsed. For example,
		/// in C#, this could be a comma, ",".
		/// </summary>
		ParameterNext = 32,

		/// <summary>
		/// A character that marks the end of a parameter list has been parsed. For example,
		/// in C#, this could be a close parenthesis, ")".
		/// </summary>
		ParameterEnd = 64,

		/// <summary>
		/// A parameter in a method's parameter list has been parsed.
		/// </summary>
		Parameter = 128,

		/// <summary>
		/// This is a mask for the flags used to govern the IntelliSense Method Tip operation.
		/// This mask is used to isolate the values Microsoft.VisualStudio.Package.TokenTriggers.Parameter,
		/// Microsoft.VisualStudio.Package.TokenTriggers.ParameterStart, Microsoft.VisualStudio.Package.TokenTriggers.ParameterNext,
		/// and Microsoft.VisualStudio.Package.TokenTriggers.ParameterEnd.
		/// </summary>
		MethodTip = 240,
	}

	public enum TokenType
	{
		Unknown = 0,
		Text = 1,
		Keyword = 2,
		Identifier = 3,
		String = 4,
		Literal = 5,
		Operator = 6,
		Delimiter = 7,
		WhiteSpace = 8,
		LineComment = 9,
		Comment = 10,
	}

	/// <summary>
	/// Helper classes for information used by syntax highlighters and editors
	/// <see cref="TokenColor"/>, <see cref="TokenTriggers"/> and TokenType are copied from the Visual studio integration assemblies.
	/// Each terminal/token would have its <see cref="TokenEditorInfo"/> that can be used either by VS integration package
	/// or any editor for syntax highligting.
	/// </summary>
	public class TokenEditorInfo
	{
		public readonly TokenColor Color;
		public readonly TokenTriggers Triggers;
		public readonly TokenType Type;
		public string ToolTip;
		public int UnderlineType;

		public TokenEditorInfo(TokenType type, TokenColor color, TokenTriggers triggers)
		{
			this.Type = type;
			this.Color = color;
			this.Triggers = triggers;
		}
	}
}
