using Irony.Ast;
using Irony.Parsing;

namespace Irony.Interpreter.Ast
{
	public class LiteralValueNode : AstNode
	{
		public object Value;

		public override void Init(AstContext context, ParseTreeNode treeNode)
		{
			base.Init(context, treeNode);
			this.Value = treeNode.Token.Value;
			this.AsString = this.Value == null ? "null" : this.Value.ToString();

			if (this.Value is string)
				this.AsString = "\"" + this.AsString + "\"";
		}

		public override bool IsConstant()
		{
			return true;
		}

		protected override object DoEvaluate(ScriptThread thread)
		{
			return this.Value;
		}
	}
}
