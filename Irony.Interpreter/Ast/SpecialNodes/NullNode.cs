using Irony.Parsing;

namespace Irony.Interpreter.Ast
{
	/// <summary>
	/// A stub to use when AST node was not created (type not specified on NonTerminal, or error on creation)
	/// The purpose of the stub is to throw a meaningful message when interpreter tries to evaluate null node.
	/// </summary>
	public class NullNode : AstNode
	{
		public NullNode(BnfTerm term)
		{
			this.Term = term;
		}

		protected override object DoEvaluate(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;
			thread.ThrowScriptError(Resources.ErrNullNodeEval, this.Term);

			// Never happens
			return null;
		}
	}
}
