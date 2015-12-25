using System.Collections.Generic;

namespace Irony.Parsing
{
	public enum WikiTermType
	{
		Text,
		Element,
		Format,
		Heading,
		List,
		Block,
		Table
	}

	public abstract class WikiTerminalBase : Terminal
	{
		public readonly string OpenTag, CloseTag;
		public readonly WikiTermType TermType;
		public string ContainerOpenHtmlTag, ContainerCloseHtmlTag;
		public string HtmlElementName, ContainerHtmlElementName;
		public string OpenHtmlTag, CloseHtmlTag;

		public WikiTerminalBase(string name, WikiTermType termType, string openTag, string closeTag, string htmlElementName) : base(name)
		{
			this.TermType = termType;
			this.OpenTag = openTag;
			this.CloseTag = closeTag;
			this.HtmlElementName = htmlElementName;

			// Longer tags have higher priority
			this.Priority = TerminalPriority.Normal + this.OpenTag.Length;
		}

		public override IList<string> GetFirsts()
		{
			return new string[] { this.OpenTag };
		}

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);

			if (!string.IsNullOrEmpty(this.HtmlElementName))
			{
				if (string.IsNullOrEmpty(this.OpenHtmlTag))
					this.OpenHtmlTag = "<" + this.HtmlElementName + ">";

				if (string.IsNullOrEmpty(this.CloseHtmlTag))
					this.CloseHtmlTag = "</" + this.HtmlElementName + ">";
			}

			if (!string.IsNullOrEmpty(this.ContainerHtmlElementName))
			{
				if (string.IsNullOrEmpty(this.ContainerOpenHtmlTag))
					this.ContainerOpenHtmlTag = "<" + this.ContainerHtmlElementName + ">";

				if (string.IsNullOrEmpty(this.ContainerCloseHtmlTag))
					this.ContainerCloseHtmlTag = "</" + this.ContainerHtmlElementName + ">";
			}
		}
	}
}
