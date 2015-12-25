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

/*
 * WARNING: Ctrl-C for aborting running script does NOT work when you run console app from Visual Studio 2010.
 * Run executable directly from bin folder.
*/

namespace Irony.Interpreter
{
	public enum ConsoleTextStyle
	{
		Normal,
		Error,
	}

	/// <summary>
	/// Default implementation of IConsoleAdaptor with System Console as input/output.
	/// </summary>
	public class ConsoleAdapter : IConsoleAdaptor
	{
		public ConsoleAdapter()
		{
			Console.CancelKeyPress += Console_CancelKeyPress;
		}

		public bool Canceled { get; set; }

		public int Read()
		{
			return Console.Read();
		}

		public string ReadLine()
		{
			var input = Console.ReadLine();

			// Windows console method ReadLine returns null if Ctrl-C was pressed.
			this.Canceled = (input == null);

			return input;
		}

		public void SetTextStyle(ConsoleTextStyle style)
		{
			switch (style)
			{
				case ConsoleTextStyle.Normal:
					Console.ForegroundColor = ConsoleColor.White;
					break;

				case ConsoleTextStyle.Error:
					Console.ForegroundColor = ConsoleColor.Red;
					break;
			}
		}

		public void SetTitle(string title)
		{
			Console.Title = title;
		}

		public void Write(string text)
		{
			Console.Write(text);
		}

		public void WriteLine(string text)
		{
			Console.WriteLine(text);
		}

		private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			// Do not kill the app yet
			e.Cancel = true;

			this.Canceled = true;
		}
	}
}
