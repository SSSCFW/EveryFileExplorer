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

		// Handle static properties and fields
		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			try
            {
				PropertyInfo prop = _type.GetProperty(binder.Name, BindingFlags.FlattenHierarchy | BindingFlags.Static | BindingFlags.Public);
				if (prop != null)
				{
					result = prop.GetValue(null, null);
					return true;
				}

				FieldInfo field = _type.GetField(binder.Name, BindingFlags.FlattenHierarchy | BindingFlags.Static | BindingFlags.Public);
				if (field != null)
				{
					result = field.GetValue(null);
					return true;
				}

				result = null;
				return false;
			} catch (Exception e)
            {
				result = null;
				return false;
			}
			
		}

		// Handle static methods
		public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
		{
			MethodInfo method = _type.GetMethod(binder.Name, BindingFlags.FlattenHierarchy | BindingFlags.Static | BindingFlags.Public);
			if (method == null)
			{
				result = null;
				return false;
			}

			result = method.Invoke(null, args);
			return true;
		}
	}

}
