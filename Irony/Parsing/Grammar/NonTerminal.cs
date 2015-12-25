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
using Irony.Ast;

namespace Irony.Parsing
{
	public partial class NonTerminal : BnfTerm
	{
		#region constructors

		public NonTerminal(string name) : base(name, null) // By default display name is null
		{ }

		public NonTerminal(string name, string errorAlias) : base(name, errorAlias)
		{ }

		public NonTerminal(string name, string errorAlias, Type nodeType) : base(name, errorAlias, nodeType)
		{ }

		public NonTerminal(string name, string errorAlias, AstNodeCreator nodeCreator) : base(name, errorAlias, nodeCreator)
		{ }

		public NonTerminal(string name, Type nodeType) : base(name, null, nodeType)
		{ }

		public NonTerminal(string name, AstNodeCreator nodeCreator) : base(name, null, nodeCreator)
		{ }

		public NonTerminal(string name, BnfExpression expression)
		  : this(name)
		{
			this.Rule = expression;
		}

		#endregion constructors

		#region properties/fields: Rule, ErrorRule

		/// <summary>
		/// Separate property for specifying error expressions. This allows putting all such expressions in a separate section
		/// in grammar for all non-terminals. However you can still put error expressions in the main Rule property, just like
		/// in YACC
		/// </summary>
		public BnfExpression ErrorRule;

		/// <summary>
		/// A template for representing ParseTreeNode in the parse tree. Can contain '#{i}' fragments referencing
		/// child nodes by index
		/// </summary>
		public string NodeCaptionTemplate;

		public BnfExpression Rule;

		/// <summary>
		/// Productions are used internally by Parser builder
		/// </summary>
		internal ProductionList Productions = new ProductionList();

		private IntList captionParameters;

		/// <summary>
		/// Converted template with index list
		/// </summary>
		private string convertedTemplate;

		#endregion properties/fields: Rule, ErrorRule

		#region Events: Reduced

		/// <summary>
		/// Note that Reduced event may be called more than once for a List node
		/// </summary>
		public event EventHandler<ReducedEventArgs> Reduced;

		internal void OnReduced(ParsingContext context, Production reducedProduction, ParseTreeNode resultNode)
		{
			if (this.Reduced != null)
				this.Reduced(this, new ReducedEventArgs(context, reducedProduction, resultNode));
		}

		#endregion Events: Reduced

		#region overrides: ToString, Init

		public override void Init(GrammarData grammarData)
		{
			base.Init(grammarData);

			if (!string.IsNullOrEmpty(this.NodeCaptionTemplate))
				this.ConvertNodeCaptionTemplate();
		}

		public override string ToString()
		{
			return this.Name;
		}

		#endregion overrides: ToString, Init

		#region Grammar hints

		/// <summary>
		/// Adds a hint at the end of all productions
		/// </summary>
		/// <param name="hint"></param>
		/// <remarks>
		/// Contributed by Alexey Yakovlev (yallie)
		/// </remarks>
		public void AddHintToAll(GrammarHint hint)
		{
			if (this.Rule == null)
				throw new Exception("Rule property must be set on non-terminal before calling AddHintToAll.");

			foreach (var plusList in this.Rule.Data)
			{
				plusList.Add(hint);
			}
		}

		#endregion Grammar hints

		#region NodeCaptionTemplate utilities

		public string GetNodeCaption(ParseTreeNode node)
		{
			var paramValues = new string[this.captionParameters.Count];
			for (int i = 0; i < this.captionParameters.Count; i++)
			{
				var childIndex = this.captionParameters[i];
				if (childIndex < node.ChildNodes.Count)
				{
					var child = node.ChildNodes[childIndex];

					// If child is a token, then child.ToString returns token.ToString which contains Value + Term;
					// In this case we prefer to have Value only
					paramValues[i] = (child.Token != null ? child.Token.ValueString : child.ToString());
				}
			}

			var result = string.Format(this.convertedTemplate, paramValues);
			return result;
		}

		/// <summary>
		/// We replace original tag '#{i}'  (where i is the index of the child node to put here)
		/// with the tag '{k}', where k is the number of the parameter. So after conversion the template can
		/// be used in string.Format() call, with parameters set to child nodes captions
		/// </summary>
		private void ConvertNodeCaptionTemplate()
		{
			this.captionParameters = new IntList();
			this.convertedTemplate = this.NodeCaptionTemplate;

			var index = 0;
			while (index < 100)
			{
				var strParam = "#{" + index + "}";
				if (this.convertedTemplate.Contains(strParam))
				{
					this.convertedTemplate = this.convertedTemplate.Replace(strParam, "{" + this.captionParameters.Count + "}");
					this.captionParameters.Add(index);
				}

				if (!this.convertedTemplate.Contains("#{"))
					return;

				index++;
			}
		}

		#endregion NodeCaptionTemplate utilities
	}

	public class NonTerminalList : List<NonTerminal>
	{
		public override string ToString()
		{
			return string.Join(" ", this);
		}
	}

	public class NonTerminalSet : HashSet<NonTerminal>
	{
		public override string ToString()
		{
			return string.Join(" ", this);
		}
	}

	internal class IntList : List<int> { }
}
