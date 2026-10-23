using System;
using UnityEngine;

namespace TotemFusion;

public static class TotemFusionSettings
{
	public const bool MergeSame = true;

	public static bool ExternalPanelReady;

	public static event Action Changed;

	public static void Notify()
	{
		try
		{
			Changed?.Invoke();
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] 设置变更回调异常: " + ex.Message));
		}
	}
}
