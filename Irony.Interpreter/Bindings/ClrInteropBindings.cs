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
using System.Reflection;
using Irony.Interpreter.Ast;

/*
 * Unfinished, work in progress, file disabled for now
*/

namespace Irony.Interpreter
{
	public enum ClrTargetType
	{
		Namespace,
		Type,
		Method,
		Property,
		Field,
	}

	/// <summary>
	/// Method for adding methods to BuiltIns table in Runtime
	/// </summary>
	public static partial class BindingSourceTableExtensions
	{
		public static void ImportStaticMembers(this BindingSourceTable targets, Type fromType)
		{
			var members = fromType.GetMembers(BindingFlags.Public | BindingFlags.Static);

			foreach (var member in members)
			{
				if (targets.ContainsKey(member.Name))
					// Do not import overloaded methods several times
					continue;

				switch (member.MemberType)
				{
					case MemberTypes.Method:
						targets.Add(member.Name, new ClrMethodBindingTargetInfo(fromType, member.Name));
						break;

					case MemberTypes.Property:
						targets.Add(member.Name, new ClrPropertyBindingTargetInfo(member as PropertyInfo, null));
						break;

					case MemberTypes.Field:
						targets.Add(member.Name, new ClrFieldBindingTargetInfo(member as FieldInfo, null));
						break;
				}
			}
		}
	}

	public class ClrFieldBindingTargetInfo : ClrInteropBindingTargetInfo
	{
		public FieldInfo Field;
		public object Instance;
		private Binding binding;

		public ClrFieldBindingTargetInfo(FieldInfo field, object instance) : base(field.Name, ClrTargetType.Field)
		{
			this.Field = field;
			this.Instance = instance;
			this.binding = new Binding(this);
			this.binding.GetValueRef = this.GetPropertyValue;
			this.binding.SetValueRef = this.SetPropertyValue;
		}

		public override Binding Bind(BindingRequest request)
		{
			return this.binding;
		}

		private object GetPropertyValue(ScriptThread thread)
		{
			var result = this.Field.GetValue(this.Instance);
			return result;
		}

		private void SetPropertyValue(ScriptThread thread, object value)
		{
			this.Field.SetValue(this.Instance, value);
		}
	}

	public class ClrInteropBindingTargetInfo : BindingTargetInfo, IBindingSource
	{
		public ClrTargetType TargetSubType;

		public ClrInteropBindingTargetInfo(string symbol, ClrTargetType targetSubType) : base(symbol, BindingTargetType.ClrInterop)
		{
			this.TargetSubType = targetSubType;
		}

		public virtual Binding Bind(BindingRequest request)
		{
			throw new NotImplementedException();
		}
	}

	public class ClrMethodBindingTargetInfo : ClrInteropBindingTargetInfo, ICallTarget
	{
		public Type DeclaringType;

		/// <summary>
		/// The object works as ICallTarget itself
		/// </summary>
		public object Instance;

		private Binding binding;
		private readonly BindingFlags invokeFlags;

		public ClrMethodBindingTargetInfo(Type declaringType, string methodName, object instance = null) : base(methodName, ClrTargetType.Method)
		{
			this.DeclaringType = declaringType;
			this.Instance = instance;
			this.invokeFlags = BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.NonPublic;

			if (this.Instance == null)
				this.invokeFlags |= BindingFlags.Static;
			else
				this.invokeFlags |= BindingFlags.Instance;

			this.binding = new ConstantBinding(target: this as ICallTarget, targetInfo: this);

			// The object works as CallTarget itself; the "as" conversion is not needed in fact, we do it just to underline the role
		}

		public override Binding Bind(BindingRequest request)
		{
			return this.binding;
		}

		#region ICalllable.Call implementation

		public object Call(ScriptThread thread, object[] args)
		{
			// TODO: fix this. Currently doing it slow but easy way, through reflection
			if (args != null && args.Length == 0)
				args = null;

			var result = DeclaringType.InvokeMember(this.Symbol, this.invokeFlags, null, this.Instance, args);
			return result;
		}

		#endregion ICalllable.Call implementation
	}

	public class ClrNamespaceBindingTargetInfo : ClrInteropBindingTargetInfo
	{
		private readonly ConstantBinding binding;

		public ClrNamespaceBindingTargetInfo(string ns) : base(ns, ClrTargetType.Namespace)
		{
			this.binding = new ConstantBinding(ns, this);
		}

		public override Binding Bind(BindingRequest request)
		{
			return this.binding;
		}
	}

	public class ClrPropertyBindingTargetInfo : ClrInteropBindingTargetInfo
	{
		public object Instance;
		public PropertyInfo Property;
		private Binding binding;

		public ClrPropertyBindingTargetInfo(PropertyInfo property, object instance) : base(property.Name, ClrTargetType.Property)
		{
			this.Property = property;
			this.Instance = instance;
			this.binding = new Binding(this);
			this.binding.GetValueRef = this.GetPropertyValue;
			this.binding.SetValueRef = this.SetPropertyValue;
		}

		public override Binding Bind(BindingRequest request)
		{
			return this.binding;
		}

		private object GetPropertyValue(ScriptThread thread)
		{
			var result = this.Property.GetValue(this.Instance, null);
			return result;
		}

		private void SetPropertyValue(ScriptThread thread, object value)
		{
			this.Property.SetValue(this.Instance, value, null);
		}
	}

	public class ClrTypeBindingTargetInfo : ClrInteropBindingTargetInfo
	{
		private readonly ConstantBinding binding;

		public ClrTypeBindingTargetInfo(Type type) : base(type.Name, ClrTargetType.Type)
		{
			this.binding = new ConstantBinding(type, this);
		}

		public override Binding Bind(BindingRequest request)
		{
			return this.binding;
		}
	}
}
