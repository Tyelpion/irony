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
	public enum GrammarErrorLevel
	{
		/// <summary>
		/// Used only for max error level when there are no errors
		/// </summary>
		NoError,

		Info,
		Warning,

		/// <summary>
		/// Shift-reduce or reduce-reduce conflict
		/// </summary>
		Conflict,

		/// <summary>
		/// Severe grammar error, parser construction cannot continue
		/// </summary>
		Error,

		/// <summary>
		/// Internal Irony error
		/// </summary>
		InternalError,
	}

	public class GrammarError
	{
		public readonly GrammarErrorLevel Level;
		public readonly string Message;

		/// <summary>
		/// Can be null!
		/// </summary>
		public readonly ParserState State;

		public GrammarError(GrammarErrorLevel level, ParserState state, string message)
		{
			this.Level = level;
			this.State = state;
			this.Message = message;
		}

		public override string ToString()
		{
			return this.Message + " (" + this.State + ")";
		}
	}

	/// <summary>
	/// Used to cancel parser construction when fatal error is found
	/// </summary>
	public class GrammarErrorException : Exception
	{
		public readonly GrammarError Error;

		public GrammarErrorException(string message, GrammarError error) : base(message)
		{
			this.Error = error;
		}
	}

	public class GrammarErrorList : List<GrammarError>
	{
		public void Add(GrammarErrorLevel level, ParserState state, string message, params object[] args)
		{
			if (args != null && args.Length > 0)
				message = String.Format(message, args);

			this.Add(new GrammarError(level, state, message));
		}

		public void AddAndThrow(GrammarErrorLevel level, ParserState state, string message, params object[] args)
		{
			this.Add(level, state, message, args);
			var error = this[this.Count - 1];
			var exc = new GrammarErrorException(error.Message, error);

			throw exc;
		}

		public GrammarErrorLevel GetMaxLevel()
		{
			var max = GrammarErrorLevel.NoError;
			foreach (var err in this)
			{
				if (max < err.Level)
					max = err.Level;
			}

			return max;
		}
	}
}
