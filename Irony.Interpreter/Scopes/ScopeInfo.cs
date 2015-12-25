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
using System.Collections.Generic;
using Irony.Interpreter.Ast;

namespace Irony.Interpreter
{
	/// <summary>
	/// Describes all variables (locals and parameters) defined in a scope of a function or module.
	/// <para />
	/// Note that all access to SlotTable is done through "lock" operator, so it's thread safe
	/// </summary>
	/// <remarks>
	/// ScopeInfo is metadata, it does not contain variable values. The Scope object (described by ScopeInfo) is a container for values.
	/// </remarks>
	public class ScopeInfo
	{
		public readonly string AsString;
		public int Level;

		/// <summary>
		/// Might be null
		/// </summary>
		public AstNode OwnerNode;

		/// <summary>
		/// Experiment: reusable scope instance; see ScriptThread.cs class
		/// </summary>
		public Scope ScopeInstance;

		/// <summary>
		/// Static/singleton scopes only; for ex,  modules are singletons. Index in App.StaticScopes array
		/// </summary>
		public int StaticIndex = -1;

		public int ValuesCount, ParametersCount;

		protected internal object LockObject = new object();

		private ScopeInfo parent;

		private SlotInfoDictionary slots;

		public ScopeInfo(AstNode ownerNode, bool caseSensitive)
		{
			if (ownerNode == null)
				throw new Exception("ScopeInfo owner node may not be null.");

			this.OwnerNode = ownerNode;
			this.slots = new SlotInfoDictionary(caseSensitive);
			this.Level = this.Parent == null ? 0 : this.Parent.Level + 1;
			var sLevel = "level=" + this.Level;
			this.AsString = this.OwnerNode == null ? sLevel : this.OwnerNode.AsString + ", " + sLevel;
		}

		/// <summary>
		/// Lexical parent
		/// </summary>
		public ScopeInfo Parent
		{
			get
			{
				if (this.parent == null)
					this.parent = this.GetParent();

				return this.parent;
			}
		}

		public ScopeInfo GetParent()
		{
			if (this.OwnerNode == null)
				return null;

			var currentParent = this.OwnerNode.Parent;

			while (currentParent != null)
			{
				var result = currentParent.DependentScopeInfo;
				if (result != null)
					return result;

				currentParent = currentParent.Parent;
			}

			// Should never happen
			return null;
		}

		#region Slot operations

		public SlotInfo AddSlot(string name, SlotType type)
		{
			lock (this.LockObject)
			{
				var index = type == SlotType.Value ? this.ValuesCount++ : this.ParametersCount++;
				var slot = new SlotInfo(this, type, name, index);
				this.slots.Add(name, slot);

				return slot;
			}
		}

		public IList<string> GetNames()
		{
			lock (this.LockObject)
			{
				return new List<string>(this.slots.Keys);
			}
		}

		/// <summary>
		/// Returns null if slot not found.
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public SlotInfo GetSlot(string name)
		{
			lock (this.LockObject)
			{
				SlotInfo slot;
				this.slots.TryGetValue(name, out slot);

				return slot;
			}
		}

		public int GetSlotCount()
		{
			lock (this.LockObject)
			{
				return this.slots.Count;
			}
		}

		public IList<SlotInfo> GetSlots()
		{
			lock (this.LockObject)
			{
				return new List<SlotInfo>(this.slots.Values);
			}
		}

		#endregion Slot operations

		public override string ToString()
		{
			return this.AsString;
		}
	}

	public class ScopeInfoList : List<ScopeInfo> { }
}
