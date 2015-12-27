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

using Irony.Ast;
using Irony.Parsing;

namespace Irony.Interpreter.Ast
{
	/* Example of use:

		// String literal with embedded expressions  ------------------------------------------------------------------
		var stringLit = new StringLiteral("string", "\"", StringOptions.AllowsAllEscapes | StringOptions.IsTemplate);
		stringLit.AstNodeType = typeof(StringTemplateNode);
		var Expr = new NonTerminal("Expr");

		// By default set to Ruby-style settings
		var templateSettings = new StringTemplateSettings();

		// This defines how to evaluate expressions inside template
		templateSettings.ExpressionRoot = Expr;
		this.SnippetRoots.Add(Expr);
		stringLit.AstNodeConfig = templateSettings;

		// Define Expr as an expression non-terminal in your grammar

	*/

	/// <summary>
	/// Implements Ruby-like active strings with embedded expressions
	/// </summary>
	public class StringTemplateNode : AstNode
	{
		#region embedded classes

		private enum SegmentType
		{
			Text,
			Expression
		}

		private class SegmentList : List<TemplateSegment>
		{ }

		private class TemplateSegment
		{
			public AstNode ExpressionNode;

			/// <summary>
			/// Position in raw text of the token for error reporting
			/// </summary>
			public int Position;

			public string Text;
			public SegmentType Type;

			public TemplateSegment(string text, AstNode node, int position)
			{
				this.Type = node == null ? SegmentType.Text : SegmentType.Expression;
				this.Text = text;
				this.ExpressionNode = node;
				this.Position = position;
			}
		}

		#endregion embedded classes

		private SegmentList segments = new SegmentList();
		private string template;

		/// <summary>
		/// Copied from Terminal.AstNodeConfig
		/// </summary>
		private StringTemplateSettings templateSettings;

		/// <summary>
		/// Used for locating error
		/// </summary>
		private string tokenText;

		public override void Init(AstContext context, ParseTreeNode treeNode)
		{
			base.Init(context, treeNode);
			this.template = treeNode.Token.ValueString;
			this.tokenText = treeNode.Token.Text;
			this.templateSettings = treeNode.Term.AstConfig.Data as StringTemplateSettings;
			this.ParseSegments(context);
			this.AsString = "\"" + this.template + "\" (templated string)";
		}

		protected override object DoEvaluate(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;
			var value = this.BuildString(thread);

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return value;
		}

		private object BuildString(ScriptThread thread)
		{
			var values = new string[this.segments.Count];

			for (int i = 0; i < this.segments.Count; i++)
			{
				var segment = this.segments[i];
				switch (segment.Type)
				{
					case SegmentType.Text:
						values[i] = segment.Text;
						break;

					case SegmentType.Expression:
						values[i] = EvaluateExpression(thread, segment);
						break;
				}
			}

			var result = string.Join(string.Empty, values);
			return result;
		}

		private void CopyMessages(LogMessageList fromList, LogMessageList toList, SourceLocation baseLocation, string messagePrefix)
		{
			foreach (var other in fromList)
			{
				toList.Add(new LogMessage(other.Level, baseLocation + other.Location, messagePrefix + other.Message, other.ParserState));
			}
		}

		private string EvaluateExpression(ScriptThread thread, TemplateSegment segment)
		{
			try
			{
				var value = segment.ExpressionNode.Evaluate(thread);
				return value == null ? string.Empty : value.ToString();
			}
			catch
			{
				// We need to catch here and set current node; ExpressionNode may have reset it, and location would be wrong
				// TODO: fix this - set error location to exact location inside string.
				thread.CurrentNode = this;
				throw;
			}
		}

		private void ParseSegments(AstContext context)
		{
			var exprParser = new Parser(context.Language, this.templateSettings.ExpressionRoot);

			// As we go along the "value text" (that has all escapes done), we track the position in raw token text  in the variable exprPosInTokenText.
			// This position is position in original text in source code, including original escaping sequences and open/close quotes.
			// It will be passed to segment constructor, and maybe used later to compute the exact position of runtime error when it occurs.
			int currentPos = 0, exprPosInTokenText = 0;

			while (true)
			{
				var startTagPos = this.template.IndexOf(this.templateSettings.StartTag, currentPos);

				if (startTagPos < 0)
					startTagPos = this.template.Length;

				var text = this.template.Substring(currentPos, startTagPos - currentPos);

				if (!string.IsNullOrEmpty(text))
					// For text segments position is not used
					this.segments.Add(new TemplateSegment(text, null, 0));

				if (startTagPos >= this.template.Length)
					// We have a real start tag, grab the expression
					break;

				currentPos = startTagPos + this.templateSettings.StartTag.Length;

				var endTagPos = this.template.IndexOf(this.templateSettings.EndTag, currentPos);
				if (endTagPos < 0)
				{
					// "No ending tag '{0}' found in embedded expression."
					context.AddMessage(ErrorLevel.Error, this.Location, Resources.ErrNoEndTagInEmbExpr, this.templateSettings.EndTag);
					return;
				}

				var exprText = this.template.Substring(currentPos, endTagPos - currentPos);

				if (!string.IsNullOrEmpty(exprText))
				{
					var exprTree = exprParser.Parse(exprText);
					if (exprTree.HasErrors())
					{
						// We use original search in token text instead of currentPos in template to avoid distortions caused by opening quote and escaped sequences
						var baseLocation = this.Location + this.tokenText.IndexOf(exprText);
						this.CopyMessages(exprTree.ParserMessages, context.Messages, baseLocation, Resources.ErrInvalidEmbeddedPrefix);
						return;
					}

					// Add the expression segment
					exprPosInTokenText = this.tokenText.IndexOf(this.templateSettings.StartTag, exprPosInTokenText) + this.templateSettings.StartTag.Length;

					var segmNode = exprTree.Root.AstNode as AstNode;

					// Important to attach the segm node to current Module
					segmNode.Parent = this;
					this.segments.Add(new TemplateSegment(null, segmNode, exprPosInTokenText));

					// Advance position beyond the expression
					exprPosInTokenText += exprText.Length + this.templateSettings.EndTag.Length;
				}

				currentPos = endTagPos + this.templateSettings.EndTag.Length;
			}
		}
	}
}
