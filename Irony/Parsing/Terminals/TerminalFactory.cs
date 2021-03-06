#region License

/* **********************************************************************************
 * This source code is subject to terms and conditions of the MIT License
 * for Irony. A copy of the license can be found in the License.txt file
 * at the root of this distribution.
 * By using this source code in any fashion, you are agreeing to be bound by the terms of the
 * MIT License.
 * You must not remove this notice from this software.
 * **********************************************************************************/

#endregion License

/*
 * Authors: Roman Ivantsov, Philipp Serr
*/

using System;
using System.Globalization;

namespace Irony.Parsing
{
	public static class TerminalFactory
	{
		public static StringLiteral CreateCSharpChar(string name)
		{
			var term = new StringLiteral(name, "'", StringOptions.IsChar);
			return term;
		}

		public static IdentifierTerminal CreateCSharpIdentifier(string name)
		{
			var id = new IdentifierTerminal(name, IdOptions.AllowsEscapes | IdOptions.CanStartWithEscape);
			id.AddPrefix("@", IdOptions.IsNotKeyword);

			// From spec:
			// Start char is "_" or letter-character, which is a Unicode character of classes Lu, Ll, Lt, Lm, Lo, or Nl
			id.StartCharCategories.AddRange(new UnicodeCategory[] {
				// Ul
				UnicodeCategory.UppercaseLetter,
				// Ll
				UnicodeCategory.LowercaseLetter,
				// Lt
				UnicodeCategory.TitlecaseLetter,
				// Lm
				UnicodeCategory.ModifierLetter,
				// Lo
				UnicodeCategory.OtherLetter,
				// Nl
				UnicodeCategory.LetterNumber
			});

			// Internal chars
			// From spec:
			// identifier-part-character: letter-character | decimal-digit-character | connecting-character |  combining-character |
			// formatting-character

			// letter-character categories
			id.CharCategories.AddRange(id.StartCharCategories);
			id.CharCategories.AddRange(new UnicodeCategory[] {
				// Nd
				UnicodeCategory.DecimalDigitNumber,
				// Pc
				UnicodeCategory.ConnectorPunctuation,
				// Mc
				UnicodeCategory.SpacingCombiningMark,
				// Mn
				UnicodeCategory.NonSpacingMark,
				// Cf
				UnicodeCategory.Format
			});

			// Chars to remove from final identifier
			id.CharsToRemoveCategories.Add(UnicodeCategory.Format);
			return id;
		}

		/// <summary>
		/// http://www.ecma-international.org/publications/files/ECMA-ST/Ecma-334.pdf section 9.4.4
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public static NumberLiteral CreateCSharpNumber(string name)
		{
			var term = new NumberLiteral(name);
			term.DefaultIntTypes = new TypeCode[] { TypeCode.Int32, TypeCode.UInt32, TypeCode.Int64, TypeCode.UInt64 };
			term.DefaultFloatType = TypeCode.Double;

			term.AddPrefix("0x", NumberOptions.Hex);
			term.AddSuffix("u", TypeCode.UInt32, TypeCode.UInt64);
			term.AddSuffix("l", TypeCode.Int64, TypeCode.UInt64);
			term.AddSuffix("ul", TypeCode.UInt64);
			term.AddSuffix("f", TypeCode.Single);
			term.AddSuffix("d", TypeCode.Double);
			term.AddSuffix("m", TypeCode.Decimal);

			return term;
		}

		public static StringLiteral CreateCSharpString(string name)
		{
			var term = new StringLiteral(name, "\"", StringOptions.AllowsAllEscapes);
			term.AddPrefix("@", StringOptions.NoEscapes | StringOptions.AllowsLineBreak | StringOptions.AllowsDoubledQuote);

			return term;
		}

		public static IdentifierTerminal CreatePythonIdentifier(string name)
		{
			// Defaults are OK
			var id = new IdentifierTerminal("Identifier");

			return id;
		}

		/// <summary>
		/// http://docs.python.org/ref/numbers.html
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public static NumberLiteral CreatePythonNumber(string name)
		{
			var term = new NumberLiteral(name, NumberOptions.AllowStartEndDot);

			// Default int types are Integer (32bit) -> LongInteger (BigInt); Try Int64 before BigInt: Better performance?
			term.DefaultIntTypes = new TypeCode[] { TypeCode.Int32, TypeCode.Int64, NumberLiteral.TypeCodeBigInt };

			term.AddPrefix("0x", NumberOptions.Hex);
			term.AddPrefix("0", NumberOptions.Octal);
			term.AddSuffix("L", TypeCode.Int64, NumberLiteral.TypeCodeBigInt);
			term.AddSuffix("J", NumberLiteral.TypeCodeImaginary);

			return term;
		}

		public static StringLiteral CreatePythonString(string name)
		{
			var term = new StringLiteral(name);
			term.AddStartEnd("'", StringOptions.AllowsAllEscapes);
			term.AddStartEnd("'''", StringOptions.AllowsAllEscapes | StringOptions.AllowsLineBreak);
			term.AddStartEnd("\"", StringOptions.AllowsAllEscapes);
			term.AddStartEnd("\"\"\"", StringOptions.AllowsAllEscapes | StringOptions.AllowsLineBreak);

			term.AddPrefix("u", StringOptions.AllowsAllEscapes);
			term.AddPrefix("r", StringOptions.NoEscapes);
			term.AddPrefix("ur", StringOptions.NoEscapes);

			return term;
		}

		/// <summary>
		/// About exponent symbols, extract from R6RS:
		///  ... representations of number objects may be written with an exponent marker that indicates the desired precision
		/// of the inexact representation. The letters s, f, d, and l specify the use of short, single, double, and long precision, respectively.
		/// ...
		/// In addition, the exponent marker e specifies the default precision for the implementation. The default precision
		///  has at least as much precision as double, but implementations may wish to allow this default to be set by the user.
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public static NumberLiteral CreateSchemeNumber(string name)
		{
			var term = new NumberLiteral(name);
			term.DefaultIntTypes = new TypeCode[] { TypeCode.Int32, TypeCode.Int64, NumberLiteral.TypeCodeBigInt };

			// It is default
			term.DefaultFloatType = TypeCode.Double;

			// Default precision for platform, double
			term.AddExponentSymbols("eE", TypeCode.Double);
			term.AddExponentSymbols("sSfF", TypeCode.Single);
			term.AddExponentSymbols("dDlL", TypeCode.Double);
			term.AddPrefix("#b", NumberOptions.Binary);
			term.AddPrefix("#o", NumberOptions.Octal);
			term.AddPrefix("#x", NumberOptions.Hex);
			term.AddPrefix("#d", NumberOptions.None);

			// Inexact prefix, has no effect
			term.AddPrefix("#i", NumberOptions.None);

			// Exact prefix, has no effect
			term.AddPrefix("#e", NumberOptions.None);
			term.AddSuffix("J", NumberLiteral.TypeCodeImaginary);

			return term;
		}

		/// <summary>
		/// Covers simple identifiers like abcd, and also quoted versions: [abc d], "abc d".
		/// </summary>
		/// <param name="grammar"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static IdentifierTerminal CreateSqlExtIdentifier(Grammar grammar, string name)
		{
			var id = new IdentifierTerminal(name);
			var term = new StringLiteral(name + "_qouted");
			term.AddStartEnd("[", "]", StringOptions.NoEscapes);
			term.AddStartEnd("\"", StringOptions.NoEscapes);

			// Term will be added to NonGrammarTerminals automatically
			term.SetOutputTerminal(grammar, id);

			return id;
		}

		/// <summary>
		/// http://www.microsoft.com/downloads/details.aspx?FamilyId=6D50D709-EAA4-44D7-8AF3-E14280403E6E&amp;displaylang=en section 2
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public static NumberLiteral CreateVbNumber(string name)
		{
			var term = new NumberLiteral(name);
			term.DefaultIntTypes = new TypeCode[] { TypeCode.Int32, TypeCode.Int64 };

			//term.DefaultFloatType = TypeCode.Double; it is default

			term.AddPrefix("&H", NumberOptions.Hex);
			term.AddPrefix("&O", NumberOptions.Octal);
			term.AddSuffix("S", TypeCode.Int16);
			term.AddSuffix("I", TypeCode.Int32);
			term.AddSuffix("%", TypeCode.Int32);
			term.AddSuffix("L", TypeCode.Int64);
			term.AddSuffix("&", TypeCode.Int64);
			term.AddSuffix("D", TypeCode.Decimal);
			term.AddSuffix("@", TypeCode.Decimal);
			term.AddSuffix("F", TypeCode.Single);
			term.AddSuffix("!", TypeCode.Single);
			term.AddSuffix("R", TypeCode.Double);
			term.AddSuffix("#", TypeCode.Double);
			term.AddSuffix("US", TypeCode.UInt16);
			term.AddSuffix("UI", TypeCode.UInt32);
			term.AddSuffix("UL", TypeCode.UInt64);

			return term;
		}

		public static StringLiteral CreateVbString(string name)
		{
			var term = new StringLiteral(name);
			term.AddStartEnd("\"", StringOptions.NoEscapes | StringOptions.AllowsDoubledQuote);
			term.AddSuffix("$", TypeCode.String);
			term.AddSuffix("c", TypeCode.Char);

			return term;
		}
	}
}
