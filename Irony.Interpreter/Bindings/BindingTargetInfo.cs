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
	public enum BindingTargetType
	{
		Slot,
		BuiltInObject,
		SpecialForm,
		ClrInterop,

		/// <summary>
		/// Any special non-standard type for specific language
		/// </summary>
		Custom,
	}

	public class BindingTargetInfo
	{
		public readonly string Symbol;
		public readonly BindingTargetType Type;

		public BindingTargetInfo(string symbol, BindingTargetType type)
		{
			this.Symbol = symbol;
			this.Type = type;
		}

		public override string ToString()
		{
			return this.Symbol + "/" + this.Type.ToString();
		}
	}
}
