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

using Irony.Interpreter.Ast;

namespace Irony.Interpreter
{
	/// <summary>
	/// Represents a set of all of static scopes/modules in the application.
	/// </summary>
	public class AppDataMap
	{
		public readonly bool LanguageCaseSensitive;

		public ModuleInfo MainModule;

		public ModuleInfoList Modules = new ModuleInfoList();

		/// <summary>
		/// Artificial root associated with MainModule
		/// </summary>
		public AstNode ProgramRoot;

		public ScopeInfoList StaticScopeInfos = new ScopeInfoList();

		public AppDataMap(bool languageCaseSensitive, AstNode programRoot = null)
		{
			this.LanguageCaseSensitive = languageCaseSensitive;
			this.ProgramRoot = programRoot ?? new AstNode();

			var mainScopeInfo = new ScopeInfo(this.ProgramRoot, this.LanguageCaseSensitive);
			this.StaticScopeInfos.Add(mainScopeInfo);
			mainScopeInfo.StaticIndex = 0;

			this.MainModule = new ModuleInfo("main", "main", mainScopeInfo);
			this.Modules.Add(this.MainModule);
		}

		public ModuleInfo GetModule(AstNode moduleNode)
		{
			foreach (var m in this.Modules)
			{
				if (m.ScopeInfo == moduleNode.DependentScopeInfo)
					return m;
			}

			return null;
		}
	}
}
