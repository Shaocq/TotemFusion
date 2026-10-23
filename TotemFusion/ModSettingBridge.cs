using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TotemFusion;

public static class ModSettingBridge
{
	private const string BuilderTypeName = "ModSetting.Api.SettingsBuilder";

	private static Type _builderType;

	private static object _builder;

	private static object _modInfo;

	private static int _attempts;

	private static bool _confirmedAbsent;

	private const int MaxAttempts = 3;

	public static void SetModInfo(object modInfo)
	{
		_modInfo = modInfo;
	}

	public static bool TryBuild()
	{
		if (_builder != null)
		{
			return true;
		}
		if (_confirmedAbsent || _modInfo == null)
		{
			return false;
		}
		if (_attempts >= 3)
		{
			return false;
		}
		_attempts++;
		try
		{
			_builderType = FindType("ModSetting.Api.SettingsBuilder");
			if (_builderType == null)
			{
				_confirmedAbsent = true;
				Debug.Log((object)"[TotemFusion] 未检测到 ModSetting 模组，设置继续使用合成面板内的开关。");
				return false;
			}
			MethodInfo method = _builderType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
			if (method == null)
			{
				Debug.LogWarning((object)"[TotemFusion] ModSetting API 不匹配（缺 Create）。");
				return false;
			}
			_builder = method.Invoke(null, new object[1] { _modInfo });
			if (_builder == null)
			{
				return false;
			}
			TotemFusionSettings.ExternalPanelReady = true;
			TotemFusionSettings.Notify();
			Debug.Log((object)"[TotemFusion] 已接入 ModSetting 设置面板（设置 → Mod 标签页）。");
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] 接入 ModSetting 失败，降级为面板内开关: " + ex.Message));
			return false;
		}
	}

	private static void AddToggle(string key, string description, bool defaultValue, Action<bool> onValueChange)
	{
		MethodInfo? method = _builderType.GetMethod("AddToggle", BindingFlags.Instance | BindingFlags.Public);
		if (method == null)
		{
			throw new MissingMethodException("ModSetting 缺少 AddToggle");
		}
		method.Invoke(_builder, new object[4] { key, description, defaultValue, onValueChange });
	}

	private static bool TryGetSavedBool(string key, out bool value)
	{
		value = false;
		try
		{
			if (_builder == null)
			{
				return false;
			}
			MethodInfo methodInfo = _builderType.GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault((MethodInfo x) => x.Name == "GetSavedValue" && x.IsGenericMethodDefinition);
			if (methodInfo == null)
			{
				return false;
			}
			MethodInfo methodInfo2 = methodInfo.MakeGenericMethod(typeof(bool));
			object[] array = new object[2] { key, false };
			bool result = (bool)methodInfo2.Invoke(_builder, array);
			value = (bool)array[1];
			return result;
		}
		catch
		{
			return false;
		}
	}

	private static Type FindType(string fullName)
	{
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly in assemblies)
		{
			Type type = null;
			try
			{
				type = assembly.GetType(fullName, throwOnError: false);
			}
			catch
			{
			}
			if (type != null)
			{
				return type;
			}
		}
		return null;
	}
}
