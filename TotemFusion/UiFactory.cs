using Duckov.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TotemFusion
{
    /// <summary>
    /// 运行时搭 UI 的小工具。
    ///
    /// 只保留「建节点 / 复制九宫格底图 / 克隆物品格子」这些基础能力；
    /// 按钮与文本一律自绘（见 SelfDrawnButton / MergePanel.CreateText），
    /// 不再克隆游戏里的按钮对象，避免继承到不可控的层级、状态和视觉节点。
    /// </summary>
    internal static class UiFactory
    {
        /// <summary>原版右键菜单里「使用」按钮的绿色，作为自绘按钮的默认底色。</summary>
        public static readonly Color FallbackGreen = new Color32(46, 160, 67, 255);

        public static RectTransform NewRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image NewImage(Transform parent, string name, Color color)
        {
            RectTransform rect = NewRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>把 RectTransform 铺满父级。</summary>
        public static void Stretch(RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>把 RectTransform 放在父级中心，并指定尺寸。</summary>
        public static void Center(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>递归设置层。游戏 UI 由相机渲染时，层不对会直接看不见、点不到。</summary>
        public static void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null)
                return;

            go.layer = layer;

            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        /// <summary>
        /// 找一份原版面板底图（九宫格 Sprite）作为模板。
        /// 优先名字里带 Panel / BG / Background / Frame 的大尺寸九宫格图片。
        /// </summary>
        public static Image FindPanelBackgroundTemplate()
        {
            Image[] all;
            try
            {
                all = Object.FindObjectsOfType<Image>(true);
            }
            catch
            {
                return null;
            }

            Image fallback = null;
            float fallbackArea = 0f;

            for (int i = 0; i < all.Length; i++)
            {
                Image image = all[i];
                if (image == null || !image.gameObject.activeInHierarchy)
                    continue;

                if (image.sprite == null || image.type != Image.Type.Sliced)
                    continue;

                Rect rect = image.rectTransform.rect;
                if (rect.width < 200f || rect.height < 150f)
                    continue;

                string objectName = image.gameObject.name.ToLowerInvariant();
                if (objectName.Contains("panel") || objectName.Contains("bg") ||
                    objectName.Contains("background") || objectName.Contains("frame"))
                    return image;

                float area = rect.width * rect.height;
                if (fallback == null || area > fallbackArea)
                {
                    fallback = image;
                    fallbackArea = area;
                }
            }

            return fallback;
        }

        /// <summary>按模板复制一张九宫格底图（只复制渲染参数，不带上子物体）。</summary>
        public static Image CreateBackground(
            Image template, Transform parent, string name, Color tint)
        {
            Image image = NewImage(parent, name, tint);

            if (template != null)
            {
                image.sprite = template.sprite;
                image.type = template.type;
                image.fillCenter = template.fillCenter;
                image.preserveAspect = template.preserveAspect;
                image.pixelsPerUnitMultiplier = template.pixelsPerUnitMultiplier;
                image.material = template.material;
            }

            image.color = tint;
            image.raycastTarget = true;
            return image;
        }

        /// <summary>
        /// 从任意已打开的界面里找一份完整的背包物品格子（ItemDisplay）作为模板。
        /// 优先当前 View 里的实例，保证拿到的是背包 / 搜刮界面正在用的那一套外观。
        /// </summary>
        public static ItemDisplay FindItemDisplayTemplate()
        {
            ItemDisplay[] all;
            try
            {
                all = Object.FindObjectsOfType<ItemDisplay>(true);
            }
            catch
            {
                all = Object.FindObjectsOfType<ItemDisplay>();
            }

            ItemDisplay best = null;
            int bestScore = -1;

            for (int i = 0; i < all.Length; i++)
            {
                ItemDisplay display = all[i];
                if (display == null || !display.gameObject.activeInHierarchy)
                    continue;

                // 结构不全的实例（正在池里回收、没有图标节点）不要
                if (display.GetComponentInChildren<Image>(true) == null)
                    continue;

                int score = 0;

                // 最想要背包里的格子：它的父级链上有 InventoryEntry
                try
                {
                    if (display.GetComponentInParent<InventoryEntry>() != null)
                        score += 2;
                }
                catch
                {
                    // 忽略
                }

                string objectName = display.gameObject.name.ToLowerInvariant();
                if (objectName.Contains("slot"))
                    score += 1;

                // 当前打开的视图里的实例优先，保证拿到背包 / 搜刮界面正在用的那一套外观
                View active = View.ActiveView;
                if (active != null && display.transform.IsChildOf(active.transform))
                    score += 2;

                if (score > bestScore)
                {
                    best = display;
                    bestScore = score;
                }
            }

            return best;
        }

        /// <summary>克隆一个物品格子，规格化成固定尺寸，并调用 Setup 显示指定物品。</summary>
        public static ItemDisplay CloneItemDisplay(ItemDisplay template, Transform parent, string name, Vector2 size)
        {
            if (template == null)
                return null;

            GameObject clone = Object.Instantiate(template.gameObject);
            clone.name = name;
            clone.SetActive(true);
            clone.transform.SetParent(parent, false);

            RectTransform rect = clone.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }

            ItemDisplay display = clone.GetComponent<ItemDisplay>();
            if (display == null)
                display = clone.GetComponentInChildren<ItemDisplay>(true);

            if (display != null)
            {
                // 先清空，退掉克隆体从原实例那里继承来的物品事件订阅
                display.Setup(null);
                display.Editable = false;
                display.Movable = false;
                display.CanDrop = false;
            }

            return display;
        }

        /// <summary>找一份原版 TMP 文本作为字体 / 文字样式 / 层参考。</summary>
        public static TMP_Text FindTextTemplate()
        {
            try
            {
                TMP_Text[] texts = Object.FindObjectsOfType<TMP_Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text text = texts[i];
                    if (text != null && text.gameObject.activeInHierarchy && text.font != null)
                        return text;
                }
            }
            catch
            {
                // 找不到就退回 TMP 默认字体
            }

            return null;
        }

        /// <summary>自绘文本：只读取原版字体与文字样式，不实例化原版对象。</summary>
        public static TMP_Text CreateText(
            TMP_Text template, Transform parent, string name,
            string content, float fontSize, Vector2 position, Vector2 size)
        {
            RectTransform rect = NewRect(parent, name);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();

            text.font = template != null && template.font != null
                ? template.font
                : TMP_Settings.defaultFontAsset;
            text.fontStyle = template != null ? template.fontStyle : FontStyles.Normal;
            text.fontSize = fontSize;
            text.text = content ?? string.Empty;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;

            Center(text.rectTransform, position, size);

            // 与游戏文本同层，相机渲染的 UI 也可见
            if (template != null && template.gameObject.layer > 0)
                SetLayerRecursive(text.gameObject, template.gameObject.layer);

            return text;
        }

        /// <summary>
        /// 找原版的一条细分隔线 / 横杠：优先名字带 divider / separator / underline / line 的细长 Image。
        /// </summary>
        public static Image FindDividerTemplate()
        {
            Image[] all;
            try
            {
                all = Object.FindObjectsOfType<Image>(true);
            }
            catch
            {
                return null;
            }

            for (int i = 0; i < all.Length; i++)
            {
                Image image = all[i];
                if (image == null || !image.gameObject.activeInHierarchy || image.sprite == null)
                    continue;

                Rect rect = image.rectTransform.rect;
                float width = Mathf.Abs(rect.width);
                float height = Mathf.Abs(rect.height);
                if (width < 80f || height <= 0f || width / height < 6f)
                    continue;

                string objectName = image.gameObject.name.ToLowerInvariant();
                if (objectName.Contains("divider") || objectName.Contains("separator") ||
                    objectName.Contains("underline") || objectName.Contains("line"))
                    return image;
            }

            return null;
        }

        /// <summary>
        /// 创建标题下的横杠：能读到原版分隔线就引用它的 Sprite 与颜色，
        /// 否则用一条自绘细线（半透明白）。
        /// </summary>
        public static Image CreateTitleDivider(
            Image template, Transform parent, string name, Vector2 position, Vector2 size)
        {
            Color color = template != null
                ? template.color
                : new Color(1f, 1f, 1f, 0.22f);

            Image divider = NewImage(parent, name, color);
            Center(divider.rectTransform, position, size);

            if (template != null && template.sprite != null)
            {
                divider.sprite = template.sprite;
                divider.type = template.type;
                divider.fillCenter = template.fillCenter;
                divider.preserveAspect = false;
                divider.pixelsPerUnitMultiplier = template.pixelsPerUnitMultiplier;
                divider.material = template.material;

                if (template.gameObject.layer > 0)
                    SetLayerRecursive(divider.gameObject, template.gameObject.layer);
            }

            divider.raycastTarget = false;
            return divider;
        }
    }
}
