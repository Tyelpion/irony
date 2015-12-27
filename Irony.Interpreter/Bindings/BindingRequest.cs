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

using System;
using Irony.Interpreter.Ast;

namespace Irony.Interpreter
{
	[Flags]
	public enum BindingRequestFlags
	{
		Read = 0x01,
		Write = 0x02,
		Invoke = 0x04,
		ExistingOrNew = 0x10,

		/// <summary>
		/// For new variable, for ex, in JavaScript "var x..." - introduces x as new variable
		/// </summary>
		NewOnly = 0x20
	}

	/// <summary>
	/// Binding request is a container for information about requested binding. Binding request goes from an Ast node to language runtime.
	/// For example, identifier node would request a binding for an identifier.
	/// </summary>
	public class BindingRequest
	{
		public BindingRequestFlags Flags;
		public ModuleInfo FromModule;
		public AstNode FromNode;
		public ScopeInfo FromScopeInfo;
		public bool IgnoreCase;
		public string Symbol;
		public ScriptThread Thread;

		public BindingRequest(ScriptThread thread, AstNode fromNode, string symbol, BindingRequestFlags flags)
		{
			this.Thread = thread;
			this.FromNode = fromNode;
			this.FromModule = thread.App.DataMap.GetModule(fromNode.ModuleNode);
			this.Symbol = symbol;
			this.Flags = flags;
			this.FromScopeInfo = thread.CurrentScope.Info;
			this.IgnoreCase = !thread.Runtime.Language.Grammar.CaseSensitive;
		}
	}
}
