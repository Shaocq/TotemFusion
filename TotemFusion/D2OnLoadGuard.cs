using Object = UnityEngine.Object;
using System;
using System.Reflection;
using ItemStatsSystem;
using UnityEngine;

namespace TotemFusion;

public static class D2OnLoadGuard
{
	public static bool Prefix(string __0, Item __1)
	{
		try
		{
			if ((Object)(object)__1 == (Object)null)
			{
				return true;
			}
			string value = null;
			try
			{
				value = __1.GetString("TotemFusion_Van", (string)null);
			}
			catch
			{
			}
			string text = "";
			try
			{
				text = __1.GetString("D2T_Meta", (string)null) ?? "";
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(text) && text.StartsWith("[D2T]"))
			{
				TotemFusionLogic.SanitizeD2Meta(__1);
				ClearByInstanceCache(__1);
				string text2 = "";
				try
				{
					text2 = __1.DisplayNameRaw ?? "";
				}
				catch
				{
				}
				Debug.Log((object)("[TFix] OnLoadGuard(Prefix): 接管 D2T.OnLoadItem 名='" + text2 + "' meta前长=" + text.Length));
			}
		}
		catch
		{
		}
		return true;
	}

	private static void ClearByInstanceCache(Item item)
	{
		try
		{
			Type type = null;
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies)
			{
				try
				{
					type = assembly.GetType("Diablo2Totem.Services.ItemPersistenceService", throwOnError: false);
				}
				catch
				{
				}
				if (type != null)
				{
					break;
				}
			}
			if (type == null)
			{
				return;
			}
			int instanceID = ((Object)item).GetInstanceID();
			string[] array = new string[4] { "_byInstance", "_appliedGood", "_noMetaWarned", "_pendingRetry" };
			foreach (string name in array)
			{
				try
				{
					object obj2 = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
					(obj2?.GetType().GetMethod("Remove", new Type[1] { typeof(int) }))?.Invoke(obj2, new object[1] { instanceID });
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}
}
