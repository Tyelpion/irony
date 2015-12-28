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
	/// A node representing an anonymous function
	/// </summary>
	public class LambdaNode : AstNode
	{
		public AstNode Body;
		public AstNode Parameters;

		public LambdaNode()
		{ }

		/// <summary>
		/// Used by <see cref="FunctionDefNode"/>
		/// </summary>
		/// <param name="context"></param>
		/// <param name="node"></param>
		/// <param name="parameters"></param>
		/// <param name="body"></param>
		public LambdaNode(AstContext context, ParseTreeNode node, ParseTreeNode parameters, ParseTreeNode body)
		{
			this.InitImpl(context, node, parameters, body);
		}

		public object Call(Scope creatorScope, ScriptThread thread, object[] parameters)
		{
			// Prolog, not standard - the caller is NOT target node's parent
			var save = thread.CurrentNode;

			thread.CurrentNode = this;
			thread.PushClosureScope(this.DependentScopeInfo, creatorScope, parameters);

			// Pre-process parameters
			this.Parameters.Evaluate(thread);
			var result = this.Body.Evaluate(thread);
			thread.PopScope();

			// Epilog, restoring caller
			thread.CurrentNode = save;
			return result;
		}

		public override void Init(AstContext context, ParseTreeNode parseNode)
		{
			var mappedNodes = parseNode.GetMappedChildNodes();
			this.InitImpl(context, parseNode, mappedNodes[0], mappedNodes[1]);
		}

		public override void Reset()
		{
			this.DependentScopeInfo = null;
			base.Reset();
		}

		public override void SetIsTail()
		{
			// Ignore this call, do not mark this node as tail, it is meaningless
		}

		protected override object DoEvaluate(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			lock (this.LockObject)
			{
				if (this.DependentScopeInfo == null)
				{
					var langCaseSensitive = thread.App.Language.Grammar.CaseSensitive;
					this.DependentScopeInfo = new ScopeInfo(this, langCaseSensitive);
				}

				// In the first evaluation the parameter list will add parameter's SlotInfo objects to Scope.ScopeInfo
				thread.PushScope(this.DependentScopeInfo, null);

				this.Parameters.Evaluate(thread);
				thread.PopScope();

				// Set Evaluate method and invoke it later
				this.Evaluate = this.EvaluateAfter;
			}

			var result = this.Evaluate(thread);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}

		private object EvaluateAfter(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			var closure = new Closure(thread.CurrentScope, this);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return closure;
		}

		private void InitImpl(AstContext context, ParseTreeNode parseNode, ParseTreeNode parametersNode, ParseTreeNode bodyNode)
		{
			base.Init(context, parseNode);
			this.Parameters = this.AddChild("Parameters", parametersNode);
			this.Body = this.AddChild("Body", bodyNode);
			this.AsString = "Lambda[" + this.Parameters.ChildNodes.Count + "]";

			// This will be propagated to the last statement
			this.Body.SetIsTail();
		}
	}
}
