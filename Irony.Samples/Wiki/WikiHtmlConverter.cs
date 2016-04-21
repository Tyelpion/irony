using System.Collections.Generic;
using System.Text;
using Irony.Parsing;

namespace Irony.Samples
{
	public class WikiHtmlConverter
	{
		private bool atLineStart = true;

		private WikiTerminalBase currentHeader = null;

		private FlagTable flags = new FlagTable();

		private bool insideCell = false;

		private bool insideTable = false;

		private WikiTagTerminal lastTableTag;

		private WikiTermStack openLists = new WikiTermStack();

		private StringBuilder output;

		private enum TableStatus
		{
			None,
			Table,
			Cell
		}

		private int CurrentListLevel
		{
			get { return this.openLists.Count == 0 ? 0 : this.openLists.Peek().OpenTag.Length; }
		}

		/// <summary>
		/// HtmlEncode method - we don't use System.Web.HttpUtility.HtmlEncode method, because System.Web assembly is not part of
		/// .NET Client profile; so we just embed implementation here
		/// This is reformatted version of Rick Strahl's original code: http://www.west-wind.com/Weblog/posts/617930.aspx
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public static string HtmlEncode(string text)
		{
			if (text == null)
				return null;

			var sb = new StringBuilder(text.Length);
			int len = text.Length;

			for (int i = 0; i < len; i++)
			{
				switch (text[i])
				{
					case '<': sb.Append("&lt;"); break;

					case '>': sb.Append("&gt;"); break;

					case '"': sb.Append("&quot;"); break;

					case '&': sb.Append("&amp;"); break;

					default:
						if (text[i] > 159)
						{
							// decimal numeric entity
							sb.Append("&#");
							sb.Append(((int) text[i]).ToString());
							sb.Append(";");
						}
						else
							sb.Append(text[i]);
						break;
				}
			}

			return sb.ToString();
		}

		public string Convert(Grammar grammar, TokenList tokens)
		{
			// 8k
			this.output = new StringBuilder(8192);
			this.output.AppendLine("<html>");

			foreach (var token in tokens)
			{
				var term = token.Terminal;

				if (this.atLineStart || term == grammar.Eof)
				{
					this.CheckOpeningClosingLists(token);
					this.CheckTableStatus(token);
					if (term == grammar.Eof) break;
				}

				if (term is WikiTerminalBase)
					this.ProcessWikiToken(token);
				else if (term == grammar.NewLine)
				{
					this.ProcessNewLine(token);
				}
				else
					// Non-wike element and not new line
					this.output.Append(HtmlEncode(token.ValueString));

				// Set for the next token
				this.atLineStart = term == grammar.NewLine;
			}

			this.output.AppendLine();
			this.output.AppendLine("</html>");

			return this.output.ToString();
		}

		/// <summary>
		/// Called at the start of each line (after NewLine)
		/// </summary>
		/// <param name="token"></param>
		private void CheckOpeningClosingLists(Token token)
		{
			var nextLevel = 0;
			var wikiTerm = token.Terminal as WikiTerminalBase;

			if (wikiTerm != null && wikiTerm.TermType == WikiTermType.List)
				nextLevel = wikiTerm.OpenTag.Length;

			// For codeplex-style additionally check that the control char is the same (# vs *).
			// If not, unwind the levels
			if (this.CurrentListLevel == nextLevel)
				// It is at the same level;
				return;

			// New list begins
			if (nextLevel > this.CurrentListLevel)
			{
				this.output.Append(wikiTerm.ContainerOpenHtmlTag);
				this.openLists.Push(wikiTerm);
				return;
			}

			// One or more lists end
			while (nextLevel < this.CurrentListLevel)
			{
				var oldTerm = this.openLists.Pop();
				this.output.Append(oldTerm.ContainerCloseHtmlTag);
			}
		}

		#region comments

		/* Note: we allow mix of bulleted/numbered lists, so we can have bulleted list inside numbered item:

		  # item 1
		  ** bullet 1
		  ** bullet 2
		  # item 2

		 This is a bit different from codeplex rules - the bulletted list resets the numeration of items, so that "item 2" would
		 appear with number 1, not 2. While our handling seems more flexible, you can easily change the following method to
		 follow codeplex rules.  */

		#endregion comments
		/// <summary>
		/// Called at the start of each line
		/// </summary>
		/// <param name="token"></param>
		private void CheckTableStatus(Token token)
		{
			var wikiTerm = token.Terminal as WikiTerminalBase;
			var isTableTag = wikiTerm != null && wikiTerm.TermType == WikiTermType.Table;

			if (!this.insideTable && !isTableTag) return;

			// If we are at line start, drop this flag
			this.insideCell = false;
			this.lastTableTag = null;

			// New table begins
			if (!this.insideTable && isTableTag)
			{
				this.output.AppendLine("<table>");
				this.output.Append("<tr>");
				this.insideTable = true;
				return;
			}

			// Existing table continues
			if (this.insideTable && isTableTag)
			{
				this.output.AppendLine("</tr>");
				this.output.Append("<tr>");
				return;
			}

			// Existing table ends
			if (this.insideTable && !isTableTag)
			{
				this.output.AppendLine("</tr>");
				this.output.AppendLine("</table>");
				this.insideTable = false;
				return;
			}
		}

		private void ProcessFormatTag(Token token)
		{
			var term = token.Terminal as WikiTerminalBase;
			var value = false;
			var isOn = this.flags.TryGetValue(term, out value) && value;

			if (isOn)
				this.output.Append(term.CloseHtmlTag);
			else
				this.output.Append(term.OpenHtmlTag);

			this.flags[term] = !isOn;
		}

		private void ProcessNewLine(Token token)
		{
			if (this.insideTable & !this.insideCell)
				// Ignore it in one special case - to make output look nicer
				return;

			if (this.currentHeader != null)
				this.output.AppendLine(this.currentHeader.CloseHtmlTag);
			else
				this.output.AppendLine("<br/>");

			this.currentHeader = null;
		}

		private void ProcessWikiBlockTag(Token token)
		{
			var term = token.Terminal as WikiBlockTerminal;
			string template;
			string[] segments;

			switch (term.BlockType)
			{
				case WikiBlockType.EscapedText:
				case WikiBlockType.CodeBlock:
					this.output.Append(term.OpenHtmlTag);
					this.output.Append(HtmlEncode(token.ValueString));
					this.output.AppendLine(term.CloseHtmlTag);
					break;

				case WikiBlockType.Anchor:
					this.output.Append("<a name=\"" + token.ValueString + "\"/>");
					break;

				case WikiBlockType.LinkToAnchor:
					this.output.Append("<a href=\"#" + token.ValueString + "\">" + HtmlEncode(token.ValueString) + "</a>");
					break;

				case WikiBlockType.Url:
				case WikiBlockType.FileLink:
					template = "<a href=\"{0}\">{1}</a>";
					segments = token.ValueString.Split('|');
					if (segments.Length > 1)
						this.output.Append(string.Format(template, segments[1], segments[0]));
					else
						this.output.Append(string.Format(template, segments[0], segments[0]));
					break;

				case WikiBlockType.Image:
					segments = token.ValueString.Split('|');
					switch (segments.Length)
					{
						case 1:
							template = "<img src=\"{0}\"/>";
							this.output.Append(string.Format(template, segments[0]));
							break;

						case 2:
							template = "<img src=\"{1}\" alt=\"{0}\" title=\"{0}\" />";
							this.output.Append(string.Format(template, segments[0], segments[1]));
							break;

						case 3:
							template = "<a href=\"{2}\"><img src=\"{1}\" alt=\"{0}\" title=\"{0}\" /></a>";
							this.output.Append(string.Format(template, segments[0], segments[1], segments[2]));
							break;
					}
					break;
			}
		}

		private void ProcessWikiToken(Token token)
		{
			// We check that token actually contains some chars - to allow "invisible spaces" after last table tag
			if (this.lastTableTag != null && !this.insideCell && token.ValueString.Trim().Length > 0)
			{
				this.output.Append(this.lastTableTag.OpenHtmlTag);
				this.insideCell = true;
			}

			var wikiTerm = token.Terminal as WikiTerminalBase;
			switch (wikiTerm.TermType)
			{
				case WikiTermType.Element:
					this.output.Append(wikiTerm.OpenHtmlTag);
					this.output.Append(wikiTerm.CloseHtmlTag);
					break;

				case WikiTermType.Format:
					ProcessFormatTag(token);
					break;

				case WikiTermType.Heading:
				case WikiTermType.List:
					this.output.Append(wikiTerm.OpenHtmlTag);
					this.currentHeader = wikiTerm;
					break;

				case WikiTermType.Block:
					ProcessWikiBlockTag(token);
					break;

				case WikiTermType.Text:
					this.output.Append(HtmlEncode(token.ValueString));
					break;

				case WikiTermType.Table:
					if (this.insideCell)
						// Write out </td> or </th>
						this.output.Append(this.lastTableTag.CloseHtmlTag);

					// We do not write opening tag immediately: we need to know if it is the last table tag on the line.
					// If yes, we don't write it at all; this.lastTableTag will be cleared when we start new line
					this.lastTableTag = wikiTerm as WikiTagTerminal;
					this.insideCell = false;
					break;
			}
		}

		internal class FlagTable : Dictionary<Terminal, bool> { }

		internal class WikiTermStack : Stack<WikiTerminalBase> { }
	}
}
