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
	public class AssignmentNode : AstNode
	{
		public string AssignmentOp;
		public ExpressionType BinaryExpressionType;
		public AstNode Expression;

		/// <summary>
		/// true if it is augmented operation like "+="
		/// </summary>
		public bool IsAugmented;

		public AstNode Target;
		private int failureCount;
		private OperatorImplementation lastUsed;

		public override void Init(AstContext context, ParseTreeNode treeNode)
		{
			base.Init(context, treeNode);
			var nodes = treeNode.GetMappedChildNodes();
			this.Target = this.AddChild(NodeUseType.ValueWrite, "To", nodes[0]);

			// Get Op and baseOp if it is combined assignment
			this.AssignmentOp = nodes[1].FindTokenAndGetText();
			if (string.IsNullOrEmpty(this.AssignmentOp))
				this.AssignmentOp = "=";

			this.BinaryExpressionType = CustomExpressionTypes.NotAnExpression;

			// There maybe an "=" sign in the middle, or not - if it is marked as punctuation; so we just take the last node in child list
			this.Expression = this.AddChild(NodeUseType.ValueRead, "Expr", nodes[nodes.Count - 1]);
			this.AsString = this.AssignmentOp + " (assignment)";

			// TODO: this is not always correct: in Pascal the assignment operator is :=.
			this.IsAugmented = this.AssignmentOp.Length > 1;
			if (this.IsAugmented)
			{
				var ictxt = context as InterpreterAstContext;
				this.ExpressionType = ictxt.OperatorHandler.GetOperatorExpressionType(this.AssignmentOp);
				this.BinaryExpressionType = ictxt.OperatorHandler.GetBinaryOperatorForAugmented(this.ExpressionType);
				this.Target.UseType = NodeUseType.ValueReadWrite;
			}
		}

		protected override object DoEvaluate(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			if (this.IsAugmented)
				this.Evaluate = this.EvaluateAugmentedFast;
			else
				// Non-augmented
				// Call self-evaluate again, now to call real methods
				this.Evaluate = this.EvaluateSimple;

			var result = this.Evaluate(thread);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}

		private object EvaluateAugmented(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			var value = this.Target.Evaluate(thread);
			var exprValue = this.Expression.Evaluate(thread);
			var result = thread.Runtime.ExecuteBinaryOperator(this.BinaryExpressionType, value, exprValue, ref this.lastUsed);
			this.Target.SetValue(thread, result);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}

		private object EvaluateAugmentedFast(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			var value = this.Target.Evaluate(thread);
			var exprValue = this.Expression.Evaluate(thread);
			object result = null;

			if (this.lastUsed != null)
			{
				try
				{
					result = this.lastUsed.EvaluateBinary(value, exprValue);
				}
				catch
				{
					this.failureCount++;

					// If failed 3 times, change to method without direct try
					if (this.failureCount > 3)
						this.Evaluate = this.EvaluateAugmented;
				}
			}

			if (result == null)
				result = thread.Runtime.ExecuteBinaryOperator(this.BinaryExpressionType, value, exprValue, ref this.lastUsed);

			this.Target.SetValue(thread, result);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}

		private object EvaluateSimple(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			var value = this.Expression.Evaluate(thread);
			this.Target.SetValue(thread, value);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return value;
		}
	}
}
