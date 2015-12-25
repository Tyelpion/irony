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

namespace Irony.Interpreter
{
	public class Scope : ScopeBase
	{
		public Scope Caller;

		/// <summary>
		/// Either caller or closure parent
		/// </summary>
		public Scope Creator;

		public object[] Parameters;

		/// <summary>
		/// Computed on demand
		/// </summary>
		private Scope parent;

		public Scope(ScopeInfo scopeInfo, Scope caller, Scope creator, object[] parameters) : base(scopeInfo)
		{
			this.Caller = caller;
			this.Creator = creator;
			this.Parameters = parameters;
		}

		/// <summary>
		/// Lexical parent, computed on demand
		/// </summary>
		public Scope Parent
		{
			get
			{
				if (this.parent == null)
					this.parent = this.GetParent();

				return this.parent;
			}
			set
			{
				this.parent = value;
			}
		}

		public object GetParameter(int index)
		{
			return this.Parameters[index];
		}

		public object[] GetParameters()
		{
			return this.Parameters;
		}

		public void SetParameter(int index, object value)
		{
			this.Parameters[index] = value;
		}

		protected Scope GetParent()
		{
			// Walk along creators chain and find a scope with ScopeInfo matching this.ScopeInfo.Parent
			var parentScopeInfo = this.Info.Parent;
			if (parentScopeInfo == null)
				return null;

			var current = this.Creator;
			while (current != null)
			{
				if (current.Info == parentScopeInfo)
					return current;

				current = current.Creator;
			}

			return null;
		}
	}
}
