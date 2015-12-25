using System;
using System.Collections.Generic;

namespace Irony.Parsing
{
	/// <summary>
	/// Terminal for reading values enclosed in a pair of start/end characters. For ex, date literal #15/10/2009# in VB
	/// </summary>
	public class QuotedValueLiteral : DataLiteralBase
	{
		public string EndSymbol;
		public string StartSymbol;

		public QuotedValueLiteral(string name, string startEndSymbol, TypeCode dataType) : this(name, startEndSymbol, startEndSymbol, dataType)
		{ }

		public QuotedValueLiteral(string name, string startSymbol, string endSymbol, TypeCode dataType) : base(name, dataType)
		{
			this.StartSymbol = startSymbol;
			this.EndSymbol = endSymbol;
		}

		public override IList<string> GetFirsts()
		{
			return new string[] { this.StartSymbol };
		}

		protected override string ReadBody(ParsingContext context, ISourceStream source)
		{
			if (!source.MatchSymbol(this.StartSymbol))
				// This will result in null returned from TryMatch, no token
				return null;

			var start = source.Location.Position + this.StartSymbol.Length;
			var end = source.Text.IndexOf(this.EndSymbol, start);

			if (end < 0)
				return null;

			var body = source.Text.Substring(start, end - start);

			// Move beyond the end of EndSymbol
			source.PreviewPosition = end + this.EndSymbol.Length;

			return body;
		}
	}
}
