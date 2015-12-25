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

namespace Irony.Parsing
{
	[AttributeUsage(AttributeTargets.Class)]
	public class LanguageAttribute : Attribute
	{
		private string description;

		private string languageName;

		private string version;

		public LanguageAttribute() : this(null)
		{ }

		public LanguageAttribute(string languageName) : this(languageName, "1.0", string.Empty)
		{ }

		public LanguageAttribute(string languageName, string version, string description)
		{
			this.languageName = languageName;
			this.version = version;
			this.description = description;
		}

		public string Description
		{
			get { return this.description; }
		}

		public string LanguageName
		{
			get { return this.languageName; }
		}

		public string Version
		{
			get { return this.version; }
		}

		public static LanguageAttribute GetValue(Type grammarClass)
		{
			object[] attrs = grammarClass.GetCustomAttributes(typeof(LanguageAttribute), true);

			if (attrs != null && attrs.Length > 0)
			{
				var la = attrs[0] as LanguageAttribute;
				return la;
			}

			return null;
		}
	}
}
