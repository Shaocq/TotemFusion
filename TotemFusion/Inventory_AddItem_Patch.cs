using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;

namespace TotemFusion;

[HarmonyPatch]
public static class Inventory_AddItem_Patch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		List<MethodBase> list = new List<MethodBase>();
		try
		{
			Type type = AccessTools.TypeByName("ItemStatsSystem.Inventory") ?? AccessTools.TypeByName("Inventory");
			if (type == null)
			{
				return list;
			}
			foreach (MethodInfo declaredMethod in AccessTools.GetDeclaredMethods(type))
			{
				if (declaredMethod.IsPublic && !declaredMethod.IsStatic && (declaredMethod.Name == "AddItem" || declaredMethod.Name == "AddAndMerge"))
				{
					ParameterInfo[] parameters = declaredMethod.GetParameters();
					if (parameters.Length >= 1 && typeof(Item).IsAssignableFrom(parameters[0].ParameterType))
					{
						list.Add(declaredMethod);
					}
				}
			}
		}
		catch
		{
		}
		return list;
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
					Debug.Log((object)("[TFix][钩子] AddItem→RepairFusedItem made=" + flag + " 有已删FX=" + flag2));
				}
				TotemFusionLogic.RepairFusedItem(__0);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TFix][钩子] AddItem RepairFusedItem 异常: " + ex));
		}
	}
}
