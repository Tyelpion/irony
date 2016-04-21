using System.Collections.Generic;
using System.Linq;

namespace Irony.Parsing
{
	/// <summary>
	/// Handles plain text
	/// </summary>
	public class WikiTextTerminal : WikiTerminalBase
	{
		public const char NoEscape = '\0';

#pragma warning disable RECS0122 // Initializing field with default value is redundant
		public char EscapeChar = NoEscape;
#pragma warning restore RECS0122 // Initializing field with default value is redundant

		private char[] stopChars;

		public WikiTextTerminal(string name) : base(name, WikiTermType.Text, string.Empty, string.Empty, string.Empty)
		{
			this.Priority = TerminalPriority.Low;
		}

		/// <summary>
		/// Override to WikiTerminalBase's method to return null, indicating there are no firsts, so it is a fallback terminal
		/// </summary>
		/// <returns></returns>
		public override IList<string> GetFirsts()
		{
			return null;
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);

			var stopCharSet = new CharHashSet();

			foreach (var term in grammarData.Terminals)
			{
				var firsts = term.GetFirsts();
				if (firsts == null)
					continue;

				foreach (var first in firsts)
				{
					if (!string.IsNullOrEmpty(first))
						stopCharSet.Add(first[0]);
				}
			}

			if (this.EscapeChar != NoEscape)
				stopCharSet.Add(this.EscapeChar);

			this.stopChars = stopCharSet.ToArray();
		}

		public override Token TryMatch(ParsingContext context, ISourceStream source)
		{
			var isEscape = source.PreviewChar == this.EscapeChar && this.EscapeChar != NoEscape;
			if (isEscape)
			{
				// Return a token containing only escaped char
				var value = source.NextPreviewChar.ToString();
				source.PreviewPosition += 2;
				return source.CreateToken(this.OutputTerminal, value);
			}

			var stopIndex = source.Text.IndexOfAny(this.stopChars, source.Location.Position + 1);
			if (stopIndex == source.Location.Position)
				return null;

			if (stopIndex < 0)
				stopIndex = source.Text.Length;

			source.PreviewPosition = stopIndex;

			return source.CreateToken(this.OutputTerminal);
		}
	}
}
