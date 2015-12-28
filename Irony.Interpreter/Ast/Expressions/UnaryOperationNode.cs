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

using Irony.Ast;
using Irony.Parsing;

namespace Irony.Interpreter.Ast
{
	public class UnaryOperationNode : AstNode
	{
		public AstNode Argument;
		public string OpSymbol;
		private OperatorImplementation lastUsed;

		public override void Init(AstContext context, ParseTreeNode treeNode)
		{
			base.Init(context, treeNode);
			var nodes = treeNode.GetMappedChildNodes();
			this.OpSymbol = nodes[0].FindTokenAndGetText();
			this.Argument = this.AddChild("Arg", nodes[1]);
			base.AsString = this.OpSymbol + "(unary op)";
			var interpContext = (InterpreterAstContext) context;
			this.ExpressionType = interpContext.OperatorHandler.GetUnaryOperatorExpressionType(this.OpSymbol);
		}

		public override void SetIsTail()
		{
			base.SetIsTail();
			this.Argument.SetIsTail();
		}

		protected override object DoEvaluate(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			var arg = this.Argument.Evaluate(thread);
			var result = thread.Runtime.ExecuteUnaryOperator(this.ExpressionType, arg, ref this.lastUsed);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}
	}
}
