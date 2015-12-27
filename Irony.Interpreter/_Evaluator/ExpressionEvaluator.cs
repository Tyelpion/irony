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
using Irony.Parsing;

namespace Irony.Interpreter.Evaluator
{
	public class ExpressionEvaluator
	{
		/// <summary>
		/// Default constructor, creates default evaluator
		/// </summary>
		public ExpressionEvaluator() : this(new ExpressionEvaluatorGrammar())
		{ }

		/// <summary>
		/// Default constructor, creates default evaluator
		/// </summary>
		/// <param name="grammar"></param>
		public ExpressionEvaluator(ExpressionEvaluatorGrammar grammar)
		{
			this.Grammar = grammar;
			this.Language = new LanguageData(this.Grammar);
			this.Parser = new Parser(this.Language);
			this.Runtime = this.Grammar.CreateRuntime(this.Language);
			this.App = new ScriptApp(this.Runtime);
		}

		public ScriptApp App { get; private set; }

		public IDictionary<string, object> Globals
		{
			get { return this.App.Globals; }
		}

		public ExpressionEvaluatorGrammar Grammar { get; private set; }

		public LanguageData Language { get; private set; }

		public Parser Parser { get; private set; }

		public LanguageRuntime Runtime { get; private set; }

		public void ClearOutput()
		{
			this.App.ClearOutputBuffer();
		}

		public object Evaluate(string script)
		{
			var result = this.App.Evaluate(script);
			return result;
		}

		public object Evaluate(ParseTree parsedScript)
		{
			var result = this.App.Evaluate(parsedScript);
			return result;
		}

		/// <summary>
		/// Evaluates again the previously parsed/evaluated script
		/// </summary>
		/// <returns></returns>
		public object Evaluate()
		{
			return this.App.Evaluate();
		}

		public string GetOutput()
		{
			return this.App.GetOutput();
		}
	}
}
