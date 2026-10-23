using Object = UnityEngine.Object;
using System;
using System.Reflection;
using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;

namespace TotemFusion;

[HarmonyPatch]
public static class CharacterMainControl_SetItem_Patch
{
	private static MethodBase TargetMethod()
	{
		try
		{
			Type type = AccessTools.TypeByName("CharacterMainControl");
			if (type == null)
			{
				return null;
			}
			return AccessTools.Method(type, "SetItem", new Type[1] { typeof(Item) });
		}
		catch
		{
			return null;
		}
	}

	private static void Postfix(object __instance, Item __0)
	{
		try
		{
			if ((Object)(object)__0 != (Object)null)
			{
				bool flag = false;
				try
				{
					flag = __0.GetString("TotemFusion_Made", (string)null) == "1";
				}
				catch
				{
				}
				bool flag2 = false;
				try
				{
					flag2 = !string.IsNullOrEmpty(__0.GetString("TotemFusion_RemovedFX", (string)null));
				}
				catch
				{
				}
				if (flag | flag2)
				{
					Debug.Log((object)("[TFix][钩子] SetItem→RepairFusedItem made=" + flag + " 有已删FX=" + flag2));
				}
				TotemFusionLogic.RepairFusedItem(__0);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TFix][钩子] SetItem RepairFusedItem 异常: " + ex));
		}
	}
}
