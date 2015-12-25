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

using System.Collections.Generic;

namespace Irony.Interpreter
{
	public class ModuleInfo
	{
		public readonly string FileName;

		public readonly BindingSourceList Imports = new BindingSourceList();

		public readonly string Name;

		/// <summary>
		/// Scope for module variables
		/// </summary>
		public readonly ScopeInfo ScopeInfo;

		public ModuleInfo(string name, string fileName, ScopeInfo scopeInfo)
		{
			this.Name = name;
			this.FileName = fileName;
			this.ScopeInfo = scopeInfo;
		}

		/// <summary>
		/// Used for imported modules
		/// </summary>
		/// <param name="request"></param>
		/// <returns></returns>
		public Binding BindToExport(BindingRequest request)
		{
			return null;
		}
	}

	public class ModuleInfoList : List<ModuleInfo> { }
}
