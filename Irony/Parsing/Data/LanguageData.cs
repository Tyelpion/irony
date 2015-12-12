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

using Irony.Parsing.Construction;

namespace Irony.Parsing
{
	public partial class LanguageData
	{
		public readonly GrammarErrorList Errors = new GrammarErrorList();

		public readonly Grammar Grammar;

		public readonly GrammarData GrammarData;

		public readonly ParserData ParserData;

		public readonly ScannerData ScannerData;

		public bool AstDataVerified;

		public long ConstructionTime;

		public GrammarErrorLevel ErrorLevel = GrammarErrorLevel.NoError;

		public LanguageData(Grammar grammar)
		{
			this.Grammar = grammar;
			this.GrammarData = new GrammarData(this);
			this.ParserData = new ParserData(this);
			this.ScannerData = new ScannerData(this);
			this.ConstructAll();
		}

		public bool CanParse()
		{
			return this.ErrorLevel < GrammarErrorLevel.Error;
		}

		public void ConstructAll()
		{
			var builder = new LanguageDataBuilder(this);
			builder.Build();
		}
	}
}
