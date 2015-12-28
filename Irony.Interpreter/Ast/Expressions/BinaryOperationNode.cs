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
	public class BinaryOperationNode : AstNode
	{
		public AstNode Left, Right;
		public ExpressionType Op;
		public string OpSymbol;

		private object constValue;
		private int failureCount;
		private bool isConstant;
		private OperatorImplementation lastUsed;

		public BinaryOperationNode()
		{ }

		public override void Init(AstContext context, ParseTreeNode treeNode)
		{
			base.Init(context, treeNode);
			var nodes = treeNode.GetMappedChildNodes();
			this.Left = this.AddChild("Arg", nodes[0]);
			this.Right = this.AddChild("Arg", nodes[2]);
			var opToken = nodes[1].FindToken();
			this.OpSymbol = opToken.Text;
			var ictxt = context as InterpreterAstContext;
			this.Op = ictxt.OperatorHandler.GetOperatorExpressionType(this.OpSymbol);

			// Set error anchor to operator, so on error (Division by zero) the explorer will point to
			// operator node as location, not to the very beginning of the first operand.
			this.ErrorAnchor = opToken.Location;
			this.AsString = Op + "(operator)";
		}

		public override bool IsConstant()
		{
			if (this.isConstant)
				return true;

			this.isConstant = this.Left.IsConstant() && this.Right.IsConstant();
			return this.isConstant;
		}

		protected object DefaultEvaluateImplementation(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;
			var arg1 = this.Left.Evaluate(thread);
			var arg2 = this.Right.Evaluate(thread);
			var result = thread.Runtime.ExecuteBinaryOperator(this.Op, arg1, arg2, ref this.lastUsed);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}

		protected override object DoEvaluate(ScriptThread thread)
		{
			// Standard prolog
			// Assign implementation method
			thread.CurrentNode = this;

			switch (Op)
			{
				case ExpressionType.AndAlso:
					this.Evaluate = this.EvaluateAndAlso;
					break;

				case ExpressionType.OrElse:
					this.Evaluate = this.EvaluateOrElse;
					break;

				default:
					this.Evaluate = this.DefaultEvaluateImplementation;
					break;
			}

			// Actually evaluate and get the result.
			var result = this.Evaluate(thread);

			// Check if result is constant - if yes, save the value and switch to method that directly returns the result.
			if (this.IsConstant())
			{
				this.constValue = result;
				this.AsString = this.Op + "(operator) Const=" + this.constValue;
				this.Evaluate = this.EvaluateConst;
			}

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}

		protected object EvaluateFast(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			var arg1 = this.Left.Evaluate(thread);
			var arg2 = this.Right.Evaluate(thread);

			// If we have _lastUsed, go straight for it; if types mismatch it will throw
			if (this.lastUsed != null)
			{
				try
				{
					var res = this.lastUsed.EvaluateBinary(arg1, arg2);

					// Standard epilog
					thread.CurrentNode = this.Parent;
					return res;
				}
				catch
				{
					this.lastUsed = null;
					this.failureCount++;

					// If failed 3 times, change to method without direct try
					if (this.failureCount > 3)
						this.Evaluate = this.DefaultEvaluateImplementation;
				}
			}

			// Go for normal evaluation
			var result = thread.Runtime.ExecuteBinaryOperator(this.Op, arg1, arg2, ref this.lastUsed);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}

		private object EvaluateAndAlso(ScriptThread thread)
		{
			var leftValue = this.Left.Evaluate(thread);
			if (!thread.Runtime.IsTrue(leftValue))
				// If false return immediately
				return leftValue;

			return this.Right.Evaluate(thread);
		}

		private object EvaluateConst(ScriptThread thread)
		{
			return this.constValue;
		}

		private object EvaluateOrElse(ScriptThread thread)
		{
			var leftValue = this.Left.Evaluate(thread);
			if (thread.Runtime.IsTrue(leftValue))
				return leftValue;

			return this.Right.Evaluate(thread);
		}
	}
}
