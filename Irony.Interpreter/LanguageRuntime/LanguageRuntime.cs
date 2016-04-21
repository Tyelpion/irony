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
	public class ConsoleWriteEventArgs : EventArgs
	{
		public string Text;

		public ConsoleWriteEventArgs(string text)
		{
			this.Text = text;
		}
	}

	/// <summary>
	/// Note: mark the derived language-specific class as sealed - important for JIT optimizations
	/// details here: http://www.codeproject.com/KB/dotnet/JITOptimizations.aspx
	/// </summary>
	public partial class LanguageRuntime
	{
		public readonly LanguageData Language;

		/// <summary>
		/// Converter of the result for comparison operation; converts bool value to values
		/// specific for the language
		/// </summary>
		public UnaryOperatorMethod BoolResultConverter;

		/// <summary>
		/// Built-in binding sources
		/// </summary>
		public BindingSourceTable BuiltIns;

		public OperatorHandler OperatorHandler;

		public LanguageRuntime(LanguageData language)
		{
			this.Language = language;
			this.NoneValue = NoneClass.Value;
			this.BuiltIns = new BindingSourceTable(this.Language.Grammar.CaseSensitive);
			this.Init();
		}

		/// <summary>
		/// An unassigned reserved object for a language implementation
		/// </summary>
		public NoneClass NoneValue { get; protected set; }

		public virtual void Init()
		{
			this.InitOperatorImplementations();
		}

		public virtual bool IsTrue(object value)
		{
			if (value is bool)
				return (bool) value;

			if (value is int)
				return ((int) value != 0);

			if (value == this.NoneValue)
				return false;

			return value != null;
		}

		protected internal void ThrowError(string message, params object[] args)
		{
			if (args != null && args.Length > 0)
				message = string.Format(message, args);

			throw new Exception(message);
		}

		protected internal void ThrowScriptError(string message, params object[] args)
		{
			if (args != null && args.Length > 0)
				message = string.Format(message, args);

			throw new ScriptException(message);
		}
	}
}
