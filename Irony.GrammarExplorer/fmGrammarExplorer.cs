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
//with contributions by Andrew Bradnan and Alexey Yakovlev

#endregion License

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Irony.Ast;
using Irony.GrammarExplorer.Highlighter;
using Irony.GrammarExplorer.Properties;
using Irony.Parsing;

namespace Irony.GrammarExplorer
{
	// That's the only place we use stuff from Irony.Interpreter
	using ScriptException = Irony.Interpreter.ScriptException;

	public partial class fmGrammarExplorer : Form
	{
		private Grammar grammar;

		private GrammarLoader grammarLoader = new GrammarLoader();

		private LanguageData language;

		private bool loaded;

		private Parser parser;

		private ParseTree parseTree;

		private ScriptException runtimeError;

		/// <summary>
		/// To temporarily disable tree click when we locate the node programmatically
		/// </summary>
		private bool treeClickDisabled;

		public fmGrammarExplorer()
		{
			this.InitializeComponent();
			this.grammarLoader.AssemblyUpdated += this.GrammarAssemblyUpdated;
		}

		#region Form load/unload events

		private void fmExploreGrammar_FormClosing(object sender, FormClosingEventArgs e)
		{
			Settings.Default.SourceSample = txtSource.Text;
			Settings.Default.LanguageIndex = cboGrammars.SelectedIndex;
			Settings.Default.SearchPattern = txtSearch.Text;
			Settings.Default.EnableTrace = chkParserTrace.Checked;
			Settings.Default.DisableHili = chkDisableHili.Checked;
			Settings.Default.AutoRefresh = chkAutoRefresh.Checked;

			var grammars = GrammarItemList.FromCombo(cboGrammars);

			Settings.Default.Grammars = grammars.ToXml();
			Settings.Default.Save();
		}

		private void fmExploreGrammar_Load(object sender, EventArgs e)
		{
			ClearLanguageInfo();
			try
			{
				txtSource.Text = Settings.Default.SourceSample;
				txtSearch.Text = Settings.Default.SearchPattern;

				var grammars = GrammarItemList.FromXml(Settings.Default.Grammars);
				grammars.ShowIn(cboGrammars);

				chkParserTrace.Checked = Settings.Default.EnableTrace;
				chkDisableHili.Checked = Settings.Default.DisableHili;
				chkAutoRefresh.Checked = Settings.Default.AutoRefresh;
				cboGrammars.SelectedIndex = Settings.Default.LanguageIndex; //this will build parser and start colorizer
			}
			catch
			{ }

			this.loaded = true;
		}

		#endregion Form load/unload events

		#region Show... methods

		private void AddAstNodeRec(TreeNode parent, object astNode)
		{
			if (astNode == null)
				return;

			var txt = astNode.ToString();

			TreeNode newNode = (parent == null ? tvAst.Nodes.Add(txt) : parent.Nodes.Add(txt));
			newNode.Tag = astNode;

			var iBrowsable = astNode as IBrowsableAstNode;

			if (iBrowsable == null)
				return;

			var childList = iBrowsable.GetChildNodes();
			foreach (var child in childList)
			{
				this.AddAstNodeRec(newNode, child);
			}
		}

		private void AddParseNodeRec(TreeNode parent, ParseTreeNode node)
		{
			if (node == null)
				return;

			var txt = node.ToString();

			TreeNode tvNode = (parent == null ? tvParseTree.Nodes.Add(txt) : parent.Nodes.Add(txt));
			tvNode.Tag = node;

			foreach (var child in node.ChildNodes)
			{
				this.AddParseNodeRec(tvNode, child);
			}
		}

		private void ClearLanguageInfo()
		{
			lblLanguage.Text = string.Empty;
			lblLanguageVersion.Text = string.Empty;
			lblLanguageDescr.Text = string.Empty;
			txtGrammarComments.Text = string.Empty;
		}

		private void ClearParserOutput()
		{
			lblSrcLineCount.Text = string.Empty;
			lblSrcTokenCount.Text = "";
			lblParseTime.Text = "";
			lblParseErrorCount.Text = "";

			lstTokens.Items.Clear();
			gridCompileErrors.Rows.Clear();
			gridParserTrace.Rows.Clear();
			lstTokens.Items.Clear();
			tvParseTree.Nodes.Clear();
			tvAst.Nodes.Clear();

			Application.DoEvents();
		}

		private void ClearRuntimeInfo()
		{
			lnkShowErrLocation.Enabled = false;
			lnkShowErrStack.Enabled = false;
			this.runtimeError = null;
			txtOutput.Text = string.Empty;
		}

		private void LocateParserState(ParserState state)
		{
			if (state == null)
				return;

			if (tabGrammar.SelectedTab != pageParserStates)
				tabGrammar.SelectedTab = pageParserStates;

			// First scroll to the bottom, so that scrolling to needed position brings it to top
			txtParserStates.SelectionStart = txtParserStates.Text.Length - 1;
			txtParserStates.ScrollToCaret();

			this.DoSearch(txtParserStates, "State " + state.Name, 0);
		}

		private void SelectTreeNode(TreeView tree, TreeNode node)
		{
			this.treeClickDisabled = true;
			tree.SelectedNode = node;

			if (node != null)
				node.EnsureVisible();

			this.treeClickDisabled = false;
		}

		private void ShowAstTree()
		{
			tvAst.Nodes.Clear();
			if (this.parseTree == null || this.parseTree.Root == null || this.parseTree.Root.AstNode == null)
				return;

			this.AddAstNodeRec(null, this.parseTree.Root.AstNode);
		}

		private void ShowCompilerErrors()
		{
			gridCompileErrors.Rows.Clear();
			if (this.parseTree == null || this.parseTree.ParserMessages.Count == 0)
				return;

			foreach (var err in this.parseTree.ParserMessages)
			{
				gridCompileErrors.Rows.Add(err.Location, err, err.ParserState);
			}

			var needPageSwitch = tabBottom.SelectedTab != pageParserOutput && !(tabBottom.SelectedTab == pageParserTrace && chkParserTrace.Checked);
			if (needPageSwitch)
				tabBottom.SelectedTab = pageParserOutput;
		}

		private void ShowCompileStats()
		{
			if (this.parseTree == null)
				return;

			lblSrcLineCount.Text = string.Empty;
			if (this.parseTree.Tokens.Count > 0)
				lblSrcLineCount.Text = (this.parseTree.Tokens[this.parseTree.Tokens.Count - 1].Location.Line + 1).ToString();

			lblSrcTokenCount.Text = this.parseTree.Tokens.Count.ToString();
			lblParseTime.Text = this.parseTree.ParseTimeMilliseconds.ToString();
			lblParseErrorCount.Text = this.parseTree.ParserMessages.Count.ToString();

			Application.DoEvents();

			// Note: this time is "pure" parse time; actual delay after cliking "Compile" includes time to fill ParseTree, AstTree controls
		}

		private void ShowGrammarErrors()
		{
			gridGrammarErrors.Rows.Clear();
			var errors = this.parser.Language.Errors;

			if (errors.Count == 0)
				return;

			foreach (var err in errors)
			{
				gridGrammarErrors.Rows.Add(err.Level.ToString(), err.Message, err.State);
			}

			if (tabBottom.SelectedTab != pageGrammarErrors)
				tabBottom.SelectedTab = pageGrammarErrors;
		}

		private void ShowLanguageInfo()
		{
			if (this.grammar == null)
				return;

			var langAttr = LanguageAttribute.GetValue(this.grammar.GetType());

			if (langAttr == null)
				return;

			lblLanguage.Text = langAttr.LanguageName;
			lblLanguageVersion.Text = langAttr.Version;
			lblLanguageDescr.Text = langAttr.Description;
			txtGrammarComments.Text = this.grammar.GrammarComments;
		}

		private void ShowParserConstructionResults()
		{
			lblParserStateCount.Text = this.language.ParserData.States.Count.ToString();
			lblParserConstrTime.Text = this.language.ConstructionTime.ToString();
			txtParserStates.Text = string.Empty;
			gridGrammarErrors.Rows.Clear();
			txtTerms.Text = string.Empty;
			txtNonTerms.Text = string.Empty;
			txtParserStates.Text = string.Empty;
			tabBottom.SelectedTab = pageLanguage;

			if (this.parser == null)
				return;

			txtTerms.Text = ParserDataPrinter.PrintTerminals(this.language);
			txtNonTerms.Text = ParserDataPrinter.PrintNonTerminals(this.language);
			txtParserStates.Text = ParserDataPrinter.PrintStateList(this.language);
			this.ShowGrammarErrors();
		}

		private void ShowParseTrace()
		{
			gridParserTrace.Rows.Clear();
			foreach (var entry in this.parser.Context.ParserTrace)
			{
				int index = gridParserTrace.Rows.Add(entry.State, entry.StackTop, entry.Input, entry.Message);
				if (entry.IsError)
					gridParserTrace.Rows[gridParserTrace.Rows.Count - 1].DefaultCellStyle.ForeColor = Color.Red;
			}

			// Show tokens
			foreach (Token tkn in this.parseTree.Tokens)
			{
				if (chkExcludeComments.Checked && tkn.Category == TokenCategory.Comment)
					continue;

				lstTokens.Items.Add(tkn);
			}
		}

		private void ShowParseTree()
		{
			tvParseTree.Nodes.Clear();
			if (this.parseTree == null)
				return;

			this.AddParseNodeRec(null, this.parseTree.Root);
		}

		private void ShowRuntimeError(ScriptException error)
		{
			this.runtimeError = error;
			lnkShowErrLocation.Enabled = this.runtimeError != null;
			lnkShowErrStack.Enabled = lnkShowErrLocation.Enabled;
			if (this.runtimeError != null)
			{
				// The exception was caught and processed by Interpreter
				this.WriteOutput("Error: " + error.Message + " At " + this.runtimeError.Location.ToUiString() + ".");
				this.ShowSourcePosition(this.runtimeError.Location.Position, 1);
			}
			else
			{
				// The exception was not caught by interpreter/AST node. Show full exception info
				this.WriteOutput("Error: " + error.Message);
				fmShowException.ShowException(error);
			}

			tabBottom.SelectedTab = pageOutput;
		}

		private void ShowSourcePosition(int position, int length)
		{
			if (position < 0)
				return;

			txtSource.SelectionStart = position;
			txtSource.SelectionLength = length;
			txtSource.DoCaretVisible();

			if (tabGrammar.SelectedTab != pageTest)
				tabGrammar.SelectedTab = pageTest;

			txtSource.Focus();
		}

		private void ShowSourcePositionAndTraceToken(int position, int length)
		{
			this.ShowSourcePosition(position, length);

			// Find token in trace
			for (int i = 0; i < lstTokens.Items.Count; i++)
			{
				var tkn = lstTokens.Items[i] as Token;
				if (tkn.Location.Position == position)
				{
					lstTokens.SelectedIndex = i;
					return;
				}
			}
		}

		#endregion Show... methods

		#region Grammar combo menu commands

		private void menuGrammars_Opening(object sender, CancelEventArgs e)
		{
			miRemove.Enabled = cboGrammars.Items.Count > 0;
		}

		private void miAdd_Click(object sender, EventArgs e)
		{
			if (dlgSelectAssembly.ShowDialog() != DialogResult.OK)
				return;

			var location = dlgSelectAssembly.FileName;
			if (string.IsNullOrEmpty(location))
				return;

			var oldGrammars = new GrammarItemList();
			foreach (var item in cboGrammars.Items)
			{
				oldGrammars.Add((GrammarItem) item);
			}

			var grammars = fmSelectGrammars.SelectGrammars(location, oldGrammars);
			if (grammars == null)
				return;

			foreach (GrammarItem item in grammars)
			{
				cboGrammars.Items.Add(item);
			}

			btnRefresh.Enabled = false;

			// Auto-select the first grammar if no grammar currently selected
			if (cboGrammars.SelectedIndex < 0 && grammars.Count > 0)
				cboGrammars.SelectedIndex = 0;
		}

		private void miRemove_Click(object sender, EventArgs e)
		{
			if (MessageBox.Show("Are you sure you want to remove grammmar " + cboGrammars.SelectedItem + "?",
			  "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
			{
				cboGrammars.Items.RemoveAt(cboGrammars.SelectedIndex);
				this.parser = null;

				if (cboGrammars.Items.Count > 0)
					cboGrammars.SelectedIndex = 0;
				else
					btnRefresh.Enabled = false;
			}
		}

		private void miRemoveAll_Click(object sender, EventArgs e)
		{
			if (MessageBox.Show("Are you sure you want to remove all grammmars in the list?",
			  "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
			{
				cboGrammars.Items.Clear();
				btnRefresh.Enabled = false;
				this.parser = null;
			}
		}

		#endregion Grammar combo menu commands

		#region Parsing and running

		private void CreateGrammar()
		{
			this.grammar = this.grammarLoader.CreateGrammar();
		}

		private void CreateParser()
		{
			this.StopHighlighter();
			btnRun.Enabled = false;
			txtOutput.Text = string.Empty;
			this.parseTree = null;

			btnRun.Enabled = this.grammar is ICanRunSample;
			this.language = new LanguageData(this.grammar);
			this.parser = new Parser(this.language);
			this.ShowParserConstructionResults();
			this.StartHighlighter();
		}

		private void ParseSample()
		{
			ClearParserOutput();
			if (this.parser == null || !this.parser.Language.CanParse()) return;
			this.parseTree = null;

			// To avoid disruption of perf times with occasional collections
			GC.Collect();

			this.parser.Context.TracingEnabled = chkParserTrace.Checked;

			try
			{
				this.parser.Parse(txtSource.Text, "<source>");
			}
			catch (Exception ex)
			{
				gridCompileErrors.Rows.Add(null, ex.Message, null);
				tabBottom.SelectedTab = pageParserOutput;
				throw;
			}
			finally
			{
				this.parseTree = this.parser.Context.CurrentParseTree;
				this.ShowCompilerErrors();

				if (chkParserTrace.Checked)
				{
					this.ShowParseTrace();
				}

				this.ShowCompileStats();
				this.ShowParseTree();
				this.ShowAstTree();
			}
		}

		private void RunSample()
		{
			this.ClearRuntimeInfo();
			var sw = new Stopwatch();
			int oldGcCount;
			txtOutput.Text = "";

			try
			{
				if (this.parseTree == null)
					this.ParseSample();

				if (this.parseTree.ParserMessages.Count > 0)
					return;

				// To avoid disruption of perf times with occasional collections
				GC.Collect();

				oldGcCount = GC.CollectionCount(0);
				System.Threading.Thread.Sleep(100);

				sw.Start();

				var iRunner = this.grammar as ICanRunSample;
				var args = new RunSampleArgs(this.language, txtSource.Text, this.parseTree);
				var output = iRunner.RunSample(args);

				sw.Stop();

				lblRunTime.Text = sw.ElapsedMilliseconds.ToString();
				var gcCount = GC.CollectionCount(0) - oldGcCount;
				lblGCCount.Text = gcCount.ToString();

				this.WriteOutput(output);
				tabBottom.SelectedTab = pageOutput;
			}
			catch (ScriptException ex)
			{
				this.ShowRuntimeError(ex);
			}
			finally
			{
				sw.Stop();
			}
		}

		private void WriteOutput(string text)
		{
			if (string.IsNullOrEmpty(text))
				return;

			txtOutput.Text += text + Environment.NewLine;
			txtOutput.Select(txtOutput.Text.Length - 1, 0);
		}

		#endregion Parsing and running

		#region miscellaneous: LoadSourceFile, Search, Source highlighting

		/// <summary>
		/// Source highlighting
		/// </summary>
		private FastColoredTextBoxHighlighter highlighter;

		public TextBoxBase GetSearchContentBox()
		{
			switch (tabGrammar.SelectedIndex)
			{
				case 0:
					return txtTerms;

				case 1:
					return txtNonTerms;

				case 2:
					return txtParserStates;

				default:
					return null;
			}
		}

		private void ClearHighlighting()
		{
			var selectedRange = txtSource.Selection;
			var visibleRange = txtSource.VisibleRange;
			var firstVisibleLine = Math.Min(visibleRange.Start.iLine, visibleRange.End.iLine);

			var txt = txtSource.Text;
			txtSource.Clear();

			// Remove all old highlighting
			txtSource.Text = txt;

			txtSource.SetVisibleState(firstVisibleLine, FastColoredTextBoxNS.VisibleState.Visible);
			txtSource.Selection = selectedRange;
		}

		/// <summary>
		/// The following methods are contributed by Andrew Bradnan; pasted here with minor changes
		/// </summary>
		private void DoSearch()
		{
			lblSearchError.Visible = false;
			TextBoxBase textBox = GetSearchContentBox();

			if (textBox == null)
				return;

			int idxStart = textBox.SelectionStart + textBox.SelectionLength;
			if (!this.DoSearch(textBox, txtSearch.Text, idxStart))
			{
				lblSearchError.Text = "Not found.";
				lblSearchError.Visible = true;
			}
		}

		private bool DoSearch(TextBoxBase textBox, string fragment, int start)
		{
			textBox.SelectionLength = 0;

			// Compile the regular expression.
			var r = new Regex(fragment, RegexOptions.IgnoreCase);

			// Match the regular expression pattern against a text string.
			var m = r.Match(textBox.Text.Substring(start));
			if (m.Success)
			{
				var i = 0;
				var g = m.Groups[i];
				var cc = g.Captures;
				var c = cc[0];
				textBox.SelectionStart = c.Index + start;
				textBox.SelectionLength = c.Length;
				textBox.Focus();
				textBox.ScrollToCaret();

				return true;
			}
			return false;
		}

		private void EnableHighlighter(bool enable)
		{
			if (this.highlighter != null)
				this.StopHighlighter();

			if (enable)
				this.StartHighlighter();
		}

		private void LoadSourceFile(string path)
		{
			this.parseTree = null;
			StreamReader reader = null;

			try
			{
				reader = new StreamReader(path);

				// To clear any old formatting
				txtSource.Text = null;
				txtSource.ClearUndo();
				txtSource.ClearStylesBuffer();
				txtSource.Text = reader.ReadToEnd();
				txtSource.SetVisibleState(0, FastColoredTextBoxNS.VisibleState.Visible);
				txtSource.Selection = txtSource.GetRange(0, 0);
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message);
			}
			finally
			{
				if (reader != null)
					reader.Close();
			}
		}

		private void StartHighlighter()
		{
			if (this.highlighter != null)
				this.StopHighlighter();

			if (chkDisableHili.Checked)
				return;

			if (!this.parser.Language.CanParse())
				return;

			this.highlighter = new FastColoredTextBoxHighlighter(txtSource, this.language);
			this.highlighter.Adapter.Activate();
		}

		private void StopHighlighter()
		{
			if (this.highlighter == null)
				return;

			this.highlighter.Dispose();
			this.highlighter = null;

			this.ClearHighlighting();
		}

		#endregion miscellaneous: LoadSourceFile, Search, Source highlighting

		#region Controls event handlers

		private bool changingGrammar;

		private void btnFileOpen_Click(object sender, EventArgs e)
		{
			if (dlgOpenFile.ShowDialog() != DialogResult.OK)
				return;

			this.ClearParserOutput();
			this.LoadSourceFile(dlgOpenFile.FileName);
		}

		private void btnLocate_Click(object sender, EventArgs e)
		{
			if (this.parseTree == null)
				this.ParseSample();

			var p = txtSource.SelectionStart;

			// Just in case we won't find
			tvParseTree.SelectedNode = null;
			tvAst.SelectedNode = null;

			this.SelectTreeNode(tvParseTree, this.LocateTreeNode(tvParseTree.Nodes, p, node => (node.Tag as ParseTreeNode).Span.Location.Position));
			this.SelectTreeNode(tvAst, this.LocateTreeNode(tvAst.Nodes, p, node => (node.Tag as IBrowsableAstNode).Position));

			// Set focus back to source
			txtSource.Focus();
		}

		private void btnManageGrammars_Click(object sender, EventArgs e)
		{
			menuGrammars.Show(btnManageGrammars, 0, btnManageGrammars.Height);
		}

		private void btnParse_Click(object sender, EventArgs e)
		{
			this.ParseSample();
		}

		private void btnRefresh_Click(object sender, EventArgs e)
		{
			this.LoadSelectedGrammar();
		}

		private void btnRun_Click(object sender, EventArgs e)
		{
			this.RunSample();
		}

		private void btnSearch_Click(object sender, EventArgs e)
		{
			this.DoSearch();
		}

		private void cboGrammars_SelectedIndexChanged(object sender, EventArgs e)
		{
			this.grammarLoader.SelectedGrammar = cboGrammars.SelectedItem as GrammarItem;
			this.LoadSelectedGrammar();
		}

		private void cboParseMethod_SelectedIndexChanged(object sender, EventArgs e)
		{
			// Changing grammar causes setting of parse method combo, so to prevent double-call to ConstructParser
			// we don't do it here if _changingGrammar is set
			if (!this.changingGrammar)
				this.CreateParser();
		}

		private void chkDisableHili_CheckedChanged(object sender, EventArgs e)
		{
			if (!this.loaded) return;
			this.EnableHighlighter(!chkDisableHili.Checked);
		}

		private void GrammarAssemblyUpdated(object sender, EventArgs args)
		{
			if (InvokeRequired)
			{
				this.Invoke(new EventHandler(this.GrammarAssemblyUpdated), sender, args);
				return;
			}
			if (chkAutoRefresh.Checked)
			{
				this.LoadSelectedGrammar();
				txtGrammarComments.Text += String.Format("{0}Grammar assembly reloaded: {1:HH:mm:ss}", Environment.NewLine, DateTime.Now);
			}
		}

		private void gridCompileErrors_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
		{
			if (e.RowIndex < 0 || e.RowIndex >= gridCompileErrors.Rows.Count)
				return;

			var err = gridCompileErrors.Rows[e.RowIndex].Cells[1].Value as LogMessage;
			switch (e.ColumnIndex)
			{
				case 0: // State
				case 1: // Stack top
					this.ShowSourcePosition(err.Location.Position, 1);
					break;

				case 2: // Input
					if (err.ParserState != null)
						this.LocateParserState(err.ParserState);
					break;
			}
		}

		private void gridGrammarErrors_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
		{
			if (e.RowIndex < 0 || e.RowIndex >= gridGrammarErrors.Rows.Count)
				return;

			var state = gridGrammarErrors.Rows[e.RowIndex].Cells[2].Value as ParserState;
			if (state != null)
				this.LocateParserState(state);
		}

		private void gridParserTrace_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
		{
			if (this.parser.Context == null || e.RowIndex < 0 || e.RowIndex >= this.parser.Context.ParserTrace.Count)
				return;

			var entry = this.parser.Context.ParserTrace[e.RowIndex];

			switch (e.ColumnIndex)
			{
				case 0: // State
				case 3: // Action
					this.LocateParserState(entry.State);
					break;

				case 1: // Stack top
					if (entry.StackTop != null)
						this.ShowSourcePositionAndTraceToken(entry.StackTop.Span.Location.Position, entry.StackTop.Span.Length);
					break;

				case 2: // Input
					if (entry.Input != null)
						this.ShowSourcePositionAndTraceToken(entry.Input.Span.Location.Position, entry.Input.Span.Length);
					break;
			}
		}

		private void lnkShowErrLocation_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			if (this.runtimeError != null)
				ShowSourcePosition(this.runtimeError.Location.Position, 1);
		}

		private void lnkShowErrStack_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			if (this.runtimeError == null)
				return;

			if (this.runtimeError.InnerException != null)
				fmShowException.ShowException(this.runtimeError.InnerException);
			else
				fmShowException.ShowException(this.runtimeError);
		}

		private void LoadSelectedGrammar()
		{
			try
			{
				this.ClearLanguageInfo();
				this.ClearParserOutput();
				this.ClearRuntimeInfo();

				this.changingGrammar = true;

				this.CreateGrammar();
				this.ShowLanguageInfo();
				this.CreateParser();
			}
			finally
			{
				// In case of exception
				this.changingGrammar = false;
			}

			btnRefresh.Enabled = true;
		}

		private TreeNode LocateTreeNode(TreeNodeCollection nodes, int position, Func<TreeNode, int> positionFunction)
		{
			TreeNode current = null;

			// Find the last node in the list that is "before or at" the position
			foreach (TreeNode node in nodes)
			{
				if (positionFunction(node) > position)
					break;

				current = node;
			}

			// If current has children, search them
			if (current != null && current.Nodes.Count > 0)
				current = this.LocateTreeNode(current.Nodes, position, positionFunction) ?? current;

			return current;
		}

		private void lstTokens_Click(object sender, EventArgs e)
		{
			if (lstTokens.SelectedIndex < 0)
				return;

			var token = (Token) lstTokens.SelectedItem;
			this.ShowSourcePosition(token.Location.Position, token.Length);
		}

		private void tvAst_AfterSelect(object sender, TreeViewEventArgs e)
		{
			if (this.treeClickDisabled)
				return;

			var treeNode = tvAst.SelectedNode;
			if (treeNode == null)
				return;

			var iBrowsable = treeNode.Tag as IBrowsableAstNode;
			if (iBrowsable == null)
				return;

			this.ShowSourcePosition(iBrowsable.Position, 1);
		}

		private void tvParseTree_AfterSelect(object sender, TreeViewEventArgs e)
		{
			if (this.treeClickDisabled)
				return;

			var vtreeNode = tvParseTree.SelectedNode;
			if (vtreeNode == null)
				return;

			var parseNode = vtreeNode.Tag as ParseTreeNode;
			if (parseNode == null)
				return;

			this.ShowSourcePosition(parseNode.Span.Location.Position, 1);
		}

		private void txtSearch_KeyPress(object sender, KeyPressEventArgs e)
		{
			// <Enter> key
			if (e.KeyChar == '\r')
				this.DoSearch();
		}

		private void txtSource_TextChanged(object sender, FastColoredTextBoxNS.TextChangedEventArgs e)
		{
			// Force it to recompile on run
			this.parseTree = null;
		}

		#endregion Controls event handlers
	}
}
