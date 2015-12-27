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

namespace Irony.Interpreter.Ast
{
	public delegate object EvaluateMethod(ScriptThread thread);

	public delegate void ValueSetterMethod(ScriptThread thread, object value);

	[Flags]
	public enum AstNodeFlags
	{
		None = 0x0,

		/// <summary>
		/// The node is in tail position
		/// </summary>
		IsTail = 0x01,

		/// <summary>
		/// Node defines scope for local variables
		/// </summary>
		IsScope = 0x02,
	}

	[Flags]
	public enum NodeUseType
	{
		Unknown,

		/// <summary>
		/// Identifier used as a Name container - system would not use it's Evaluate method directly
		/// </summary>
		Name,

		CallTarget,
		ValueRead,
		ValueWrite,
		ValueReadWrite,
		Parameter,
		Keyword,
		SpecialSymbol,
	}
}
