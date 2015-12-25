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
using System.Diagnostics;
using System.Windows.Forms;

namespace Irony.GrammarExplorer
{
	internal class Program : MarshalByRefObject
	{
		private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
		{
			fmShowException.ShowException(e.Exception);
			Debug.Write("Exception!: ############################################## \n" + e.Exception.ToString());
		}

		private static Program CreateInstanceInSeparateDomain()
		{
			var setup = new AppDomainSetup
			{
				ShadowCopyFiles = true.ToString()
			};

			var domain = AppDomain.CreateDomain("HostedDomain", null, setup);
			return (Program) domain.CreateInstanceAndUnwrap(typeof(Program).Assembly.FullName, typeof(Program).FullName);
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Exception ex = e.ExceptionObject as Exception;
			string message = (ex == null ? e.ExceptionObject.ToString() : ex.Message);
			if (ex == null)
			{
				Debug.Write("Exception!: ############################################## \n" + e.ExceptionObject.ToString());
				MessageBox.Show(message, "Exception");
			}
			else
			{
				fmShowException.ShowException(ex);
			}
		}

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		private static void Main()
		{
			var program = CreateInstanceInSeparateDomain();
			program.RunApplication();
		}

		private void RunApplication()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			Application.Run(new fmGrammarExplorer());
		}
	}
}
