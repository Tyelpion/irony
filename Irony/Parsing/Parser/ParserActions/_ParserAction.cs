using System.Collections.Generic;

namespace Irony.Parsing
{
	public abstract partial class ParserAction
	{
		public virtual void Execute(ParsingContext context)
		{ }

		public override string ToString()
		{
			// Should never happen
			return Resources.LabelActionUnknown;
		}
	}

	public class ParserActionTable : Dictionary<BnfTerm, ParserAction> { }
}
