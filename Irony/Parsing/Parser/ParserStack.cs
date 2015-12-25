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

namespace Irony.Parsing
{
	public class ParserStack : List<ParseTreeNode>
	{
		public ParserStack() : base(200)
		{ }

		public ParseTreeNode Top
		{
			get
			{
				if (this.Count == 0)
					return null;

				return base[this.Count - 1];
			}
		}

		public ParseTreeNode Pop()
		{
			var top = this.Top;
			this.RemoveAt(this.Count - 1);

			return top;
		}

		public void Pop(int count)
		{
			this.RemoveRange(this.Count - count, count);
		}

		public void PopUntil(int finalCount)
		{
			if (finalCount < this.Count)
				this.Pop(this.Count - finalCount);
		}

		public void Push(ParseTreeNode nodeInfo)
		{
			this.Add(nodeInfo);
		}

		public void Push(ParseTreeNode nodeInfo, ParserState state)
		{
			nodeInfo.State = state;
			this.Add(nodeInfo);
		}
	}
}
