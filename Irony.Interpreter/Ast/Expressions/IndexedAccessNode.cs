using System;
using System.Collections;
using System.Linq;
using System.Reflection;

using Irony.Ast;
using Irony.Parsing;

namespace Irony.Interpreter.Ast
{
	public class IndexedAccessNode : AstNode
	{
		private AstNode target, index;

		public override void DoSetValue(ScriptThread thread, object value)
		{
			// Standard prolog
			thread.CurrentNode = this;

			var targetValue = this.target.Evaluate(thread);
			if (targetValue == null)
				thread.ThrowScriptError("Target object is null.");

			var type = targetValue.GetType();
			var indexValue = this.index.Evaluate(thread);

			// String and array are special cases
			if (type == typeof(string))
			{
				thread.ThrowScriptError("String is read-only.");
			}
			else if (type.IsArray)
			{
				var arr = targetValue as Array;
				var iIndex = Convert.ToInt32(indexValue);
				arr.SetValue(value, iIndex);
			}
			else if (targetValue is IDictionary)
			{
				var dict = (IDictionary) targetValue;
				dict[indexValue] = value;
			}
			else
			{
				const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.InvokeMethod;
				type.InvokeMember("set_Item", flags, null, targetValue, new object[] { indexValue, value });
			}

			// Standard epilog
			thread.CurrentNode = this.Parent;
		}

		public override void Init(AstContext context, ParseTreeNode treeNode)
		{
			base.Init(context, treeNode);
			var nodes = treeNode.GetMappedChildNodes();
			this.target = this.AddChild("Target", nodes.First());
			this.index = this.AddChild("Index", nodes.Last());
			this.AsString = "[" + this.index + "]";
		}

		protected override object DoEvaluate(ScriptThread thread)
		{
			// Standard prolog
			thread.CurrentNode = this;

			object result = null;
			var targetValue = this.target.Evaluate(thread);
			if (targetValue == null)
				thread.ThrowScriptError("Target object is null.");

			var type = targetValue.GetType();
			var indexValue = this.index.Evaluate(thread);

			// String and array are special cases
			if (type == typeof(string))
			{
				var sTarget = targetValue as string;
				var iIndex = Convert.ToInt32(indexValue);
				result = sTarget[iIndex];
			}
			else if (type.IsArray)
			{
				var arr = targetValue as Array;
				var iIndex = Convert.ToInt32(indexValue);
				result = arr.GetValue(iIndex);
			}
			else if (targetValue is IDictionary)
			{
				var dict = (IDictionary) targetValue;
				result = dict[indexValue];
			}
			else
			{
				const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.InvokeMethod;
				result = type.InvokeMember("get_Item", flags, null, targetValue, new object[] { indexValue });
			}

			// Standard epilog
			thread.CurrentNode = this.Parent;
			return result;
		}
	}
}
