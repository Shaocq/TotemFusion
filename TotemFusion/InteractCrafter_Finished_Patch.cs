using Object = UnityEngine.Object;
using System;
using HarmonyLib;
using UnityEngine;

namespace TotemFusion;

[HarmonyPatch(typeof(InteractCrafter), "OnInteractFinished")]
public static class InteractCrafter_Finished_Patch
{
	[HarmonyPrefix]
	public static bool Prefix(InteractCrafter __instance)
	{
		try
		{
			if ((Object)(object)__instance != (Object)null && __instance.requireTag == "TotemFusionMod")
			{
				TotemFusionView.Open();
				return false;
			}
			if ((Object)(object)__instance != (Object)null && __instance.requireTag == "TotemFusionPurge")
			{
				TotemPurgeView.Open();
				return false;
			}
		}
		catch (Exception ex)
		{
			Debug.LogError((object)("[TotemFusion] 打开合成界面失败: " + ex));
		}
		return true;
	}
}
