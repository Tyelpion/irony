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
	/// <summary>
	/// A node representing expression list - for example, list of argument expressions in function call
	/// </summary>
	public class ExpressionListNode : AstNode
	{
		public override void Init(AstContext context, ParseTreeNode treeNode)
		{
			base.Init(context, treeNode);
			foreach (var child in treeNode.ChildNodes)
			{
				this.AddChild(NodeUseType.Parameter, "expr", child);
			}

			this.AsString = "Expression list";
		}

		protected override object DoEvaluate(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			var values = new object[this.ChildNodes.Count];
			for (int i = 0; i < values.Length; i++)
			{
				values[i] = this.ChildNodes[i].Evaluate(thread);
			}

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return values;
		}
	}
}
