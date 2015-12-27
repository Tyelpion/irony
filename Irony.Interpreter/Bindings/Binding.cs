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

using Irony.Interpreter.Ast;

namespace Irony.Interpreter
{
	/// <summary>
	/// Binding is a link between a variable in the script (for ex, IdentifierNode) and a value storage  -
	/// a slot in local or module-level Scope. Binding to internal variables is supported by SlotBinding class.
	/// Alternatively a symbol can be bound to external CLR entity in imported namespace - class, function, property, etc.
	/// Binding is produced by Runtime.Bind method and allows read/write operations through GetValueRef and SetValueRef methods.
	/// </summary>
	public class Binding
	{
		public readonly BindingTargetInfo TargetInfo;

		/// <summary>
		/// Ref to Getter method implementation
		/// </summary>
		public EvaluateMethod GetValueRef;

		/// <summary>
		/// Ref to Setter method implementation
		/// </summary>
		public ValueSetterMethod SetValueRef;

		public Binding(BindingTargetInfo targetInfo)
		{
			this.TargetInfo = targetInfo;
		}

		public Binding(string symbol, BindingTargetType targetType)
		{
			this.TargetInfo = new BindingTargetInfo(symbol, targetType);
		}

		public bool IsConstant { get; protected set; }

		public override string ToString()
		{
			return "{Binding to + " + this.TargetInfo.ToString() + "}";
		}
	}

	/// <summary>
	/// Binding to a "fixed", constant value
	/// </summary>
	public class ConstantBinding : Binding
	{
		public object Target;

		public ConstantBinding(object target, BindingTargetInfo targetInfo) : base(targetInfo)
		{
			this.Target = target;
			this.GetValueRef = this.GetValue;
			this.IsConstant = true;
		}

		public object GetValue(ScriptThread thread)
		{
			return this.Target;
		}
	}
}
