using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using LibEveryFileExplorer.Files;
using LibEveryFileExplorer.Compression;
using LibEveryFileExplorer;

namespace EveryFileExplorer.Plugins
{
	public class Plugin
	{
		public EFEPlugin Initializer;

		public String Name;
		public String Description;
		public String Version;
		public Type[] CompressionTypes;
		public Type[] FileFormatTypes;
		public Type[] ProjectTypes;

		public Plugin(Assembly Assembly)
		{
			Name = Assembly.GetCustomAttributes(typeof(AssemblyTitleAttribute), false).OfType<AssemblyTitleAttribute>().FirstOrDefault().Title;
			Description = Assembly.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false).OfType<AssemblyDescriptionAttribute>().FirstOrDefault().Description;
			try
			{
				Version = Assembly.GetCustomAttributes(typeof(AssemblyVersionAttribute), false).OfType<AssemblyVersionAttribute>().FirstOrDefault().Version;
			}
			catch
			{
				Version = null;
			}

			Type[] tt = Assembly.GetExportedTypes();
			List<Type> fe = new List<Type>();
			List<Type> ce = new List<Type>();
			List<Type> pe = new List<Type>();

			foreach (var t in tt)
			{
				if (!t.IsClass) continue;
				if (t.IsSubclassOf(typeof(EFEPlugin)))
				{
					Initializer = (EFEPlugin)t.InvokeMember(null, BindingFlags.CreateInstance, null, null, null);
					continue;
				}

				var interfaces = t.GetInterfaces();
				bool isFileFormat = interfaces.Any(i => i.Name == "FileFormatBase");
				bool isCompression = interfaces.Any(i => i.Name == "CompressionFormatBase");
				bool isProject = interfaces.Any(i => i.Name == "ProjectBase");

				if (!isFileFormat && !isCompression && !isProject) continue;

				// Form1 and FileManager expect every registered plugin type to expose
				// a static Identifier member. Do the validation here so a malformed or
				// dependency type can never reach a dynamic Identifier lookup and crash
				// the application during startup.
				object identifier;
				if (!StaticDynamic.TryGetStaticMember(t, "Identifier", out identifier) || identifier == null)
				{
					continue;
				}

				if (isFileFormat) fe.Add(t);
				else if (isCompression) ce.Add(t);
				else if (isProject) pe.Add(t);
			}

			FileFormatTypes = fe.ToArray();
			CompressionTypes = ce.ToArray();
			ProjectTypes = pe.ToArray();
		}

		public static bool IsPlugin(Assembly Assembly)
		{
			Type[] tt = Assembly.GetExportedTypes();
			foreach (var t in tt)
			{
				if (!t.IsClass) continue;

				var interfaces = t.GetInterfaces();
				bool isPluginType = interfaces.Any(i => i.Name == "FileFormatBase" || i.Name == "CompressionFormatBase" || i.Name == "ProjectBase");
				if (!isPluginType) continue;

				object identifier;
				if (StaticDynamic.TryGetStaticMember(t, "Identifier", out identifier) && identifier != null)
				{
					return true;
				}
			}
			return false;
		}
	}
}
