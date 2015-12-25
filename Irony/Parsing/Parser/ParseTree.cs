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
	public enum ParseTreeStatus
	{
		Parsing,
		Partial,
		Parsed,
		Error,
	}

	public class ParseTree
	{
		public readonly string FileName;

		public readonly TokenList OpenBraces = new TokenList();

		public readonly LogMessageList ParserMessages = new LogMessageList();

		public readonly string SourceText;

		public readonly TokenList Tokens = new TokenList();

		public long ParseTimeMilliseconds;

		public ParseTreeNode Root;

		/// <summary>
		/// Custom data object, use it anyway you want
		/// </summary>
		public object Tag;

		public ParseTree(string sourceText, string fileName)
		{
			this.SourceText = sourceText;
			this.FileName = fileName;
			this.Status = ParseTreeStatus.Parsing;
		}

		public ParseTreeStatus Status { get; internal set; }

		public bool HasErrors()
		{
			if (this.ParserMessages.Count == 0)
				return false;

			foreach (var err in this.ParserMessages)
			{
				if (err.Level == ErrorLevel.Error)
					return true;
			}

			return false;
		}
	}

	/// <summary>
	/// A node for a parse tree (concrete syntax tree) - an initial syntax representation produced by parser.
	/// It contains all syntax elements of the input text, each element represented by a generic node ParseTreeNode.
	/// The parse tree is converted into abstract syntax tree (AST) which contains custom nodes. The conversion might
	/// happen on-the-fly: as parser creates the parse tree nodes it can create the AST nodes and puts them into AstNode field.
	/// Alternatively it might happen as a separate step, after completing the parse tree.
	/// AST node might optinally implement IAstNodeInit interface, so Irony parser can initialize the node providing it
	/// with all relevant information.
	/// The ParseTreeNode also works as a stack element in the parser stack, so it has the State property to carry
	/// the pushed parser state while it is in the stack.
	/// </summary>
	public class ParseTreeNode
	{
		public Associativity Associativity;
		public object AstNode;

		/// <summary>
		/// Comments preceding this node
		/// </summary>
		public TokenList Comments;

		public bool IsError;
		public int Precedence;
		public SourceSpan Span;

		/// <summary>
		/// For use by custom parsers, Irony does not use it
		/// </summary>
		public object Tag;

		public BnfTerm Term;
		public Token Token;

		/// <summary>
		/// Used by parser to store current state when node is pushed into the parser stack
		/// </summary>
		internal ParserState State;

		public ParseTreeNode(Token token) : this()
		{
			this.Token = token;
			this.Term = token.Terminal;
			this.Precedence = this.Term.Precedence;
			this.Associativity = token.Terminal.Associativity;
			this.Span = new SourceSpan(token.Location, token.EndLocation);
			this.IsError = token.IsError();
		}

		public ParseTreeNode(ParserState initialState) : this()
		{
			this.State = initialState;
		}

		public ParseTreeNode(NonTerminal term, SourceSpan span) : this()
		{
			this.Term = term;
			this.Span = span;
		}

		private ParseTreeNode()
		{
			this.ChildNodes = new ParseTreeNodeList();
		}

		/// <summary>
		/// Making ChildNodes property (not field) following request by Matt K, Bill H
		/// </summary>
		public ParseTreeNodeList ChildNodes { get; private set; }

		public Token FindToken()
		{
			return FindFirstChildTokenRec(this);
		}

		public string FindTokenAndGetText()
		{
			var tkn = FindToken();
			return tkn == null ? null : tkn.Text;
		}

		public bool IsOperator()
		{
			return this.Term.Flags.IsSet(TermFlags.IsOperator);
		}

		/// <summary>Returns true if the node is punctuation or it is transient with empty child list.</summary>
		/// <returns>True if parser can safely ignore this node.</returns>
		public bool IsPunctuationOrEmptyTransient()
		{
			if (this.Term.Flags.IsSet(TermFlags.IsPunctuation))
				return true;

			if (this.Term.Flags.IsSet(TermFlags.IsTransient) && this.ChildNodes.Count == 0)
				return true;

			return false;
		}

		public override string ToString()
		{
			if (this.Term == null)
				return "(S0)"; //initial state node
			else
				return this.Term.GetParseNodeCaption(this);
		}

		private static Token FindFirstChildTokenRec(ParseTreeNode node)
		{
			if (node.Token != null)
				return node.Token;

			foreach (var child in node.ChildNodes)
			{
				var tkn = FindFirstChildTokenRec(child);
				if (tkn != null)
					return tkn;
			}

			return null;
		}
	}

	public class ParseTreeNodeList : List<ParseTreeNode> { }
}
