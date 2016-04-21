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
	/// <summary>
	/// A class for special reserved None value used in many scripting languages.
	/// </summary>
	public class NoneClass
	{
		public static NoneClass Value = new NoneClass();

		private readonly string toString;

		public NoneClass(string toString)
		{
			this.toString = toString;
		}

		private NoneClass()
		{
			this.toString = Resources.LabelNone;
		}

		public override string ToString()
		{
			return this.toString;
		}
	}
}
