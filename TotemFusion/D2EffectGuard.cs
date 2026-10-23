using Object = UnityEngine.Object;
using System;
using System.Reflection;
using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;

namespace TotemFusion;

public static class D2EffectGuard
{
	private static bool _installed;

	private static bool _onLoadInstalled;

	public static bool TryInstall()
	{
		if (!_installed)
		{
			TryInstallEffectGuard();
		}
		return _installed;
	}

	private static bool TryInstallEffectGuard()
	{
		if (_installed)
		{
			return true;
		}
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
				return false;
			}
			MethodInfo method = type.GetMethod("TryDisableEffectComponents", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				return false;
			}
			Harmony harmony = new Harmony("TotemFusion.D2EffectGuard");
			MethodInfo method2 = typeof(D2EffectGuard).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			harmony.Patch(method, new HarmonyMethod(method2));
			_installed = true;
			Debug.Log((object)"[TotemFusion] 已装上保护：合成图腾的效果(Effect)不会被 Diablo2Totem 清除。");
			TryInstallOnLoadPatch();
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] D2 效果保护补丁安装失败: " + ex.Message));
			return false;
		}
	}

	public static bool Prefix(Item item)
	{
		try
		{
			if ((Object)(object)item != (Object)null && item.GetString("TotemFusion_Made", (string)null) == "1")
			{
				return false;
			}
		}
		catch
		{
		}
		return true;
	}

	private static void TryInstallOnLoadPatch()
	{
		if (_onLoadInstalled)
		{
			return;
		}
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
			if (!(type == null))
			{
				MethodInfo method = type.GetMethod("OnLoadItem", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[2]
				{
					typeof(string),
					typeof(Item)
				}, null);
				if (!(method == null))
				{
					Harmony harmony = new Harmony("TotemFusion.D2OnLoadGuard");
					MethodInfo method2 = typeof(D2OnLoadGuard).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					harmony.Patch(method, new HarmonyMethod(method2));
					_onLoadInstalled = true;
					Debug.Log((object)"[TotemFusion] 已装保护：拦截 D2T.OnLoadItem，阻止其为“已删词条”的图腾兜底成别的图腾。");
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] D2 OnLoad 诊断补丁安装失败: " + ex.Message));
		}
	}
}
