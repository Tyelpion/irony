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
	public enum SlotType
	{
		/// <summary>
		/// Local or property value
		/// </summary>
		Value,

		/// <summary>
		/// Function parameter
		/// </summary>
		Parameter,

		Function,
		Closure,
	}

	/// <summary>
	/// Describes a variable.
	/// </summary>
	public class SlotInfo
	{
		public readonly int Index;

		public readonly string Name;

		public readonly ScopeInfo ScopeInfo;

		public readonly SlotType Type;

		/// <summary>
		/// For module-level slots, indicator that symbol is "exported" and visible by code that imports the module
		/// </summary>
		public bool IsPublic = true;

		internal SlotInfo(ScopeInfo scopeInfo, SlotType type, string name, int index)
		{
			this.ScopeInfo = scopeInfo;
			this.Type = type;
			this.Name = name;
			this.Index = index;
		}
	}

	public class SlotInfoDictionary : Dictionary<string, SlotInfo>
	{
		public SlotInfoDictionary(bool caseSensitive)
		  : base(32, caseSensitive ? StringComparer.InvariantCulture : StringComparer.InvariantCultureIgnoreCase)
		{ }
	}
}
