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
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Irony.Parsing;

namespace Irony.GrammarExplorer
{
	public class RichTextBoxHighlighter : NativeWindow, IDisposable, IUIThreadInvoker
	{
		public readonly EditorAdapter Adapter;
		public readonly TokenColorTable TokenColors = new TokenColorTable();
		public readonly EditorViewAdapter ViewAdapter;
		public RichTextBox TextBox;

		private bool colorizing;
		private bool disposed;
		private IntPtr savedEventMask = IntPtr.Zero;

		#region constructor, initialization and disposing

		public RichTextBoxHighlighter(RichTextBox textBox, LanguageData language)
		{
			this.TextBox = textBox;
			this.Adapter = new EditorAdapter(language);
			this.ViewAdapter = new EditorViewAdapter(this.Adapter, this);
			this.InitColorTable();
			this.Connect();
			this.UpdateViewRange();
			this.ViewAdapter.SetNewText(TextBox.Text);
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
			this.TextBox.VScroll += this.TextBox_ScrollResize;
			this.TextBox.HScroll += this.TextBox_ScrollResize;
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
				this.TextBox.VScroll -= this.TextBox_ScrollResize;
				this.TextBox.HScroll -= this.TextBox_ScrollResize;
				this.TextBox.SizeChanged -= this.TextBox_ScrollResize;
			}

			this.TextBox = null;
		}

		private void InitColorTable()
		{
			this.TokenColors[TokenColor.Comment] = Color.Green;
			this.TokenColors[TokenColor.Identifier] = Color.Black;
			this.TokenColors[TokenColor.Keyword] = Color.Blue;
			this.TokenColors[TokenColor.Number] = Color.DarkRed;
			this.TokenColors[TokenColor.String] = Color.DarkSlateGray;
			this.TokenColors[TokenColor.Text] = Color.Black;
		}

		#endregion constructor, initialization and disposing

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

		private void TextBox_TextChanged(object sender, EventArgs e)
		{
			// If we are here while colorizing, it means the "change" event is a result of our coloring action
			if (this.colorizing)
				return;

			this.ViewAdapter.SetNewText(this.TextBox.Text);
		}

		private void UpdateViewRange()
		{
			int minpos = this.TextBox.GetCharIndexFromPosition(new Point(0, 0));
			int maxpos = this.TextBox.GetCharIndexFromPosition(new Point(this.TextBox.ClientSize.Width, this.TextBox.ClientSize.Height));
			this.ViewAdapter.SetViewRange(minpos, maxpos);
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
			SendMessage(this.TextBox.Handle, WM_SETREDRAW, 0, IntPtr.Zero);

			// Stop sending of events:
			this.savedEventMask = SendMessage(this.TextBox.Handle, EM_GETEVENTMASK, 0, IntPtr.Zero);

			//SendMessage(TextBox.Handle, EM_SETEVENTMASK, 0, IntPtr.Zero);
		}

		public void UnlockTextBox()
		{
			// turn on events
			SendMessage(this.TextBox.Handle, EM_SETEVENTMASK, 0, this.savedEventMask);

			// turn on redrawing
			SendMessage(this.TextBox.Handle, WM_SETREDRAW, 1, IntPtr.Zero);
		}

		private void Adapter_ColorizeTokens(object sender, ColorizeEventArgs args)
		{
			if (this.disposed)
				return;

			this.colorizing = true;

			int hscroll = HScrollPos;
			int vscroll = VScrollPos;
			int selstart = TextBox.SelectionStart;
			int selLength = TextBox.SelectionLength;

			this.LockTextBox();

			try
			{
				foreach (Token tkn in args.Tokens)
				{
					Color color = this.GetTokenColor(tkn);
					this.TextBox.Select(tkn.Location.Position, tkn.Length);
					this.TextBox.SelectionColor = color;
				}
			}
			finally
			{
				this.TextBox.Select(selstart, selLength);
				this.HScrollPos = hscroll;
				this.VScrollPos = vscroll;
				this.UnlockTextBox();
				this.colorizing = false;
			}

			this.TextBox.Invalidate();
		}

		private Color GetTokenColor(Token token)
		{
			if (token.EditorInfo == null)
				return Color.Black;

			// Right now we scan source, not parse; initially all keywords are recognized as Identifiers; then they are "backpatched"
			// by parser when it detects that it is in fact keyword from Grammar. So now this backpatching does not happen,
			// so we have to detect keywords here
			var colorIndex = token.EditorInfo.Color;
			if (token.KeyTerm != null && token.KeyTerm.EditorInfo != null && token.KeyTerm.Flags.IsSet(TermFlags.IsKeyword))
			{
				colorIndex = token.KeyTerm.EditorInfo.Color;
			}

			Color result;
			if (this.TokenColors.TryGetValue(colorIndex, out result))
				return result;

			return Color.Black;
		}

		#endregion Colorizing tokens

		#region IUIThreadInvoker Members

		public void InvokeOnUIThread(ColorizeMethod colorize)
		{
			this.TextBox.BeginInvoke(new MethodInvoker(colorize));
		}

		#endregion IUIThreadInvoker Members
	}

	public class TokenColorTable : Dictionary<TokenColor, Color> { }
}
