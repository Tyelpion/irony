using Irony.Ast;
using Irony.Parsing;

namespace Irony.Interpreter.Ast
{
	/// <summary>
	/// Extension of AstContext
	/// </summary>
	public class InterpreterAstContext : AstContext
	{
		public readonly OperatorHandler OperatorHandler;

		public InterpreterAstContext(LanguageData language, OperatorHandler operatorHandler = null) : base(language)
		{
			this.OperatorHandler = operatorHandler ?? new OperatorHandler(language.Grammar.CaseSensitive);
			this.DefaultIdentifierNodeType = typeof(IdentifierNode);
			this.DefaultLiteralNodeType = typeof(LiteralValueNode);
			this.DefaultNodeType = null;
		}
	}
}
