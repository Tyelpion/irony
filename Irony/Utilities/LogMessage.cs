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

using Irony.Parsing;

namespace Irony
{
	public enum ErrorLevel
	{
		Info = 0,
		Warning = 1,
		Error = 2,
	}

	/// <summary>
	/// Container for syntax errors and warnings
	/// </summary>
	public class LogMessage
	{
		public readonly ErrorLevel Level;

		public readonly SourceLocation Location;

		public readonly string Message;

		public readonly ParserState ParserState;

		public LogMessage(ErrorLevel level, SourceLocation location, string message, ParserState parserState)
		{
			this.Level = level;
			this.Location = location;
			this.Message = message;
			this.ParserState = parserState;
		}

		public override string ToString()
		{
			return this.Message;
		}
	}

	public class LogMessageList : List<LogMessage>
	{
		public static int ByLocation(LogMessage x, LogMessage y)
		{
			return SourceLocation.Compare(x.Location, y.Location);
		}
	}
}
