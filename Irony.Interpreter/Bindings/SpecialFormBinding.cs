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

namespace Irony.Interpreter
{
	public static partial class BindingSourceTableExtensions
	{
		/// <summary>
		/// Method for adding methods to BuiltIns table in Runtime
		/// </summary>
		/// <param name="targets"></param>
		/// <param name="form"></param>
		/// <param name="formName"></param>
		/// <param name="minChildCount"></param>
		/// <param name="maxChildCount"></param>
		/// <param name="parameterNames"></param>
		/// <returns></returns>
		public static BindingTargetInfo AddSpecialForm(this BindingSourceTable targets, SpecialForm form, string formName,
			int minChildCount = 0, int maxChildCount = 0, string parameterNames = null)
		{
			var formInfo = new SpecialFormBindingInfo(formName, form, minChildCount, maxChildCount, parameterNames);
			targets.Add(formName, formInfo);

			return formInfo;
		}
	}

	public class SpecialFormBindingInfo : BindingTargetInfo, IBindingSource
	{
		public readonly ConstantBinding Binding;
		public readonly int MinChildCount, MaxChildCount;
		public string[] ChildRoles;

		public SpecialFormBindingInfo(string symbol, SpecialForm form, int minChildCount = 0, int maxChildCount = 0, string childRoles = null)
			  : base(symbol, BindingTargetType.SpecialForm)
		{
			this.Binding = new ConstantBinding(form, this);
			this.MinChildCount = minChildCount;

			// If maxParamCount=0 then set it equal to minParamCount
			this.MaxChildCount = Math.Max(minChildCount, maxChildCount);

			if (!string.IsNullOrEmpty(childRoles))
			{
				this.ChildRoles = childRoles.Split(',');

				// TODO: add check that paramNames array is in accord with min/max param counts
			}
		}

		#region IBindingSource Members

		public Binding Bind(BindingRequest request)
		{
			return this.Binding;
		}

		#endregion IBindingSource Members
	}
}
