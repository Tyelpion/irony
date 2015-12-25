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
using System.Text;

namespace Irony.Parsing
{
	//A terminal for DSV-formatted files (Delimiter-Separated Values), a generalization of CSV (comma-separated values) format.
	// See http://en.wikipedia.org/wiki/Delimiter-separated_values
	// For CSV format, there's a recommendation RFC4180 (http://tools.ietf.org/html/rfc4180)
	// It might seem that this terminal is not that useful and it is easy enough to create a custom CSV reader for a particular data format
	// format. However, if you consider all escaping and double-quote enclosing rules, then a custom reader solution would not seem so trivial.
	// So DsvLiteral can simplify this task.
	public class DsvLiteral : DataLiteralBase
	{
		public bool ConsumeTerminator = true;
		public string Terminator = ",";

		//if true, the source pointer moves after the separator
		private char[] terminators;

		//For last value on the line specify terminator = null; the DsvLiteral will then look for NewLine as terminator
		public DsvLiteral(string name, TypeCode dataType, string terminator) : this(name, dataType)
		{
			this.Terminator = terminator;
		}

		public DsvLiteral(string name, TypeCode dataType) : base(name, dataType)
		{
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);

			if (this.Terminator == null)
				this.terminators = new char[] { '\n', '\r' };
			else
				this.terminators = new char[] { this.Terminator[0] };
		}

		protected override string ReadBody(ParsingContext context, ISourceStream source)
		{
			string body;
			if (source.PreviewChar == '"')
				body = this.ReadQuotedBody(context, source);
			else
				body = this.ReadNotQuotedBody(context, source);

			if (this.ConsumeTerminator && this.Terminator != null)
				this.MoveSourcePositionAfterTerminator(source);

			return body;
		}

		private void MoveSourcePositionAfterTerminator(ISourceStream source)
		{
			while (!source.EOF())
			{
				while (source.PreviewChar != this.Terminator[0])
				{
					source.PreviewPosition++;
				}

				if (source.MatchSymbol(this.Terminator))
				{
					source.PreviewPosition += this.Terminator.Length;
					return;
				}
			}
		}

		private string ReadNotQuotedBody(ParsingContext context, ISourceStream source)
		{
			var startPos = source.Location.Position;
			var sepPos = source.Text.IndexOfAny(this.terminators, startPos);
			if (sepPos < 0)
				sepPos = source.Text.Length;

			source.PreviewPosition = sepPos;
			var valueText = source.Text.Substring(startPos, sepPos - startPos);

			return valueText;
		}

		private string ReadQuotedBody(ParsingContext context, ISourceStream source)
		{
			const char dQuoute = '"';
			StringBuilder sb = null;

			// Skip initial double quote
			var from = source.Location.Position + 1;

			while (true)
			{
				var until = source.Text.IndexOf(dQuoute, from);
				if (until < 0)
					// "Could not find a closing quote for quoted value."
					throw new Exception(Resources.ErrDsvNoClosingQuote);

				// Now points at double-quote
				source.PreviewPosition = until;
				var piece = source.Text.Substring(from, until - from);

				// Move after double quote
				source.PreviewPosition++;
				if (source.PreviewChar != dQuoute && sb == null)
					// Quick path - if sb (string builder) was not created yet, we are looking at the very first segment;
					// and if we found a standalone dquote, then we are done - the "piece" is the result.
					return piece;

				if (sb == null)
					sb = new StringBuilder(100);

				sb.Append(piece);

				if (source.PreviewChar != dQuoute)
					return sb.ToString();

				// We have doubled double-quote; add a single double-quoute char to the result and move over both symbols
				sb.Append(dQuoute);
				from = source.PreviewPosition + 1;
			}
		}
	}
}
