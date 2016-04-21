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

namespace Irony.Parsing
{
	public class PrecedenceBasedParserAction : ConditionalParserAction
	{
		private readonly ReduceParserAction reduceAction;
		private ShiftParserAction shiftAction;

		public PrecedenceBasedParserAction(BnfTerm shiftTerm, ParserState newShiftState, Production reduceProduction)
		{
			this.reduceAction = new ReduceParserAction(reduceProduction);
			var reduceEntry = new ConditionalEntry(this.CheckMustReduce, this.reduceAction, "(Precedence comparison)");
			this.ConditionalEntries.Add(reduceEntry);
			this.DefaultAction = this.shiftAction = new ShiftParserAction(shiftTerm, newShiftState);
		}

		public override string ToString()
		{
			return string.Format(Resources.LabelActionOp, this.shiftAction.NewState.Name, this.reduceAction.Production.ToStringQuoted());
		}

		private bool CheckMustReduce(ParsingContext context)
		{
			var input = context.CurrentParserInput;
			var stackCount = context.ParserStack.Count;
			var prodLength = this.reduceAction.Production.RValues.Count;

			for (int i = 1; i <= prodLength; i++)
			{
				var prevNode = context.ParserStack[stackCount - i];
				if (prevNode == null)
					continue;

				if (prevNode.Precedence == BnfTerm.NoPrecedence)
					continue;

				// If previous operator has the same precedence then use associativity
				if (prevNode.Precedence == input.Precedence)
					return (input.Associativity == Associativity.Left); // if true then Reduce
				else
					return (prevNode.Precedence > input.Precedence); // if true then Reduce
			}

			// If no operators found on the stack, do shift
			return false;
		}
	}
}
