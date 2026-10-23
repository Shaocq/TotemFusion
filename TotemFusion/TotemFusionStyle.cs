using TMPro;
using UnityEngine;

namespace TotemFusion;

/// <summary>
/// 原版字体缓存：界面文字统一使用游戏正在使用的 TMP 字体，避免中文缺字。
///
/// 面板底图、分隔线、物品格子、按钮等外观全部在运行时引用原版资源或自绘，
/// 不再内嵌任何图片文件。
/// </summary>
internal static class TotemFusionStyle
{
	private static TMP_FontAsset _font;

	public static TMP_FontAsset GameFont
	{
		get
		{
			if (_font != null)
				return _font;

			try
			{
				TextMeshProUGUI text = Object.FindObjectOfType<TextMeshProUGUI>();
				if (text != null && text.font != null)
				{
					_font = text.font;
					return _font;
				}
			}
			catch
			{
				// 忽略
			}

			try
			{
				if (TMP_Settings.defaultFontAsset != null)
					_font = TMP_Settings.defaultFontAsset;
			}
			catch
			{
				// 忽略
			}

			return _font;
		}
	}
}
