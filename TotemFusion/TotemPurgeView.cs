using System;
using System.Collections.Generic;
using Duckov.Economy;
using Duckov.UI;
using ItemStatsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TotemFusion;

/// <summary>
/// 图腾词条去除界面。
///
/// 外观与图腾融合工作台一致：居中面板、标题 + 横杠、原版背包格子样式的图腾槽、
/// 可滚动词条列表、底部自绘圆角按钮；面板大小自适应且可以拖动。
/// </summary>
public static class TotemPurgeView
{
	internal class Ticker : MonoBehaviour
	{
		private void Update()
		{
			try
			{
				TotemPurgeView.TickDragHighlight();
			}
			catch
			{
				// 忽略
			}

			try
			{
				if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
				{
					Input.ResetInputAxes();
					Close();
				}
			}
			catch
			{
				// 忽略
			}
		}
	}

	private static GameObject _root;

	private static RectTransform _panel;

	private static TMP_Text _textTemplate;

	private static Item _sel;

	private static ItemDisplay _slotDisplay;

	private static TextMeshProUGUI _slotText;

	private static TextMeshProUGUI _slotPh;

	private static Image _slotImg;

	private static RectTransform _slotRect;

	private static TextMeshProUGUI _balanceText;

	private static TextMeshProUGUI _resultText;

	private static Transform _listContent;

	private static ScrollRect _listScroll;

	private static Item _dragging;

	private static bool _dragHooked;

	private static bool _healed;

	private static Ticker _ticker;

	private const float SlotSize = 150f;

	private const float PanelWidth = 900f;

	private const float PanelHeight = 860f;

	/// <summary>面板占屏幕宽度的比例，与图腾融合界面一致。</summary>
	private const float PanelScreenWidthRatio = 0.3f;

	/// <summary>原版右键菜单按钮的底色。</summary>
	private static readonly Color OriginalButtonGreen =
		new Color(0.4314703f, 0.9056604f, 0.4833889f, 1f);

	/// <summary>原版按钮描边的深绿色。</summary>
	private static readonly Color OriginalButtonEdge =
		new Color(0.1012371f, 0.3301887f, 0.16086f, 1f);

	/// <summary>「去除」按钮的红色。</summary>
	private static readonly Color RemoveButtonRed =
		new Color(0.55f, 0.2f, 0.17f, 1f);

	private static readonly Color RemoveButtonEdge =
		new Color(0.28f, 0.08f, 0.07f, 1f);

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
				TotemFusionLogic.RegisterMadeOverrides();
			}

			TotemFusionLogic.RepairFused();

			// 新界面要克隆原版背包格子（ItemDisplay）作为模板，所以先打开背包
			try
			{
				InventoryView.Show();
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[TotemFusion] 打开背包失败: " + ex.Message);
			}

			if (_root == null || _slotDisplay == null)
			{
				if (_root != null)
				{
					_root.SetActive(false);
					Object.Destroy(_root);
					_root = null;
				}
				BuildRoot();
			}

			if (_root == null)
				return;

			HookDragEvents();
			_sel = null;
			SetResult(string.Empty);

			_root.SetActive(true);
			ApplyAdaptiveScale();
			RefreshSlots();
			RefreshBalance();
			RefreshFont();
		}
		catch (Exception ex2)
		{
			Debug.LogError("[TotemFusion] 打开去除界面失败: " + ex2);
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
			"TotemPurgePanel",
			typeof(RectTransform),
			typeof(Canvas),
			typeof(CanvasScaler),
			typeof(GraphicRaycaster));
		Object.DontDestroyOnLoad(_root);

		Canvas canvas = _root.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 31999;

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

		PanelDragHandler drag = panelBackground.gameObject.AddComponent<PanelDragHandler>();
		drag.Configure(_panel);

		CreateText("Title", "图腾词条去除", 44f,
			new Vector2(0f, 365f), new Vector2(860f, 70f),
			TextAlignmentOptions.Center, false);

		// 标题下的原版画风横杠
		UiFactory.CreateTitleDivider(
			dividerTemplate, _panel, "TotemFusionTitleDivider",
			new Vector2(0f, 322f), new Vector2(620f, 2f));

		CreateSlot(displayTemplate);

		_slotText = CreateText("SlotLabel", "图腾：未放入", 22f,
			new Vector2(0f, 105f), new Vector2(600f, 32f),
			TextAlignmentOptions.Center, false);
		_slotText.color = new Color(0.9f, 0.9f, 0.84f, 1f);

		_balanceText = CreateText("Balance", string.Empty, 20f,
			new Vector2(0f, 70f), new Vector2(600f, 28f),
			TextAlignmentOptions.Center, false);
		_balanceText.color = new Color(0.62f, 0.68f, 0.62f, 1f);

		CreateList(backgroundTemplate);

		_resultText = CreateText("Result", string.Empty, 20f,
			new Vector2(0f, -276f), new Vector2(840f, 36f),
			TextAlignmentOptions.Center, true);
		_resultText.color = new Color(0.85f, 0.95f, 0.85f, 1f);

		CreateButtons();

		// 每次都挂到新建的根节点上：重建时旧的 Ticker 要等帧末才销毁
		_ticker = _root.AddComponent<Ticker>();

		if (displayTemplate == null)
			Debug.LogWarning("[TotemFusion] 未找到原版物品格子模板，图腾槽使用兜底样式");

		_root.SetActive(false);
	}

	private static void CreateSlot(ItemDisplay displayTemplate)
	{
		_slotRect = UiFactory.NewRect(_panel, "Slot");
		UiFactory.Center(_slotRect, new Vector2(0f, 205f), new Vector2(SlotSize, SlotSize));

		_slotDisplay = UiFactory.CloneItemDisplay(
			displayTemplate, _slotRect, "Display", new Vector2(SlotSize, SlotSize));

		if (_slotDisplay != null)
		{
			UiFactory.Center(_slotDisplay.transform as RectTransform,
				Vector2.zero, new Vector2(SlotSize, SlotSize));
		}
		else
		{
			// 找不到原版格子模板时的兜底底板
			Image fallback = UiFactory.NewImage(_slotRect, "Fallback", new Color(0.18f, 0.18f, 0.20f, 1f));
			UiFactory.Stretch(fallback.rectTransform);
		}

		_slotPh = CreateTextAt(_slotRect, "Placeholder", "图腾\n拖入", 20f,
			Vector2.zero, new Vector2(SlotSize - 16f, SlotSize - 16f),
			TextAlignmentOptions.Center, false);
		_slotPh.color = new Color(1f, 1f, 1f, 0.45f);

		// 最上层透明遮罩：接收点击，同时兼作拖拽悬停高亮
		_slotImg = UiFactory.NewImage(_slotRect, "ClickCatcher", Color.clear);
		UiFactory.Stretch(_slotImg.rectTransform);
		_slotImg.raycastTarget = true;

		TotemSlotClick click = _slotImg.gameObject.AddComponent<TotemSlotClick>();
		click.OnClick = () =>
		{
			if (_dragging != null)
				AssignSlot(_dragging);
			else
			{
				_sel = null;
				RefreshSlots();
			}
		};
	}

	private static void CreateList(Image backgroundTemplate)
	{
		RectTransform listArea = UiFactory.NewRect(_panel, "ListArea");
		UiFactory.Center(listArea, new Vector2(0f, -105f), new Vector2(820f, 290f));

		Image background = UiFactory.CreateBackground(
			backgroundTemplate, listArea, "Background", new Color(0.07f, 0.08f, 0.10f, 0.6f));
		UiFactory.Stretch(background.rectTransform);
		background.raycastTarget = true;

		RectTransform viewport = UiFactory.NewRect(listArea, "Viewport");
		UiFactory.Stretch(viewport);
		viewport.offsetMin = new Vector2(12f, 10f);
		viewport.offsetMax = new Vector2(-12f, -10f);
		viewport.gameObject.AddComponent<RectMask2D>();

		RectTransform content = UiFactory.NewRect(viewport, "Content");
		content.anchorMin = new Vector2(0f, 1f);
		content.anchorMax = new Vector2(1f, 1f);
		content.pivot = new Vector2(0.5f, 1f);
		content.anchoredPosition = Vector2.zero;
		content.sizeDelta = Vector2.zero;

		VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
		layout.spacing = 4f;
		layout.padding = new RectOffset(4, 4, 4, 4);
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		layout.childAlignment = TextAnchor.UpperCenter;

		ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		_listScroll = listArea.gameObject.AddComponent<ScrollRect>();
		_listScroll.content = content;
		_listScroll.viewport = viewport;
		_listScroll.horizontal = false;
		_listScroll.vertical = true;
		_listScroll.movementType = ScrollRect.MovementType.Clamped;
		_listScroll.scrollSensitivity = 30f;

		_listContent = content;
	}

	private static void CreateButtons()
	{
		ButtonStyle style = ButtonStyle.Create();
		style.Font = ResolveFont();
		style.FontSize = 30f;
		style.FontStyle = _textTemplate != null ? _textTemplate.fontStyle : FontStyles.Normal;
		style.TextColor = Color.white;
		style.FallbackBackground = new Color(0.07f, 0.07f, 0.08f, 1f);
		style.FallbackBorder = new Color(0.28f, 0.28f, 0.31f, 1f);
		style.EdgeShadowColor = Color.clear;
		style.OuterShadowColor = Color.clear;
		style.Layer = _textTemplate != null && _textTemplate.gameObject.layer > 0
			? _textTemplate.gameObject.layer : 5;

		SelfDrawnButton button = SelfDrawnButton.Create(
			_panel, "CloseButton", "点击关闭", new Vector2(320f, 80f), style);

		if (button == null)
			return;

		RectTransform rect = button.transform as RectTransform;
		if (rect != null)
			UiFactory.Center(rect, new Vector2(0f, -365f), new Vector2(320f, 80f));

		button.SetOnClick(Close);
	}

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

	private static void ApplyAdaptiveScale()
	{
		if (_panel == null)
			return;

		float canvasScale = GetCanvasScaleFactor();
		float targetPixelWidth = Mathf.Max(1f, Screen.width) * PanelScreenWidthRatio;
		float scale = targetPixelWidth / (PanelWidth * Mathf.Max(0.01f, canvasScale));
		scale = Mathf.Clamp(scale, 0.2f, 1.5f);

		_panel.localScale = new Vector3(scale, scale, 1f);

		if (!Mathf.Approximately(scale, _lastAppliedScale))
			_lastAppliedScale = scale;
	}

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

	private static void RefreshBalance()
	{
		if (_balanceText == null)
			return;

		try
		{
			long cash = EconomyManager.Cash;
			long money = 0L;
			try
			{
				if (LevelConfig.SaveCharacter)
					money = EconomyManager.Money;
			}
			catch
			{
				// 忽略
			}

			_balanceText.text = "现金 " + cash + (money > 0 ? " · 存款 " + money : "");
		}
		catch (Exception ex)
		{
			_balanceText.text = string.Empty;
			Debug.LogWarning("[TotemFusion] 读取余额失败: " + ex.Message);
		}
	}

	private static void RefreshSlots()
	{
		if (_slotText != null)
			_slotText.text = "图腾：" + (IsTotem() ? TotemFusionLogic.PrettyName(_sel) : "未放入");

		if (_slotPh != null)
			_slotPh.gameObject.SetActive(!IsTotem());

		if (_slotDisplay != null)
		{
			try
			{
				_slotDisplay.Setup(IsTotem() ? _sel : null);
			}
			catch (Exception e)
			{
				SafeGuard.Report("刷新图腾格子显示失败", e);
			}
		}

		TickDragHighlight();
		RefreshList();
	}

	private static bool IsTotem()
	{
		return _sel != null && TotemFusionLogic.IsTotem(_sel);
	}

	private static void RefreshList()
	{
		if (_listContent == null)
			return;

		try
		{
			for (int i = _listContent.childCount - 1; i >= 0; i--)
				Object.Destroy(_listContent.GetChild(i).gameObject);

			if (!IsTotem())
			{
				AddRow("把图腾拖入方框后，这里会列出它的全部词条", 0, false);
			}
			else
			{
				List<TotemFusionLogic.PurgeEntry> entries = TotemFusionLogic.ListAffixes(_sel);
				if (entries == null || entries.Count == 0)
				{
					AddRow("该图腾没有任何可去除的词条。", 0, false);
				}
				else
				{
					for (int i = 0; i < entries.Count; i++)
					{
						TotemFusionLogic.PurgeEntry entry = entries[i];
						AddRow(entry.label + "   花费 " + entry.cost, i, true);
					}
				}
			}

			if (_listScroll != null)
				_listScroll.verticalNormalizedPosition = 1f;
		}
		catch (Exception e)
		{
			SafeGuard.Report("刷新词条列表失败", e);
		}
	}

	/// <summary>添加一行词条：左侧文字，右侧「去除」圆角按钮。</summary>
	private static void AddRow(string label, int index, bool removable)
	{
		RectTransform row = UiFactory.NewRect(_listContent, "Row" + index);
		row.sizeDelta = new Vector2(0f, 48f);

		LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
		rowLayout.preferredHeight = 48f;
		rowLayout.flexibleWidth = 1f;

		Image rowBackground = row.gameObject.AddComponent<Image>();
		rowBackground.color = new Color(1f, 1f, 1f, removable ? 0.05f : 0.025f);
		rowBackground.raycastTarget = false;

		HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
		layout.spacing = 10f;
		layout.padding = new RectOffset(14, 10, 4, 4);
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = false;
		layout.childForceExpandHeight = false;
		layout.childAlignment = TextAnchor.MiddleLeft;

		RectTransform labelRect = UiFactory.NewRect(row, "Label");
		LayoutElement labelLayout = labelRect.gameObject.AddComponent<LayoutElement>();
		// 不写 preferredWidth（TMP 不换行时会把整行文字宽度报成首选宽度），
		// 交给 flexibleWidth 占满「去除」按钮之外的剩余空间
		labelLayout.minWidth = 0f;
		labelLayout.preferredWidth = 0f;
		labelLayout.flexibleWidth = 1f;
		labelLayout.preferredHeight = 40f;

		TextMeshProUGUI text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
		text.font = ResolveFont();
		text.fontStyle = _textTemplate != null ? _textTemplate.fontStyle : FontStyles.Normal;
		text.fontSize = 22f;
		text.text = label;
		text.color = new Color(0.9f, 0.9f, 0.86f, 1f);
		text.alignment = TextAlignmentOptions.MidlineLeft;
		text.enableWordWrapping = false;
		text.overflowMode = TextOverflowModes.Overflow;
		text.richText = true;
		text.raycastTarget = false;
		_tmpPool.Add(text);

		if (_textTemplate != null && _textTemplate.gameObject.layer > 0)
			UiFactory.SetLayerRecursive(labelRect.gameObject, _textTemplate.gameObject.layer);

		if (!removable)
			return;

		ButtonStyle style = ButtonStyle.Create();
		style.Font = ResolveFont();
		style.FontSize = 22f;
		style.FontStyle = _textTemplate != null ? _textTemplate.fontStyle : FontStyles.Normal;
		style.TextColor = Color.white;
		style.FallbackBackground = RemoveButtonRed;
		style.FallbackBorder = RemoveButtonEdge;
		style.EdgeShadowColor = Color.clear;
		style.OuterShadowColor = Color.clear;
		style.Layer = _textTemplate != null && _textTemplate.gameObject.layer > 0
			? _textTemplate.gameObject.layer : 5;

		SelfDrawnButton button = SelfDrawnButton.Create(
			row, "RemoveButton", "去除", new Vector2(96f, 40f), style);

		if (button == null)
			return;

		LayoutElement buttonLayout = button.gameObject.AddComponent<LayoutElement>();
		buttonLayout.preferredWidth = 96f;
		buttonLayout.preferredHeight = 40f;
		buttonLayout.flexibleWidth = 0f;

		int idx = index;
		button.SetOnClick(() => OnRemove(idx));
	}

	private static void OnRemove(int index)
	{
		if (!IsTotem())
		{
			SetResult("请先放入一个图腾");
			return;
		}

		List<TotemFusionLogic.PurgeEntry> entries = TotemFusionLogic.ListAffixes(_sel);
		if (entries == null || index < 0 || index >= entries.Count)
			return;

		TotemFusionLogic.PurgeEntry entry = entries[index];

		long money = EconomyManager.Cash;
		try
		{
			if (LevelConfig.SaveCharacter)
				money += EconomyManager.Money;
		}
		catch
		{
			// 忽略
		}

		if (money < entry.cost)
		{
			SetResult("现金/存款不足，需要 " + entry.cost + "，当前 " + money);
			return;
		}

		bool paid = false;
		try
		{
			paid = EconomyManager.Pay(new Cost(entry.cost), LevelConfig.SaveCharacter, true);
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[TotemFusion] 扣费失败: " + ex.Message);
		}

		if (!paid)
		{
			SetResult("扣费失败，词条未去除");
			return;
		}

		if (!TotemFusionLogic.RemoveAffix(_sel, entry))
		{
			SetResult("去除失败，费用已回退");
			try
			{
				EconomyManager.Pay(new Cost(-entry.cost), LevelConfig.SaveCharacter, true);
			}
			catch
			{
				// 忽略
			}
			return;
		}

		SetResult("已去除：" + entry.label);
		RefreshSlots();
		RefreshBalance();
	}

	private static void SetResult(string msg)
	{
		if (_resultText != null)
			_resultText.text = msg ?? string.Empty;
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
			Debug.LogWarning("[TotemFusion] 订阅拖拽失败: " + ex.Message);
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

		if (HitSlot(_slotRect))
			AssignSlot(dropped);
	}

	private static bool HitSlot(RectTransform rt)
	{
		if (rt == null)
			return false;

		return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null);
	}

	private static void AssignSlot(Item item)
	{
		if (item == null)
			return;

		if (!TotemFusionLogic.IsTotem(item))
		{
			SetResult("「" + TotemFusionLogic.PrettyName(item) + "」不是图腾");
			return;
		}

		_sel = item;

		if (_slotDisplay != null)
		{
			try
			{
				_slotDisplay.Punch();
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

		if (_slotImg != null)
		{
			_slotImg.color = _dragging != null && HitSlot(_slotRect)
				? new Color(1f, 0.85f, 0.35f, 0.22f)
				: Color.clear;
		}
	}
}
