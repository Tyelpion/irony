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

// Aknowledgments
// This module borrows code and ideas from TinyPG framework by Herre Kuijpers,
// specifically TextMarker.cs and TextHighlighter.cs classes.
// http://www.codeproject.com/KB/recipes/TinyPG.aspx
// Written by Alexey Yakovlev <yallie@yandex.ru>, based on RichTextBoxHighlighter
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FastColoredTextBoxNS;
using Irony.Parsing;

namespace Irony.GrammarExplorer.Highlighter
{
	/// <summary>
	/// Highlights text inside FastColoredTextBox control.
	/// </summary>
	public class FastColoredTextBoxHighlighter : NativeWindow, IDisposable, IUIThreadInvoker
	{
		public readonly EditorAdapter Adapter;
		public readonly LanguageData Language;
		public readonly EditorViewAdapter ViewAdapter;
		private readonly Style DefaultTokenStyle = new TextStyle(Brushes.Black, null, FontStyle.Regular);
		private readonly Style ErrorTokenStyle = new WavyLineStyle(240, Color.Red);
		private readonly Dictionary<TokenColor, Style> TokenStyles = new Dictionary<TokenColor, Style>();

		public FastColoredTextBox TextBox;

		private bool colorizing;
		private bool disposed;
		private IntPtr savedEventMask = IntPtr.Zero;

		#region Constructor, initialization and disposing

		public FastColoredTextBoxHighlighter(FastColoredTextBox textBox, LanguageData language)
		{
			this.TextBox = textBox;
			this.Adapter = new EditorAdapter(language);
			this.ViewAdapter = new EditorViewAdapter(this.Adapter, this);
			this.Language = language;

			this.InitStyles();
			this.InitBraces();
			this.Connect();
			this.UpdateViewRange();
			this.ViewAdapter.SetNewText(this.TextBox.Text);
		}

		public void Dispose()
		{
			this.Adapter.Stop();
			this.disposed = true;
			this.Disconnect();
			this.ReleaseHandle();

			GC.SuppressFinalize(this);
		}

		private void Connect()
		{
			this.TextBox.MouseMove += this.TextBox_MouseMove;
			this.TextBox.TextChanged += this.TextBox_TextChanged;
			this.TextBox.KeyDown += this.TextBox_KeyDown;
			this.TextBox.VisibleRangeChanged += this.TextBox_ScrollResize;
			this.TextBox.SizeChanged += this.TextBox_ScrollResize;
			this.TextBox.Disposed += this.TextBox_Disposed;
			this.ViewAdapter.ColorizeTokens += this.Adapter_ColorizeTokens;

			this.AssignHandle(this.TextBox.Handle);
		}

		private void Disconnect()
		{
			if (this.TextBox != null)
			{
				this.TextBox.MouseMove -= this.TextBox_MouseMove;
				this.TextBox.TextChanged -= this.TextBox_TextChanged;
				this.TextBox.KeyDown -= this.TextBox_KeyDown;
				this.TextBox.Disposed -= this.TextBox_Disposed;
				this.TextBox.VisibleRangeChanged -= this.TextBox_ScrollResize;
				this.TextBox.SizeChanged -= this.TextBox_ScrollResize;
			}

			this.TextBox = null;
		}

		private void InitBraces()
		{
			// Select the first two pair of braces with the length of exactly one char (FCTB restrictions)
			var braces = Language.Grammar.KeyTerms
			  .Select(pair => pair.Value)
			  .Where(term => term.Flags.IsSet(TermFlags.IsOpenBrace))
			  .Where(term => term.IsPairFor != null && term.IsPairFor is KeyTerm)
			  .Where(term => term.Text.Length == 1)
			  .Where(term => ((KeyTerm) term.IsPairFor).Text.Length == 1)
			  .Take(2);

			if (braces.Any())
			{
				// First pair
				var brace = braces.First();
				this.TextBox.LeftBracket = brace.Text.First();
				this.TextBox.RightBracket = ((KeyTerm) brace.IsPairFor).Text.First();

				// Second pair
				if (braces.Count() > 1)
				{
					brace = braces.Last();
					this.TextBox.LeftBracket2 = brace.Text.First();
					this.TextBox.RightBracket2 = ((KeyTerm) brace.IsPairFor).Text.First();
				}
			}
		}

		private void InitStyles()
		{
			var commentStyle = new TextStyle(Brushes.Green, null, FontStyle.Italic);
			var keywordStyle = new TextStyle(Brushes.Blue, null, FontStyle.Bold);
			var literalStyle = new TextStyle(Brushes.DarkRed, null, FontStyle.Regular);

			this.TokenStyles[TokenColor.Comment] = commentStyle;
			this.TokenStyles[TokenColor.Identifier] = DefaultTokenStyle;
			this.TokenStyles[TokenColor.Keyword] = keywordStyle;
			this.TokenStyles[TokenColor.Number] = literalStyle;
			this.TokenStyles[TokenColor.String] = literalStyle;
			this.TokenStyles[TokenColor.Text] = DefaultTokenStyle;

			this.TextBox.ClearStylesBuffer();
			this.TextBox.AddStyle(this.DefaultTokenStyle);
			this.TextBox.AddStyle(this.ErrorTokenStyle);
			this.TextBox.AddStyle(commentStyle);
			this.TextBox.AddStyle(keywordStyle);
			this.TextBox.AddStyle(literalStyle);
			this.TextBox.BracketsStyle = new MarkerStyle(new SolidBrush(Color.FromArgb(50, Color.Blue)));
			this.TextBox.BracketsStyle2 = new MarkerStyle(new SolidBrush(Color.FromArgb(70, Color.Green)));
		}

		#endregion Constructor, initialization and disposing

		#region TextBox event handlers

		private void TextBox_Disposed(object sender, EventArgs e)
		{
			this.Dispose();
		}

		private void TextBox_KeyDown(object sender, KeyEventArgs e)
		{
			// TODO: implement showing intellisense hints or drop-downs
		}

		private void TextBox_MouseMove(object sender, MouseEventArgs e)
		{
			// TODO: implement showing tip
		}

		private void TextBox_ScrollResize(object sender, EventArgs e)
		{
			this.UpdateViewRange();
		}

		private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			// If we are here while colorizing, it means the "change" event is a result of our coloring action
			if (this.colorizing)
				return;

			this.ViewAdapter.SetNewText(this.TextBox.Text);
		}

		private void UpdateViewRange()
		{
			this.ViewAdapter.SetViewRange(0, this.TextBox.Text.Length);
		}

		#endregion TextBox event handlers

		#region WinAPI

		private const int EM_GETEVENTMASK = (WM_USER + 59);

		private const int EM_SETEVENTMASK = (WM_USER + 69);

		private const int SB_HORZ = 0x0;

		private const int SB_THUMBPOSITION = 4;

		private const int SB_VERT = 0x1;

		private const int WM_HSCROLL = 0x114;

		private const int WM_PAINT = 0x000F;

		private const int WM_SETREDRAW = 0x000B;

		private const int WM_USER = 0x400;

		private const int WM_VSCROLL = 0x115;

		private int HScrollPos
		{
			get
			{
				// Sometimes explodes with null reference exception
				return GetScrollPos((int) this.TextBox.Handle, SB_HORZ);
			}
			set
			{
				SetScrollPos((IntPtr) this.TextBox.Handle, SB_HORZ, value, true);
				PostMessageA((IntPtr) this.TextBox.Handle, WM_HSCROLL, SB_THUMBPOSITION + 0x10000 * value, 0);
			}
		}

		private int VScrollPos
		{
			get
			{
				return GetScrollPos((int) this.TextBox.Handle, SB_VERT);
			}
			set
			{
				SetScrollPos((IntPtr) this.TextBox.Handle, SB_VERT, value, true);
				PostMessageA((IntPtr) this.TextBox.Handle, WM_VSCROLL, SB_THUMBPOSITION + 0x10000 * value, 0);
			}
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern int GetScrollPos(int hWnd, int nBar);

		[DllImport("user32.dll")]
		private static extern bool PostMessageA(IntPtr hWnd, int nBar, int wParam, int lParam);

		[DllImport("user32", CharSet = CharSet.Auto)]
		private extern static IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		private static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

		#endregion WinAPI

		#region Colorizing tokens

		public void LockTextBox()
		{
			// Stop redrawing:
			this.TextBox.BeginUpdate();
			SendMessage(this.TextBox.Handle, WM_SETREDRAW, 0, IntPtr.Zero);

			// Stop sending of events:
			this.savedEventMask = SendMessage(this.TextBox.Handle, EM_GETEVENTMASK, 0, IntPtr.Zero);
			SendMessage(this.TextBox.Handle, EM_SETEVENTMASK, 0, IntPtr.Zero);
		}

		public void UnlockTextBox()
		{
			// Turn on events
			SendMessage(this.TextBox.Handle, EM_SETEVENTMASK, 0, this.savedEventMask);

			// Turn on redrawing
			SendMessage(this.TextBox.Handle, WM_SETREDRAW, 1, IntPtr.Zero);
			this.TextBox.EndUpdate();
		}

		private void Adapter_ColorizeTokens(object sender, ColorizeEventArgs args)
		{
			if (this.disposed)
				return;

			this.colorizing = true;
			this.TextBox.BeginUpdate();

			try
			{
				foreach (Token tkn in args.Tokens)
				{
					var tokenRange = this.TextBox.GetRange(tkn.Location.Position, tkn.Location.Position + tkn.Length);
					var tokenStyle = this.GetTokenStyle(tkn);
					tokenRange.ClearStyle(StyleIndex.All);
					tokenRange.SetStyle(tokenStyle);
				}
			}
			finally
			{
				this.TextBox.EndUpdate();
				this.colorizing = false;
			}
		}

		private Style GetTokenStyle(Token token)
		{
			if (token.IsError())
				return this.ErrorTokenStyle;

			if (token.EditorInfo == null)
				return this.DefaultTokenStyle;

			// Right now we scan source, not parse; initially all keywords are recognized as Identifiers; then they are "backpatched"
			// by parser when it detects that it is in fact keyword from Grammar. So now this backpatching does not happen,
			// so we have to detect keywords here
			var styleIndex = token.EditorInfo.Color;
			if (token.KeyTerm != null && token.KeyTerm.EditorInfo != null && token.KeyTerm.Flags.IsSet(TermFlags.IsKeyword))
			{
				styleIndex = token.KeyTerm.EditorInfo.Color;
			}

			Style result;
			if (this.TokenStyles.TryGetValue(styleIndex, out result))
				return result;

			return this.DefaultTokenStyle;
		}

		#endregion Colorizing tokens

		#region IUIThreadInvoker Members

		public void InvokeOnUIThread(ColorizeMethod colorize)
		{
			this.TextBox.BeginInvoke(new MethodInvoker(colorize));
		}

		#endregion IUIThreadInvoker Members
	}
}
