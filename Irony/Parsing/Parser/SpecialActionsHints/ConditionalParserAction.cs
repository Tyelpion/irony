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
	public enum PreferredActionType
	{
		Shift,
		Reduce,
	}

	public class ConditionalParserAction : ParserAction
	{
		#region embedded types

		public delegate bool ConditionChecker(ParsingContext context);

		public class ConditionalEntry
		{
			public ParserAction Action;
			public ConditionChecker Condition;

			/// <summary>
			/// For tracing
			/// </summary>
			public string Description;

			public ConditionalEntry(ConditionChecker condition, ParserAction action, string description)
			{
				this.Condition = condition;
				this.Action = action;
				this.Description = description;
			}

			public override string ToString()
			{
				return this.Description + "; action: " + this.Action.ToString();
			}
		}

		public class ConditionalEntryList : List<ConditionalEntry> { }

		#endregion embedded types

		public ConditionalEntryList ConditionalEntries = new ConditionalEntryList();
		public ParserAction DefaultAction;

		public override void Execute(ParsingContext context)
		{
			var traceEnabled = context.TracingEnabled;
			if (traceEnabled)
				context.AddTrace("Conditional Parser Action.");

			for (int i = 0; i < this.ConditionalEntries.Count; i++)
			{
				var ce = this.ConditionalEntries[i];
				if (traceEnabled)
					context.AddTrace("  Checking condition: " + ce.Description);

				if (ce.Condition(context))
				{
					if (traceEnabled)
						context.AddTrace("  Condition is TRUE, executing action: " + ce.Action.ToString());

					ce.Action.Execute(context);
					return;
				}
			}

			// If no conditions matched, execute default action
			if (this.DefaultAction == null)
			{
				context.AddParserError("Fatal parser error: no conditions matched in conditional parser action, and default action is null. State: {0}", context.CurrentParserState.Name);
				context.Parser.RecoverFromError();
				return;
			}

			if (traceEnabled)
				context.AddTrace("  All conditions failed, executing default action: " + this.DefaultAction.ToString());

			this.DefaultAction.Execute(context);
		}
	}
}
