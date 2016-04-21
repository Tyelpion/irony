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
using System.Reflection;
using System.Security;
using System.Text;
using Irony.Interpreter.Ast;
using Irony.Parsing;

namespace Irony.Interpreter
{
	public enum AppStatus
	{
		Ready,
		Evaluating,

		/// <summary>
		/// Command line only
		/// </summary>
		WaitingMoreInput,

		SyntaxError,
		RuntimeError,

		/// <summary>
		/// Interpreter crash
		/// </summary>
		Crash,

		Aborted
	}

	/// <summary>
	/// Represents a running instance of a script application.
	/// </summary>
	public sealed class ScriptApp
	{
		public readonly LanguageData Language;

		public readonly LanguageRuntime Runtime;

		public AppDataMap DataMap;

		public long EvaluationTime;

		public Exception LastException;

		public Scope MainScope;

		public StringBuilder OutputBuffer = new StringBuilder();

		public bool RethrowExceptions = true;

		public Scope[] StaticScopes;

		public AppStatus Status;

		private readonly object lockObject = new object();

		private IList<Assembly> importedAssemblies = new List<Assembly>();

		public IDictionary<string, object> Globals { get; private set; }

		/// <summary>
		/// The root node of the last executed script
		/// </summary>
		public ParseTree LastScript { get; private set; }

		public Parser Parser { get; private set; }

		#region Constructors

		public ScriptApp(LanguageData language)
		{
			this.Language = language;
			var grammar = language.Grammar as InterpretedLanguageGrammar;
			this.Runtime = grammar.CreateRuntime(language);
			this.DataMap = new AppDataMap(this.Language.Grammar.CaseSensitive);
			this.Init();
		}

		public ScriptApp(LanguageRuntime runtime)
		{
			this.Runtime = runtime;
			this.Language = this.Runtime.Language;
			this.DataMap = new AppDataMap(this.Language.Grammar.CaseSensitive);
			this.Init();
		}

		public ScriptApp(AppDataMap dataMap)
		{
			this.DataMap = dataMap;
			this.Init();
		}

		[SecuritySafeCritical]
		private void Init()
		{
			this.Parser = new Parser(this.Language);

			// Create static scopes
			this.MainScope = new Scope(this.DataMap.MainModule.ScopeInfo, null, null, null);
			this.StaticScopes = new Scope[this.DataMap.StaticScopeInfos.Count];
			this.StaticScopes[0] = this.MainScope;
			this.Globals = this.MainScope.AsDictionary();
		}

		#endregion Constructors

		public ParseMode ParserMode
		{
			get { return this.Parser.Context.Mode; }
			set { this.Parser.Context.Mode = value; }
		}

		public IEnumerable<Assembly> GetImportAssemblies()
		{
			// Simple default case - return all assemblies loaded in domain
			return AppDomain.CurrentDomain.GetAssemblies();
		}

		public LogMessageList GetParserMessages()
		{
			return this.Parser.Context.CurrentParseTree.ParserMessages;
		}

		#region Evaluation

		public object Evaluate(string script)
		{
			try
			{
				var parsedScript = this.Parser.Parse(script);
				if (parsedScript.HasErrors())
				{
					this.Status = AppStatus.SyntaxError;
					if (this.RethrowExceptions)
						throw new ScriptException("Syntax errors found.");

					return null;
				}

				if (this.ParserMode == ParseMode.CommandLine && this.Parser.Context.Status == ParserStatus.AcceptedPartial)
				{
					this.Status = AppStatus.WaitingMoreInput;
					return null;
				}

				this.LastScript = parsedScript;
				var result = this.EvaluateParsedScript();

				return result;
			}
			catch (ScriptException)
			{
				throw;
			}
			catch (Exception ex)
			{
				this.LastException = ex;
				this.Status = AppStatus.Crash;
				return null;
			}
		}

		/// <summary>
		/// Irony interpreter requires that once a script is executed in a ScriptApp, it is bound to AppDataMap object,
		/// and all later script executions should be performed only in the context of the same app (or at least by an App with the same DataMap).
		/// The reason is because the first execution sets up a data-binding fields, like slots, scopes, etc, which are bound to ScopeInfo objects,
		/// which in turn is part of DataMap.
		/// </summary>
		/// <param name="parsedScript"></param>
		/// <returns></returns>
		public object Evaluate(ParseTree parsedScript)
		{
			Util.Check(parsedScript.Root.AstNode != null, "Root AST node is null, cannot evaluate script. Create AST tree first.");
			var root = parsedScript.Root.AstNode as AstNode;

			Util.Check(root != null, "Root AST node {0} is not a subclass of Irony.Interpreter.AstNode. ScriptApp cannot evaluate this script.", root.GetType());
			Util.Check(root.Parent == null || root.Parent == this.DataMap.ProgramRoot, "Cannot evaluate parsed script. It had been already evaluated in a different application.");
			this.LastScript = parsedScript;

			return this.EvaluateParsedScript();
		}

		public object Evaluate()
		{
			Util.Check(this.LastScript != null, "No previously parsed/evaluated script.");

			return this.EvaluateParsedScript();
		}

		/// <summary>
		/// Actual implementation
		/// </summary>
		/// <returns></returns>
		private object EvaluateParsedScript()
		{
			this.LastScript.Tag = this.DataMap;
			var root = this.LastScript.Root.AstNode as AstNode;
			root.DependentScopeInfo = this.MainScope.Info;

			this.Status = AppStatus.Evaluating;
			ScriptThread thread = null;

			try
			{
				thread = new ScriptThread(this);
				var result = root.Evaluate(thread);

				if (result != null)
					thread.App.WriteLine(result.ToString());

				this.Status = AppStatus.Ready;
				return result;
			}
			catch (ScriptException se)
			{
				this.Status = AppStatus.RuntimeError;
				se.Location = thread.CurrentNode.Location;
				se.ScriptStackTrace = thread.GetStackTrace();
				this.LastException = se;

				if (this.RethrowExceptions)
					throw;

				return null;
			}
			catch (Exception ex)
			{
				this.Status = AppStatus.RuntimeError;
				var se = new ScriptException(ex.Message, ex, thread.CurrentNode.Location, thread.GetStackTrace());
				this.LastException = se;

				if (this.RethrowExceptions)
					throw se;

				return null;
			}
		}

		#endregion Evaluation

		#region Output writing

		#region ConsoleWrite event

		public event EventHandler<ConsoleWriteEventArgs> ConsoleWrite;

		private void OnConsoleWrite(string text)
		{
			if (this.ConsoleWrite != null)
			{
				var args = new ConsoleWriteEventArgs(text);
				this.ConsoleWrite(this, args);
			}
		}

		#endregion ConsoleWrite event

		public void ClearOutputBuffer()
		{
			lock (this.lockObject)
			{
				this.OutputBuffer.Clear();
			}
		}

		public string GetOutput()
		{
			lock (this.lockObject)
			{
				return OutputBuffer.ToString();
			}
		}

		public void Write(string text)
		{
			lock (this.lockObject)
			{
				this.OnConsoleWrite(text);
				this.OutputBuffer.Append(text);
			}
		}

		public void WriteLine(string text)
		{
			lock (this.lockObject)
			{
				this.OnConsoleWrite(text + Environment.NewLine);
				this.OutputBuffer.AppendLine(text);
			}
		}

		#endregion Output writing
	}
}
