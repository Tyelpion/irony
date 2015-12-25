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
	/// <summary>
	/// Note: This in incomplete implementation.
	/// this implementation sets precedence only on operator symbols that are already "shifted" into the parser stack,
	/// ie those on the "left" of precedence comparison. It does not set precedence when operator symbol first appears in parser
	/// input. This works OK for unary operator but might break some advanced scenarios.
	/// </summary>
	public class ImpliedPrecedenceHint : GrammarHint
	{
		/// <summary>
		/// A flag to mark a state for setting implied precedence
		/// </summary>
		public const int ImpliedPrecedenceCustomFlag = 0x01000000;

		/// <summary>
		/// GrammarHint inherits Precedence and Associativity members from BnfTerm; we'll use them to store implied values for this hint
		/// </summary>
		/// <param name="precedence"></param>
		/// <param name="associativity"></param>
		public ImpliedPrecedenceHint(int precedence, Associativity associativity)
		{
			this.Precedence = precedence;
			this.Associativity = associativity;
		}

		public override void Apply(LanguageData language, Construction.LRItem owner)
		{
			// Check that owner is not final - we can imply precedence only in shift context
			var curr = owner.Core.Current;
			if (curr == null)
				return;

			// Mark the state, to make sure we do stuff in Term_Shifting event handler only in appropriate states
			owner.State.CustomFlags |= ImpliedPrecedenceCustomFlag;
			curr.Shifting += Term_Shifting;
		}

		private void Term_Shifting(object sender, ParsingEventArgs e)
		{
			// Set the values only if we are in the marked state
			if (!e.Context.CurrentParserState.CustomFlagIsSet(ImpliedPrecedenceCustomFlag))
				return;

			e.Context.CurrentParserInput.Associativity = this.Associativity;
			e.Context.CurrentParserInput.Precedence = this.Precedence;
		}
	}
}
