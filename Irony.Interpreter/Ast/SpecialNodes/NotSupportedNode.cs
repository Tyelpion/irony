using Irony.Ast;
using Irony.Parsing;

namespace Irony.Interpreter.Ast
{
	/// <summary>
	/// A substitute node to use on constructs that are not yet supported by language implementation.
	/// The script would compile Ok but on attempt to evaluate the node would throw a runtime exception
	/// </summary>
	public class NotSupportedNode : AstNode
	{
		private string Name;

		public override void Init(AstContext context, ParseTreeNode treeNode)
		{
			base.Init(context, treeNode);
			this.Name = treeNode.Term.ToString();
			this.AsString = this.Name + " (not supported)";
		}

		protected override object DoEvaluate(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;
			thread.ThrowScriptError(Resources.ErrConstructNotSupported, this.Name);

			// Never happens
			return null;
		}
	}
}
