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
using System.Diagnostics;
using System.Threading;
using Irony.Parsing;

namespace Irony.GrammarExplorer
{
	public class EditorAdapter
	{
		private Thread colorizerThread;
		private string newText;
		private Parser parser;
		private Thread parserThread;
		private ParseTree parseTree;
		private Scanner scanner;
		private bool stopped;
		private EditorViewAdapterList views = new EditorViewAdapterList();

		/// <summary>
		/// copy used in refresh loop; set to null when views are added/removed
		/// </summary>
		private EditorViewAdapterList viewsCopy;

		public EditorAdapter(LanguageData language)
		{
			this.parser = new Parser(language);
			this.scanner = this.parser.Scanner;
			this.colorizerThread = new Thread(this.ColorizerLoop);
			this.colorizerThread.IsBackground = true;
			this.parserThread = new Thread(this.ParserLoop);
			this.parserThread.IsBackground = true;
		}

		public ParseTree ParseTree
		{
			get { return this.parseTree; }
		}

		public void Activate()
		{
			if ((this.colorizerThread.ThreadState & System.Threading.ThreadState.Running) == 0)
			{
				this.parserThread.Start();
				this.colorizerThread.Start();
			}
		}

		public void SetNewText(string text)
		{
			// Force it to become not null; null is special value meaning "no changes"
			text = text ?? string.Empty;
			this.newText = text;
		}

		public void Stop()
		{
			try
			{
				this.stopped = true;
				this.parserThread.Join(500);
				if (this.parserThread.IsAlive)
					this.parserThread.Abort();

				this.colorizerThread.Join(500);
				if (this.colorizerThread.IsAlive)
					this.colorizerThread.Abort();
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Error when stopping EditorAdapter: " + ex.Message);
			}
		}

		private void ColorizerLoop()
		{
			while (!this.stopped)
			{
				EditorViewAdapterList views = this.GetViews();

				// Go through views and invoke refresh
				foreach (EditorViewAdapter view in views)
				{
					if (this.stopped)
						break;

					if (view.WantsColorize)
						view.TryInvokeColorize();
				}

				Thread.Sleep(10);
			}
		}

		private void ParserLoop()
		{
			while (!this.stopped)
			{
				try
				{
					string newtext = Interlocked.Exchange(ref this.newText, null);
					if (newtext != null)
					{
						ParseSource(newtext);
					}

					Thread.Sleep(10);
				}
				catch (Exception ex)
				{
					fmShowException.ShowException(ex);
					System.Windows.Forms.MessageBox.Show("Fatal error in code colorizer. Colorizing had been disabled.");
					this.stopped = true;
				}
			}
		}

		/// <summary>
		/// Note: we don't actually parse in current version, only scan. Will implement full parsing in the future,
		/// to support all intellisense operations
		/// </summary>
		/// <param name="newText"></param>
		private void ParseSource(string newText)
		{
			// Explicitly catch the case when new text is empty
			if (newText != string.Empty)
			{
				this.parseTree = this.parser.Parse(newText);// .ScanOnly(newText, "Source");
			}

			// Notify views
			var views = GetViews();

			foreach (var view in views)
			{
				view.UpdateParsedSource(this.parseTree);
			}
		}

		#region Views manipulation: AddView, RemoveView, GetViews

		public void AddView(EditorViewAdapter view)
		{
			lock (this)
			{
				this.views.Add(view);
				this.viewsCopy = null;
			}
		}

		public void RemoveView(EditorViewAdapter view)
		{
			lock (this)
			{
				this.views.Remove(view);
				this.viewsCopy = null;
			}
		}

		private EditorViewAdapterList GetViews()
		{
			EditorViewAdapterList result = this.viewsCopy;
			if (result == null)
			{
				lock (this)
				{
					this.viewsCopy = new EditorViewAdapterList();
					this.viewsCopy.AddRange(this.views);
					result = this.viewsCopy;
				}
			}

			return result;
		}

		#endregion Views manipulation: AddView, RemoveView, GetViews
	}
}
