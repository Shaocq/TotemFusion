using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Duckov.Modding;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TotemFusion;

// 同名类型的基类必须写全名：命名空间内已声明了 ModBehaviour，
// 简写会被编译器解析成自身（CS0146 循环基类型），强转也会变成递归调用。
public class ModBehaviour : Duckov.Modding.ModBehaviour
{
	protected override void OnAfterSetup()
	{
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		base.OnAfterSetup();
		try
		{
			bool flag = false;
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (int i = 0; i < assemblies.Length; i++)
			{
				if (assemblies[i].GetName().Name == "0Harmony")
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				string text = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "0Harmony.dll");
				if (File.Exists(text))
				{
					Assembly.LoadFrom(text);
				}
			}
			else
			{
				Debug.Log((object)"[TotemFusion] 0Harmony 已由其它 mod 加载，复用之。");
			}
		}
		catch (Exception ex)
		{
			Debug.LogError((object)("[TotemFusion] 加载 0Harmony.dll 失败: " + ex));
		}
		Debug.Log((object)"[TotemFusion] Loaded. Patching InteractCrafter...");
		try
		{
			new Harmony("TotemFusion").PatchAll();
			Debug.Log((object)"[TotemFusion] Patched OK (制作台交互 -> ‘图腾合成’选项).");
		}
		catch (Exception ex2)
		{
			Debug.LogError((object)("[TotemFusion] Patch failed: " + ex2));
		}
		try
		{
			new GameObject("TotemFusionInstaller").AddComponent<TotemFusionInstaller>();
			Debug.Log((object)"[TotemFusion] 安装器已启动（将在制作台注入‘图腾合成’选项）。");
		}
		catch (Exception ex3)
		{
			Debug.LogError((object)("[TotemFusion] 创建安装器失败: " + ex3));
		}
		((MonoBehaviour)this).StartCoroutine(InstallD2EffectGuard());
		try
		{
			ModSettingBridge.SetModInfo(((Duckov.Modding.ModBehaviour)this).info);
			ModSettingBridge.TryBuild();
		}
		catch (Exception ex4)
		{
			Debug.LogWarning((object)("[TotemFusion] ModSetting 对接异常: " + ex4.Message));
		}
		SceneManager.sceneLoaded += OnSceneLoaded;
		((MonoBehaviour)this).Invoke("DelayedRepair", 1.5f);
		((MonoBehaviour)this).StartCoroutine(DelayedRepairCoroutine());
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		try
		{
			// 界面是 DontDestroyOnLoad 的，换场景时先收起，避免显示上一张图里的旧物品
			TotemFusionView.Close();
			TotemPurgeView.Close();
			((MonoBehaviour)this).StartCoroutine(DelayedRepairCoroutine());
		}
		catch
		{
		}
	}

	private IEnumerator DelayedRepairCoroutine()
	{
		yield return (object)new WaitForSeconds(1f);
		TryRepairAll();
		yield return (object)new WaitForSeconds(3f);
		TryRepairAll();
		yield return (object)new WaitForSeconds(5f);
		TryRepairAll();
		yield return (object)new WaitForSeconds(8f);
		TryRepairAll();
		yield return (object)new WaitForSeconds(13f);
		TryRepairAll();
	}

	private void DelayedRepair()
	{
		TryRepairAll();
	}

	private void TryRepairAll()
	{
		try
		{
			TotemFusionLogic.RepairFused();
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] 自动修复合成品失败: " + ex.Message));
		}
	}

	private IEnumerator InstallD2EffectGuard()
	{
		if (D2EffectGuard.TryInstall())
		{
			yield break;
		}
		yield return (object)new WaitForSeconds(1f);
		for (int i = 0; i < 8; i++)
		{
			if (D2EffectGuard.TryInstall())
			{
				yield break;
			}
			yield return (object)new WaitForSeconds(2f);
		}
		Debug.Log((object)"[TotemFusion] 未检测到 Diablo2Totem，跳过效果保护补丁（无 D2 时本就不需要）。");
	}

	protected override void OnBeforeDeactivate()
	{
		base.OnBeforeDeactivate();
		try
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
		}
		catch
		{
		}
		TotemFusionView.Deactivate();
		TotemPurgeView.Deactivate();
	}
}
