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
using System.Linq.Expressions;

using Irony.Ast;
using Irony.Parsing;

namespace Irony.Interpreter.Ast
{
	public static class CustomExpressionTypes
	{
		public const ExpressionType NotAnExpression = (ExpressionType) (-1);
	}

	/// <summary>
	/// Base AST node class
	/// </summary>
	public partial class AstNode : IAstNodeInit, IBrowsableAstNode, IVisitableNode
	{
		/// <summary>
		/// List of child nodes
		/// </summary>
		public readonly AstNodeList ChildNodes = new AstNodeList();

		/// <summary>
		/// Used for pointing to error location. For most nodes it would be the location of the node itself.
		/// One exception is BinExprNode: when we get "Division by zero" error evaluating
		///  x = (5 + 3) / (2 - 2)
		/// it is better to point to "/" as error location, rather than the first "(" - which is the start
		/// location of binary expression.
		/// </summary>
		public SourceLocation ErrorAnchor;

		/// <summary>
		/// Reference to Evaluate method implementation. Initially set to DoEvaluate virtual method.
		/// </summary>
		public EvaluateMethod Evaluate;

		public AstNodeFlags Flags;
		public AstNode Parent;

		/// <summary>
		/// Role is a free-form string used as prefix in ToString() representation of the node.
		/// Node's parent can set it to "property name" or role of the child node in parent's node currentFrame.Context.
		/// </summary>
		public string Role;

		public ValueSetterMethod SetValue;
		public BnfTerm Term;

		/// <summary>
		/// UseType is set by parent
		/// </summary>
		public NodeUseType UseType = NodeUseType.Unknown;

		protected ExpressionType ExpressionType = CustomExpressionTypes.NotAnExpression;

		protected object LockObject = new object();

		/// <summary>
		/// ModuleNode - computed on demand
		/// </summary>
		private AstNode moduleNode;

		/// <summary>
		/// Public default constructor
		/// </summary>
		public AstNode()
		{
			this.Evaluate = DoEvaluate;
			this.SetValue = DoSetValue;
		}

		/// <summary>
		/// Default AstNode.ToString() returns 'Role: AsString', which is used for showing node in AST tree.
		/// </summary>
		public virtual string AsString { get; protected set; }

		public SourceLocation Location { get { return Span.Location; } }

		/// <summary>
		/// ModuleNode - computed on demand
		/// </summary>
		public AstNode ModuleNode
		{
			get
			{
				if (this.moduleNode == null)
				{
					this.moduleNode = (this.Parent == null) ? this : this.Parent.ModuleNode;
				}

				return this.moduleNode;
			}
			set
			{
				this.moduleNode = value;
			}
		}

		public SourceSpan Span { get; set; }

		#region IAstNodeInit Members

		public virtual void Init(AstContext context, ParseTreeNode treeNode)
		{
			this.Term = treeNode.Term;
			this.Span = treeNode.Span;
			this.ErrorAnchor = this.Location;
			treeNode.AstNode = this;
			this.AsString = (this.Term == null ? this.GetType().Name : this.Term.Name);
		}

		#endregion IAstNodeInit Members

		#region virtual methods: DoEvaluate, SetValue, IsConstant, SetIsTail, GetDependentScopeInfo

		private ScopeInfo dependentScope;

		/// <summary>
		/// Dependent scope is a scope produced by the node. For ex, FunctionDefNode defines a scope
		/// </summary>
		public virtual ScopeInfo DependentScopeInfo
		{
			get { return this.dependentScope; }

			set { this.dependentScope = value; }
		}

		public virtual void DoSetValue(ScriptThread thread, object value)
		{
			// Place the prolog/epilog lines in every implementation of SetValue method (see DoEvaluate above)
		}

		public virtual bool IsConstant()
		{
			return false;
		}

		public virtual void Reset()
		{
			this.moduleNode = null;
			this.Evaluate = this.DoEvaluate;

			foreach (var child in this.ChildNodes)
			{
				child.Reset();
			}
		}

		/// <summary>
		/// Sets a flag indicating that the node is in tail position. The value is propagated from parent to children.
		/// Should propagate this call to appropriate children.
		/// </summary>
		public virtual void SetIsTail()
		{
			this.Flags |= AstNodeFlags.IsTail;
		}

		/// <summary>
		/// By default the Evaluate field points to this method.
		/// </summary>
		/// <param name="thread"></param>
		/// <returns></returns>
		protected virtual object DoEvaluate(ScriptThread thread)
		{
			// These 2 lines are standard prolog/epilog statements.
			// Place them in every Evaluate and SetValue implementations.

			// Standard prolog
			thread.CurrentNode = this;

			// tandard epilog
			thread.CurrentNode = this.Parent;
			return null;
		}

		#endregion virtual methods: DoEvaluate, SetValue, IsConstant, SetIsTail, GetDependentScopeInfo

		#region IBrowsableAstNode Members

		public int Position
		{
			get { return this.Span.Location.Position; }
		}

		public virtual System.Collections.IEnumerable GetChildNodes()
		{
			return this.ChildNodes;
		}

		#endregion IBrowsableAstNode Members

		#region Visitors, Iterators

		/// <summary>
		/// The first primitive Visitor facility
		/// </summary>
		/// <param name="visitor"></param>
		public virtual void AcceptVisitor(IAstVisitor visitor)
		{
			visitor.BeginVisit(this);

			if (this.ChildNodes.Count > 0)
			{
				foreach (AstNode node in this.ChildNodes)
				{
					node.AcceptVisitor(visitor);
				}
			}

			visitor.EndVisit(this);
		}

		public IEnumerable<AstNode> GetAll()
		{
			var result = new AstNodeList();
			this.AddAll(result);

			return result;
		}

		private void AddAll(AstNodeList list)
		{
			list.Add(this);

			foreach (AstNode child in this.ChildNodes)
			{
				if (child != null)
					child.AddAll(list);
			}
		}

		#endregion Visitors, Iterators

		#region overrides: ToString

		public override string ToString()
		{
			return string.IsNullOrEmpty(Role) ? this.AsString : this.Role + ": " + this.AsString;
		}

		#endregion overrides: ToString

		#region Utility methods: AddChild, HandleError

		protected AstNode AddChild(string role, ParseTreeNode childParseNode)
		{
			return AddChild(NodeUseType.Unknown, role, childParseNode);
		}

		protected AstNode AddChild(NodeUseType useType, string role, ParseTreeNode childParseNode)
		{
			var child = (AstNode) childParseNode.AstNode;
			if (child == null)
				// Put a stub to throw an exception with clear message on attempt to evaluate.
				child = new NullNode(childParseNode.Term);

			child.Role = role;
			child.Parent = this;

			this.ChildNodes.Add(child);
			return child;
		}

		#endregion Utility methods: AddChild, HandleError
	}

	public class AstNodeList : List<AstNode> { }
}
