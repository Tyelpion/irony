namespace Irony.Interpreter.Ast
{
	public class Closure : ICallTarget
	{
		public LambdaNode Lamda;

		/// <summary>
		/// The scope that created closure; is used to find Parents (enclosing scopes)
		/// </summary>
		public Scope ParentScope;

		public Closure(Scope parentScope, LambdaNode targetNode)
		{
			this.ParentScope = parentScope;
			this.Lamda = targetNode;
		}

		public object Call(ScriptThread thread, object[] parameters)
		{
			return this.Lamda.Call(this.ParentScope, thread, parameters);
		}

		public override string ToString()
		{
			// Returns nice string like "<function add>"
			return this.Lamda.ToString();
		}
	}
}
