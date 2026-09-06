using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;
using System.Reflection;

namespace LibEveryFileExplorer
{
	public class StaticDynamic : DynamicObject
	{
		private Type _type;
		public StaticDynamic(Type type) { _type = type; }

		public static bool TryGetStaticMember(Type type, string name, out object result)
		{
			result = null;
			if (type == null || String.IsNullOrEmpty(name)) return false;

			// Do not rely on BindingFlags.FlattenHierarchy here. Some of the legacy
			// plugin types inherit Identifier from a constructed generic base class,
			// and resolving the member explicitly on each base type is more reliable.
			for (Type current = type; current != null; current = current.BaseType)
			{
				try
				{
					PropertyInfo prop = current.GetProperty(
						name,
						BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					if (prop != null && prop.GetIndexParameters().Length == 0)
					{
						result = prop.GetValue(null, null);
						return true;
					}

					FieldInfo field = current.GetField(
						name,
						BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					if (field != null)
					{
						result = field.GetValue(null);
						return true;
					}
				}
				catch
				{
					// Continue walking the hierarchy. A malformed plugin member should not
					// prevent a valid Identifier on a base class from being found.
				}
			}

			return false;
		}

		// Handle static properties/fields.
		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			return TryGetStaticMember(_type, binder.Name, out result);
		}

		// Handle static methods.
		public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
		{
			for (Type current = _type; current != null; current = current.BaseType)
			{
				MethodInfo method = current.GetMethod(
					binder.Name,
					BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (method == null) continue;

				result = method.Invoke(null, args);
				return true;
			}

			result = null;
			return false;
		}
	}
}
