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
	public delegate void ExecuteActionMethod(ParsingContext context, CustomParserAction action);

	/// <summary>
	/// These two delegates define custom methods that Grammar can implement to execute custom action
	/// </summary>
	/// <param name="action"></param>
	public delegate void PreviewActionMethod(CustomParserAction action);

	public class CustomActionHint : GrammarHint
	{
		private ExecuteActionMethod executeMethod;
		private PreviewActionMethod previewMethod;

		public CustomActionHint(ExecuteActionMethod executeMethod, PreviewActionMethod previewMethod = null)
		{
			this.executeMethod = executeMethod;
			this.previewMethod = previewMethod;
		}

		public override void Apply(LanguageData language, Construction.LRItem owner)
		{
			// Create custom action and put it into state.Actions table
			var state = owner.State;

			var action = new CustomParserAction(language, state, this.executeMethod);
			if (this.previewMethod != null)
				this.previewMethod(action);

			// Adequate state, with a single possible action which is DefaultAction
			if (!state.BuilderData.IsInadequate)
				state.DefaultAction = action;
			// Shift action
			else if (owner.Core.Current != null)
				state.Actions[owner.Core.Current] = action;
			else foreach (var lkh in owner.Lookaheads)
				state.Actions[lkh] = action;

			// We consider all conflicts handled by the action
			state.BuilderData.Conflicts.Clear();
		}
	}

	/// <summary>
	/// CustomParserAction is in fact action selector: it allows custom Grammar code to select the action
	/// to execute from a set of shift/reduce actions available in this state.
	/// </summary>
	public class CustomParserAction : ParserAction
	{
		public TerminalSet Conflicts = new TerminalSet();
		public object CustomData;
		public ExecuteActionMethod ExecuteRef;
		public LanguageData Language;
		public IList<ReduceParserAction> ReduceActions = new List<ReduceParserAction>();
		public IList<ShiftParserAction> ShiftActions = new List<ShiftParserAction>();
		public ParserState State;

		public CustomParserAction(LanguageData language, ParserState state, ExecuteActionMethod executeRef)
		{
			this.Language = language;
			this.State = state;
			this.ExecuteRef = executeRef;
			this.Conflicts.UnionWith(state.BuilderData.Conflicts);

			// Create default shift and reduce actions
			foreach (var shiftItem in state.BuilderData.ShiftItems)
			{
				this.ShiftActions.Add(new ShiftParserAction(shiftItem));
			}

			foreach (var item in state.BuilderData.ReduceItems)
			{
				ReduceActions.Add(ReduceParserAction.Create(item.Core.Production));
			}
		}

		public override void Execute(ParsingContext context)
		{
			if (context.TracingEnabled)
				context.AddTrace(Resources.MsgTraceExecCustomAction);

			// States with DefaultAction do NOT read input, so we read it here
			if (context.CurrentParserInput == null)
				context.Parser.ReadInput();

			// Remember old state and input; if they don't change after custom action - it is error, we may fall into an endless loop
			var oldState = context.CurrentParserState;
			var oldInput = context.CurrentParserInput;
			this.ExecuteRef(context, this);

			// Prevent from falling into an infinite loop
			if (context.CurrentParserState == oldState && context.CurrentParserInput == oldInput)
			{
				context.AddParserError(Resources.MsgErrorCustomActionDidNotAdvance);
				context.Parser.RecoverFromError();
			}
		}

		public override string ToString()
		{
			return "CustomParserAction";
		}
	}
}
