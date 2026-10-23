using System;
using System.Collections.Generic;
using System.Text;
using Duckov.UI;
using ItemStatsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TotemFusion;

/// <summary>
/// 图腾融合界面。
///
/// 外观对齐「物品耐久度融合」的工作台：居中的原版九宫格面板、标题 + 横杠、
/// 原版背包格子样式的图腾槽、中间的「+」，底部两颗自绘圆角按钮。
/// 面板按屏幕宽度整体缩放（大小自适应），按住面板空白处可以拖动。
/// </summary>
public static class TotemFusionView
{
	private static GameObject _root;

	private static RectTransform _panel;

	private static TMP_Text _textTemplate;

	private static Item _selA;

	private static Item _selB;

	private static ItemDisplay _slotADisplay;

	private static ItemDisplay _slotBDisplay;

	private static TextMeshProUGUI _slotAText;

	private static TextMeshProUGUI _slotBText;

	private static TextMeshProUGUI _slotAPh;

	private static TextMeshProUGUI _slotBPh;

	private static TextMeshProUGUI _resultText;

	private static TextMeshProUGUI _mergeText;

	private static TextMeshProUGUI _previewText;

	private static ScrollRect _previewScroll;

	private static SelfDrawnButton _fuseBtn;

	private static Item _dragging;

	private static RectTransform _slotARect;

	private static RectTransform _slotBRect;

	private static Image _slotAImg;

	private static Image _slotBImg;

	private static Ticker _ticker;

	private static bool _dragHooked;

	private static bool _healed;

	private const float SlotSize = 160f;

	private const float PanelWidth = 900f;

	private const float PanelHeight = 860f;

	/// <summary>面板占屏幕宽度的比例，与「物品耐久度融合」界面一致。</summary>
	private const float PanelScreenWidthRatio = 0.3f;

	/// <summary>原版右键菜单按钮的底色。</summary>
	private static readonly Color OriginalButtonGreen =
		new Color(0.4314703f, 0.9056604f, 0.4833889f, 1f);

	/// <summary>原版按钮描边的深绿色。</summary>
	private static readonly Color OriginalButtonEdge =
		new Color(0.1012371f, 0.3301887f, 0.16086f, 1f);

	private static float _lastAppliedScale = -1f;

	private static readonly List<TextMeshProUGUI> _tmpPool = new List<TextMeshProUGUI>();

	public static bool IsOpen => _root != null && _root.activeSelf;

	public static void Open()
	{
		try
		{
			if (!_healed)
			{
				_healed = true;
				TotemFusionLogic.HealStarNames();
			}
			TotemFusionLogic.RepairFused();
			D2EffectGuard.TryInstall();

			// 新界面要克隆原版背包格子（ItemDisplay）作为模板，所以先打开背包
			try
			{
				InventoryView.Show();
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[TotemFusion] 打开背包界面失败: " + ex.Message);
			}

			// 找不到原版格子模板（背包尚未生成）时重建一次
			if (_root == null || _slotADisplay == null)
			{
				if (_root != null)
				{
					// 先停用再销毁：销毁是延迟到帧末的，
					// 构建时查找原版模板不能把旧界面自己的节点当成模板
					_root.SetActive(false);
					Object.Destroy(_root);
					_root = null;
				}
				BuildRoot();
			}

			if (_root == null)
				return;

			HookDragEvents();
			_selA = null;
			_selB = null;
			if (_resultText != null)
				_resultText.text = string.Empty;

			_root.SetActive(true);
			ApplyAdaptiveScale();
			UpdateSettingsUI();
			RefreshSlots();
			TotemFusionLogic.DumpInventoryToLog();
			RefreshFont();
		}
		catch (Exception ex2)
		{
			Debug.LogError("[TotemFusion] 打开合成界面失败: " + ex2);
		}
	}

	public static void Close()
	{
		if (_root != null)
			_root.SetActive(false);

		_dragging = null;
	}

	public static void Deactivate()
	{
		Close();
		UnhookDragEvents();
	}

	private static void RefreshFont()
	{
		TMP_FontAsset gameFont = TotemFusionStyle.GameFont;
		if (gameFont == null)
			return;

		for (int i = 0; i < _tmpPool.Count; i++)
		{
			if (_tmpPool[i] != null)
				_tmpPool[i].font = gameFont;
		}
	}

	// ---------------------------------------------------------------- 构建界面

	private static void BuildRoot()
	{
		// 模板必须在创建任何节点之前取，避免把自己刚建出来的节点当成原版模板
		_textTemplate = UiFactory.FindTextTemplate();
		ItemDisplay displayTemplate = UiFactory.FindItemDisplayTemplate();
		Image backgroundTemplate = UiFactory.FindPanelBackgroundTemplate();
		Image dividerTemplate = UiFactory.FindDividerTemplate();

		_root = new GameObject(
			"TotemFusionPanel",
			typeof(RectTransform),
			typeof(Canvas),
			typeof(CanvasScaler),
			typeof(GraphicRaycaster));
		Object.DontDestroyOnLoad(_root);

		Canvas canvas = _root.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 32000;

		CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
		scaler.matchWidthOrHeight = 0.5f;

		_panel = UiFactory.NewRect(_root.transform, "Panel");
		UiFactory.Center(_panel, Vector2.zero, new Vector2(PanelWidth, PanelHeight));

		// 面板底板：复制原版九宫格底图，同时挡住面板区域的点击
		Image panelBackground = UiFactory.CreateBackground(
			backgroundTemplate, _panel, "Background", new Color(0.12f, 0.12f, 0.14f, 1f));
		UiFactory.Stretch(panelBackground.rectTransform);

		// 按住面板空白处可以拖动整个面板
		PanelDragHandler drag = panelBackground.gameObject.AddComponent<PanelDragHandler>();
		drag.Configure(_panel);

		CreateText("Title", "图腾融合", 44f,
			new Vector2(0f, 365f), new Vector2(860f, 70f),
			TextAlignmentOptions.Center, false);

		// 标题下的原版画风横杠
		UiFactory.CreateTitleDivider(
			dividerTemplate, _panel, "TotemFusionTitleDivider",
			new Vector2(0f, 322f), new Vector2(620f, 2f));

		// 两个图腾槽：直接克隆原版背包格子
		_slotARect = CreateSlot(0, displayTemplate, -190f, out _slotADisplay, out _slotAPh, out _slotAImg);
		_slotBRect = CreateSlot(1, displayTemplate, 190f, out _slotBDisplay, out _slotBPh, out _slotBImg);

		// 中间的「+」
		TextMeshProUGUI plus = CreateText("Plus", "+", 48f,
			new Vector2(0f, 170f), new Vector2(80f, 80f),
			TextAlignmentOptions.Center, false);
		plus.color = new Color(1f, 1f, 1f, 0.5f);

		_slotAText = CreateText("SlotALabel", "主图腾：未选择", 22f,
			new Vector2(-190f, 62f), new Vector2(320f, 32f),
			TextAlignmentOptions.Center, false);
		_slotAText.color = new Color(0.9f, 0.9f, 0.84f, 1f);

		_slotBText = CreateText("SlotBLabel", "材料图腾：未选择", 22f,
			new Vector2(190f, 62f), new Vector2(320f, 32f),
			TextAlignmentOptions.Center, false);
		_slotBText.color = new Color(0.9f, 0.9f, 0.84f, 1f);

		_previewText = CreatePreviewArea(backgroundTemplate);

		_resultText = CreateText("Result", string.Empty, 20f,
			new Vector2(0f, -255f), new Vector2(840f, 56f),
			TextAlignmentOptions.Center, true);
		_resultText.color = new Color(0.85f, 0.95f, 0.85f, 1f);

		_mergeText = CreateText("MergeHint",
			"相同属性：数值相加（+1 与 +2 → +3） · 合成过的图腾不能再参与合成",
			20f, new Vector2(0f, -310f), new Vector2(840f, 28f),
			TextAlignmentOptions.Center, false);
		_mergeText.color = new Color(0.62f, 0.68f, 0.62f, 1f);

		CreateButtons();

		if (_fuseBtn != null)
			_fuseBtn.SetInteractable(false);

		// 每次都挂到新建的根节点上：重建时旧的 Ticker 要等帧末才销毁
		_ticker = _root.AddComponent<Ticker>();

		TotemFusionSettings.Changed -= UpdateSettingsUI;
		TotemFusionSettings.Changed += UpdateSettingsUI;

		if (displayTemplate == null)
			Debug.LogWarning("[TotemFusion] 未找到原版物品格子模板，图腾槽使用兜底样式");

		_root.SetActive(false);
	}

	/// <summary>创建一个图腾槽，返回槽位的判定区域。</summary>
	private static RectTransform CreateSlot(
		int index, ItemDisplay displayTemplate, float x,
		out ItemDisplay display, out TextMeshProUGUI placeholder, out Image catcher)
	{
		RectTransform slotRoot = UiFactory.NewRect(_panel, "Slot" + index);
		UiFactory.Center(slotRoot, new Vector2(x, 170f), new Vector2(SlotSize, SlotSize));

		display = UiFactory.CloneItemDisplay(
			displayTemplate, slotRoot, "Display", new Vector2(SlotSize, SlotSize));

		if (display != null)
			UiFactory.Center(display.transform as RectTransform, Vector2.zero, new Vector2(SlotSize, SlotSize));
		else
		{
			// 找不到原版格子模板时的兜底底板
			Image fallback = UiFactory.NewImage(slotRoot, "Fallback", new Color(0.18f, 0.18f, 0.20f, 1f));
			UiFactory.Stretch(fallback.rectTransform);
		}

		// 空槽提示盖在格子上
		placeholder = CreateTextAt(slotRoot, "Placeholder",
			index == 0 ? "主图腾\n拖入" : "材料\n拖入", 20f,
			Vector2.zero, new Vector2(SlotSize - 16f, SlotSize - 16f),
			TextAlignmentOptions.Center, false);
		placeholder.color = new Color(1f, 1f, 1f, 0.45f);

		// 最上层透明遮罩：接收点击，同时兼作拖拽悬停高亮
		catcher = UiFactory.NewImage(slotRoot, "ClickCatcher", Color.clear);
		UiFactory.Stretch(catcher.rectTransform);
		catcher.raycastTarget = true;

		TotemSlotClick click = catcher.gameObject.AddComponent<TotemSlotClick>();
		int slotIndex = index;
		click.OnClick = () =>
		{
			if (_dragging != null)
				AssignSlot(slotIndex, _dragging);
			else
				ClearSlot(slotIndex);
		};

		return slotRoot;
	}

	/// <summary>详细的合成预览：可滚动的富文本区域。</summary>
	private static TextMeshProUGUI CreatePreviewArea(Image backgroundTemplate)
	{
		RectTransform area = UiFactory.NewRect(_panel, "PreviewArea");
		UiFactory.Center(area, new Vector2(0f, -100f), new Vector2(820f, 240f));

		Image background = UiFactory.CreateBackground(
			backgroundTemplate, area, "Background", new Color(0.07f, 0.08f, 0.10f, 0.6f));
		UiFactory.Stretch(background.rectTransform);
		background.raycastTarget = true;

		RectTransform viewport = UiFactory.NewRect(area, "Viewport");
		UiFactory.Stretch(viewport);
		viewport.offsetMin = new Vector2(16f, 12f);
		viewport.offsetMax = new Vector2(-24f, -12f);
		viewport.gameObject.AddComponent<RectMask2D>();

		// 文本本身作为可滚动内容：TMP 实现 ILayoutElement，配合 ContentSizeFitter 自适应高度
		TextMeshProUGUI text = UiFactory.NewRect(viewport, "PreviewText").gameObject
			.AddComponent<TextMeshProUGUI>();
		text.font = ResolveFont();
		text.fontStyle = _textTemplate != null ? _textTemplate.fontStyle : FontStyles.Normal;
		text.fontSize = 21f;
		text.color = new Color(0.9f, 0.92f, 0.9f, 1f);
		text.alignment = TextAlignmentOptions.TopLeft;
		text.enableWordWrapping = true;
		text.overflowMode = TextOverflowModes.Overflow;
		text.richText = true;
		text.lineSpacing = 1.12f;
		text.raycastTarget = false;
		text.text = string.Empty;

		RectTransform textRect = text.rectTransform;
		textRect.anchorMin = new Vector2(0f, 1f);
		textRect.anchorMax = new Vector2(1f, 1f);
		textRect.pivot = new Vector2(0.5f, 1f);
		textRect.anchoredPosition = Vector2.zero;
		textRect.sizeDelta = Vector2.zero;

		ContentSizeFitter fitter = textRect.gameObject.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		if (_textTemplate != null && _textTemplate.gameObject.layer > 0)
			UiFactory.SetLayerRecursive(textRect.gameObject, _textTemplate.gameObject.layer);

		_previewScroll = area.gameObject.AddComponent<ScrollRect>();
		_previewScroll.content = textRect;
		_previewScroll.viewport = viewport;
		_previewScroll.horizontal = false;
		_previewScroll.vertical = true;
		_previewScroll.movementType = ScrollRect.MovementType.Clamped;
		_previewScroll.scrollSensitivity = 30f;

		_tmpPool.Add(text);
		return text;
	}

	private static void CreateButtons()
	{
		TMP_FontAsset font = ResolveFont();
		FontStyles style = _textTemplate != null ? _textTemplate.fontStyle : FontStyles.Normal;
		int layer = _textTemplate != null && _textTemplate.gameObject.layer > 0
			? _textTemplate.gameObject.layer : 5;

		// 「点击融合」：原版绿色按钮的配色，圆角与阴影由自绘层生成
		ButtonStyle fuseStyle = ButtonStyle.Create();
		fuseStyle.Font = font;
		fuseStyle.FontSize = 30f;
		fuseStyle.FontStyle = style;
		fuseStyle.TextColor = Color.white;
		fuseStyle.FallbackBackground = OriginalButtonGreen;
		fuseStyle.FallbackBorder = OriginalButtonEdge;
		fuseStyle.EdgeShadowColor = Color.clear;
		fuseStyle.OuterShadowColor = new Color(0f, 0f, 0f, 0.6f);
		fuseStyle.Layer = layer;

		// 「点击关闭」：黑底白字
		ButtonStyle closeStyle = ButtonStyle.Create();
		closeStyle.Tint = Color.white;
		closeStyle.Font = font;
		closeStyle.FontSize = 30f;
		closeStyle.FontStyle = style;
		closeStyle.TextColor = Color.white;
		closeStyle.FallbackBackground = new Color(0.07f, 0.07f, 0.08f, 1f);
		closeStyle.FallbackBorder = new Color(0.28f, 0.28f, 0.31f, 1f);
		closeStyle.EdgeShadowColor = Color.clear;
		closeStyle.OuterShadowColor = Color.clear;
		closeStyle.Layer = layer;

		_fuseBtn = CreatePanelButton("FuseButton", "点击融合", fuseStyle, 175f, OnFuse);
		CreatePanelButton("CloseButton", "点击关闭", closeStyle, -175f, Close);
	}

	private static SelfDrawnButton CreatePanelButton(
		string name, string label, ButtonStyle style, float x, Action onClick)
	{
		SelfDrawnButton button = SelfDrawnButton.Create(
			_panel, name, label, new Vector2(320f, 80f), style);

		if (button == null)
			return null;

		RectTransform rect = button.transform as RectTransform;
		if (rect != null)
			UiFactory.Center(rect, new Vector2(x, -380f), new Vector2(320f, 80f));

		button.SetOnClick(onClick);
		return button;
	}

	/// <summary>自绘文本并登记到字体池。</summary>
	private static TextMeshProUGUI CreateText(
		string name, string content, float fontSize,
		Vector2 position, Vector2 size,
		TextAlignmentOptions alignment, bool wrap)
	{
		return CreateTextAt(_panel, name, content, fontSize, position, size, alignment, wrap);
	}

	private static TextMeshProUGUI CreateTextAt(
		Transform parent, string name, string content, float fontSize,
		Vector2 position, Vector2 size,
		TextAlignmentOptions alignment, bool wrap)
	{
		RectTransform rect = UiFactory.NewRect(parent, name);
		TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();

		text.font = ResolveFont();
		text.fontStyle = _textTemplate != null ? _textTemplate.fontStyle : FontStyles.Normal;
		text.fontSize = fontSize;
		text.text = content ?? string.Empty;
		text.color = Color.white;
		text.alignment = alignment;
		text.enableWordWrapping = wrap;
		text.overflowMode = TextOverflowModes.Overflow;
		text.richText = true;
		text.raycastTarget = false;

		UiFactory.Center(rect, position, size);

		if (_textTemplate != null && _textTemplate.gameObject.layer > 0)
			UiFactory.SetLayerRecursive(rect.gameObject, _textTemplate.gameObject.layer);

		_tmpPool.Add(text);
		return text;
	}

	private static TMP_FontAsset ResolveFont()
	{
		if (_textTemplate != null && _textTemplate.font != null)
			return _textTemplate.font;

		TMP_FontAsset gameFont = TotemFusionStyle.GameFont;
		if (gameFont != null)
			return gameFont;

		return TMP_Settings.defaultFontAsset;
	}

	/// <summary>
	/// 按屏幕宽度设置面板的整体缩放，让面板在任何分辨率下都占屏宽 30%。
	///
	/// 面板实际像素宽 = PanelWidth × localScale × CanvasScaler.scaleFactor，
	/// 令它等于 Screen.width × 30% 即可。
	/// </summary>
	private static void ApplyAdaptiveScale()
	{
		if (_panel == null)
			return;

		float canvasScale = GetCanvasScaleFactor();
		float targetPixelWidth = Mathf.Max(1f, Screen.width) * PanelScreenWidthRatio;
		float scale = targetPixelWidth / (PanelWidth * Mathf.Max(0.01f, canvasScale));

		// 异常分辨率兜底，避免面板大到离谱或小到看不见
		scale = Mathf.Clamp(scale, 0.2f, 1.5f);

		_panel.localScale = new Vector3(scale, scale, 1f);

		if (!Mathf.Approximately(scale, _lastAppliedScale))
			_lastAppliedScale = scale;
	}

	/// <summary>
	/// 按 CanvasScaler 的设置计算它实际会用的 scaleFactor。
	/// 不直接读 Canvas.scaleFactor：构建当帧它可能还没刷新。
	/// </summary>
	private static float GetCanvasScaleFactor()
	{
		try
		{
			CanvasScaler scaler = _root != null ? _root.GetComponent<CanvasScaler>() : null;
			if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
			{
				Vector2 reference = scaler.referenceResolution;
				float match = Mathf.Clamp01(scaler.matchWidthOrHeight);
				float widthRatio = Mathf.Max(0.01f, Screen.width / Mathf.Max(1f, reference.x));
				float heightRatio = Mathf.Max(0.01f, Screen.height / Mathf.Max(1f, reference.y));
				return Mathf.Pow(widthRatio, 1f - match) * Mathf.Pow(heightRatio, match);
			}
		}
		catch
		{
			// 读不到就按 1 处理
		}

		return 1f;
	}

	// ---------------------------------------------------------------- 刷新

	private static void UpdateSettingsUI()
	{
		if (_mergeText != null)
			_mergeText.text = "相同属性：数值相加（+1 与 +2 → +3） · 合成过的图腾不能再参与合成";

		UpdatePreview();
	}

	private static void RefreshSlots()
	{
		ApplySlot(0, _selA);
		ApplySlot(1, _selB);
		TickDragHighlight();
		UpdatePreview();
	}

	private static void ApplySlot(int index, Item item)
	{
		bool isMain = index == 0;
		TextMeshProUGUI label = isMain ? _slotAText : _slotBText;
		TextMeshProUGUI placeholder = isMain ? _slotAPh : _slotBPh;
		ItemDisplay display = isMain ? _slotADisplay : _slotBDisplay;

		if (label != null)
			label.text = (isMain ? "主图腾：" : "材料图腾：") +
				(item != null ? TotemFusionLogic.PrettyName(item) : "未选择");

		if (placeholder != null)
			placeholder.gameObject.SetActive(item == null);

		if (display != null)
		{
			try
			{
				display.Setup(item);
			}
			catch (Exception e)
			{
				SafeGuard.Report("刷新图腾格子显示失败", e);
			}
		}
	}

	private static void ClearSlot(int slot)
	{
		if (slot == 0)
			_selA = null;
		else
			_selB = null;

		RefreshSlots();
	}

	private static void UpdatePreview()
	{
		if (_previewText == null)
			return;

		bool ready = _selA != null && _selB != null && _selA != _selB;
		RefreshFuseButton(ready);

		if (!ready)
		{
			SetPreviewText("<color=#8FA7AB>把两个图腾分别放进左右槽位，这里会显示合成结果</color>");
			return;
		}

		TotemFusionLogic.FusePreview preview;
		try
		{
			preview = TotemFusionLogic.Preview(_selA, _selB, mergeSame: true);
		}
		catch (Exception ex)
		{
			preview = new TotemFusionLogic.FusePreview { Error = ex.Message };
		}

		if (preview == null || !string.IsNullOrEmpty(preview.Error))
		{
			SetPreviewText("<color=#E08A8A>" +
				(preview == null ? "预览失败" : preview.Error) + "</color>");
			return;
		}

		StringBuilder sb = new StringBuilder();
		sb.Append("成品：<color=#7FD6C6>").Append(preview.Name).Append("</color>\n");
		sb.Append("属性 ").Append(preview.TotalProps).Append(" 条");
		if (preview.AddedProps > 0)
			sb.Append(" · <color=#8CE27A>新增 ").Append(preview.AddedProps).Append("</color>");
		if (preview.StackedProps > 0)
			sb.Append(" · <color=#F2C46B>合并 ").Append(preview.StackedProps).Append("</color>");
		if (preview.AddedEffects > 0)
			sb.Append(" · 效果 ").Append(preview.TotalEffects)
				.Append("（<color=#8CE27A>新增 ").Append(preview.AddedEffects).Append("</color>）");
		else if (preview.TotalEffects > 0)
			sb.Append(" · 效果 ").Append(preview.TotalEffects).Append(" 条");
		if (preview.CustomTotal > 0)
			sb.Append(" · 其它模组 ").Append(preview.CustomTotal).Append(" 条");
		if (preview.Value > 0)
			sb.Append(" · 价值 ").Append(preview.Value);
		sb.Append('\n');
		if (preview.Added.Count > 0)
			sb.Append("<color=#8CE27A>新增：").Append(JoinTags(preview.Added)).Append("</color>\n");
		if (preview.Stacked.Count > 0)
			sb.Append("<color=#F2C46B>合并：").Append(JoinTags(preview.Stacked)).Append("</color>");

		SetPreviewText(sb.ToString().TrimEnd());
	}

	private static void SetPreviewText(string rich)
	{
		if (_previewText == null)
			return;

		_previewText.text = rich ?? string.Empty;

		// 刷新内容后回到顶部，避免停留在上一件图腾的滚动位置
		if (_previewScroll != null)
			_previewScroll.verticalNormalizedPosition = 1f;
	}

	private static string JoinTags(List<string> list)
	{
		int count = Mathf.Min(list.Count, 6);
		StringBuilder sb = new StringBuilder();

		for (int i = 0; i < count; i++)
		{
			if (i > 0)
				sb.Append(" · ");
			sb.Append(list[i]);
		}

		if (list.Count > count)
			sb.Append(" 等 ").Append(list.Count).Append(" 条");

		return sb.ToString();
	}

	private static void RefreshFuseButton(bool ready)
	{
		_fuseBtn?.SetInteractable(ready);
	}

	private static void OnFuse()
	{
		if (_selA == null || _selB == null)
		{
			if (_resultText != null)
				_resultText.text = "请先选择两个图腾";
			return;
		}

		Item result = TotemFusionLogic.Fuse(_selA, _selB, mergeSame: true, out string error);
		if (result == null)
		{
			if (_resultText != null)
				_resultText.text = "合成失败：" + error;
			return;
		}

		if (!TotemFusionLogic.PlaceFusedItem(result, out string destination, out string err))
		{
			Debug.LogError("[TotemFusion] 合成品放置失败（素材已保留）: " + err);
			try
			{
				Object.Destroy(result.gameObject);
			}
			catch
			{
				// 忽略
			}

			if (_resultText != null)
				_resultText.text = "合成失败：" + err + "，素材已保留";
			return;
		}

		TotemFusionLogic.ForceD2Reapply(result);

		Item selA = _selA;
		Item selB = _selB;
		TotemFusionLogic.EnsureEffects(result, selA, selB);
		TryDestroy(selA);
		TryDestroy(selB);

		if (_resultText != null)
		{
			int effectCount = TotemFusionLogic.CountEffects(result);
			_resultText.text = "合成成功！已放入" + destination + "：" +
				TotemFusionLogic.PrettyName(result) +
				"（属性 " + TotemFusionLogic.PropertyCount(result) + " 条" +
				(effectCount > 0 ? " · 效果 " + effectCount + " 条" : "") +
				" · 已合并同类）\n主槽已清空，合成过的图腾不能再参与合成";
		}

		// 合成完成后自动清空主槽：成品已在 PlaceFusedItem 中放入背包，
		// 不再挂回槽位，避免用户误以为可以继续合成。
		_selA = null;
		_selB = null;
		RefreshSlots();
	}

	private static void TryDestroy(Item item)
	{
		try
		{
			if (item != null)
				ItemTreeExtensions.DestroyTree(item);
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[TotemFusion] 消耗源图腾失败: " + ex);
		}
	}

	// ---------------------------------------------------------------- 拖拽

	private static void HookDragEvents()
	{
		if (_dragHooked)
			return;

		try
		{
			IItemDragSource.OnStartDragItem -= OnStartDragItem;
			IItemDragSource.OnEndDragItem -= OnEndDragItem;
			IItemDragSource.OnStartDragItem += OnStartDragItem;
			IItemDragSource.OnEndDragItem += OnEndDragItem;
			_dragHooked = true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[TotemFusion] 订阅拖拽事件失败，无法从背包拖入图腾: " + ex.Message);
		}
	}

	private static void UnhookDragEvents()
	{
		if (!_dragHooked)
			return;

		try
		{
			IItemDragSource.OnStartDragItem -= OnStartDragItem;
			IItemDragSource.OnEndDragItem -= OnEndDragItem;
		}
		catch
		{
			// 忽略
		}

		_dragHooked = false;
	}

	private static void OnStartDragItem(Item item)
	{
		_dragging = item;
	}

	private static void OnEndDragItem(Item item)
	{
		Item dropped = item ?? _dragging;
		_dragging = null;

		if (dropped == null || _root == null || !_root.activeSelf)
			return;

		if (HitSlot(_slotARect))
			AssignSlot(0, dropped);
		else if (HitSlot(_slotBRect))
			AssignSlot(1, dropped);
	}

	private static bool HitSlot(RectTransform rt)
	{
		if (rt == null)
			return false;

		return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null);
	}

	private static void AssignSlot(int slot, Item item)
	{
		if (item == null)
			return;

		if (TotemFusionLogic.IsFusedTotem(item))
		{
			if (_resultText != null)
				_resultText.text = "「" + TotemFusionLogic.PrettyName(item) + "」已经合成过，不能再次参与合成";
			return;
		}

		if (!TotemFusionLogic.IsFusable(item))
		{
			if (_resultText != null)
				_resultText.text = "「" + TotemFusionLogic.PrettyName(item) + "」没有可合并的属性";
			return;
		}

		if (slot == 0)
		{
			if (_selB == item)
				_selB = null;
			_selA = item;
		}
		else
		{
			if (_selA == item)
				_selA = null;
			_selB = item;
		}

		// 放入后播放一次原版格子的轻微动画，与背包里的手感一致
		ItemDisplay display = slot == 0 ? _slotADisplay : _slotBDisplay;
		if (display != null)
		{
			try
			{
				display.Punch();
			}
			catch
			{
				// 忽略
			}
		}

		RefreshSlots();
	}

	internal static void TickDragHighlight()
	{
		if (_root == null || !_root.activeSelf)
			return;

		bool dragging = _dragging != null;
		Color hot = new Color(1f, 0.85f, 0.35f, 0.22f);

		if (_slotAImg != null)
			_slotAImg.color = dragging && HitSlot(_slotARect) ? hot : Color.clear;

		if (_slotBImg != null)
			_slotBImg.color = dragging && HitSlot(_slotBRect) ? hot : Color.clear;
	}
}
