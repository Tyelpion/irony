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
using System.Threading;
using Irony.Parsing;

/*
 * WARNING: Ctrl-C for aborting running script does NOT work when you run console app from Visual Studio 2010.
 * Run executable directly from bin folder.
*/

namespace Irony.Interpreter
{
	/// <summary>
	/// An abstraction of a Console.
	/// </summary>
	public interface IConsoleAdaptor
	{
		bool Canceled { get; set; }

		int Read();

		//reads a key
		string ReadLine();

		void SetTextStyle(ConsoleTextStyle style);

		//reads a line; returns null if Ctrl-C is pressed
		void SetTitle(string title);

		void Write(string text);

		void WriteLine(string text);
	}

	public class CommandLine
	{
		#region Fields and properties

		public readonly IConsoleAdaptor Console;

		public readonly ScriptApp App;

		public readonly LanguageRuntime Runtime;

		public string Greeting;

		/// <summary>
		/// Default prompt
		/// </summary>
		public string Prompt;

		/// <summary>
		/// Prompt to show when more input is expected
		/// </summary>
		public string PromptMoreInput;

		public string Title;

		private Thread workerThread;

		public bool IsEvaluating { get; private set; }

		#endregion Fields and properties

		public CommandLine(LanguageRuntime runtime, IConsoleAdaptor console = null)
		{
			this.Runtime = runtime;
			this.Console = console ?? new ConsoleAdapter();
			var grammar = runtime.Language.Grammar;
			this.Title = grammar.ConsoleTitle;
			this.Greeting = grammar.ConsoleGreeting;
			this.Prompt = grammar.ConsolePrompt;
			this.PromptMoreInput = grammar.ConsolePromptMoreInput;
			this.App = new ScriptApp(Runtime);
			this.App.ParserMode = ParseMode.CommandLine;
			// this.App.PrintParseErrors = false;
			this.App.RethrowExceptions = false;
		}

		public void Run()
		{
			try
			{
				this.RunImpl();
			}
			catch (Exception ex)
			{
				this.Console.SetTextStyle(ConsoleTextStyle.Error);
				this.Console.WriteLine(Resources.ErrConsoleFatalError);
				this.Console.WriteLine(ex.ToString());
				this.Console.SetTextStyle(ConsoleTextStyle.Normal);
				this.Console.WriteLine(Resources.MsgPressAnyKeyToExit);
				this.Console.Read();
			}
		}

		private bool Confirm(string message)
		{
			this.Console.WriteLine(string.Empty);
			this.Console.Write(message);
			var input = this.Console.ReadLine();

			return Resources.ConsoleYesChars.Contains(input);
		}

		private void Evaluate(string script)
		{
			try
			{
				this.IsEvaluating = true;
				this.App.Evaluate(script);
			}
			finally
			{
				this.IsEvaluating = false;
			}
		}

		private void EvaluateAsync(string script)
		{
			this.IsEvaluating = true;
			this.workerThread = new Thread(this.WorkerThreadStart);
			this.workerThread.Start(script);
		}

		private void ReportException()
		{
			this.Console.SetTextStyle(ConsoleTextStyle.Error);
			var ex = this.App.LastException;
			var scriptEx = ex as ScriptException;
			if (scriptEx != null)
				this.Console.WriteLine(scriptEx.Message + " " + Resources.LabelLocation + " " + scriptEx.Location.ToUiString());
			else
			{
				if (this.App.Status == AppStatus.Crash)
					// Unexpected interpreter crash:  the full stack when debugging your language
					this.Console.WriteLine(ex.ToString());
				else
					this.Console.WriteLine(ex.Message);
			}
		}

		private void RunImpl()
		{
			this.Console.SetTitle(this.Title);
			this.Console.WriteLine(this.Greeting);
			string input;

			while (true)
			{
				this.Console.Canceled = false;
				this.Console.SetTextStyle(ConsoleTextStyle.Normal);
				string prompt = (this.App.Status == AppStatus.WaitingMoreInput ? this.PromptMoreInput : this.Prompt);

				// Write prompt, read input, check for Ctrl-C
				this.Console.Write(prompt);
				input = this.Console.ReadLine();

				if (this.Console.Canceled)
				{
					if (this.Confirm(Resources.MsgExitConsoleYN))
						return;
					else
						// From the start of the loop
						continue;
				}

				// Execute
				this.App.ClearOutputBuffer();
				this.EvaluateAsync(input);

				// Evaluate(input);
				this.WaitForScriptComplete();

				switch (App.Status)
				{
					case AppStatus.Ready:
						// Success
						this.Console.WriteLine(this.App.GetOutput());
						break;

					case AppStatus.SyntaxError:
						// Write all output we have
						this.Console.WriteLine(this.App.GetOutput());
						this.Console.SetTextStyle(ConsoleTextStyle.Error);

						foreach (var err in App.GetParserMessages())
						{
							// Show err location
							this.Console.WriteLine(string.Empty.PadRight(prompt.Length + err.Location.Column) + "^");

							// Print message
							this.Console.WriteLine(err.Message);
						}

						break;

					case AppStatus.Crash:
					case AppStatus.RuntimeError:
						this.ReportException();
						break;

					default: break;
				}
			}
		}

		private void WaitForScriptComplete()
		{
			this.Console.Canceled = false;

			while (true)
			{
				Thread.Sleep(50);
				if (!this.IsEvaluating)
					return;

				if (this.Console.Canceled)
				{
					this.Console.Canceled = false;
					if (this.Confirm(Resources.MsgAbortScriptYN))
						this.WorkerThreadAbort();
				}
			}
		}

		private void WorkerThreadAbort()
		{
			try
			{
				this.workerThread.Abort();
				this.workerThread.Join(50);
			}
			finally
			{
				this.IsEvaluating = false;
			}
		}

		private void WorkerThreadStart(object data)
		{
			try
			{
				var script = data as string;
				this.App.Evaluate(script);
			}
			finally
			{
				this.IsEvaluating = false;
			}
		}
	}
}
