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
	/// <summary>
	/// A general delegate representing a built-in method implementation.
	/// </summary>
	/// <param name="thread"></param>
	/// <param name="args"></param>
	/// <returns></returns>
	public delegate object BuiltInMethod(ScriptThread thread, object[] args);

	/// <summary>
	/// Method for adding methods to BuiltIns table in Runtime
	/// </summary>
	public static partial class BindingSourceTableExtensions
	{
		public static BindingTargetInfo AddMethod(this BindingSourceTable targets, BuiltInMethod method, string methodName,
			  int minParamCount = 0, int maxParamCount = 0, string parameterNames = null)
		{
			var callTarget = new BuiltInCallTarget(method, methodName, minParamCount, maxParamCount, parameterNames);
			var targetInfo = new BuiltInCallableTargetInfo(callTarget);
			targets.Add(methodName, targetInfo);
			return targetInfo;
		}
	}

	/// <summary>
	/// The class contains information about built-in function. It has double purpose.
	/// First, it is used as a BindingTargetInfo instance (meta-data) for a binding to a built-in function.
	/// Second, we use it as a reference to a custom built-in method that we store in LanguageRuntime.BuiltIns table.
	/// For this, we make it implement IBindingSource - we can add it to BuiltIns table of LanguageRuntime, which is a table of IBindingSource instances.
	/// Being IBindingSource, it can produce a binding object to the target method - singleton in fact;
	/// the same binding object is used for all calls to the method from all function-call AST nodes.
	/// </summary>
	public class BuiltInCallableTargetInfo : BindingTargetInfo, IBindingSource
	{
		/// <summary>
		/// A singleton binding instance; we share it for all AST nodes (function call nodes) that call the method.
		/// </summary>
		public Binding BindingInstance;

		public BuiltInCallableTargetInfo(BuiltInMethod method, string methodName, int minParamCount = 0, int maxParamCount = 0, string parameterNames = null)
			: this(new BuiltInCallTarget(method, methodName, minParamCount, maxParamCount, parameterNames))
		{ }

		public BuiltInCallableTargetInfo(BuiltInCallTarget target) : base(target.Name, BindingTargetType.BuiltInObject)
		{
			this.BindingInstance = new ConstantBinding(target, this);
		}

		/// <summary>
		/// Implement <see cref="IBindingSource.Bind(BindingRequest)"/>
		/// </summary>
		/// <param name="request"></param>
		/// <returns></returns>
		public Binding Bind(BindingRequest request)
		{
			return this.BindingInstance;
		}
	}

	/// <summary>
	/// A wrapper to convert <see cref="BuiltInMethod"/> delegate (referencing some custom method in <see cref="LanguageRuntime"/>)
	/// into an <see cref="ICallTarget"/> instance (expected by <see cref="FunctionCallNode"/>)
	/// </summary>
	public class BuiltInCallTarget : ICallTarget
	{
		public readonly BuiltInMethod Method;
		public readonly int MinParamCount, MaxParamCount;
		public string Name;

		/// <summary>
		/// Just for information purpose
		/// </summary>
		public string[] ParameterNames;

		public BuiltInCallTarget(BuiltInMethod method, string name, int minParamCount = 0, int maxParamCount = 0, string parameterNames = null)
		{
			this.Method = method;
			this.Name = name;
			this.MinParamCount = minParamCount;
			this.MaxParamCount = Math.Max(this.MinParamCount, maxParamCount);

			if (!string.IsNullOrEmpty(parameterNames))
				this.ParameterNames = parameterNames.Split(',');
		}

		#region ICallTarget Members

		public object Call(ScriptThread thread, object[] parameters)
		{
			return this.Method(thread, parameters);
		}

		#endregion ICallTarget Members
	}
}
