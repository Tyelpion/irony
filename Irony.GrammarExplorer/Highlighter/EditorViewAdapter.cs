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
using System.Threading;
using Irony.Parsing;

namespace Irony.GrammarExplorer
{
	public delegate void ColorizeMethod();

	public interface IUIThreadInvoker
	{
		void InvokeOnUIThread(ColorizeMethod colorize);
	}

	public class ColorizeEventArgs : EventArgs
	{
		public readonly TokenList Tokens;

		public ColorizeEventArgs(TokenList tokens)
		{
			this.Tokens = tokens;
		}
	}

	public class EditorViewAdapter
	{
		private int colorizing;

		private ViewData data;

		private IUIThreadInvoker invoker;

		private ViewRange range;

		private bool wantsColorize;

		public EditorViewAdapter(EditorAdapter adapter, IUIThreadInvoker invoker)
		{
			this.Adapter = adapter;
			this.invoker = invoker;
			this.Adapter.AddView(this);
			this.range = new ViewRange(-1, -1);
		}

		public event EventHandler<ColorizeEventArgs> ColorizeTokens;

		public EditorAdapter Adapter { get; private set; }

		/// <summary>
		/// The new text is passed directly to <see cref="EditorAdapter"/> instance (possibly shared by several view adapters).
		/// <see cref="EditorAdapter"/> parses the text on a separate background thread, and notifies back this and other
		/// view adapters and provides them with newly parsed source through UpdateParsedSource method (see below)
		/// </summary>
		/// <param name="newText"></param>
		public void SetNewText(string newText)
		{
			// TODO: fix this
			// hack, temp solution for more general problem
			// When we load/replace/clear entire text, clear out colored tokens to force recoloring from scratch
			if (string.IsNullOrEmpty(newText))
				this.data = null;

			this.Adapter.SetNewText(newText);
		}

		/// <summary>
		/// <see cref="SetViewRange(int, int)"/> and <see cref="SetNewText(string)"/> are called by text box's event handlers to notify adapter that user did something edit box
		/// </summary>
		/// <param name="min"></param>
		/// <param name="max"></param>
		public void SetViewRange(int min, int max)
		{
			this.range = new ViewRange(min, max);
			this.wantsColorize = true;
		}

		/// <summary>
		/// Called by <see cref="EditorAdapter"/> to provide the latest parsed source
		/// </summary>
		/// <param name="newTree"></param>
		public void UpdateParsedSource(ParseTree newTree)
		{
			lock (this)
			{
				var oldData = this.data;
				this.data = new ViewData(newTree);

				// Now try to figure out tokens that match old Colored tokens
				if (oldData != null && oldData.Tree != null)
				{
					DetectAlreadyColoredTokens(oldData.ColoredTokens, this.data.Tree.SourceText.Length - oldData.Tree.SourceText.Length);
				}

				this.wantsColorize = true;
			}
		}

		#region Colorizing

		public bool WantsColorize
		{
			get { return this.wantsColorize; }
		}

		public void TryInvokeColorize()
		{
			if (!this.wantsColorize)
				return;

			int colorizing = Interlocked.Exchange(ref this.colorizing, 1);
			if (colorizing != 0)
				return;

			this.invoker.InvokeOnUIThread(Colorize);
		}

		private void Colorize()
		{
			var range = this.range;
			var data = this.data;
			if (data != null)
			{
				TokenList tokensToColor;
				lock (this)
				{
					tokensToColor = this.ExtractTokensInRange(data.NotColoredTokens, range.Min, range.Max);
				}
				if (this.ColorizeTokens != null && tokensToColor != null && tokensToColor.Count > 0)
				{
					data.ColoredTokens.AddRange(tokensToColor);
					var args = new ColorizeEventArgs(tokensToColor);
					this.ColorizeTokens(this, args);
				}
			}

			this.wantsColorize = false;
			this.colorizing = 0;
		}

		private void DetectAlreadyColoredTokens(TokenList oldColoredTokens, int shift)
		{
			foreach (Token oldColored in oldColoredTokens)
			{
				int index;
				Token newColored;

				if (this.FindMatchingToken(this.data.NotColoredTokens, oldColored, 0, out index, out newColored) ||
					this.FindMatchingToken(this.data.NotColoredTokens, oldColored, shift, out index, out newColored))
				{
					this.data.NotColoredTokens.RemoveAt(index);
					this.data.ColoredTokens.Add(newColored);
				}
			}
		}

		#endregion Colorizing

		#region token utilities

		public TokenList ExtractTokensInRange(TokenList tokens, int from, int until)
		{
			TokenList result = new TokenList();
			for (int i = tokens.Count - 1; i >= 0; i--)
			{
				var tkn = tokens[i];
				if (tkn.Location.Position > until || (tkn.Location.Position + tkn.Length < from))
					continue;

				result.Add(tkn);
				tokens.RemoveAt(i);
			}

			return result;
		}

		public TokenList GetTokensInRange(int from, int until)
		{
			ViewData data = this.data;
			if (data == null)
				return null;

			return this.GetTokensInRange(data.Tree.Tokens, from, until);
		}

		public TokenList GetTokensInRange(TokenList tokens, int from, int until)
		{
			var result = new TokenList();
			int fromIndex = this.LocateToken(tokens, from);
			int untilIndex = this.LocateToken(tokens, until);
			if (fromIndex < 0)
				fromIndex = 0;

			if (untilIndex >= tokens.Count)
				untilIndex = tokens.Count - 1;

			for (int i = fromIndex; i <= untilIndex; i++)
			{
				result.Add(tokens[i]);
			}

			return result;
		}

		/// <summary>
		/// TODO: find better place for these methods
		/// </summary>
		/// <param name="tokens"></param>
		/// <param name="position"></param>
		/// <returns></returns>
		public int LocateToken(TokenList tokens, int position)
		{
			if (tokens == null || tokens.Count == 0)
				return -1;

			var lastToken = tokens[tokens.Count - 1];
			var lastTokenEnd = lastToken.Location.Position + lastToken.Length;
			if (position < tokens[0].Location.Position || position > lastTokenEnd)
				return -1;

			return this.LocateTokenExt(tokens, position, 0, tokens.Count - 1);
		}

		public bool TokensMatch(Token x, Token y, int shift)
		{
			if (x.Location.Position + shift != y.Location.Position)
				return false;

			if (x.Terminal != y.Terminal)
				return false;

			if (x.Text != y.Text)
				return false;

			// Note: be careful comparing x.Value and y.Value - if value is "ValueType", it is boxed and erroneously reports non-equal
			return true;
		}

		private bool FindMatchingToken(TokenList inTokens, Token token, int shift, out int index, out Token result)
		{
			index = this.LocateToken(inTokens, token.Location.Position + shift);
			if (index >= 0)
			{
				result = inTokens[index];
				if (this.TokensMatch(token, result, shift))
					return true;
			}

			index = -1;
			result = null;

			return false;
		}

		private int LocateTokenExt(TokenList tokens, int position, int fromIndex, int untilIndex)
		{
			if (fromIndex + 1 >= untilIndex)
				return fromIndex;

			int midIndex = (fromIndex + untilIndex) / 2;
			Token middleToken = tokens[midIndex];
			if (middleToken.Location.Position <= position)
				return this.LocateTokenExt(tokens, position, midIndex, untilIndex);
			else
				return this.LocateTokenExt(tokens, position, fromIndex, midIndex);
		}

		#endregion token utilities
	}

	/// <summary>
	/// Two scenarios:
	/// 1. Colorizing in current view range. We colorize only those tokens in current view range that were not colorized yet.
	///    For this we keep two lists (colorized and not colorized) tokens, and move tokens from one list to another when
	///    we actually colorize them.
	/// 2. Typing/Editing - new editor content is being pushed from EditorAdapter. We try to avoid recoloring all visible tokens, when
	///    user just typed a single char. What we do is try to identify "already-colored" tokens in new token list by matching
	///    old viewData.ColoredTokens to newly scanned token list - initially in new-viewData.NonColoredTokens. If we find a "match",
	///    we move the token from NonColored to Colored in new viewData. This all happens on background thread.
	/// </summary>
	public class EditorViewAdapterList : List<EditorViewAdapter> { }

	public class ViewData
	{
		/// <summary>
		/// ColoredTokens + NotColoredTokens == Source.Tokens
		/// </summary>
		public readonly TokenList ColoredTokens = new TokenList();

		/// <summary>
		/// Tokens not colored yet
		/// </summary>
		public readonly TokenList NotColoredTokens = new TokenList();

		public ParseTree Tree;

		public ViewData(ParseTree tree)
		{
			this.Tree = tree;
			if (tree == null)
				return;

			this.NotColoredTokens.AddRange(tree.Tokens);
		}
	}

	/// <summary>
	/// Container for two numbers representing visible range of the source text (min...max)
	/// we use it to allow replacing two numbers in atomic operation
	/// </summary>
	public class ViewRange
	{
		public readonly int Min, Max;

		public ViewRange(int min, int max)
		{
			this.Min = min;
			this.Max = max;
		}

		public bool Equals(ViewRange other)
		{
			return other.Min == this.Min && other.Max == this.Max;
		}
	}
}
