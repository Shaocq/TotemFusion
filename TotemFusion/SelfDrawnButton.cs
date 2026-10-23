using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TotemFusion
{
    /// <summary>
    /// 按钮外观参数。
    ///
    /// 背景一律由本 mod 自绘（运行时生成的圆角九宫格），只从原版按钮读取
    /// 颜色、字体、字号、文字样式和阴影参数。不能复用原版按钮的 sprite：
    /// 原版按钮的绿色底是 ProceduralImage 程序化绘制的圆角矩形，
    /// 它的 sprite 只是一张占位图，贴上去会变成直角矩形。
    /// </summary>
    internal struct ButtonStyle
    {
        /// <summary>按钮底色（原版按钮 BG 的实际显示色）。</summary>
        public Color Tint;

        public Color TextColor;
        public Color FallbackBackground;
        public Color FallbackBorder;

        /// <summary>边缘内阴影（原版 BG 上的 TrueShadow，inset）：深色从边缘向内渐隐。</summary>
        public Color EdgeShadowColor;

        /// <summary>外阴影（原版按钮根节点上的 TrueShadow）：黑色向四周扩散。</summary>
        public Color OuterShadowColor;

        public TMP_FontAsset Font;
        public float FontSize;
        public FontStyles FontStyle;
        public int Layer;

        public static ButtonStyle Create()
        {
            ButtonStyle style = new ButtonStyle();
            style.Tint = Color.white;
            style.TextColor = Color.white;
            style.FallbackBackground = new Color32(46, 160, 67, 255);
            style.FallbackBorder = new Color32(28, 104, 42, 255);
            style.EdgeShadowColor = Color.clear;
            style.OuterShadowColor = Color.clear;
            style.FontSize = 34f;
            style.FontStyle = FontStyles.Normal;
            style.Layer = 5;
            return style;
        }

        /// <summary>从原版按钮 / 文本读取外观参考。</summary>
        public static ButtonStyle FromTemplate(Image image, TMP_Text text)
        {
            ButtonStyle style = Create();

            if (image != null)
            {
                // 只读取颜色，不读取 sprite：原版按钮的底图是 ProceduralImage
                // 程序化绘制的圆角矩形，sprite 只是占位图，没有圆角。
                //
                // 视觉颜色 = Image.color × CanvasRenderer 的颜色。
                // 原版按钮的颜色有可能不写在 Image.color 上，而是 Selectable
                // 的状态色（normalColor）通过 CanvasRenderer 乘上去的，
                // 只读 Image.color 会拿到白色，自绘出来就成了白底按钮。
                style.Tint = ReadRenderedColor(image);

                if (image.gameObject.layer > 0)
                    style.Layer = image.gameObject.layer;
            }

            // 外阴影取自原版按钮根节点上的 TrueShadow：黑色、向四周扩散、右下偏移。
            // 内阴影不启用：自绘的角部渐变和原版 TrueShadow 的观感对不上，用户要求去掉。
            style.OuterShadowColor = new Color(0f, 0f, 0f, 0.6f);

            if (text != null)
            {
                style.Font = text.font;
                style.FontSize = text.fontSize > 0f ? text.fontSize : 34f;
                style.FontStyle = text.fontStyle;
                style.TextColor = text.color;

                if (text.gameObject.layer > 0)
                    style.Layer = text.gameObject.layer;
            }

            return style;
        }

        /// <summary>
        /// 读取 Image 当前实际显示的乘算颜色（含 Selectable 状态色）。
        ///
        /// 只乘 RGB，不乘 CanvasRenderer 的 alpha：菜单可能正在做淡入，
        /// 若把过渡中的 alpha 也乘进来，自绘按钮会跟着半透明。
        /// </summary>
        private static Color ReadRenderedColor(Image image)
        {
            try
            {
                CanvasRenderer renderer = image.canvasRenderer;
                if (renderer != null)
                {
                    Color canvasColor = renderer.GetColor();
                    float r = image.color.r * canvasColor.r;
                    float g = image.color.g * canvasColor.g;
                    float b = image.color.b * canvasColor.b;

                    // 菜单淡入 / 淡出时 CanvasRenderer 的颜色可能整体压黑，
                    // 乘出来接近黑色就退回 Image 自身的颜色，避免按钮变黑
                    if (r + g + b > 0.01f)
                        return new Color(r, g, b, image.color.a);
                }
            }
            catch
            {
                // 读不到就退回基础色
            }

            return image.color;
        }
    }

    /// <summary>
    /// 完全自绘的按钮。
    ///
    /// 不实例化游戏里的任何按钮对象：背景、文字、点击判定、按下回弹动画
    /// 全部由本组件自己创建和驱动。背景统一为运行时生成的圆角九宫格
    /// （与原版 ProceduralImage 的圆角一致），并按原版参数叠加
    /// 「深色内阴影 + 黑色外阴影」两层 TrueShadow 效果。
    ///
    /// 之前克隆原版按钮时，背景节点是新建的（Default 层），文字是克隆来的
    /// （继承原按钮的 UI 层）。如果游戏的 UI 画布由相机渲染，Default 层既不会被
    /// 画出来、也收不到 GraphicRaycaster 的射线，表现就是「只剩文字、点不了」。
    /// 所以这里创建的所有节点都会套用参考按钮所在的层。
    /// </summary>
    internal sealed class SelfDrawnButton : MonoBehaviour,
        IPointerClickHandler, IPointerDownHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>原版按钮的按下动画时长（SizeDeltaToggle 的 duration）。</summary>
        private const float PressDuration = 0.1f;

        /// <summary>原版按钮按下时缩进的像素数。</summary>
        private const float PressDistance = 8f;

        /// <summary>指针移动超过这个距离就视为拖动，不再触发点击。</summary>
        private const float ClickTolerance = 14f;

        /// <summary>外阴影 Image 在按钮四周外扩的像素数（原版 TrueShadow size=8 的近似）。</summary>
        private const float OuterShadowPadding = 12f;

        /// <summary>原版外观的外阴影偏移：右下 (1.414, -1.414)。</summary>
        private const float OuterShadowOffset = 1.5f;

        private static Sprite _roundedSprite;
        private static Sprite _edgeShadowSprite;
        private static Sprite _outerShadowSprite;
        private static AnimationCurve _pressCurve;

        private RectTransform _pressRoot;
        private Image _border;
        private Image _background;

        /// <summary>边缘内阴影（原版 BG 的 inset TrueShadow）。</summary>
        private Image _edgeShadow;

        /// <summary>外阴影（原版按钮根节点的 TrueShadow）。</summary>
        private Image _outerShadow;

        private TMP_Text _label;

        private Color _baseTint = Color.white;
        private Color _borderColor;
        private Color _edgeShadowBase;
        private Color _outerShadowBase;

        private Action _onClick;
        private bool _interactable = true;
        private bool _hovered;
        private bool _pointerDown;
        private bool _pointerDownOnButton;

        private Vector2 _pointerDownPosition;

        // 按下动画：从 _pressFrom 补间到 _pressTo，曲线复刻原版按钮的回弹
        private float _pressFrom;
        private float _pressTo;
        private float _pressStartTime = -1f;
        private float _appliedDistance;

        public bool Interactable => _interactable;

        /// <summary>
        /// 创建按钮。size 是参考原版按钮得到的尺寸；
        /// style 里的层必须与游戏 UI 一致，否则相机渲染模式下会看不见、点不到。
        /// </summary>
        public static SelfDrawnButton Create(
            Transform parent, string name, string label, Vector2 size, ButtonStyle style)
        {
            RectTransform root = null;

            try
            {
                root = UiFactory.NewRect(parent, name);
                root.anchorMin = new Vector2(0.5f, 0.5f);
                root.anchorMax = new Vector2(0.5f, 0.5f);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.sizeDelta = size;
                root.localScale = Vector3.one;
                root.localRotation = Quaternion.identity;

                SelfDrawnButton button = root.gameObject.AddComponent<SelfDrawnButton>();
                button.Build(label, style);

                // 与参考按钮同层：相机渲染模式下这一层才可见、才能被射线命中
                UiFactory.SetLayerRecursive(root.gameObject, style.Layer > 0 ? style.Layer : 5);
                return button;
            }
            catch (Exception e)
            {
                SafeGuard.Report("创建自绘按钮失败", e);

                // 创建到一半失败时把半成品节点清掉，别留在菜单里
                try
                {
                    if (root != null)
                        UnityEngine.Object.Destroy(root.gameObject);
                }
                catch
                {
                    // 忽略
                }

                return null;
            }
        }

        public void SetOnClick(Action action)
        {
            _onClick = action;
        }

        public void SetLabel(string text)
        {
            if (_label != null)
                _label.text = text ?? string.Empty;
        }

        /// <summary>置灰（不可点击），用于「物品不足 2 件」时的融合按钮。</summary>
        public void SetInteractable(bool value)
        {
            _interactable = value;

            if (!value)
            {
                _pointerDown = false;
                _hovered = false;
            }

            ApplyColors();
        }

        private void Build(string label, ButtonStyle style)
        {
            _borderColor = style.FallbackBorder;

            // 动画只改这一层的 sizeDelta，因此即使父级有布局组件也不会被覆盖
            _pressRoot = UiFactory.NewRect(transform, "PressRoot");
            UiFactory.Stretch(_pressRoot);

            // 1. 外阴影：原版按钮根节点上的 TrueShadow（黑色、向四周扩散、右下偏移）。
            //    放在最底层且外扩，超出按钮边缘的部分就是玩家看到的「两侧淡黑色」。
            if (style.OuterShadowColor.a > 0.001f)
            {
                _outerShadowBase = style.OuterShadowColor;
                _outerShadow = CreateStretchedImage(_pressRoot, "OuterShadow", _outerShadowBase);
                _outerShadow.sprite = OuterShadowSprite;
                _outerShadow.type = Image.Type.Sliced;

                RectTransform rect = _outerShadow.rectTransform;
                rect.offsetMin = new Vector2(-OuterShadowPadding, -OuterShadowPadding);
                rect.offsetMax = new Vector2(OuterShadowPadding, OuterShadowPadding);
                rect.anchoredPosition = new Vector2(OuterShadowOffset, -OuterShadowOffset);
            }

            // 2. 深色圆角描边（满铺），底色内缩 2px 后露出这一圈
            _border = CreateStretchedImage(_pressRoot, "Border", style.FallbackBorder);
            ApplyRoundedSprite(_border);

            // 3. 按钮底色：原版按钮 BG 的颜色，圆角与本 mod 所有按钮一致
            Color background = IsWhite(style.Tint)
                ? style.FallbackBackground
                : style.Tint;

            _baseTint = background;
            _background = UiFactory.NewImage(_pressRoot, "Background", background);
            ApplyRoundedSprite(_background);
            _background.raycastTarget = true;

            RectTransform backgroundRect = _background.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = new Vector2(2f, 2f);
            backgroundRect.offsetMax = new Vector2(-2f, -2f);

            // 4. 边缘内阴影：原版 BG 上的 inset TrueShadow（深绿、从边缘向内渐隐）
            if (style.EdgeShadowColor.a > 0.001f)
            {
                _edgeShadowBase = style.EdgeShadowColor;
                _edgeShadow = CreateStretchedImage(_pressRoot, "EdgeShadow", _edgeShadowBase);
                _edgeShadow.sprite = EdgeShadowSprite;
                _edgeShadow.type = Image.Type.Sliced;
            }

            RectTransform labelRect = UiFactory.NewRect(_pressRoot, "Label");
            TextMeshProUGUI text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = style.Font != null ? style.Font : TMP_Settings.defaultFontAsset;
            text.fontSize = style.FontSize > 0f ? style.FontSize : 34f;
            text.fontStyle = style.FontStyle;
            text.text = label ?? string.Empty;
            text.color = style.TextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            UiFactory.Stretch(labelRect);
            labelRect.localScale = Vector3.one;
            _label = text;

            ApplyColors();
        }

        private static Image CreateStretchedImage(Transform parent, string name, Color color)
        {
            Image image = UiFactory.NewImage(parent, name, color);
            UiFactory.Stretch(image.rectTransform);
            image.raycastTarget = false;
            return image;
        }

        private void Update()
        {
            // 每帧逻辑整体兜底：这里出异常不能影响游戏
            try
            {
                UpdatePressAnimation();
            }
            catch (Exception e)
            {
                SafeGuard.Report("按钮动画更新失败", e);
                enabled = false;
            }
        }

        private void UpdatePressAnimation()
        {
            if (_pressStartTime < 0f || _pressRoot == null)
                return;

            float elapsed = Time.unscaledTime - _pressStartTime;
            float progress;

            if (elapsed >= PressDuration)
            {
                progress = _pressTo;
                _pressStartTime = -1f;
            }
            else
            {
                progress = Mathf.LerpUnclamped(_pressFrom, _pressTo, elapsed / PressDuration);
            }

            ApplyPress(progress);
        }

        private void ApplyPress(float progress)
        {
            // 原版「分割」按钮的曲线：0 → 过冲 1.5279 → 回落 1
            float eased = PressCurve.Evaluate(progress);
            float distance = PressDistance * eased;

            if (Mathf.Approximately(distance, _appliedDistance))
                return;

            _appliedDistance = distance;

            // Stretch 状态下 sizeDelta 为 (-d, -d) 表示四周内缩 d/2，总缩小 d
            _pressRoot.sizeDelta = new Vector2(-distance, -distance);
        }

        private void StartPress(bool down)
        {
            float target = down ? 1f : 0f;

            // 从当前实际进度接着补间，快速连按也不会跳变
            _pressFrom = _pressStartTime >= 0f ? CurrentProgress() : _pressTo;
            _pressTo = target;
            _pressStartTime = Time.unscaledTime;
        }

        private float CurrentProgress()
        {
            if (_pressStartTime < 0f)
                return _pressTo;

            float t = (Time.unscaledTime - _pressStartTime) / PressDuration;
            if (t >= 1f)
                return _pressTo;

            return Mathf.LerpUnclamped(_pressFrom, _pressTo, t);
        }

        private void ApplyColors()
        {
            if (_background == null)
                return;

            if (!_interactable)
            {
                _background.color = Scale(_baseTint, 0.55f, 0.75f);

                if (_edgeShadow != null)
                    _edgeShadow.color = Scale(_edgeShadowBase, 1f, 0.75f);

                if (_outerShadow != null)
                    _outerShadow.color = Scale(_outerShadowBase, 1f, 0.75f);

                if (_border != null)
                    _border.color = new Color(0.22f, 0.22f, 0.24f, 1f);

                return;
            }

            // 与原版 Selectable 一致：悬停 / 按下都是把底图颜色压暗一点
            float factor = _pointerDown ? 0.88f : (_hovered ? 0.96f : 1f);
            _background.color = Scale(_baseTint, factor, 1f);

            if (_edgeShadow != null)
                _edgeShadow.color = _edgeShadowBase;

            if (_outerShadow != null)
                _outerShadow.color = _outerShadowBase;

            if (_border != null)
                _border.color = _borderColor;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            try
            {
                if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
                    return;

                _pointerDownOnButton = _interactable;
                if (!_interactable)
                    return;

                _pointerDown = true;
                _pointerDownPosition = eventData.position;
                StartPress(true);
                ApplyColors();
            }
            catch (Exception e)
            {
                SafeGuard.Report("按钮按下处理失败", e);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            try
            {
                if (!_pointerDown)
                    return;

                _pointerDown = false;
                StartPress(false);
                ApplyColors();
            }
            catch (Exception e)
            {
                SafeGuard.Report("按钮抬起处理失败", e);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            try
            {
                if (eventData == null ||
                    eventData.button != PointerEventData.InputButton.Left ||
                    !_interactable ||
                    !_pointerDownOnButton)
                    return;

                _pointerDownOnButton = false;

                // 拖动过（例如从按钮上拖走物品）不算点击
                if (eventData.dragging ||
                    Vector2.Distance(eventData.position, _pointerDownPosition) > ClickTolerance)
                    return;

                Action action = _onClick;
                if (action != null)
                    action();
            }
            catch (Exception e)
            {
                SafeGuard.Report("按钮点击处理失败", e);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            try
            {
                _hovered = true;
                ApplyColors();
            }
            catch (Exception e)
            {
                SafeGuard.Report("按钮悬停处理失败", e);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            try
            {
                _hovered = false;
                ApplyColors();
            }
            catch (Exception e)
            {
                SafeGuard.Report("按钮移出处理失败", e);
            }
        }

        // ------------------------------------------------------------ 外观资源

        private static Color Scale(Color color, float factor, float alphaFactor)
        {
            return new Color(
                color.r * factor,
                color.g * factor,
                color.b * factor,
                color.a * alphaFactor);
        }

        private static bool IsWhite(Color color)
        {
            return color.r > 0.95f && color.g > 0.95f && color.b > 0.95f;
        }

        private static void ApplyRoundedSprite(Image image)
        {
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
        }

        private static AnimationCurve PressCurve
        {
            get
            {
                if (_pressCurve != null)
                    return _pressCurve;

                // 与原版「分割」按钮一致：起点斜率 2，0.666 处过冲到 1.5279，1.0 回落到 1
                _pressCurve = new AnimationCurve(
                    new Keyframe(0f, 0f, 2f, 2f),
                    new Keyframe(0.6662211f, 1.527896f, 0f, 0f),
                    new Keyframe(1f, 1f, 0f, 0f));
                _pressCurve.preWrapMode = WrapMode.Clamp;
                _pressCurve.postWrapMode = WrapMode.Clamp;
                return _pressCurve;
            }
        }

        /// <summary>运行时生成的白色圆角矩形（九宫格），只在找不到原版底图时使用。</summary>
        private static Sprite RoundedSprite
        {
            get
            {
                if (_roundedSprite != null)
                    return _roundedSprite;

                _roundedSprite = CreateRoundedSprite();
                return _roundedSprite;
            }
        }

        private static Sprite CreateRoundedSprite()
        {
            const int size = 64;
            const int radius = 18;
            const int border = 20;

            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.name = "ItemDurabilityMerge_RoundedButton";
            texture.hideFlags = HideFlags.DontSave;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = RoundedAlpha(x + 0.5f, y + 0.5f, size, radius);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = "ItemDurabilityMerge_RoundedButton";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        /// <summary>圆角矩形的距离场，1 像素抗锯齿。</summary>
        private static float RoundedAlpha(float x, float y, float size, float radius)
        {
            float centerX = Mathf.Clamp(x, radius, size - radius);
            float centerY = Mathf.Clamp(y, radius, size - radius);
            float dx = x - centerX;
            float dy = y - centerY;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(radius - distance + 0.5f);
        }

        /// <summary>
        /// 边缘内阴影贴图：白色，alpha 只在四个圆角处出现。
        ///
        /// 原版按钮（TrueShadow inset）的内阴影集中在四个圆角，上下左右直边的
        /// 中段是干净的。所以这里以「到最近圆角圆心的距离」计算强度：
        /// 圆角圆弧上最强，向角内和沿边各渐隐一段后归零，直边中段完全透明。
        /// </summary>
        private static Sprite EdgeShadowSprite
        {
            get
            {
                if (_edgeShadowSprite != null)
                    return _edgeShadowSprite;

                _edgeShadowSprite = CreateEdgeShadowSprite();
                return _edgeShadowSprite;
            }
        }

        private static Sprite CreateEdgeShadowSprite()
        {
            const int size = 128;
            const int radius = 18;
            const int fade = 16;
            float half = size * 0.5f;

            // 四个圆角圆心相对贴图中心的偏移
            float corner = half - radius;

            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.name = "ItemDurabilityMerge_EdgeShadow";
            texture.hideFlags = HideFlags.DontSave;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;
                    float sdf = RoundedDistance(px, py, half, half, radius);

                    // 圆角矩形外部保持透明，否则按钮圆角外会露出一块方的阴影
                    float alpha = 0f;
                    if (sdf <= 0f)
                    {
                        // 到最近圆角圆心的距离（利用四角对称，取绝对值即可）
                        float dx = Mathf.Abs(px) - corner;
                        float dy = Mathf.Abs(py) - corner;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);

                        if (d < radius)
                        {
                            // 圆角内部：从角弧（1.0）向圆心柔和衰减，避免角上出现实心色块
                            alpha = Mathf.Lerp(0.35f, 1f, d / radius);
                        }
                        else
                        {
                            // 从角弧沿边向内渐隐，fade 之外完全透明 —— 直边中段没有阴影
                            alpha = Mathf.Clamp01(1f - (d - radius) / fade);
                            alpha *= alpha;
                        }
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            // 九宫格边界必须让四周边条带完全落在阴影之外（alpha = 0），
            // 否则拉伸边条带时会把残余阴影拉成一条贯穿按钮的暗带。
            // 距圆角圆心达到 radius + fade 才完全透明，而角区域边缘距圆心的
            // 最小距离是 border - radius，所以 border = 2 * radius + fade。
            float border = radius * 2 + fade;
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = "ItemDurabilityMerge_EdgeShadow";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        /// <summary>外阴影贴图：圆角矩形外侧的模糊扩散，形状内部保持透明。</summary>
        private static Sprite OuterShadowSprite
        {
            get
            {
                if (_outerShadowSprite != null)
                    return _outerShadowSprite;

                _outerShadowSprite = CreateOuterShadowSprite();
                return _outerShadowSprite;
            }
        }

        private static Sprite CreateOuterShadowSprite()
        {
            const int size = 96;
            const int radius = 18;
            const int padding = 12;
            float half = size * 0.5f - padding;

            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.name = "ItemDurabilityMerge_OuterShadow";
            texture.hideFlags = HideFlags.DontSave;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            float center = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - center;
                    float py = y + 0.5f - center;
                    float sdf = RoundedDistance(px, py, half, half, radius);

                    // 只画形状外部：内部被按钮本身盖住，留透明避免半透明状态透出黑块
                    float alpha = 0f;
                    if (sdf > 0f)
                    {
                        alpha = Mathf.Clamp01(1f - sdf / padding);
                        alpha *= alpha;
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            // 九宫格边界要同时盖住扩散区（padding）和圆角（radius）
            float border = padding + radius;
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = "ItemDurabilityMerge_OuterShadow";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        /// <summary>
        /// 圆角矩形的有符号距离：内部为负、边界为 0、外部为正。
        /// 坐标以矩形中心为原点。
        /// </summary>
        private static float RoundedDistance(
            float px, float py, float halfWidth, float halfHeight, float radius)
        {
            float qx = Mathf.Abs(px) - (halfWidth - radius);
            float qy = Mathf.Abs(py) - (halfHeight - radius);
            float outsideX = Mathf.Max(qx, 0f);
            float outsideY = Mathf.Max(qy, 0f);
            float outside = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY);
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - radius;
        }
    }
}
