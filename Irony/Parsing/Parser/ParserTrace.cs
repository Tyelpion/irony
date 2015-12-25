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

namespace Irony.Parsing
{
	public class ParserTrace : List<ParserTraceEntry> { }

	public class ParserTraceEntry
	{
		public ParseTreeNode Input;
		public bool IsError;
		public string Message;
		public ParseTreeNode StackTop;
		public ParserState State;

		public ParserTraceEntry(ParserState state, ParseTreeNode stackTop, ParseTreeNode input, string message, bool isError)
		{
			this.State = state;
			this.StackTop = stackTop;
			this.Input = input;
			this.Message = message;
			this.IsError = isError;
		}
	}

	public class ParserTraceEventArgs : EventArgs
	{
		public readonly ParserTraceEntry Entry;

		public ParserTraceEventArgs(ParserTraceEntry entry)
		{
			this.Entry = entry;
		}

		public override string ToString()
		{
			return this.Entry.ToString();
		}
	}
}
