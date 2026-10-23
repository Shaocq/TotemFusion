using System;
using System.Collections.Generic;
using UnityEngine;

namespace TotemFusion;

/// <summary>
/// 异常兜底：本 mod 的异常不允许抛回游戏或其他 mod 的调用栈。
/// 同类日志 30 秒内只写一次，避免高频回调刷屏。
/// </summary>
internal static class SafeGuard
{
	private const float LogCooldown = 30f;

	/// <summary>限流表的条目上限：异常信息里可能带变化的文本，防止无限膨胀。</summary>
	private const int MaxEntries = 256;

	private static readonly object SyncRoot = new object();

	private static readonly Dictionary<string, float> LastLogTime =
		new Dictionary<string, float>();

	public static void Report(string context, Exception exception)
	{
		try
		{
			string message = $"{context}：{exception}";
			if (!TryEnterCooldown(message))
			{
				return;
			}

			Debug.LogError($"[TotemFusion] {message}");
			Debug.LogException(exception);
		}
		catch
		{
			// 日志本身失败就只能放弃
		}
	}

	public static void Report(string message)
	{
		try
		{
			if (!TryEnterCooldown(message))
			{
				return;
			}

			Debug.LogError($"[TotemFusion] {message}");
		}
		catch
		{
			// 连日志都写不了就没办法了
		}
	}

	private static bool TryEnterCooldown(string message)
	{
		float now = Time.realtimeSinceStartup;

		lock (SyncRoot)
		{
			if (LastLogTime.TryGetValue(message, out float last) && now - last < LogCooldown)
			{
				return false;
			}

			if (LastLogTime.Count >= MaxEntries)
			{
				LastLogTime.Clear();
			}

			LastLogTime[message] = now;
			return true;
		}
	}
}
