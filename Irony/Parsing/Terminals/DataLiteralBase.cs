using System;

namespace Irony.Parsing
{
	/// <summary>
	/// DataLiteralBase is a base class for a set of specialized terminals with a primary purpose of building data readers
	/// DsvLiteral is used for reading delimiter-separated values (DSV), comma-separated format is a specific case of DSV
	/// FixedLengthLiteral may be used to read values of fixed length
	/// </summary>
	public class DataLiteralBase : Terminal
	{
		public TypeCode DataType;

		/// <summary>
		/// Standard format, identifies MM/dd/yyyy for invariant culture.
		/// </summary>
		/// <remarks>
		/// For date format strings see MSDN help for "Custom format strings", available through help for DateTime.ParseExact(...) method
		/// </remarks>
		public string DateTimeFormat = "d";

		/// <summary>
		/// Radix (base) for numeric numbers
		/// </summary>
		public int IntRadix = 10;

		public DataLiteralBase(string name, TypeCode dataType) : base(name)
		{
			DataType = dataType;
		}

		public override Token TryMatch(ParsingContext context, ISourceStream source)
		{
			try
			{
				var textValue = ReadBody(context, source);
				if (textValue == null)
					return null;

				var value = ConvertValue(context, textValue);
				return source.CreateToken(this.OutputTerminal, value);
			}
			catch (Exception ex)
			{
				// We throw exception in DsvLiteral when we cannot find a closing quote for quoted value
				return context.CreateErrorToken(ex.Message);
			}
		}

		protected virtual object ConvertValue(ParsingContext context, string textValue)
		{
			switch (DataType)
			{
				case TypeCode.String:
					return textValue;

				case TypeCode.DateTime:
					return DateTime.ParseExact(textValue, DateTimeFormat, context.Culture);

				case TypeCode.Single:
				case TypeCode.Double:
					var dValue = Convert.ToDouble(textValue, context.Culture);
					if (DataType == TypeCode.Double)
						return dValue;

					return Convert.ChangeType(dValue, DataType, context.Culture);

				default: // Integer types
					var iValue = (IntRadix == 10) ? Convert.ToInt64(textValue, context.Culture) : Convert.ToInt64(textValue, IntRadix);
					if (DataType == TypeCode.Int64)
						return iValue;

					return Convert.ChangeType(iValue, DataType, context.Culture);
			}
		}

		protected virtual string ReadBody(ParsingContext context, ISourceStream source)
		{
			return null;
		}
	}
}
