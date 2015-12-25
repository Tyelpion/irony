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
using System.Threading;

namespace Irony.Interpreter
{
	public class ScopeBase
	{
		public ScopeInfo Info;

		public volatile object[] Values;

		public ScopeBase(ScopeInfo scopeInfo) : this(scopeInfo, null)
		{ }

		public ScopeBase(ScopeInfo scopeInfo, object[] values)
		{
			this.Info = scopeInfo;
			this.Values = values;
			if (this.Values == null)
				this.Values = new object[scopeInfo.ValuesCount];
		}

		public SlotInfo AddSlot(string name)
		{
			var slot = this.Info.AddSlot(name, SlotType.Value);
			if (slot.Index >= this.Values.Length)
				this.Resize(this.Values.Length + 4);

			return slot;
		}

		public IDictionary<string, object> AsDictionary()
		{
			return new ScopeValuesDictionary(this);
		}

		public object GetValue(int index)
		{
			try
			{
				var tmp = Values;

				// The following line may throw null-reference exception (tmp==null), if resizing is happening at the same time
				// It may also throw IndexOutOfRange exception if new variable was added by another thread in another frame(scope)
				// but this scope and Values array were created before that, so Values is shorter than #slots in SlotInfo.
				// But in this case, it does not matter, result value is null (unassigned)
				return tmp[index];
			}
			catch (NullReferenceException)
			{
				Thread.Sleep(0);

				// Silverlight does not have Thread.Yield;
				// Thread.Yield(); // maybe SpinWait.SpinOnce?

				// Repeat attempt
				return this.GetValue(index);
			}
			catch (IndexOutOfRangeException)
			{
				// We do not resize here, value is unassigned anyway.
				return null;
			}
		}

		public object[] GetValues()
		{
			return this.Values;
		}

		public void SetValue(int index, object value)
		{
			try
			{
				var tmp = this.Values;

				// The following line may throw null-reference exception (tmp==null), if resizing is happening at the same time
				// It may also throw IndexOutOfRange exception if new variable was added by another thread in another frame(scope)
				// but this scope and Values array were created before that, so Values is shorter than #slots in SlotInfo
				tmp[index] = value;

				// Now check that tmp is the same as Values - if not, then resizing happened in the middle,
				// so repeat assignment to make sure the value is in resized array.
				if (tmp != this.Values)
					// Do it again
					this.SetValue(index, value);
			}
			catch (NullReferenceException)
			{
				// It's  OK to Sleep intead of SpinWait - it is really rare event, so we don't care losing a few more cycles here.
				Thread.Sleep(0);

				// Repeat it again
				this.SetValue(index, value);
			}
			catch (IndexOutOfRangeException)
			{
				this.Resize(this.Info.GetSlotCount());

				// Repeat it again
				this.SetValue(index, value);
			}
		}

		#region Disable CS0420

		// Disabling warning: 'Values: a reference to a volatile field will not be treated as volatile'
		// According to MSDN for CS0420 warning (see http://msdn.microsoft.com/en-us/library/4bw5ewxy.aspx),
		// this does NOT apply to Interlocked API - which we use here.
		#pragma warning disable 0420

		#endregion Disable CS0420

		public override string ToString()
		{
			return this.Info.ToString();
		}

		protected void Resize(int newSize)
		{
			lock (this.Info.LockObject)
			{
				if (this.Values.Length >= newSize)
					return;

				object[] tmp = Interlocked.Exchange(ref this.Values, null);
				Array.Resize(ref tmp, newSize);
				Interlocked.Exchange(ref this.Values, tmp);
			}
		}
	}
}
