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
	/// A node representing function definition (named lambda)
	/// </summary>
	public class FunctionDefNode : AstNode
	{
		public LambdaNode Lambda;
		public AstNode NameNode;

		public override void Init(AstContext context, ParseTreeNode treeNode)
		{
			base.Init(context, treeNode);

			// Child #0 is usually a keyword like "def"
			var nodes = treeNode.GetMappedChildNodes();
			this.NameNode = AddChild("Name", nodes[1]);

			// Node, params, body
			this.Lambda = new LambdaNode(context, treeNode, nodes[2], nodes[3]);
			this.Lambda.Parent = this;
			this.AsString = "<Function " + this.NameNode.AsString + ">";

			// Lamda will set treeNode.AstNode to itself, we need to set it back to "this" here
			treeNode.AstNode = this;
		}

		public override void Reset()
		{
			this.DependentScopeInfo = null;
			this.Lambda.Reset();
			base.Reset();
		}

		protected override object DoEvaluate(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			// Returns closure
			var closure = this.Lambda.Evaluate(thread);
			this.NameNode.SetValue(thread, closure);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return closure;
		}
	}
}
