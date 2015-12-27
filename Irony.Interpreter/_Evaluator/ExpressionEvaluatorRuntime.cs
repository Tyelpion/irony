using System;
using System.Linq;
using Irony.Parsing;

namespace Irony.Interpreter.Evaluator
{
	public class ExpressionEvaluatorRuntime : LanguageRuntime
	{
		public ExpressionEvaluatorRuntime(LanguageData language) : base(language)
		{ }

		public override void Init()
		{
			base.Init();

			// Add built-in methods, special form IIF, import Math and Environment methods
			this.BuiltIns.AddMethod(BuiltInPrintMethod, "print");
			this.BuiltIns.AddMethod(BuiltInFormatMethod, "format");
			this.BuiltIns.AddSpecialForm(SpecialFormsLibrary.Iif, "iif", 3, 3);
			this.BuiltIns.ImportStaticMembers(typeof(System.Math));
			this.BuiltIns.ImportStaticMembers(typeof(Environment));
		}

		private object BuiltInFormatMethod(ScriptThread thread, object[] args)
		{
			if (args == null || args.Length == 0)
				return null;

			var template = args[0] as string;
			if (template == null)
				this.ThrowScriptError("Format template must be a string.");

			if (args.Length == 1)
				return template;

			// Create formatting args array
			var formatArgs = args.Skip(1).ToArray();
			var text = string.Format(template, formatArgs);

			return text;
		}

		private object BuiltInPrintMethod(ScriptThread thread, object[] args)
		{
			var text = string.Empty;

			switch (args.Length)
			{
				case 1:
					// Compact and safe conversion ToString()
					text = string.Empty + args[0];
					break;

				case 0:
					break;

				default:
					text = string.Join(" ", args);
					break;
			}

			thread.App.WriteLine(text);

			return null;
		}
	}
}
