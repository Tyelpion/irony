using System.Reflection;

using Irony.Ast;
using Irony.Parsing;

namespace Irony.Interpreter.Ast
{
	/// <summary>
	/// For now we do not support dotted namespace/type references like System.Collections or System.Collections.List.
	/// Only references to objects like 'objFoo.Name' or 'objFoo.DoStuff()'
	/// </summary>
	public class MemberAccessNode : AstNode
	{
		private AstNode left;
		private string memberName;

		public override void DoSetValue(ScriptThread thread, object value)
		{
			// Standard prolog
			thread.CurrentNode = this;

			var leftValue = this.left.Evaluate(thread);
			if (leftValue == null)
				thread.ThrowScriptError("Target object is null.");

			var type = leftValue.GetType();
			var members = type.GetMember(this.memberName);
			if (members == null || members.Length == 0)
				thread.ThrowScriptError("Member {0} not found in object of type {1}.", this.memberName, type);

			var member = members[0];
			switch (member.MemberType)
			{
				case MemberTypes.Property:
					var propInfo = member as PropertyInfo;
					propInfo.SetValue(leftValue, value, null);
					break;

				case MemberTypes.Field:
					var fieldInfo = member as FieldInfo;
					fieldInfo.SetValue(leftValue, value);
					break;

				default:
					thread.ThrowScriptError("Cannot assign to member {0} of type {1}.", this.memberName, type);
					break;
			}

			// Standard epilog
			thread.CurrentNode = this.Parent;
		}

		public override void Init(AstContext context, ParseTreeNode treeNode)
		{
			base.Init(context, treeNode);
			var nodes = treeNode.GetMappedChildNodes();
			this.left = this.AddChild("Target", nodes[0]);
			var right = nodes[nodes.Count - 1];
			this.memberName = right.FindTokenAndGetText();
			this.ErrorAnchor = right.Span.Location;
			this.AsString = "." + this.memberName;
		}

		protected override object DoEvaluate(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			object result = null;
			var leftValue = this.left.Evaluate(thread);
			if (leftValue == null)
				thread.ThrowScriptError("Target object is null.");

			var type = leftValue.GetType();
			var members = type.GetMember(this.memberName);
			if (members == null || members.Length == 0)
				thread.ThrowScriptError("Member {0} not found in object of type {1}.", this.memberName, type);

			var member = members[0];
			switch (member.MemberType)
			{
				case MemberTypes.Property:
					var propInfo = member as PropertyInfo;
					result = propInfo.GetValue(leftValue, null);
					break;

				case MemberTypes.Field:
					var fieldInfo = member as FieldInfo;
					result = fieldInfo.GetValue(leftValue);
					break;

				case MemberTypes.Method:
					// This bindingInfo works as a call target
					result = new ClrMethodBindingTargetInfo(type, this.memberName, leftValue);
					break;

				default:
					thread.ThrowScriptError("Invalid member type ({0}) for member {1} of type {2}.", member.MemberType, this.memberName, type);
					result = null;
					break;
			}

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}
	}
}
