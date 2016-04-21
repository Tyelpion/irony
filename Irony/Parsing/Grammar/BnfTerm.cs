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
	[Flags]
	public enum TermFlags
	{
		None = 0,
		IsOperator = 0x01,
		IsOpenBrace = 0x02,
		IsCloseBrace = 0x04,
		IsBrace = IsOpenBrace | IsCloseBrace,
		IsLiteral = 0x08,

		IsConstant = 0x10,
		IsPunctuation = 0x20,
		IsDelimiter = 0x40,
		IsReservedWord = 0x080,
		IsMemberSelect = 0x100,

		/// <summary>
		/// Signals that non-terminal must inherit precedence and assoc values from its children.
		/// Typically set for BinOp nonterminal (where BinOp.Rule = '+' | '-' | ...)
		/// </summary>
		InheritPrecedence = 0x200,

		/// <summary>
		/// Indicates that tokens for this terminal are NOT produced by scanner.
		/// </summary>
		IsNonScanner = 0x01000,

		/// <summary>
		/// If set, parser would eliminate the token from the input stream; terms in Grammar.NonGrammarTerminals have this flag set.
		/// </summary>
		IsNonGrammar = 0x02000,

		/// <summary>
		/// Transient non-terminal - should be replaced by it's child in the AST tree.
		/// </summary>
		IsTransient = 0x04000,

		/// <summary>
		/// Exclude from expected terminals list on syntax error.
		/// </summary>
		IsNotReported = 0x08000,

		IsNullable = 0x010000,
		IsVisible = 0x020000,
		IsKeyword = 0x040000,
		IsMultiline = 0x100000,

		IsList = 0x200000,
		IsListContainer = 0x400000,

		/// <summary>
		/// Indicates not to create AST node; mainly to suppress warning message on some special nodes that AST node type is not specified
		/// Automatically set by MarkTransient method
		/// </summary>
		NoAstNode = 0x800000,

		/// <summary>
		/// A flag to suppress automatic AST creation for child nodes in global AST construction. Will be used to supress full
		/// "compile" of method bodies in modules. The module might be large, but the running code might
		/// be actually using only a few methods or global members; so in this case it makes sense to "compile" only global/public
		/// declarations, including method headers but not full bodies. The body will be compiled on the first call.
		/// This makes even more sense when processing module imports.
		/// </summary>
		AstDelayChildren = 0x1000000,
	}

	/// <summary>
	/// Basic Backus-Naur Form element. Base class for <see cref="Terminal"/>, <see cref="NonTerminal"/>, <see cref="BnfExpression"/>, <see cref="GrammarHint"/>
	/// </summary>
	public abstract class BnfTerm
	{
		#region consructors

		protected BnfTerm(string name) : this(name, name)
		{
		}

		protected BnfTerm(string name, string errorAlias, Type nodeType) : this(name, errorAlias)
		{
			this.AstConfig.NodeType = nodeType;
		}

		protected BnfTerm(string name, string errorAlias, AstNodeCreator nodeCreator) : this(name, errorAlias)
		{
			this.AstConfig.NodeCreator = nodeCreator;
		}

		protected BnfTerm(string name, string errorAlias)
		{
			this.Name = name;
			this.ErrorAlias = errorAlias;
			this.hashCode = (_hashCounter++).GetHashCode();
		}

		#endregion consructors

		#region virtuals and overrides

		/// <summary>
		/// Hash code - we use static counter to generate hash codes
		/// </summary>
		private static int _hashCounter;

		private int hashCode;

		public override int GetHashCode()
		{
			return this.hashCode;
		}

		public virtual string GetParseNodeCaption(ParseTreeNode node)
		{
			if (this.GrammarData != null)
				return this.GrammarData.Grammar.GetParseNodeCaption(node);
			else
				return this.Name;
		}

		public virtual void Init(GrammarData grammarData)
		{
			this.GrammarData = grammarData;
		}

		public override string ToString()
		{
			return this.Name;
		}

		#endregion virtuals and overrides

		public const int NoPrecedence = 0;

		#region properties: Name, DisplayName, Key, Options

		public Associativity Associativity = Associativity.Neutral;

		/// <summary>
		/// ErrorAlias is used in error reporting, e.g. "Syntax error, expected &lt;list-of-display-names&gt;".
		/// </summary>
		public string ErrorAlias;

		public TermFlags Flags;
		public string Name;

#pragma warning disable RECS0122 // Initializing field with default value is redundant
		public int Precedence = NoPrecedence;
#pragma warning restore RECS0122 // Initializing field with default value is redundant

		protected GrammarData GrammarData;

		public Grammar Grammar
		{
			get { return this.GrammarData.Grammar; }
		}

		public void SetFlag(TermFlags flag)
		{
			this.SetFlag(flag, true);
		}

		public void SetFlag(TermFlags flag, bool value)
		{
			if (value)
				this.Flags |= flag;
			else
				this.Flags &= ~flag;
		}

		#endregion properties: Name, DisplayName, Key, Options

		#region events: Shifting

		/// <summary>
		/// An event fired after AST node is created.
		/// </summary>
		public event EventHandler<AstNodeEventArgs> AstNodeCreated;

		public event EventHandler<ParsingEventArgs> Shifting;

		protected internal void OnAstNodeCreated(ParseTreeNode parseNode)
		{
			if (this.AstNodeCreated == null || parseNode.AstNode == null)
				return;

			var args = new AstNodeEventArgs(parseNode);
			this.AstNodeCreated(this, args);
		}

		protected internal void OnShifting(ParsingEventArgs args)
		{
			if (this.Shifting != null)
				this.Shifting(this, args);
		}

		#endregion events: Shifting

		#region AST node creations: AstNodeType, AstNodeCreator, AstNodeCreated

		private AstNodeConfig astConfig;

		/// <summary>
		/// We autocreate AST config on first GET;
		/// </summary>
		public AstNodeConfig AstConfig
		{
			get
			{
				if (this.astConfig == null)
					this.astConfig = new Ast.AstNodeConfig();

				return this.astConfig;
			}
			set
			{
				this.astConfig = value;
			}
		}

		public bool HasAstConfig()
		{
			return this.astConfig != null;
		}

		#endregion AST node creations: AstNodeType, AstNodeCreator, AstNodeCreated

		#region Kleene operator Q()

		private NonTerminal q;

		public BnfExpression Q()
		{
			if (this.q != null)
				return this.q;

			this.q = new NonTerminal(this.Name + "?");
			this.q.Rule = this | Grammar.CurrentGrammar.Empty;

			return this.q;
		}

		#endregion Kleene operator Q()

		#region Operators: +, |, implicit

		public static BnfExpression operator |(BnfTerm term1, BnfTerm term2)
		{
			return PipeOperator(term1, term2);
		}

		public static BnfExpression operator |(BnfTerm term1, string symbol2)
		{
			return PipeOperator(term1, Grammar.CurrentGrammar.ToTerm(symbol2));
		}

		public static BnfExpression operator |(string symbol1, BnfTerm term2)
		{
			return PipeOperator(Grammar.CurrentGrammar.ToTerm(symbol1), term2);
		}

		public static BnfExpression operator +(BnfTerm term1, BnfTerm term2)
		{
			return PlusOperator(term1, term2);
		}

		public static BnfExpression operator +(BnfTerm term1, string symbol2)
		{
			return PlusOperator(term1, Grammar.CurrentGrammar.ToTerm(symbol2));
		}

		public static BnfExpression operator +(string symbol1, BnfTerm term2)
		{
			return PlusOperator(Grammar.CurrentGrammar.ToTerm(symbol1), term2);
		}

		/// <summary>
		/// New version proposed by the codeplex user bdaugherty
		/// </summary>
		/// <param name="term1"></param>
		/// <param name="term2"></param>
		/// <returns></returns>
		internal static BnfExpression PipeOperator(BnfTerm term1, BnfTerm term2)
		{
			var expr1 = term1 as BnfExpression;
			if (expr1 == null)
				expr1 = new BnfExpression(term1);

			var expr2 = term2 as BnfExpression;
			if (expr2 == null)
				expr2 = new BnfExpression(term2);

			expr1.Data.AddRange(expr2.Data);

			return expr1;
		}

		internal static BnfExpression PlusOperator(BnfTerm term1, BnfTerm term2)
		{
			// Check term1 and see if we can use it as result, simply adding term2 as operand
			var expr1 = term1 as BnfExpression;

			// Either not expression at all, or Pipe-type expression (count > 1)
			if (expr1 == null || expr1.Data.Count > 1)
				expr1 = new BnfExpression(term1);

			expr1.Data[expr1.Data.Count - 1].Add(term2);

			return expr1;
		}

		#endregion Operators: +, |, implicit
	}

	public class BnfTermList : List<BnfTerm> { }

	public class BnfTermSet : HashSet<BnfTerm> { }
}
