using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TotemFusion
{
    /// <summary>
    /// 拖动整个融合面板。
    ///
    /// 挂在面板的底图（以及图标遮罩）上：按钮、格子这些交互控件是底图的兄弟节点，
    /// 事件不会向上冒泡到这里，所以从按钮 / 格子上按下不会误拖面板。
    /// </summary>
    internal sealed class PanelDragHandler : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform _target;
        private RectTransform _canvasRect;
        private Vector2 _offset;

        /// <summary>要移动的面板根节点。</summary>
        public void Configure(RectTransform target)
        {
            _target = target;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            try
            {
                if (eventData == null ||
                    eventData.button != PointerEventData.InputButton.Left ||
                    _target == null)
                    return;

                Canvas canvas = _target.GetComponentInParent<Canvas>();
                if (canvas == null)
                    return;

                _canvasRect = canvas.rootCanvas.transform as RectTransform;
                if (_canvasRect == null)
                    return;

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _canvasRect, eventData.position, null, out Vector2 local))
                    return;

                _offset = _target.anchoredPosition - local;
            }
            catch (Exception e)
            {
                SafeGuard.Report("开始拖动融合面板失败", e);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            try
            {
                if (eventData == null || _target == null || _canvasRect == null)
                    return;

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _canvasRect, eventData.position, null, out Vector2 local))
                    return;

                Vector2 position = local + _offset;
                _target.anchoredPosition = ClampToCanvas(position);
            }
            catch (Exception e)
            {
                SafeGuard.Report("拖动融合面板失败", e);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // 位置保留在拖动结束处，下次打开界面仍在原位
        }

        /// <summary>把面板限制在画布内，避免拖到完全看不见的地方。</summary>
        private Vector2 ClampToCanvas(Vector2 position)
        {
            if (_canvasRect == null || _target == null)
                return position;

            Rect canvasBounds = _canvasRect.rect;
            Vector2 scale = _target.localScale;
            float halfWidth = Mathf.Abs(_target.rect.width * scale.x) * 0.5f;
            float halfHeight = Mathf.Abs(_target.rect.height * scale.y) * 0.5f;

            // 面板边缘不超出画布；画布比面板还小时锁定在中心
            float limitX = Mathf.Max(0f, canvasBounds.width * 0.5f - halfWidth);
            float limitY = Mathf.Max(0f, canvasBounds.height * 0.5f - halfHeight);

            position.x = Mathf.Clamp(position.x, -limitX, limitX);
            position.y = Mathf.Clamp(position.y, -limitY, limitY);
            return position;
        }
    }
}
