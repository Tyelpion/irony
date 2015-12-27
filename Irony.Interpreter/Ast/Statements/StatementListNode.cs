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
	public class StatementListNode : AstNode
	{
		/// <summary>
		/// Stores a single child when child count == 1, for fast access
		/// </summary>
		private AstNode singleChild;

		public override void Init(AstContext context, ParseTreeNode treeNode)
		{
			base.Init(context, treeNode);
			var nodes = treeNode.GetMappedChildNodes();

			foreach (var child in nodes)
			{
				// Don't add if it is null; it can happen that "statement" is a comment line and statement's node is null.
				// So to make life easier for language creator, we just skip if it is null
				if (child.AstNode != null)
					this.AddChild(string.Empty, child);
			}

			this.AsString = "Statement List";

			if (this.ChildNodes.Count == 0)
				this.AsString += " (Empty)";
			else
				this.ChildNodes[this.ChildNodes.Count - 1].Flags |= AstNodeFlags.IsTail;
		}

		public override void SetIsTail()
		{
			base.SetIsTail();

			if (this.ChildNodes.Count > 0)
				this.ChildNodes[this.ChildNodes.Count - 1].SetIsTail();
		}

		protected override object DoEvaluate(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			lock (this.LockObject)
			{
				switch (this.ChildNodes.Count)
				{
					case 0:
						this.Evaluate = this.EvaluateEmpty;
						break;

					case 1:
						this.singleChild = this.ChildNodes[0];
						this.Evaluate = this.EvaluateOne;
						break;

					default:
						this.Evaluate = this.EvaluateMultiple;
						break;
				}
			}

			var result = this.Evaluate(thread);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}

		private object EvaluateEmpty(ScriptThread thread)
		{
			return null;
		}

		private object EvaluateMultiple(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			object result = null;
			for (int i = 0; i < this.ChildNodes.Count; i++)
			{
				result = this.ChildNodes[i].Evaluate(thread);
			}

			// Standard epilog
			thread.CurrentNode = this.Parent;

			// Return result of last statement
			return result;
		}

		private object EvaluateOne(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			object result = this.singleChild.Evaluate(thread);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}
	}
}
