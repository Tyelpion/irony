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

namespace Irony.Parsing
{
	public struct SourceLocation
	{
		/// <summary>
		/// Source column number, 0-based.
		/// </summary>
		public int Column;

		/// <summary>
		/// Source line number, 0-based.
		/// </summary>
		public int Line;

		public int Position;

		private static SourceLocation _empty = new SourceLocation();

		public SourceLocation(int position, int line, int column)
		{
			this.Position = position;
			this.Line = line;
			this.Column = column;
		}

		public static SourceLocation Empty
		{
			get { return _empty; }
		}

		public static int Compare(SourceLocation x, SourceLocation y)
		{
			if (x.Position < y.Position)
				return -1;

			if (x.Position == y.Position)
				return 0;

			return 1;
		}

		public static SourceLocation operator +(SourceLocation x, SourceLocation y)
		{
			return new SourceLocation(x.Position + y.Position, x.Line + y.Line, x.Column + y.Column);
		}

		public static SourceLocation operator +(SourceLocation x, int offset)
		{
			return new SourceLocation(x.Position + offset, x.Line, x.Column + offset);
		}

		/// <summary>
		/// Line/col are zero-based internally
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return string.Format(Resources.FmtRowCol, this.Line + 1, this.Column + 1);
		}

		/// <summary>
		/// Line and Column displayed to user should be 1-based
		/// </summary>
		/// <returns></returns>
		public string ToUiString()
		{
			return string.Format(Resources.FmtRowCol, this.Line + 1, this.Column + 1);
		}
	}

	public struct SourceSpan
	{
		public readonly int Length;

		public readonly SourceLocation Location;

		public SourceSpan(SourceLocation location, int length)
		{
			this.Location = location;
			this.Length = length;
		}

		public int EndPosition
		{
			get { return this.Location.Position + this.Length; }
		}

		public bool InRange(int position)
		{
			return (position >= this.Location.Position && position <= this.EndPosition);
		}
	}
}
