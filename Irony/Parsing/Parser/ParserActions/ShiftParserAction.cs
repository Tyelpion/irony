using System;

namespace Irony.Parsing
{
	public class ShiftParserAction : ParserAction
	{
		public readonly ParserState NewState;
		public readonly BnfTerm Term;

		public ShiftParserAction(Construction.LRItem item) : this(item.Core.Current, item.ShiftedItem.State)
		{ }

		public ShiftParserAction(BnfTerm term, ParserState newState)
		{
			if (newState == null)
				throw new Exception($"ParserShiftAction: newState may not be null. term: {term}");

			this.Term = term;
			this.NewState = newState;
		}

		public override void Execute(ParsingContext context)
		{
			var currInput = context.CurrentParserInput;
			currInput.Term.OnShifting(context.SharedParsingEventArgs);

			context.ParserStack.Push(currInput, this.NewState);
			context.CurrentParserState = this.NewState;
			context.CurrentParserInput = null;
		}

		public override string ToString()
		{
			return string.Format(Resources.LabelActionShift, this.NewState.Name);
		}
	}
}
