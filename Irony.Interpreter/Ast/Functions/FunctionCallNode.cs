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
	/// A node representing function call. Also handles Special Forms
	/// </summary>
	public class FunctionCallNode : AstNode
	{
		private SpecialForm specialForm;
		private AstNode[] specialFormArgs;
		private string targetName;
		private AstNode arguments;
		private AstNode targetRef;

		public override void Init(AstContext context, ParseTreeNode treeNode)
		{
			base.Init(context, treeNode);
			var nodes = treeNode.GetMappedChildNodes();
			this.targetRef = AddChild("Target", nodes[0]);
			this.targetRef.UseType = NodeUseType.CallTarget;
			this.targetName = nodes[0].FindTokenAndGetText();
			this.arguments = AddChild("Args", nodes[1]);
			AsString = "Call " + this.targetName;
		}

		protected override object DoEvaluate(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			this.SetupEvaluateMethod(thread);
			var result = this.Evaluate(thread);

			// Standard epilog
			thread.CurrentNode = Parent;
			return result;
		}

		/// <summary>
		/// Evaluation for non-tail languages
		/// </summary>
		/// <param name="thread"></param>
		/// <returns></returns>
		private object EvaluateNoTail(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			var target = this.targetRef.Evaluate(thread);
			var iCall = target as ICallTarget;
			if (iCall == null)
				thread.ThrowScriptError(Resources.ErrVarIsNotCallable, this.targetName);

			var args = (object[]) this.arguments.Evaluate(thread);
			object result = iCall.Call(thread, args);

			// Standard epilog
			thread.CurrentNode = Parent;
			return result;
		}

		/// <summary>
		/// Evaluation for special forms
		/// </summary>
		/// <param name="thread"></param>
		/// <returns></returns>
		private object EvaluateSpecialForm(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			var result = this.specialForm(thread, this.specialFormArgs);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}

		/// <summary>
		/// Evaluation for tailed languages
		/// </summary>
		/// <param name="thread"></param>
		/// <returns></returns>
		private object EvaluateTail(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			var target = this.targetRef.Evaluate(thread);
			var iCall = target as ICallTarget;
			if (iCall == null)
				thread.ThrowScriptError(Resources.ErrVarIsNotCallable, this.targetName);

			var args = (object[]) this.arguments.Evaluate(thread);
			thread.Tail = iCall;
			thread.TailArgs = args;

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return null;
		}

		private object EvaluateWithTailCheck(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			var target = this.targetRef.Evaluate(thread);
			var iCall = target as ICallTarget;
			if (iCall == null)
				thread.ThrowScriptError(Resources.ErrVarIsNotCallable, this.targetName);

			var args = (object[]) this.arguments.Evaluate(thread);
			object result = null;
			result = iCall.Call(thread, args);

			// Note that after invoking tail we can get another tail.
			// So we need to keep calling tails while they are there.
			while (thread.Tail != null)
			{
				var tail = thread.Tail;
				var tailArgs = thread.TailArgs;
				thread.Tail = null;
				thread.TailArgs = null;
				result = tail.Call(thread, tailArgs);
			}

			// Standard epilog
			thread.CurrentNode = Parent;
			return result;
		}

		private void SetupEvaluateMethod(ScriptThread thread)
		{
			var languageTailRecursive = thread.Runtime.Language.Grammar.LanguageFlags.IsSet(LanguageFlags.TailRecursive);
			lock (this.LockObject)
			{
				var target = this.targetRef.Evaluate(thread);
				if (target is SpecialForm)
				{
					this.specialForm = target as SpecialForm;
					this.specialFormArgs = this.arguments.ChildNodes.ToArray();
					this.Evaluate = this.EvaluateSpecialForm;
				}
				else {
					if (languageTailRecursive)
					{
						var isTail = Flags.IsSet(AstNodeFlags.IsTail);
						if (isTail)
							this.Evaluate = this.EvaluateTail;
						else
							this.Evaluate = this.EvaluateWithTailCheck;
					}
					else
						this.Evaluate = this.EvaluateNoTail;
				}
			}
		}
	}
}
