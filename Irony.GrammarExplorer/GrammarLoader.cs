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
// This file and all functionality of dynamic assembly reloading was contributed by Alexey Yakovlev (yallie)

#endregion License

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Irony.Parsing;

namespace Irony.GrammarExplorer
{
	/// <summary>
	/// Maintains grammar assemblies, reloads updated files automatically.
	/// </summary>
	internal class GrammarLoader
	{
		private static bool _enableBrowsingForAssemblyResolution = false;
		private static HashSet<Assembly> _loadedAssemblies = new HashSet<Assembly>();
		private static Dictionary<string, Assembly> _loadedAssembliesByNames = new Dictionary<string, Assembly>();
		private static HashSet<string> _probingPaths = new HashSet<string>();

		private TimeSpan autoRefreshDelay = TimeSpan.FromMilliseconds(1000);
		private Dictionary<string, CachedAssembly> cachedGrammarAssemblies = new Dictionary<string, CachedAssembly>();

		static GrammarLoader()
		{
			AppDomain.CurrentDomain.AssemblyLoad += (sender, args) => _loadedAssembliesByNames[args.LoadedAssembly.FullName] = args.LoadedAssembly;
			AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => FindAssembly(args.Name);
		}

		public event EventHandler AssemblyUpdated;

		public GrammarItem SelectedGrammar { get; set; }

		private Assembly SelectedGrammarAssembly
		{
			get
			{
				if (this.SelectedGrammar == null)
					return null;

				// Create assembly cache entry as needed
				var location = this.SelectedGrammar.Location;
				if (!this.cachedGrammarAssemblies.ContainsKey(location))
				{
					var fileInfo = new FileInfo(location);
					this.cachedGrammarAssemblies[location] =
					  new CachedAssembly
					  {
						  LastWriteTime = fileInfo.LastWriteTime,
						  FileSize = fileInfo.Length,
						  Assembly = null
					  };

					// Set up file system watcher
					this.cachedGrammarAssemblies[location].Watcher = this.CreateFileWatcher(location);
				}

				// get loaded assembly from cache if possible
				var assembly = this.cachedGrammarAssemblies[location].Assembly;
				if (assembly == null)
				{
					assembly = LoadAssembly(location);
					this.cachedGrammarAssemblies[location].Assembly = assembly;
				}

				return assembly;
			}
		}

		public static Assembly LoadAssembly(string fileName)
		{
			// Normalize the filename
			fileName = new FileInfo(fileName).FullName;

			// Save assembly path for dependent assemblies probing
			var path = Path.GetDirectoryName(fileName);
			_probingPaths.Add(path);

			// Try to load assembly using the standard policy
			var assembly = Assembly.LoadFrom(fileName);

			// If the standard policy returned the old version, force reload
			if (_loadedAssemblies.Contains(assembly))
			{
				assembly = Assembly.Load(File.ReadAllBytes(fileName));
			}

			// Cache the loaded assembly by its location
			_loadedAssemblies.Add(assembly);

			return assembly;
		}

		public Grammar CreateGrammar()
		{
			if (this.SelectedGrammar == null)
				return null;

			// Resolve dependencies while loading and creating grammars
			_enableBrowsingForAssemblyResolution = true;

			try
			{
				var type = this.SelectedGrammarAssembly.GetType(this.SelectedGrammar.TypeName, true, true);
				return Activator.CreateInstance(type) as Grammar;
			}
			finally
			{
				_enableBrowsingForAssemblyResolution = false;
			}
		}

		private static string BrowseFor(string assemblyName)
		{
			var fileDialog = new OpenFileDialog
			{
				Title = "Please locate assembly: " + assemblyName,
				Filter = "Assemblies (*.dll)|*.dll|All files (*.*)|*.*"
			};

			using (fileDialog)
			{
				if (fileDialog.ShowDialog() == DialogResult.OK)
					return fileDialog.FileName;
			}

			return null;
		}

		private static Assembly FindAssembly(string assemblyName)
		{
			if (_loadedAssembliesByNames.ContainsKey(assemblyName))
				return _loadedAssembliesByNames[assemblyName];

			// Ignore resource assemblies
			if (assemblyName.ToLower().Contains(".resources, version="))
				return _loadedAssembliesByNames[assemblyName] = null;

			// Use probing paths to look for dependency assemblies
			var fileName = assemblyName.Split(',').First() + ".dll";

			foreach (var path in _probingPaths)
			{
				var fullName = Path.Combine(path, fileName);
				if (File.Exists(fullName))
				{
					try
					{
						return LoadAssembly(fullName);
					}
					catch
					{
						// The file seems to be bad, let's try to find another one
					}
				}
			}

			// The last chance: try asking user to locate the assembly
			if (_enableBrowsingForAssemblyResolution)
			{
				fileName = BrowseFor(assemblyName);
				if (!string.IsNullOrWhiteSpace(fileName))
					return LoadAssembly(fileName);
			}

			// Assembly not found, don't search for it again
			return _loadedAssembliesByNames[assemblyName] = null;
		}

		private FileSystemWatcher CreateFileWatcher(string location)
		{
			var folder = Path.GetDirectoryName(location);
			var watcher = new FileSystemWatcher(folder);
			watcher.Filter = Path.GetFileName(location);

			watcher.Changed += (s, args) =>
			{
				if (args.ChangeType != WatcherChangeTypes.Changed)
					return;

				lock (this)
				{
					// Check if assembly file was changed indeed since the last event
					var cacheEntry = this.cachedGrammarAssemblies[location];
					var fileInfo = new FileInfo(location);
					if (cacheEntry.LastWriteTime == fileInfo.LastWriteTime && cacheEntry.FileSize == fileInfo.Length)
						return;

					// Reset cached assembly and save last file update time
					cacheEntry.LastWriteTime = fileInfo.LastWriteTime;
					cacheEntry.FileSize = fileInfo.Length;
					cacheEntry.Assembly = null;

					// Check if file update is already scheduled (work around multiple FileSystemWatcher event firing)
					if (!cacheEntry.UpdateScheduled)
					{
						cacheEntry.UpdateScheduled = true;

						// Delay auto-refresh to make sure the file is closed by the writer
						ThreadPool.QueueUserWorkItem(_ =>
						{
							Thread.Sleep(this.autoRefreshDelay);
							cacheEntry.UpdateScheduled = false;
							this.OnAssemblyUpdated(location);
						});
					}
				}
			};

			watcher.EnableRaisingEvents = true;
			return watcher;
		}

		private void OnAssemblyUpdated(string location)
		{
			if (this.AssemblyUpdated == null || this.SelectedGrammar == null || this.SelectedGrammar.Location != location)
				return;

			this.AssemblyUpdated(this, EventArgs.Empty);
		}

		private class CachedAssembly
		{
			public Assembly Assembly;
			public long FileSize;
			public DateTime LastWriteTime;
			public bool UpdateScheduled;
			public FileSystemWatcher Watcher;
		}
	}
}
