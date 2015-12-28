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
	public class IfNode : AstNode
	{
		public AstNode IfFalse;
		public AstNode IfTrue;
		public AstNode Test;

		public override void Init(AstContext context, ParseTreeNode treeNode)
		{
			base.Init(context, treeNode);
			var nodes = treeNode.GetMappedChildNodes();
			this.Test = this.AddChild("Test", nodes[0]);
			this.IfTrue = this.AddChild("IfTrue", nodes[1]);

			if (nodes.Count > 2)
				this.IfFalse = this.AddChild("IfFalse", nodes[2]);
		}

		public override void SetIsTail()
		{
			base.SetIsTail();

			if (this.IfTrue != null)
				this.IfTrue.SetIsTail();

			if (this.IfFalse != null)
				this.IfFalse.SetIsTail();
		}

		protected override object DoEvaluate(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			object result = null;
			var test = this.Test.Evaluate(thread);
			var isTrue = thread.Runtime.IsTrue(test);
			if (isTrue)
			{
				if (this.IfTrue != null)
					result = this.IfTrue.Evaluate(thread);
			}
			else if (this.IfFalse != null)
				result = this.IfFalse.Evaluate(thread);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}
	}
}
