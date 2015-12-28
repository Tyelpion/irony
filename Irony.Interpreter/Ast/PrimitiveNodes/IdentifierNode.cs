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
	public class IdentifierNode : AstNode
	{
		public string Symbol;
		private Binding accessor;

		public IdentifierNode()
		{ }

		public override void DoSetValue(ScriptThread thread, object value)
		{
			// Standard prolog
			thread.CurrentNode = this;

			if (this.accessor == null)
			{
				this.accessor = thread.Bind(this.Symbol, BindingRequestFlags.Write | BindingRequestFlags.ExistingOrNew);
			}

			this.accessor.SetValueRef(thread, value);

			// Standard epilog
			thread.CurrentNode = this.Parent;
		}

		public override void Init(AstContext context, ParseTreeNode treeNode)
		{
			base.Init(context, treeNode);
			this.Symbol = treeNode.Token.ValueString;
			this.AsString = this.Symbol;
		}

		/// <summary>
		/// Executed only once, on the first call
		/// </summary>
		/// <param name="thread"></param>
		/// <returns></returns>
		protected override object DoEvaluate(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;
			this.accessor = thread.Bind(Symbol, BindingRequestFlags.Read);

			// Optimization - directly set method ref to accessor's method. EvaluateReader;
			this.Evaluate = this.accessor.GetValueRef;
			var result = this.Evaluate(thread);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}
	}
}
