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

using System.Linq.Expressions;

using Irony.Ast;
using Irony.Parsing;

namespace Irony.Interpreter.Ast
{
	public class IncDecNode : AstNode
	{
		public AstNode Argument;
		public ExpressionType BinaryOp;

		/// <summary>
		/// Corresponding binary operation: + for ++, - for --
		/// </summary>
		public string BinaryOpSymbol;

		public bool IsPostfix;
		public string OpSymbol;

		private OperatorImplementation lastUsed;

		public override void Init(AstContext context, ParseTreeNode treeNode)
		{
			base.Init(context, treeNode);
			var nodes = treeNode.GetMappedChildNodes();
			this.FindOpAndDetectPostfix(nodes);
			int argIndex = this.IsPostfix ? 0 : 1;
			this.Argument = this.AddChild(NodeUseType.ValueReadWrite, "Arg", nodes[argIndex]);

			// Take a single char out of ++ or --
			this.BinaryOpSymbol = this.OpSymbol[0].ToString();
			var interpContext = (InterpreterAstContext) context;
			this.BinaryOp = interpContext.OperatorHandler.GetOperatorExpressionType(this.BinaryOpSymbol);
			base.AsString = this.OpSymbol + (this.IsPostfix ? "(postfix)" : "(prefix)");
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

			var oldValue = this.Argument.Evaluate(thread);
			var newValue = thread.Runtime.ExecuteBinaryOperator(this.BinaryOp, oldValue, 1, ref this.lastUsed);
			this.Argument.SetValue(thread, newValue);
			var result = this.IsPostfix ? oldValue : newValue;

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}

		private void FindOpAndDetectPostfix(ParseTreeNodeList mappedNodes)
		{
			// Assume it
			this.IsPostfix = false;

			this.OpSymbol = mappedNodes[0].FindTokenAndGetText();
			if (this.OpSymbol == "--" || this.OpSymbol == "++")
				return;

			this.IsPostfix = true;
			this.OpSymbol = mappedNodes[1].FindTokenAndGetText();
		}
	}
}
