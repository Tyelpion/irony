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
using Irony.Parsing;

namespace Irony.Interpreter
{
	/// <summary>
	/// Represents a running thread in script application.
	/// </summary>
	public sealed class ScriptThread : IBindingSource
	{
		public readonly ScriptApp App;

		public readonly LanguageRuntime Runtime;

		public AstNode CurrentNode;

		public Scope CurrentScope;

		/// <summary>
		/// Tail call parameters
		/// </summary>
		public ICallTarget Tail;

		public object[] TailArgs;

		public ScriptThread(ScriptApp app)
		{
			this.App = app;
			this.Runtime = this.App.Runtime;
			this.CurrentScope = app.MainScope;
		}

		public Binding Bind(string symbol, BindingRequestFlags options)
		{
			var request = new BindingRequest(this, this.CurrentNode, symbol, options);
			var binding = this.Bind(request);

			if (binding == null)
				this.ThrowScriptError("Unknown symbol '{0}'.", symbol);

			return binding;
		}

		public void PopScope()
		{
			this.CurrentScope = this.CurrentScope.Caller;
		}

		public void PushClosureScope(ScopeInfo scopeInfo, Scope closureParent, object[] parameters)
		{
			this.CurrentScope = new Scope(scopeInfo, this.CurrentScope, closureParent, parameters);
		}

		public void PushScope(ScopeInfo scopeInfo, object[] parameters)
		{
			this.CurrentScope = new Scope(scopeInfo, this.CurrentScope, this.CurrentScope, parameters);
		}

		#region Exception handling

		/// <summary>
		/// TODO: add construction of Script Call stack
		/// </summary>
		/// <returns></returns>
		public ScriptStackTrace GetStackTrace()
		{
			return new ScriptStackTrace();
		}

		public object HandleError(Exception exception)
		{
			if (exception is ScriptException)
				throw exception;

			var stack = this.GetStackTrace();
			var rex = new ScriptException(exception.Message, exception, CurrentNode.ErrorAnchor, stack);

			throw rex;
		}

		/// <summary>
		/// Throws ScriptException exception.
		/// </summary>
		/// <param name="message"></param>
		/// <param name="args"></param>
		public void ThrowScriptError(string message, params object[] args)
		{
			if (args != null && args.Length > 0)
				message = string.Format(message, args);

			var loc = this.GetCurrentLocation();
			var stack = this.GetStackTrace();

			throw new ScriptException(message, null, loc, stack);
		}

		private SourceLocation GetCurrentLocation()
		{
			return this.CurrentNode == null ? new SourceLocation() : this.CurrentNode.Location;
		}

		#endregion Exception handling

		#region IBindingSource Members

		public Binding Bind(BindingRequest request)
		{
			return this.Runtime.Bind(request);
		}

		#endregion IBindingSource Members
	}
}
