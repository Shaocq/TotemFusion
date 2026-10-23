using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TotemFusion;

/// <summary>
/// 格子上的透明遮罩：点击清空对应槽位。
/// 拖动仍然由 IItemDragSource 的全局事件处理，这里只负责点击。
/// </summary>
internal sealed class TotemSlotClick : MonoBehaviour, IPointerClickHandler
{
	public Action OnClick;

	public void OnPointerClick(PointerEventData eventData)
	{
		try
		{
			if (eventData == null ||
				eventData.button != PointerEventData.InputButton.Left ||
				eventData.dragging)
			{
				return;
			}

			OnClick?.Invoke();
			eventData.Use();
		}
		catch (Exception e)
		{
			SafeGuard.Report("点击图腾格子失败", e);
		}
	}
}
