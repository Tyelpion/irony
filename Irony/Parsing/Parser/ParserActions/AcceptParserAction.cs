namespace Irony.Parsing
{
	public class AcceptParserAction : ParserAction
	{
		public override void Execute(ParsingContext context)
		{
			// Pop root
			context.CurrentParseTree.Root = context.ParserStack.Pop();
			context.Status = ParserStatus.Accepted;
		}

		public override string ToString()
		{
			return Resources.LabelActionAccept;
		}
	}
}
