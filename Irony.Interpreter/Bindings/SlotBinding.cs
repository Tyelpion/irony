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
	/// Implements fast access to a variable (local/global var or parameter) in local scope or in any enclosing scope
	/// Important: the following code is very sensitive to even tiny changes - do not know exactly particular reasons.
	/// </summary>
	public sealed class SlotBinding : Binding
	{
		public AstNode FromNode;
		public ScopeInfo FromScope;
		public SlotInfo Slot;
		public int SlotIndex;
		public int StaticScopeIndex;

		public SlotBinding(SlotInfo slot, AstNode fromNode, ScopeInfo fromScope) : base(slot.Name, BindingTargetType.Slot)
		{
			this.Slot = slot;
			this.FromNode = fromNode;
			this.FromScope = fromScope;
			this.SlotIndex = slot.Index;
			this.StaticScopeIndex = Slot.ScopeInfo.StaticIndex;
			this.SetupAccessorMethods();
		}

		private object FastGetCurrentScopeParameter(ScriptThread thread)
		{
			// Optimization: we go directly for parameters array; if we fail, then we fallback to regular "proper" method.
			try
			{
				return thread.CurrentScope.Parameters[this.SlotIndex];
			}
			catch
			{
				return this.GetCurrentScopeParameter(thread);
			}
		}

		private object FastGetCurrentScopeValue(ScriptThread thread)
		{
			try
			{
				// Optimization: we go directly for values array; if we fail, then we fallback to regular "proper" method.
				return thread.CurrentScope.Values[this.SlotIndex];
			}
			catch
			{
				return this.GetCurrentScopeValue(thread);
			}
		}

		private object FastGetStaticValue(ScriptThread thread)
		{
			try
			{
				return thread.App.StaticScopes[StaticScopeIndex].Values[this.SlotIndex];
			}
			catch
			{
				return this.GetStaticValue(thread);
			}
		}

		private object GetCurrentScopeParameter(ScriptThread thread)
		{
			try
			{
				return thread.CurrentScope.GetParameter(this.SlotIndex);
			}
			catch
			{
				thread.CurrentNode = this.FromNode;
				throw;
			}
		}

		private object GetCurrentScopeValue(ScriptThread thread)
		{
			try
			{
				return thread.CurrentScope.GetValue(this.SlotIndex);
			}
			catch
			{
				thread.CurrentNode = this.FromNode;
				throw;
			}
		}

		private object GetImmediateParentScopeParameter(ScriptThread thread)
		{
			try
			{
				return thread.CurrentScope.Parent.Parameters[this.SlotIndex];
			}
			catch
			{ }

			// Full method
			try
			{
				return thread.CurrentScope.Parent.GetParameter(this.SlotIndex);
			}
			catch
			{
				thread.CurrentNode = this.FromNode;
				throw;
			}
		}

		private object GetImmediateParentScopeValue(ScriptThread thread)
		{
			try
			{
				return thread.CurrentScope.Parent.Values[this.SlotIndex];
			}
			catch
			{ }

			// Full method
			try
			{
				return thread.CurrentScope.Parent.GetValue(this.SlotIndex);
			}
			catch
			{
				thread.CurrentNode = this.FromNode;
				throw;
			}
		}

		private object GetParentScopeParameter(ScriptThread thread)
		{
			var targetScope = this.GetTargetScope(thread);

			return targetScope.GetParameter(this.SlotIndex);
		}

		private object GetParentScopeValue(ScriptThread thread)
		{
			var targetScope = this.GetTargetScope(thread);

			return targetScope.GetValue(this.SlotIndex);
		}

		private object GetStaticValue(ScriptThread thread)
		{
			try
			{
				return thread.App.StaticScopes[StaticScopeIndex].GetValue(this.SlotIndex);
			}
			catch
			{
				thread.CurrentNode = this.FromNode;
				throw;
			}
		}

		private Scope GetTargetScope(ScriptThread thread)
		{
			var targetLevel = this.Slot.ScopeInfo.Level;
			var scope = thread.CurrentScope.Parent;

			while (scope.Info.Level > targetLevel)
			{
				scope = scope.Parent;
			}

			return scope;
		}

		private void SetCurrentScopeParameter(ScriptThread thread, object value)
		{
			thread.CurrentScope.SetParameter(this.SlotIndex, value);
		}

		private void SetCurrentScopeValue(ScriptThread thread, object value)
		{
			thread.CurrentScope.SetValue(this.SlotIndex, value);
		}

		private void SetImmediateParentScopeParameter(ScriptThread thread, object value)
		{
			thread.CurrentScope.Parent.SetParameter(this.SlotIndex, value);
		}

		private void SetImmediateParentScopeValue(ScriptThread thread, object value)
		{
			thread.CurrentScope.Parent.SetValue(this.SlotIndex, value);
		}

		private void SetParentScopeParameter(ScriptThread thread, object value)
		{
			var targetScope = this.GetTargetScope(thread);
			targetScope.SetParameter(this.SlotIndex, value);
		}

		private void SetParentScopeValue(ScriptThread thread, object value)
		{
			var targetScope = this.GetTargetScope(thread);
			targetScope.SetValue(this.SlotIndex, value);
		}

		private void SetStatic(ScriptThread thread, object value)
		{
			thread.App.StaticScopes[this.StaticScopeIndex].SetValue(this.SlotIndex, value);
		}

		private void SetupAccessorMethods()
		{
			// Check module scope
			if (this.Slot.ScopeInfo.StaticIndex >= 0)
			{
				this.GetValueRef = this.FastGetStaticValue;
				this.SetValueRef = this.SetStatic;
				return;
			}

			var levelDiff = this.Slot.ScopeInfo.Level - this.FromScope.Level;
			switch (levelDiff)
			{
				case 0: // Local scope
					if (this.Slot.Type == SlotType.Value)
					{
						this.GetValueRef = this.FastGetCurrentScopeValue;
						this.SetValueRef = this.SetCurrentScopeValue;
					}
					else
					{
						this.GetValueRef = this.FastGetCurrentScopeParameter;
						this.SetValueRef = this.SetCurrentScopeParameter;
					}
					return;

				case 1: // Direct parent
					if (Slot.Type == SlotType.Value)
					{
						this.GetValueRef = this.GetImmediateParentScopeValue;
						this.SetValueRef = this.SetImmediateParentScopeValue;
					}
					else
					{
						this.GetValueRef = this.GetImmediateParentScopeParameter;
						this.SetValueRef = this.SetImmediateParentScopeParameter;
					}
					return;

				default: // Some enclosing scope
					if (Slot.Type == SlotType.Value)
					{
						this.GetValueRef = this.GetParentScopeValue;
						this.SetValueRef = this.SetParentScopeValue;
					}
					else
					{
						this.GetValueRef = this.GetParentScopeParameter;
						this.SetValueRef = this.SetParentScopeParameter;
					}
					return;
			}
		}

		#region Specific method implementations

		// Specific method implementations =======================================================================================================
		// Optimization: in most cases we go directly for Values array; if we fail, then we fallback to full method
		// with proper exception handling. This fallback is expected to be extremely rare, so overall we have considerable perf gain
		// Note that in we expect the methods to be used directly by identifier node (like: IdentifierNode.EvaluateRef = Binding.GetValueRef; } -
		// to save a few processor cycles. Therefore, we need to provide a proper context (thread.CurrentNode) in case of exception.
		// In all "full-method" implementations we set current node to FromNode, so exception correctly points
		// to the owner Identifier node as a location of error.

		#endregion Specific method implementations
	}
}
