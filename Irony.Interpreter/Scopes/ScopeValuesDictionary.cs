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

namespace Irony.Interpreter
{
	/// <summary>
	/// A wrapper around Scope exposing it as a string-object dictionary. Used to expose Globals dictionary from Main scope
	/// </summary>
	public class ScopeValuesDictionary : IDictionary<string, object>
	{
		private readonly ScopeBase scope;

		internal ScopeValuesDictionary(ScopeBase scope)
		{
			this.scope = scope;
		}

		public int Count
		{
			get { return this.scope.Info.GetSlotCount(); }
		}

		public bool IsReadOnly
		{
			get { return true; }
		}

		public ICollection<string> Keys
		{
			get { return this.scope.Info.GetNames(); }
		}

		public ICollection<object> Values
		{
			get { return this.scope.GetValues(); }
		}

		public object this[string key]
		{
			get
			{
				object value;
				this.TryGetValue(key, out value);

				return value;
			}
			set
			{
				this.Add(key, value);
			}
		}

		public void Add(string key, object value)
		{
			var slot = this.scope.Info.GetSlot(key);

			if (slot == null)
				slot = this.scope.AddSlot(key);

			this.scope.SetValue(slot.Index, value);
		}

		public void Add(KeyValuePair<string, object> item)
		{
			this.Add(item.Key, item.Value);
		}

		public void Clear()
		{
			var values = this.scope.GetValues();

			for (var i = 0; i < values.Length; i++)
			{
				values[i] = null;
			}
		}

		public bool Contains(KeyValuePair<string, object> item)
		{
			return this.scope.Info.GetSlot(item.Key) != null;
		}

		public bool ContainsKey(string key)
		{
			return this.scope.Info.GetSlot(key) != null;
		}

		public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
		{
			// Make local copy
			var slots = this.scope.Info.GetSlots();

			foreach (var slot in slots)
			{
				yield return new KeyValuePair<string, object>(slot.Name, this.scope.GetValue(slot.Index));
			}
		}

		/// <summary>
		/// We do not remove the slotInfo (you can't do that, slot set can only grow); instead we set the value to null
		/// to indicate "unassigned"
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public bool Remove(string key)
		{
			this[key] = null;
			return true;
		}

		public bool Remove(KeyValuePair<string, object> item)
		{
			return Remove(item.Key);
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public bool TryGetValue(string key, out object value)
		{
			value = null;
			var slot = this.scope.Info.GetSlot(key);

			if (slot == null)
				return false;

			value = this.scope.GetValue(slot.Index);
			return true;
		}
	}
}
