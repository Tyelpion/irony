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
using System.Windows.Forms;

namespace Irony.GrammarExplorer
{
	public partial class fmShowException : Form
	{
		public fmShowException()
		{
			this.InitializeComponent();
		}

		public static void ShowException(Exception ex)
		{
			var fm = new fmShowException();
			fm.txtException.Text = ex.ToString();
			fm.txtException.Select(0, 0);
			fm.Show();
		}
	}
}
