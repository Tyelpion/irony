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

/* Authors: Roman Ivantsov - initial implementation and some later edits
 *			Philipp Serr - implementation of advanced features for c#, python, VB
*/

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Irony.Parsing
{
	using Irony.Ast;
	using BigInteger = System.Numerics.BigInteger;
	using Complex64 = System.Numerics.Complex;

	[Flags]
	public enum NumberOptions
	{
		None = 0,
		Default = None,

		/// <summary>
		/// Python : http://docs.python.org/ref/floating.html
		/// </summary>
		AllowStartEndDot = 0x01,

		IntOnly = 0x02,

		/// <summary>
		/// For use with IntOnly flag; essentially tells terminal to avoid matching integer if
		/// it is followed by dot (or exp symbol) - leave to another terminal that will handle float numbers
		/// </summary>
		NoDotAfterInt = 0x04,

		AllowSign = 0x08,
		DisableQuickParse = 0x10,

		/// <summary>
		/// Allow number be followed by a letter or underscore; by default this flag is not set, so "3a" would not be
		/// recognized as number followed by an identifier
		/// </summary>
		AllowLetterAfter = 0x20,

		/// <summary>
		/// Ruby allows underscore inside number: 1_234
		/// </summary>
		AllowUnderscore = 0x40,

		/// <summary>
		/// E.g. GNU GCC C Extension supports binary number literals
		/// </summary>
		Binary = 0x0100,

		Octal = 0x0200,
		Hex = 0x0400,
	}

	public class NumberLiteral : CompoundTerminalBase
	{
		/// <summary>
		/// Flags for internal use
		/// </summary>
		public enum NumberFlagsInternal : short
		{
			HasDot = 0x1000,
			HasExp = 0x2000,
		}

		/// <summary>
		/// Nested helper class
		/// </summary>
		public class ExponentsTable : Dictionary<char, TypeCode> { }

		#region Public Consts

		/// <summary>
		/// Currently using TypeCodes for identifying numeric types
		/// </summary>
		public const TypeCode TypeCodeBigInt = (TypeCode) 30;

		public const TypeCode TypeCodeImaginary = (TypeCode) 31;

		#endregion Public Consts

		#region constructors and initialization

		public NumberLiteral(string name) : this(name, NumberOptions.Default)
		{ }

		public NumberLiteral(string name, NumberOptions options, Type astNodeType) : this(name, options)
		{
			this.AstConfig.NodeType = astNodeType;
		}

		public NumberLiteral(string name, NumberOptions options, AstNodeCreator astNodeCreator) : this(name, options)
		{
			this.AstConfig.NodeCreator = astNodeCreator;
		}

		public NumberLiteral(string name, NumberOptions options) : base(name)
		{
			this.Options = options;
			this.SetFlag(TermFlags.IsLiteral);
		}

		public void AddExponentSymbols(string symbols, TypeCode floatType)
		{
			foreach (var exp in symbols)
			{
				this.exponentsTable[exp] = floatType;
			}
		}

		public void AddPrefix(string prefix, NumberOptions options)
		{
			this.PrefixFlags.Add(prefix, (short) options);
			this.Prefixes.Add(prefix);
		}

		#endregion constructors and initialization

		#region Public fields/properties: ExponentSymbols, Suffixes

		public char DecimalSeparator = '.';
		public TypeCode DefaultFloatType = TypeCode.Double;

		/// <summary>
		/// Default types are assigned to literals without suffixes; first matching type used
		/// </summary>
		public TypeCode[] DefaultIntTypes = new TypeCode[] { TypeCode.Int32 };

		public NumberOptions Options;
		private ExponentsTable exponentsTable = new ExponentsTable();

		public bool IsSet(NumberOptions option)
		{
			return (this.Options & option) != 0;
		}

		#endregion Public fields/properties: ExponentSymbols, Suffixes



		#region overrides

		public override IList<string> GetFirsts()
		{
			var result = new StringList();
			result.AddRange(this.Prefixes);

			// We assume that prefix is always optional, so number can always start with plain digit
			result.AddRange(new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" });

			// Python float numbers can start with a dot
			if (this.IsSet(NumberOptions.AllowStartEndDot))
				result.Add(DecimalSeparator.ToString());

			if (this.IsSet(NumberOptions.AllowSign))
				result.AddRange(new string[] { "-", "+" });

			return result;
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);

			// Default Exponent symbols if table is empty
			if (this.exponentsTable.Count == 0 && !this.IsSet(NumberOptions.IntOnly))
			{
				this.exponentsTable['e'] = DefaultFloatType;
				this.exponentsTable['E'] = DefaultFloatType;
			}

			if (this.EditorInfo == null)
				this.EditorInfo = new TokenEditorInfo(TokenType.Literal, TokenColor.Number, TokenTriggers.None);
		}

		protected internal override void OnValidateToken(ParsingContext context)
		{
			if (!this.IsSet(NumberOptions.AllowLetterAfter))
			{
				var current = context.Source.PreviewChar;
				if (char.IsLetter(current) || current == '_')
				{
					// "Number cannot be followed by a letter."
					context.CurrentToken = context.CreateErrorToken(Resources.ErrNoLetterAfterNum);
				}
			}

			base.OnValidateToken(context);
		}

		protected override bool ConvertValue(CompoundTokenDetails details)
		{
			if (string.IsNullOrEmpty(details.Body))
			{
				// "Invalid number.";
				details.Error = Resources.ErrInvNumber;
				return false;
			}

			this.AssignTypeCodes(details);

			// Check for underscore
			if (this.IsSet(NumberOptions.AllowUnderscore) && details.Body.Contains("_"))
				details.Body = details.Body.Replace("_", string.Empty);

			// Try quick paths
			switch (details.TypeCodes[0])
			{
				case TypeCode.Int32:
					if (this.QuickConvertToInt32(details))
						return true;

					break;

				case TypeCode.Double:
					if (this.QuickConvertToDouble(details))
						return true;

					break;
			}

			// Go full cycle
			details.Value = null;
			foreach (TypeCode typeCode in details.TypeCodes)
			{
				switch (typeCode)
				{
					case TypeCode.Single:
					case TypeCode.Double:
					case TypeCode.Decimal:
					case TypeCodeImaginary:
						return this.ConvertToFloat(typeCode, details);

					case TypeCode.SByte:
					case TypeCode.Byte:
					case TypeCode.Int16:
					case TypeCode.UInt16:
					case TypeCode.Int32:
					case TypeCode.UInt32:
					case TypeCode.Int64:
					case TypeCode.UInt64:
						if (details.Value == null)
							// if it is not done yet
							// Try to convert to Long/Ulong and place the result into details.Value field;
							this.TryConvertToLong(details, typeCode == TypeCode.UInt64);

						// Now try to cast the ULong value to the target type
						if (this.TryCastToIntegerType(typeCode, details))
							return true;

						break;

					case TypeCodeBigInt:
						if (this.ConvertToBigInteger(details))
							return true;

						break;
				}
			}
			return false;
		}

		protected override void InitDetails(ParsingContext context, CompoundTokenDetails details)
		{
			base.InitDetails(context, details);
			details.Flags = (short) this.Options;
		}

		/// <summary>
		/// Most numbers in source programs are just one-digit instances of 0, 1, 2, and maybe others until 9
		/// so we try to do a quick parse for these, without starting the whole general process
		/// </summary>
		/// <param name="context"></param>
		/// <param name="source"></param>
		/// <returns></returns>
		protected override Token QuickParse(ParsingContext context, ISourceStream source)
		{
			if (this.IsSet(NumberOptions.DisableQuickParse))
				return null;

			var current = source.PreviewChar;

			// It must be a digit followed by a whitespace or delimiter
			if (!char.IsDigit(current))
				return null;

			if (!this.Grammar.IsWhitespaceOrDelimiter(source.NextPreviewChar))
				return null;

			var iValue = current - '0';
			object value = null;

			switch (DefaultIntTypes[0])
			{
				case TypeCode.Int32:
					value = iValue;
					break;

				case TypeCode.UInt32:
					value = (UInt32) iValue;
					break;

				case TypeCode.Byte:
					value = (byte) iValue;
					break;

				case TypeCode.SByte:
					value = (sbyte) iValue;
					break;

				case TypeCode.Int16:
					value = (Int16) iValue;
					break;

				case TypeCode.UInt16:
					value = (UInt16) iValue;
					break;

				default:
					return null;
			}

			source.PreviewPosition++;
			return source.CreateToken(this.OutputTerminal, value);
		}

		protected override bool ReadBody(ISourceStream source, CompoundTokenDetails details)
		{
			// Remember start - it may be different from source.TokenStart, we may have skipped prefix
			var start = source.PreviewPosition;
			var current = source.PreviewChar;

			if (this.IsSet(NumberOptions.AllowSign) && (current == '-' || current == '+'))
			{
				details.Sign = current.ToString();
				source.PreviewPosition++;
			}

			// Figure out digits set
			var digits = this.GetDigits(details);
			var isDecimal = !details.IsSet((short) (NumberOptions.Binary | NumberOptions.Octal | NumberOptions.Hex));
			var allowFloat = !this.IsSet(NumberOptions.IntOnly);
			var foundDigits = false;

			while (!source.EOF())
			{
				current = source.PreviewChar;

				// 1. If it is a digit, just continue going; the same for '_' if it is allowed
				if (digits.IndexOf(current) >= 0 || this.IsSet(NumberOptions.AllowUnderscore) && current == '_')
				{
					source.PreviewPosition++;
					foundDigits = true;
					continue;
				}

				// 2. Check if it is a dot in float number
				var isDot = current == this.DecimalSeparator;
				if (allowFloat && isDot)
				{
					// If we had seen already a dot or exponent, don't accept this one;
					var hasDotOrExp = details.IsSet((short) (NumberFlagsInternal.HasDot | NumberFlagsInternal.HasExp));
					if (hasDotOrExp)
						break;

					// In python number literals (NumberAllowPointFloat) a point can be the first and last character,
					// We accept dot only if it is followed by a digit
					if (digits.IndexOf(source.NextPreviewChar) < 0 && !IsSet(NumberOptions.AllowStartEndDot))
						break;

					details.Flags |= (int) NumberFlagsInternal.HasDot;
					source.PreviewPosition++;

					continue;
				}

				// 3. Check if it is int number followed by dot or exp symbol
				var isExpSymbol = (details.ExponentSymbol == null) && this.exponentsTable.ContainsKey(current);
				if (!allowFloat && foundDigits && (isDot || isExpSymbol))
				{
					// If no partial float allowed then return false - it is not integer, let float terminal recognize it as float
					if (this.IsSet(NumberOptions.NoDotAfterInt))
						return false;

					// Otherwise break, it is integer and we're done reading digits
					break;
				}

				// 4. Only for decimals - check if it is (the first) exponent symbol
				if (allowFloat && isDecimal && isExpSymbol)
				{
					var next = source.NextPreviewChar;
					var nextIsSign = next == '-' || next == '+';
					var nextIsDigit = digits.IndexOf(next) >= 0;

					if (!nextIsSign && !nextIsDigit)
						// Exponent should be followed by either sign or digit
						break;

					// We've got real exponent
					// remember the exp char
					details.ExponentSymbol = current.ToString();
					details.Flags |= (int) NumberFlagsInternal.HasExp;
					source.PreviewPosition++;

					if (nextIsSign)
						// Skip +/- explicitly so we don't have to deal with them on the next iteration
						source.PreviewPosition++;

					continue;
				}

				//5. It is something else (not digit, not dot or exponent) - we're done
				break;
			}

			var end = source.PreviewPosition;
			if (!foundDigits)
				return false;

			details.Body = source.Text.Substring(start, end - start);
			return true;
		}

		protected override void ReadPrefix(ISourceStream source, CompoundTokenDetails details)
		{
			// Check that is not a  0 followed by dot;
			// this may happen in Python for number "0.123" - we can mistakenly take "0" as octal prefix
			if (source.PreviewChar == '0' && source.NextPreviewChar == '.')
				return;

			base.ReadPrefix(source, details);
		}

		private void AssignTypeCodes(CompoundTokenDetails details)
		{
			// Type could be assigned when we read suffix; if so, just exit
			if (details.TypeCodes != null)
				return;

			// Decide on float types
			var hasDot = details.IsSet((short) (NumberFlagsInternal.HasDot));
			var hasExp = details.IsSet((short) (NumberFlagsInternal.HasExp));
			var isFloat = (hasDot || hasExp);

			if (!isFloat)
			{
				details.TypeCodes = this.DefaultIntTypes;
				return;
			}

			// So we have a float. If we have exponent symbol then use it to select type
			if (hasExp)
			{
				TypeCode code;
				if (this.exponentsTable.TryGetValue(details.ExponentSymbol[0], out code))
				{
					details.TypeCodes = new TypeCode[] { code };
					return;
				}
			}

			// Finally assign default float type
			details.TypeCodes = new TypeCode[] { this.DefaultFloatType };
		}

		#endregion overrides

		#region private utilities

		private static bool IsIntegerCode(TypeCode code)
		{
			return (code >= TypeCode.SByte && code <= TypeCode.UInt64);
		}

		private bool ConvertToBigInteger(CompoundTokenDetails details)
		{
			// Ignore leading zeros and sign
			details.Body = details.Body.TrimStart('+').TrimStart('-').TrimStart('0');
			if (string.IsNullOrEmpty(details.Body))
				details.Body = "0";

			var bodyLength = details.Body.Length;
			var radix = this.GetRadix(details);
			var wordLength = this.GetSafeWordLength(details);
			var sectionCount = this.GetSectionCount(bodyLength, wordLength);

			// Big endian
			var numberSections = new ulong[sectionCount];

			try
			{
				var startIndex = details.Body.Length - wordLength;
				for (var sectionIndex = sectionCount - 1; sectionIndex >= 0; sectionIndex--)
				{
					if (startIndex < 0)
					{
						wordLength += startIndex;
						startIndex = 0;
					}

					// Workaround for .Net FX bug: http://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=278448
					if (radix == 10)
						numberSections[sectionIndex] = Convert.ToUInt64(details.Body.Substring(startIndex, wordLength));
					else
						numberSections[sectionIndex] = Convert.ToUInt64(details.Body.Substring(startIndex, wordLength), radix);

					startIndex -= wordLength;
				}
			}
			catch
			{
				// "Invalid number.";
				details.Error = Resources.ErrInvNumber;
				return false;
			}

			// Produce big integer
			ulong safeWordRadix = this.GetSafeWordRadix(details);
			BigInteger bigIntegerValue = numberSections[0];

			for (var i = 1; i < sectionCount; i++)
			{
				bigIntegerValue = checked(bigIntegerValue * safeWordRadix + numberSections[i]);
			}

			if (details.Sign == "-")
				bigIntegerValue = -bigIntegerValue;

			details.Value = bigIntegerValue;
			return true;
		}

		private bool ConvertToFloat(TypeCode typeCode, CompoundTokenDetails details)
		{
			// Only decimal numbers can be fractions
			if (details.IsSet((short) (NumberOptions.Binary | NumberOptions.Octal | NumberOptions.Hex)))
			{
				// "Invalid number."
				details.Error = Resources.ErrInvNumber;
				return false;
			}

			string body = details.Body;

			// Some languages allow exp symbols other than E. Check if it is the case, and change it to E
			// - otherwise .NET conversion methods may fail
			if (details.IsSet((short) NumberFlagsInternal.HasExp) && details.ExponentSymbol.ToUpper() != "E")
				body = body.Replace(details.ExponentSymbol, "E");

			// '.' decimal seperator required by invariant culture
			if (details.IsSet((short) NumberFlagsInternal.HasDot) && DecimalSeparator != '.')
				body = body.Replace(DecimalSeparator, '.');

			switch (typeCode)
			{
				case TypeCode.Double:
				case TypeCodeImaginary:
					double dValue;
					if (!Double.TryParse(body, NumberStyles.Float, CultureInfo.InvariantCulture, out dValue))
						return false;

					if (typeCode == TypeCodeImaginary)
						details.Value = new Complex64(0, dValue);
					else
						details.Value = dValue;

					return true;

				case TypeCode.Single:
					float fValue;
					if (!Single.TryParse(body, NumberStyles.Float, CultureInfo.InvariantCulture, out fValue))
						return false;

					details.Value = fValue;

					return true;

				case TypeCode.Decimal:
					decimal decValue;
					if (!Decimal.TryParse(body, NumberStyles.Float, CultureInfo.InvariantCulture, out decValue))
						return false;

					details.Value = decValue;

					return true;
			}
			return false;
		}

		private string GetDigits(CompoundTokenDetails details)
		{
			if (details.IsSet((short) NumberOptions.Hex))
				return Strings.HexDigits;

			if (details.IsSet((short) NumberOptions.Octal))
				return Strings.OctalDigits;

			if (details.IsSet((short) NumberOptions.Binary))
				return Strings.BinaryDigits;

			return Strings.DecimalDigits;
		}

		private int GetRadix(CompoundTokenDetails details)
		{
			if (details.IsSet((short) NumberOptions.Hex))
				return 16;

			if (details.IsSet((short) NumberOptions.Octal))
				return 8;

			if (details.IsSet((short) NumberOptions.Binary))
				return 2;

			return 10;
		}

		private int GetSafeWordLength(CompoundTokenDetails details)
		{
			if (details.IsSet((short) NumberOptions.Hex))
				return 15;

			if (details.IsSet((short) NumberOptions.Octal))
				// maxWordLength 22
				return 21;

			if (details.IsSet((short) NumberOptions.Binary))
				return 63;

			// maxWordLength 20
			return 19;
		}

		/// <summary>
		/// radix ^ safeWordLength
		/// </summary>
		/// <param name="details"></param>
		/// <returns></returns>
		private ulong GetSafeWordRadix(CompoundTokenDetails details)
		{
			if (details.IsSet((short) NumberOptions.Hex))
				return 1152921504606846976;

			if (details.IsSet((short) NumberOptions.Octal))
				return 9223372036854775808;

			if (details.IsSet((short) NumberOptions.Binary))
				return 9223372036854775808;

			return 10000000000000000000;
		}

		private int GetSectionCount(int stringLength, int safeWordLength)
		{
			int quotient = stringLength / safeWordLength;
			int remainder = stringLength - quotient * safeWordLength;
			return remainder == 0 ? quotient : quotient + 1;
		}

		private bool QuickConvertToDouble(CompoundTokenDetails details)
		{
			if (details.IsSet((short) (NumberOptions.Binary | NumberOptions.Octal | NumberOptions.Hex)))
				return false;

			if (details.IsSet((short) (NumberFlagsInternal.HasExp)))
				return false;

			if (this.DecimalSeparator != '.')
				return false;

			double dvalue;
			if (!double.TryParse(details.Body, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out dvalue))
				return false;

			details.Value = dvalue;
			return true;
		}

		private bool QuickConvertToInt32(CompoundTokenDetails details)
		{
			var radix = this.GetRadix(details);
			if (radix == 10 && details.Body.Length > 10)
				// 10 digits is maximum for int32; int32.MaxValue = 2 147 483 647
				return false;

			try
			{
				// Workaround for .Net FX bug: http://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=278448
				var iValue = 0;
				if (radix == 10)
					iValue = Convert.ToInt32(details.Body, CultureInfo.InvariantCulture);
				else
					iValue = Convert.ToInt32(details.Body, radix);

				details.Value = iValue;
				return true;
			}
			catch
			{
				return false;
			}
		}

		private bool TryCastToIntegerType(TypeCode typeCode, CompoundTokenDetails details)
		{
			if (details.Value == null)
				return false;

			try
			{
				if (typeCode != TypeCode.UInt64)
					details.Value = Convert.ChangeType(details.Value, typeCode, CultureInfo.InvariantCulture);

				return true;
			}
			catch (Exception)
			{
				details.Error = string.Format(Resources.ErrCannotConvertValueToType, details.Value, typeCode.ToString());
				return false;
			}
		}

		private bool TryConvertToLong(CompoundTokenDetails details, bool useULong)
		{
			try
			{
				var radix = GetRadix(details);

				// Workaround for .Net FX bug: http://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=278448
				if (radix == 10)
				{
					if (useULong)
						details.Value = Convert.ToUInt64(details.Body, CultureInfo.InvariantCulture);
					else
						details.Value = Convert.ToInt64(details.Body, CultureInfo.InvariantCulture);
				}
				else if (useULong)
					details.Value = Convert.ToUInt64(details.Body, radix);
				else
					details.Value = Convert.ToInt64(details.Body, radix);

				return true;
			}
			catch (OverflowException)
			{
				details.Error = string.Format(Resources.ErrCannotConvertValueToType, details.Value, TypeCode.Int64.ToString());
				return false;
			}
		}

		#endregion private utilities
	}
}
