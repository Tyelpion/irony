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
using System.Windows.Forms;
using System.Xml;
using Irony.Parsing;

namespace Irony.GrammarExplorer
{
	/// <summary>
	/// Helper classes for supporting showing grammar list in top combo, saving list on exit and loading on start
	/// </summary>
	public class GrammarItem
	{
		public readonly string Caption;

		/// <summary>
		/// Location of assembly containing the grammar
		/// </summary>
		public readonly string Location;

		public readonly string LongCaption;

		/// <summary>
		/// Full type name
		/// </summary>
		public readonly string TypeName;

		internal bool loading;

		public GrammarItem(string caption, string location, string typeName)
		{
			this.Caption = caption;
			this.Location = location;
			this.TypeName = typeName;
		}

		public GrammarItem(Type grammarClass, string assemblyLocation)
		{
			this.loading = true;
			this.Location = assemblyLocation;
			this.TypeName = grammarClass.FullName;

			// Get language name from Language attribute
			this.Caption = grammarClass.Name;
			this.LongCaption = Caption;

			var langAttr = LanguageAttribute.GetValue(grammarClass);
			if (langAttr != null)
			{
				this.Caption = langAttr.LanguageName;

				if (!string.IsNullOrEmpty(langAttr.Version))
					this.Caption += ", version " + langAttr.Version;

				this.LongCaption = this.Caption;
				if (!string.IsNullOrEmpty(langAttr.Description))
					this.LongCaption += ": " + langAttr.Description;
			}
		}

		public GrammarItem(XmlElement element)
		{
			this.Caption = element.GetAttribute("Caption");
			this.Location = element.GetAttribute("Location");
			this.TypeName = element.GetAttribute("TypeName");
		}

		public void Save(XmlElement toElement)
		{
			toElement.SetAttribute("Caption", this.Caption);
			toElement.SetAttribute("Location", this.Location);
			toElement.SetAttribute("TypeName", this.TypeName);
		}

		public override string ToString()
		{
			return this.loading ? this.LongCaption : this.Caption;
		}
	}

	public class GrammarItemList : List<GrammarItem>
	{
		public static GrammarItemList FromCombo(ComboBox combo)
		{
			var list = new GrammarItemList();
			foreach (GrammarItem item in combo.Items)
			{
				list.Add(item);
			}

			return list;
		}

		public static GrammarItemList FromXml(string xml)
		{
			var list = new GrammarItemList();
			var xdoc = new XmlDocument();
			xdoc.LoadXml(xml);

			var xlist = xdoc.SelectNodes("//Grammar");
			foreach (XmlElement xitem in xlist)
			{
				var item = new GrammarItem(xitem);
				list.Add(item);
			}

			return list;
		}

		public void ShowIn(ComboBox combo)
		{
			combo.Items.Clear();
			foreach (GrammarItem item in this)
			{
				combo.Items.Add(item);
			}
		}

		public string ToXml()
		{
			var xdoc = new XmlDocument();
			var xlist = xdoc.CreateElement("Grammars");
			xdoc.AppendChild(xlist);

			foreach (GrammarItem item in this)
			{
				var xitem = xdoc.CreateElement("Grammar");
				xlist.AppendChild(xitem);
				item.Save(xitem);
			}

			return xdoc.OuterXml;
		}
	}
}
