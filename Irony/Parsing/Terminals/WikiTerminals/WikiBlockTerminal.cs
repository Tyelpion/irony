namespace Irony.Parsing
{
	public enum WikiBlockType
	{
		EscapedText,
		CodeBlock,
		Anchor,
		LinkToAnchor,
		Url,

		/// <summary>
		/// Looks like it is the same as Url
		/// </summary>
		FileLink,

		Image,
	}

	public class WikiBlockTerminal : WikiTerminalBase
	{
		public readonly WikiBlockType BlockType;

		public WikiBlockTerminal(string name, WikiBlockType blockType, string openTag, string closeTag, string htmlElementName)
			: base(name, WikiTermType.Block, openTag, closeTag, htmlElementName)
		{
			this.BlockType = blockType;
		}

		public override Token TryMatch(ParsingContext context, ISourceStream source)
		{
			if (!source.MatchSymbol(this.OpenTag))
				return null;

			source.PreviewPosition += this.OpenTag.Length;
			var endPos = source.Text.IndexOf(this.CloseTag, source.PreviewPosition);
			string content;

			if (endPos > 0)
			{
				content = source.Text.Substring(source.PreviewPosition, endPos - source.PreviewPosition);
				source.PreviewPosition = endPos + this.CloseTag.Length;
			}
			else
			{
				content = source.Text.Substring(source.PreviewPosition, source.Text.Length - source.PreviewPosition);
				source.PreviewPosition = source.Text.Length;
			}

			var token = source.CreateToken(this.OutputTerminal, content);

			return token;
		}
	}
}
