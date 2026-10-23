using Object = UnityEngine.Object;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Duckov.Utilities;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using ItemStatsSystem.Stats;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TotemFusion;

public static class TotemFusionLogic
{
	public enum PurgeKind
	{
		Pos,
		Neg,
		Vanilla,
		Effect
	}

	public class PurgeEntry
	{
		public PurgeKind kind;

		public int index;

		public string label = "";

		public float strength;

		public long cost;
	}

	private struct AffixEntry
	{
		public string id;

		public string key;

		public string mod;

		public float val;
	}

	[Serializable]
	public class D2AffixJson
	{
		public string id;

		public string statKey;

		public float value;

		public string modifier;
	}

	[Serializable]
	public class D2MetaJson
	{
		public string rarityId;

		public int quality;

		public int displayQuality;

		public string displayName;

		public string remark;

		public int value;

		public int typeId;

		public D2AffixJson[] positives;

		public D2AffixJson[] negatives;
	}

	[Serializable]
	public class VanillaModJson
	{
		public string key;

		public string type;

		public float value;

		public string target;

		public bool inv;

		public bool display;

		public bool oo;

		public int order;

		public bool stacked;
	}

	[Serializable]
	public class VanillaModList
	{
		public VanillaModJson[] mods;
	}

	private class D2Sig
	{
		public string key;

		public ModifierType type;

		public float val;
	}

	public sealed class FusePreview
	{
		public string Name = "";

		public int TotalProps;

		public int AddedProps;

		public int StackedProps;

		public int TotalEffects;

		public int AddedEffects;

		public int CustomTotal;

		public int Value;

		public List<string> Added = new List<string>();

		public List<string> Stacked = new List<string>();

		public string Error;
	}

	private class VmRow
	{
		public string key;

		public string type;

		public float value;
	}

	public const string ModTag = "TotemFusionMod";

	private const string KMeta = "D2T_Meta";

	private const string KPosCount = "D2T_PosCount";

	private const string KNegCount = "D2T_NegCount";

	private const string KTypeId = "D2T_TypeID";

	private const string KName = "D2T_Name";

	private const string KQuality = "D2T_Quality";

	private const string KDisplayQuality = "D2T_DisplayQuality";

	private const string KRarity = "D2T_Rarity";

	private const string KDesc = "D2T_Desc";

	private const string KSaleValue = "D2T_SaleValue";

	public const string MadeKey = "TotemFusion_Made";

	private const string KMadeBy = "TotemFusion_Made";

	private const string KSourceIds = "TotemFusion_Src";

	private const string KRemovedEffects = "TotemFusion_RemovedFX";

	private const string KMadeDesc = "TotemFusion_Desc";

	private const string KMadeName = "TotemFusion_Name";

	private const string KVanMods = "TotemFusion_Van";

	private const string KModSnap = "TotemFusion_ModSnap";

	private const string KPrice = "TotemFusion_Price";

	private const string KPosId = "D2T_Pos_{0}_id";

	private const string KPosKey = "D2T_Pos_{0}_key";

	private const string KPosVal = "D2T_Pos_{0}_val";

	private const string KPosMod = "D2T_Pos_{0}_mod";

	private const string KNegId = "D2T_Neg_{0}_id";

	private const string KNegKey = "D2T_Neg_{0}_key";

	private const string KNegVal = "D2T_Neg_{0}_val";

	private const string KNegMod = "D2T_Neg_{0}_mod";

	private static readonly HashSet<int> VanillaTotemTypeIds = new HashSet<int>
	{
		319, 320, 321, 322, 323, 324, 325, 370, 371, 372,
		947, 951, 954, 955, 956, 961, 962, 963, 965, 966,
		969, 970, 971, 972, 974, 975, 976, 979, 980, 982,
		984, 985, 986, 987, 989, 990, 991, 993
	};

	private static readonly HashSet<string> SystemVarKeys = new HashSet<string>(StringComparer.Ordinal) { "Count", "ItemFromInfo", "Inspected", "MaxDurability", "Durability", "DurabilityLoss", "Item_ID", "InstanceId", "IsNew", "UID" };

	private static Type _locType;

	private static MethodInfo _locSetOverride;

	private static bool _locResolved;

	private static MethodInfo _locGetPlain;

	private static readonly Dictionary<string, string> PlainTextCache = new Dictionary<string, string>();

	public const string ModTagRemove = "TotemFusionPurge";

	public const int PurgeBasePrice = 200;

	public const float PurgePricePerStrength = 40f;

	public const int PurgeEffectPrice = 1500;

	private static readonly Dictionary<string, string> StatCN = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{ "BodyArmor", "护甲" },
		{ "HeadArmor", "头部护甲" },
		{ "CritDamageFactor", "暴击伤害" },
		{ "StormProtection", "风暴防护" },
		{ "InventoryCapacity", "背包容量" },
		{ "MaxWeight", "最大负重" },
		{ "Stamina", "耐力" },
		{ "FishingQualityFactor", "钓鱼品质" },
		{ "FishingTime", "钓鱼耗时" },
		{ "SenseRange", "感知范围" },
		{ "ViewAngle", "视场角度" },
		{ "ViewDistance", "视野距离" },
		{ "SoundVisable", "脚步声响" },
		{ "HearingAbility", "听觉能力" },
		{ "Moveability", "机动性" },
		{ "WaterCost", "饮水消耗" },
		{ "EnergyCost", "能量消耗" },
		{ "RecoilControl", "后坐力控制" },
		{ "GunCritDamageGain", "枪械暴击伤害" },
		{ "GunScatterMultiplier", "枪械散射" },
		{ "GunDamageMultiplier", "枪械伤害" },
		{ "GunDistanceMultiplier", "枪械射程" },
		{ "ElementFactor_Electricity", "雷电元素" },
		{ "ElementFactor_Fire", "火焰元素" },
		{ "ElementFactor_Space", "空间元素" },
		{ "ElementFactor_Poison", "毒素元素" },
		{ "ElementFactor_Physics", "物理元素" },
		{ "MaxHealth", "最大生命" },
		{ "MeleeCritRateGain", "近战暴击率" },
		{ "StaminaDrainRate", "耐力消耗" },
		{ "RunSpeed", "奔跑速度" },
		{ "WalkSpeed", "行走速度" },
		{ "RunSoundRange", "奔跑声响" },
		{ "WalkSoundRange", "行走声响" },
		{ "FoodGain", "食物获取" },
		{ "HealGain", "治疗获取" },
		{ "MeleeDamageMultiplier", "近战伤害" },
		{ "StaminaRecoverRate", "耐力恢复" },
		{ "MeleeCritDamageGain", "近战暴击伤害" },
		{ "ReloadSpeedGain", "换弹速度" },
		{ "D2T_ArmorBreakGain", "破甲增益" },
		{ "D2T_ArmorPiercingGain", "穿甲增益" },
		{ "D2T_ExplosionDamageMultiplier", "爆炸伤害倍率" },
		{ "D2T_BulletExplosionDamageGain", "子弹爆炸伤害" },
		{ "D2T_BulletExplosionRangeGain", "子弹爆炸范围" },
		{ "D2T_PenetrateGain", "贯穿增益" },
		{ "D2T_SoundRangeMultiplier", "声音距离倍率" },
		{ "D2T_AdsTimeMultiplier", "瞄准时间倍率" },
		{ "D2T_ShotCountGain", "多重射击弹头数" },
		{ "D2T_ShotAngleGain", "多重射击角度" },
		{ "Damage", "伤害" },
		{ "Defense", "防御" },
		{ "Armor", "护甲" },
		{ "HealthRegen", "生命恢复" },
		{ "MoveSpeed", "移动速度" },
		{ "AttackSpeed", "攻击速度" },
		{ "CritChance", "暴击几率" },
		{ "CritDamage", "暴击伤害" },
		{ "LifeSteal", "生命偷取" },
		{ "Dodge", "闪避" },
		{ "Block", "格挡" },
		{ "ResistFire", "火焰抗性" },
		{ "ResistCold", "冰冷抗性" },
		{ "ResistLightning", "闪电抗性" },
		{ "ResistPoison", "毒素抗性" },
		{ "AllResist", "全抗性" },
		{ "PickupRange", "拾取范围" },
		{ "LightRadius", "光照范围" },
		{ "GoldFind", "金币获取" },
		{ "MagicFind", "魔法获取" },
		{ "XPBoost", "经验加成" },
		{ "Thorns", "反伤" },
		{ "HPRegen", "生命恢复" },
		{ "MaxStamina", "最大耐力" },
		{ "MaxMana", "最大法力" },
		{ "ManaRegen", "法力恢复" }
	};

	private static readonly HashSet<string> _runtimeRefFields = new HashSet<string>(StringComparer.Ordinal)
	{
		"_mainControl", "master", "selfBuff", "owner", "target", "source", "_owner", "gameObject", "buff", "fx",
		"buffs", "transform", "parent", "instanceID", "tag", "name", "_target", "_source", "originator", "instigator",
		"caster", "_caster"
	};

	private static readonly HashSet<string> TierRemarks = new HashSet<string>(StringComparer.Ordinal)
	{
		"普通", "优秀", "精良", "卓越", "稀有", "史诗", "传说", "神话", "完美", "神圣",
		"非凡", "不朽", "独特", "远古", "太古", "传奇"
	};

	private static Type _itemUtilitiesType;

	private static bool _itemUtilitiesResolved;

	private const char KVAN_SEP = '|';

	private const float ValueBonusOfMin = 0.5f;

	public const int MaxPreviewTags = 6;

	private static readonly Dictionary<string, int> AffixCategoryOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
	{
		{ "Damage", 0 },
		{ "Defense", 0 },
		{ "Armor", 0 },
		{ "BodyArmor", 0 },
		{ "HeadArmor", 0 },
		{ "MaxHealth", 0 },
		{ "HealthRegen", 0 },
		{ "HPRegen", 0 },
		{ "MaxStamina", 0 },
		{ "MaxMana", 0 },
		{ "ManaRegen", 0 },
		{ "CritChance", 1 },
		{ "CritDamage", 1 },
		{ "CritDamageFactor", 1 },
		{ "MeleeCritRateGain", 1 },
		{ "MeleeCritDamageGain", 1 },
		{ "GunCritDamageGain", 1 },
		{ "ElementFactor_Fire", 2 },
		{ "ElementFactor_Electricity", 2 },
		{ "ElementFactor_Poison", 2 },
		{ "ElementFactor_Physics", 2 },
		{ "ElementFactor_Space", 2 },
		{ "ResistFire", 2 },
		{ "ResistCold", 2 },
		{ "ResistLightning", 2 },
		{ "ResistPoison", 2 },
		{ "AllResist", 2 },
		{ "StormProtection", 2 },
		{ "RunSpeed", 3 },
		{ "WalkSpeed", 3 },
		{ "MoveSpeed", 3 },
		{ "Moveability", 3 },
		{ "MaxWeight", 3 },
		{ "Stamina", 3 },
		{ "StaminaDrainRate", 3 },
		{ "StaminaRecoverRate", 3 },
		{ "GunDamageMultiplier", 4 },
		{ "GunDistanceMultiplier", 4 },
		{ "GunScatterMultiplier", 4 },
		{ "ReloadSpeedGain", 4 },
		{ "RecoilControl", 4 },
		{ "D2T_PenetrateGain", 4 },
		{ "D2T_ShotCountGain", 4 },
		{ "D2T_ShotAngleGain", 4 },
		{ "D2T_ArmorPiercingGain", 4 },
		{ "D2T_ArmorBreakGain", 4 },
		{ "D2T_ExplosionDamageMultiplier", 4 },
		{ "D2T_BulletExplosionDamageGain", 4 },
		{ "D2T_BulletExplosionRangeGain", 4 },
		{ "D2T_SoundRangeMultiplier", 4 },
		{ "D2T_AdsTimeMultiplier", 4 },
		{ "MeleeDamageMultiplier", 5 },
		{ "SenseRange", 6 },
		{ "ViewAngle", 6 },
		{ "ViewDistance", 6 },
		{ "HearingAbility", 6 },
		{ "SoundVisable", 6 },
		{ "RunSoundRange", 6 },
		{ "WalkSoundRange", 6 },
		{ "LightRadius", 6 },
		{ "InventoryCapacity", 7 },
		{ "PickupRange", 7 },
		{ "GoldFind", 7 },
		{ "MagicFind", 7 },
		{ "XPBoost", 7 },
		{ "FoodGain", 7 },
		{ "HealGain", 7 },
		{ "AttackSpeed", 8 },
		{ "Dodge", 8 },
		{ "Block", 8 },
		{ "LifeSteal", 8 },
		{ "Thorns", 8 },
		{ "FishingQualityFactor", 8 },
		{ "FishingTime", 8 },
		{ "WaterCost", 8 },
		{ "EnergyCost", 8 }
	};

	private static readonly Dictionary<string, string> BuffLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{ "BurnResist", "免疫点燃" },
		{ "PoisonResist", "免疫中毒" },
		{ "BleedResist", "免疫流血" },
		{ "ShockResist", "免疫电击" },
		{ "FreezeResist", "免疫冰冻" },
		{ "SlowResist", "免疫减速" },
		{ "Buff_Burn", "燃烧" },
		{ "Buff_Poison", "中毒" }
	};

	private static readonly HashSet<string> DefaultObjNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "New Effect", "New Game Object", "GameObject", "Game Object", "Effect", "Effects", "Canvas", "Image", "Text", "New Sprite" };

	private static readonly HashSet<string> UselessLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"未定义", "未定义描述", "undefined", "null", "none", "missing", "未知", "??", "<null>", "无",
		"无效果", "错误", "error"
	};

	private static readonly HashSet<string> InternalObjNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"character", "player", "target", "root", "origin", "parent", "owner", "self", "holder", "anchor",
		"transform", "gameobject", "instance", "visual", "effect", "effects"
	};

	private static readonly HashSet<string> FusionPlaceholderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"character", "player", "target", "root", "origin", "parent", "owner", "self", "holder", "anchor",
		"transform", "gameobject", "instance", "visual"
	};

	private static FieldInfo _groupField;

	private static bool IsMergeableVarKey(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}
		if (key.StartsWith("D2T_", StringComparison.Ordinal))
		{
			return false;
		}
		if (SystemVarKeys.Contains(key))
		{
			return false;
		}
		return true;
	}

	public static int CountCustomVars(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return 0;
		}
		try
		{
			CustomDataCollection variables = item.Variables;
			if (variables == null)
			{
				return 0;
			}
			int num = 0;
			foreach (CustomData item2 in variables)
			{
				if (item2 != null && IsMergeableVarKey(item2.Key))
				{
					num++;
				}
			}
			return num;
		}
		catch
		{
			return 0;
		}
	}

	/// <summary>是否为「图腾合成」产出过的合成图腾（由 TotemFusion_Made 标记）。</summary>
	public static bool IsFusedTotem(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return false;
		}
		try
		{
			return item.GetString(MadeKey, (string)null) == "1";
		}
		catch
		{
			return false;
		}
	}

	public static bool IsFusable(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return false;
		}
		// 限制版规则：合成过的图腾不能再参与合成
		if (IsFusedTotem(item))
		{
			return false;
		}
		if (IsTotem(item))
		{
			return true;
		}
		try
		{
			if (CountVanillaModifiers(item) > 0)
			{
				return true;
			}
		}
		catch
		{
		}
		try
		{
			if (CountCustomVars(item) > 0)
			{
				return true;
			}
		}
		catch
		{
		}
		try
		{
			if (CountEffects(item) > 0)
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	private static string StripStars(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		int i = 0;
		int num;
		for (num = s.Length - 1; i <= num && s[i] == '*'; i++)
		{
		}
		while (num >= i && s[num] == '*')
		{
			num--;
		}
		if (i <= num)
		{
			return s.Substring(i, num - i + 1).Trim();
		}
		return "";
	}

	private static bool LooksLikeKey(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return false;
		}
		foreach (char c in s)
		{
			switch (c)
			{
			case '-':
			case '.':
			case '_':
			case 'a':
			case 'b':
			case 'c':
			case 'd':
			case 'e':
			case 'f':
			case 'g':
			case 'h':
			case 'i':
			case 'j':
			case 'k':
			case 'l':
			case 'm':
			case 'n':
			case 'o':
			case 'p':
			case 'q':
			case 'r':
			case 's':
			case 't':
			case 'u':
			case 'v':
			case 'w':
			case 'x':
			case 'y':
			case 'z':
				continue;
			}
			if ((c < 'A' || c > 'Z') && (c < '0' || c > '9'))
			{
				return false;
			}
		}
		return true;
	}

	private static string CleanName(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return "";
		}
		string text = null;
		try
		{
			text = StripStars(item.DisplayName);
		}
		catch
		{
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			try
			{
				text = StripStars(item.DisplayNameRaw);
			}
			catch
			{
			}
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			return "";
		}
		text = text.Trim();
		if (text.StartsWith("D2T_") || text.StartsWith("Item_"))
		{
			string text2 = null;
			try
			{
				text2 = StripStars(item.DisplayNameRaw);
			}
			catch
			{
			}
			text = ((string.IsNullOrWhiteSpace(text2) || text2.StartsWith("D2T_") || text2.StartsWith("Item_")) ? "图腾" : text2.Trim());
		}
		return text;
	}

	public static void HealStarNames()
	{
		try
		{
			List<Item> list = ItemUtilities.FindAllBelongsToPlayer((Predicate<Item>)((Item it) => (Object)(object)it != (Object)null));
			if (list == null)
			{
				return;
			}
			int num = 0;
			foreach (Item item in list)
			{
				if ((Object)(object)item == (Object)null)
				{
					continue;
				}
				string text = null;
				try
				{
					text = item.DisplayNameRaw;
				}
				catch
				{
				}
				if (string.IsNullOrEmpty(text) || text.IndexOf('*') < 0)
				{
					continue;
				}
				string text2 = StripStars(text);
				if (!string.IsNullOrEmpty(text2))
				{
					try
					{
						item.DisplayNameRaw = text2;
					}
					catch
					{
					}
					RegisterNameOverride(text2);
					num++;
				}
			}
			if (num > 0)
			{
				Debug.Log((object)("[TotemFusion] 已修复 " + num + " 个名字带星号的物品"));
			}
		}
		catch
		{
		}
	}

	private static Item[] GetPlayerItems()
	{
		List<Item> list = new List<Item>();
		try
		{
			LevelManager instance = LevelManager.Instance;
			CharacterMainControl obj = ((instance != null) ? instance.MainCharacter : null);
			Item val = ((obj != null) ? obj.CharacterItem : null);
			if ((Object)(object)val != (Object)null)
			{
				Inventory inventory = val.Inventory;
				if ((Object)(object)inventory != (Object)null && inventory.Content != null)
				{
					foreach (Item item in inventory.Content)
					{
						if ((Object)(object)item != (Object)null)
						{
							list.Add(item);
						}
					}
				}
				SlotCollection slots = val.Slots;
				if ((Object)(object)slots != (Object)null)
				{
					foreach (Slot item2 in slots)
					{
						if (item2 != null)
						{
							Item content = item2.Content;
							if ((Object)(object)content != (Object)null)
							{
								list.Add(content);
							}
						}
					}
				}
			}
		}
		catch
		{
		}
		return list.ToArray();
	}

	public static void RepairFused()
	{
		try
		{
			Item[] playerItems = GetPlayerItems();
			if (playerItems == null || playerItems.Length == 0)
			{
				return;
			}
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			Item[] array = playerItems;
			foreach (Item val in array)
			{
				if ((Object)(object)val == (Object)null)
				{
					continue;
				}
				try
				{
					ReapplyVanillaRemovals(val);
				}
				catch
				{
				}
				try
				{
					ReapplyD2Removals(val);
				}
				catch
				{
				}
				try
				{
					int num5 = PurgeRemovedEffects(val);
					if (num5 > 0)
					{
						num2 += num5;
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning((object)("[TFix] RepairFused 内 PurgeRemovedEffects 异常: " + ex));
				}
				string value = null;
				try
				{
					value = val.GetString("TotemFusion_Made", (string)null);
				}
				catch
				{
				}
				if (string.IsNullOrEmpty(value))
				{
					continue;
				}
				ForceD2Reapply(val);
				num++;
				try
				{
					num2 += RepairFusedEffects(val);
				}
				catch (Exception ex2)
				{
					Debug.LogWarning((object)("[TFix] RepairFused 内 RepairFusedEffects 异常: " + ex2));
				}
				try
				{
					int num6 = PurgeRemovedEffects(val);
					if (num6 > 0)
					{
						num2 += num6;
					}
				}
				catch (Exception ex3)
				{
					Debug.LogWarning((object)("[TFix] RepairFused 内 PurgeRemovedEffects(made) 异常: " + ex3));
				}
				try
				{
					int num7 = RepairModifiers(val);
					if (num7 > 0)
					{
						num2 += num7;
					}
				}
				catch (Exception ex4)
				{
					Debug.LogWarning((object)("[TFix] RepairFused 内 RepairModifiers 异常: " + ex4));
				}
				try
				{
					RepairFusedItem(val);
				}
				catch
				{
				}
				string text = null;
				try
				{
					text = val.GetString("TotemFusion_Name", (string)null);
				}
				catch
				{
				}
				if (!string.IsNullOrWhiteSpace(text))
				{
					string text2 = null;
					try
					{
						text2 = StripStars(val.DisplayNameRaw);
					}
					catch
					{
					}
					try
					{
						val.DisplayNameRaw = text;
					}
					catch (Exception ex5)
					{
						Debug.LogWarning((object)("[TotemFusion] 设置名字失败: " + ex5.Message));
					}
					try
					{
						val.SetString("D2T_Name", text, true);
					}
					catch
					{
					}
					if (!string.IsNullOrEmpty(text2) && text2 != text)
					{
						RegisterOverride(text2, text);
						num4++;
					}
					RegisterOverride(text, text);
					num3++;
				}
				string text3 = null;
				bool flag = false;
				try
				{
					flag = !string.IsNullOrEmpty(val.GetString("TotemFusion_Src", (string)null));
				}
				catch
				{
				}
				try
				{
					text3 = val.GetString("TotemFusion_Desc", (string)null);
				}
				catch
				{
				}
				if (string.IsNullOrEmpty(text3) & flag)
				{
					try
					{
						text3 = ResolveFromSources(val);
					}
					catch
					{
					}
				}
				string text4 = (string.IsNullOrEmpty(text3) ? "" : StripTierRemarks(text3));
				if (flag && string.IsNullOrEmpty(text4))
				{
					try
					{
						List<AffixEntry> list = ReadAffixesSmart(val, positive: true, out var _);
						List<AffixEntry> list2 = ReadAffixesSmart(val, positive: false, out var _);
						OrderAffixes(list);
						OrderAffixes(list2);
						string text5 = BuildAffixDescription(list, list2);
						if (!string.IsNullOrEmpty(text5))
						{
							text4 = text5;
						}
					}
					catch
					{
					}
				}
				if (string.IsNullOrEmpty(text4))
				{
					continue;
				}
				try
				{
					SetDescriptionRaw(val, text4);
				}
				catch (Exception ex6)
				{
					Debug.LogWarning((object)("[TotemFusion] 设置描述失败: " + ex6.Message));
				}
				try
				{
					val.SetString("D2T_Desc", text4, true);
				}
				catch
				{
				}
				string text6 = null;
				try
				{
					text6 = val.DisplayNameRaw;
				}
				catch
				{
				}
				if (!string.IsNullOrEmpty(text6))
				{
					RegisterOverride(text6 + "_Desc", text4);
					num4++;
				}
				if (text4 != text3)
				{
					try
					{
						val.SetString("TotemFusion_Desc", text4, true);
					}
					catch
					{
					}
				}
			}
			if (num > 0 || num2 > 0 || num3 > 0 || num4 > 0)
			{
				Debug.Log((object)$"[TotemFusion] 合成品维护：D2 重建 {num} 个，补回效果 {num2} 条，名字修复 {num3} 条，描述修复 {num4} 条");
			}
		}
		catch
		{
		}
	}

	private static void SetDescriptionRaw(Item item, string value)
	{
		if ((Object)(object)item == (Object)null)
		{
			return;
		}
		try
		{
			PropertyInfo property = ((object)item).GetType().GetProperty("DescriptionRaw", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null && property.CanWrite)
			{
				property.SetValue(item, value, null);
			}
		}
		catch
		{
		}
	}

	public static string PrettyName(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return "图腾";
		}
		string text = null;
		try
		{
			text = StripStars(item.DisplayName);
		}
		catch
		{
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			try
			{
				text = StripStars(item.DisplayNameRaw);
			}
			catch
			{
			}
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			return "图腾";
		}
		text = text.Trim();
		if (text.StartsWith("D2T_") || text.StartsWith("Item_"))
		{
			return "图腾";
		}
		return text;
	}

	private static int ParsePlusLevel(string name, out string stem)
	{
		stem = (name ?? "").TrimEnd();
		string text = stem;
		if (string.IsNullOrEmpty(text))
		{
			return 0;
		}
		int num = text.Length - 1;
		if (text[num] == '+' || text[num] == '＋')
		{
			return 1;
		}
		int num2 = text.LastIndexOf('+');
		int num3 = text.LastIndexOf('＋');
		if (num3 > num2)
		{
			num2 = num3;
		}
		if (num2 < 0 || num2 == num)
		{
			return 0;
		}
		if (int.TryParse(text.Substring(num2 + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var result) && result >= 1)
		{
			stem = text.Substring(0, num2 + 1);
			return result;
		}
		return 0;
	}

	private static string NextPlusSuffix(int level)
	{
		int num = level + 1;
		if (num > 1)
		{
			return num.ToString(CultureInfo.InvariantCulture);
		}
		return "+";
	}

	public static string BuildFusedName(Item baseItem, Item otherItem)
	{
		string text = CleanName(baseItem);
		if (string.IsNullOrEmpty(text))
		{
			text = CleanName(otherItem);
		}
		if (string.IsNullOrEmpty(text))
		{
			text = "图腾";
		}
		int level = ParsePlusLevel(text, out var stem);
		return stem + NextPlusSuffix(level);
	}

	public static string BuildFusedRawName(Item baseItem, Item otherItem, string displayName)
	{
		if (string.IsNullOrEmpty(displayName))
		{
			return displayName;
		}
		string text = null;
		try
		{
			if ((Object)(object)baseItem != (Object)null)
			{
				text = StripStars(baseItem.DisplayNameRaw);
			}
		}
		catch
		{
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			try
			{
				if ((Object)(object)otherItem != (Object)null)
				{
					text = StripStars(otherItem.DisplayNameRaw);
				}
			}
			catch
			{
			}
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			return displayName;
		}
		text = text.Trim();
		int level = ParsePlusLevel(text, out var stem);
		if (!LooksLikeKey(stem))
		{
			return displayName;
		}
		return stem + NextPlusSuffix(level);
	}

	private static void RegisterNameOverride(string name)
	{
		RegisterOverride(name, name);
	}

	public static void RegisterMadeOverrides()
	{
		try
		{
			List<Item> list = ItemUtilities.FindAllBelongsToPlayer((Predicate<Item>)((Item it) => (Object)(object)it != (Object)null));
			if (list == null)
			{
				return;
			}
			foreach (Item item in list)
			{
				if ((Object)(object)item == (Object)null)
				{
					continue;
				}
				string value = null;
				try
				{
					value = item.GetString("TotemFusion_Made", (string)null);
				}
				catch
				{
				}
				if (string.IsNullOrEmpty(value))
				{
					continue;
				}
				string text = null;
				try
				{
					text = item.DisplayNameRaw;
				}
				catch
				{
				}
				if (string.IsNullOrEmpty(text))
				{
					continue;
				}
				string text2 = StripStars(text);
				if (text2 != text)
				{
					try
					{
						item.DisplayNameRaw = text2;
					}
					catch
					{
					}
				}
				if (!LooksLikeKey(text2))
				{
					string text3 = null;
					try
					{
						text3 = item.GetString("TotemFusion_Name", (string)null);
					}
					catch
					{
					}
					RegisterOverride(text2, string.IsNullOrEmpty(text3) ? text2 : text3);
					string text4 = null;
					try
					{
						text4 = item.GetString("TotemFusion_Desc", (string)null);
					}
					catch
					{
					}
					if (!string.IsNullOrEmpty(text4))
					{
						RegisterOverride(text2 + "_Desc", StripTierRemarks(text4));
					}
				}
			}
		}
		catch
		{
		}
	}

	public static List<PurgeEntry> ListAffixes(Item item)
	{
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		List<PurgeEntry> list = new List<PurgeEntry>();
		if ((Object)(object)item == (Object)null)
		{
			return list;
		}
		try
		{
			List<AffixEntry> list2 = ReadAffixesSmart(item, positive: true, out var _);
			List<AffixEntry> list3 = ReadAffixesSmart(item, positive: false, out var _);
			for (int i = 0; i < list2.Count; i++)
			{
				list.Add(MakeD2Entry(PurgeKind.Pos, i, list2[i]));
			}
			for (int j = 0; j < list3.Count; j++)
			{
				list.Add(MakeD2Entry(PurgeKind.Neg, j, list3[j]));
			}
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			foreach (AffixEntry item3 in list2)
			{
				if (!string.IsNullOrEmpty(item3.key))
				{
					hashSet.Add(item3.key);
				}
			}
			foreach (AffixEntry item4 in list3)
			{
				if (!string.IsNullOrEmpty(item4.key))
				{
					hashSet.Add(item4.key);
				}
			}
			HashSet<string> hashSet2 = new HashSet<string>(StringComparer.Ordinal);
			try
			{
				ModifierDescriptionCollection modifiers = item.Modifiers;
				if ((Object)(object)modifiers != (Object)null)
				{
					int num = 0;
					foreach (ModifierDescription item5 in modifiers)
					{
						if (item5 == null)
						{
							continue;
						}
						string text = "";
						try
						{
							text = item5.Key;
						}
						catch
						{
						}
						if (string.IsNullOrEmpty(text) || hashSet.Contains(text))
						{
							continue;
						}
						string text2 = "";
						try
						{
							text2 = ((object)item5.Type/*cast due to constrained. prefix*/).ToString();
						}
						catch
						{
						}
						string item2 = text + "|" + text2;
						if (hashSet2.Add(item2))
						{
							float num2 = 0f;
							try
							{
								num2 = item5.Value;
							}
							catch
							{
							}
							list.Add(MakeEntry(PurgeKind.Vanilla, num++, DisplayKey(text) + " " + SignedVal(num2, text2), Math.Abs(num2)));
						}
					}
				}
			}
			catch
			{
			}
			try
			{
				List<Effect> effects = item.Effects;
				if (effects != null)
				{
					List<string> list4 = new List<string>();
					List<int> list5 = new List<int>();
					int num3 = 0;
					foreach (Effect item6 in effects)
					{
						if ((Object)(object)item6 != (Object)null)
						{
							string text3 = null;
							try
							{
								text3 = (((Object)(object)((Component)item6).gameObject != (Object)null) ? ((Object)((Component)item6).gameObject).name : null);
							}
							catch
							{
							}
							if (!string.IsNullOrEmpty(text3) && IsEffectInternalPlaceholder(text3))
							{
								Debug.Log((object)("[TotemFusion][诊断] 跳过内部占位特效(根名=" + text3 + ")"));
								continue;
							}
							string text4 = EffectLabel(item6);
							if (string.IsNullOrEmpty(text4) || IsInternalObjectName(text4))
							{
								text4 = "特殊效果";
							}
							if (text4 == "特殊效果" && !string.IsNullOrEmpty(text3))
							{
								Debug.Log((object)("[TotemFusion][诊断] 特效降级为‘特殊效果’: rname=" + text3));
							}
							list4.Add(text4);
							list5.Add(num3);
						}
						num3++;
					}
					Dictionary<string, int> dictionary = new Dictionary<string, int>();
					foreach (string item7 in list4)
					{
						dictionary.TryGetValue(item7, out var value);
						dictionary[item7] = value + 1;
					}
					Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
					for (int k = 0; k < list4.Count; k++)
					{
						string text5 = list4[k];
						dictionary2.TryGetValue(text5, out var value2);
						dictionary2[text5] = value2 + 1;
						string label = ((dictionary[text5] > 1) ? (text5 + " (" + (value2 + 1) + ")") : text5);
						list.Add(MakeEntry(PurgeKind.Effect, list5[k], label, 0f));
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning((object)("[TotemFusion] 列举特效失败: " + ex.Message));
			}
		}
		catch (Exception ex2)
		{
			Debug.LogWarning((object)("[TotemFusion] 列举词条失败: " + ex2.Message));
		}
		return list;
	}

	private static PurgeEntry MakeD2Entry(PurgeKind kind, int index, AffixEntry e)
	{
		return MakeEntry(kind, index, AffixLabel(e), Math.Abs(e.val));
	}

	private static PurgeEntry MakeEntry(PurgeKind kind, int index, string label, float strength)
	{
		long num;
		if (kind == PurgeKind.Effect)
		{
			num = 1500L;
		}
		else
		{
			num = 200 + (long)Math.Round(strength * 40f);
			if (num < 200)
			{
				num = 200L;
			}
		}
		return new PurgeEntry
		{
			kind = kind,
			index = index,
			label = label,
			strength = strength,
			cost = num
		};
	}

	public static bool RemoveAffix(Item item, PurgeEntry e)
	{
		if ((Object)(object)item == (Object)null || e == null)
		{
			return false;
		}
		bool flag = false;
		try
		{
			if (e.kind == PurgeKind.Pos)
			{
				flag = RemoveD2Affix(item, positive: true, e.index);
			}
			else if (e.kind == PurgeKind.Neg)
			{
				flag = RemoveD2Affix(item, positive: false, e.index);
			}
			else if (e.kind == PurgeKind.Vanilla)
			{
				flag = RemoveVanillaAffix(item, e.index);
			}
			else if (e.kind == PurgeKind.Effect)
			{
				flag = RemoveEffect(item, e.index);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] 去除词条失败: " + ex.Message));
			return false;
		}
		if (!flag)
		{
			return false;
		}
		try
		{
			RegisterMadeOverrides();
		}
		catch
		{
		}
		try
		{
			ForceD2Reapply(item);
		}
		catch
		{
		}
		return true;
	}

	private static bool RemoveD2Affix(Item item, bool positive, int index)
	{
		List<AffixEntry> list = ReadAffixesSmart(item, positive: true, out var _);
		List<AffixEntry> list2 = ReadAffixesSmart(item, positive: false, out var _);
		List<AffixEntry> list3 = (positive ? list : list2);
		if (index < 0 || index >= list3.Count)
		{
			return false;
		}
		string text = list3[index].key ?? "";
		list3.RemoveAt(index);
		if (positive)
		{
			RewriteD2Affixes(item, list3, list2);
		}
		else
		{
			RewriteD2Affixes(item, list, list3);
		}
		try
		{
			RemoveLiveD2Modifiers(item, text);
		}
		catch
		{
		}
		try
		{
			if (!string.IsNullOrEmpty(text))
			{
				List<VanillaModJson> list4 = ReadVanillaList(item) ?? new List<VanillaModJson>();
				bool flag = false;
				foreach (VanillaModJson item2 in list4)
				{
					if (item2 != null && string.Equals(item2.key, text, StringComparison.OrdinalIgnoreCase))
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					list4.Add(new VanillaModJson
					{
						key = text,
						type = ""
					});
					WriteVanillaBlacklist(item, list4);
				}
			}
		}
		catch
		{
		}
		try
		{
			List<AffixEntry> list5 = ReadAffixesSmart(item, positive: false, out var _);
			List<PurgeEntry> list6 = ListAffixes(item);
			List<string> list7 = new List<string>();
			foreach (PurgeEntry item3 in list6)
			{
				if (item3.kind == PurgeKind.Neg)
				{
					list7.Add(item3.label);
				}
			}
			Debug.Log((object)("[TotemFusion][诊断] RemoveD2Affix: 删除前neg=" + list2.Count + " 删除后本地=" + list3.Count + " 重读neg=" + list5.Count + " 重读Neg标签=[" + string.Join(",", list7.ToArray()) + "]"));
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] 删除诊断失败: " + ex.Message));
		}
		try
		{
			ForceD2Reapply(item);
		}
		catch
		{
		}
		return true;
	}

	private static void RemoveLiveD2Modifiers(Item item, string key)
	{
		if ((Object)(object)item == (Object)null || string.IsNullOrEmpty(key))
		{
			return;
		}
		try
		{
			ModifierDescriptionCollection modifiers = item.Modifiers;
			if ((Object)(object)modifiers == (Object)null)
			{
				return;
			}
			List<ModifierDescription> list = new List<ModifierDescription>();
			foreach (ModifierDescription item2 in modifiers)
			{
				if (item2 != null)
				{
					string a = "";
					try
					{
						a = item2.Key;
					}
					catch
					{
					}
					if (string.Equals(a, key, StringComparison.OrdinalIgnoreCase))
					{
						list.Add(item2);
					}
				}
			}
			if (list.Count == 0)
			{
				return;
			}
			foreach (ModifierDescription item3 in list)
			{
				try
				{
					MethodInfo method = ((object)modifiers).GetType().GetMethod("Remove", new Type[1] { typeof(ModifierDescription) });
					if (method == null)
					{
						method = ((object)modifiers).GetType().GetMethod("Remove", new Type[1] { typeof(object) });
					}
					method?.Invoke(modifiers, new object[1] { item3 });
				}
				catch
				{
				}
			}
			try
			{
				((object)modifiers).GetType().GetMethod("ReapplyModifiers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(modifiers, null);
			}
			catch
			{
			}
			Debug.Log((object)("[TFix] RemoveLiveD2Modifiers: 摘除活体词条 key=" + key + " 共 " + list.Count + " 条"));
		}
		catch
		{
		}
	}

	private static void RewriteD2Affixes(Item item, List<AffixEntry> pos, List<AffixEntry> neg)
	{
		int cleanupTo = item.GetInt("D2T_PosCount", 0);
		int cleanupTo2 = item.GetInt("D2T_NegCount", 0);
		WriteAffixes(item, pos, positive: true, cleanupTo);
		WriteAffixes(item, neg, positive: false, cleanupTo2);
		int num = item.GetInt("D2T_TypeID", 0);
		if (num <= 0)
		{
			num = item.TypeID;
		}
		int quality = item.GetInt("D2T_Quality", 0);
		int displayQuality = item.GetInt("D2T_DisplayQuality", 0);
		string rarityId = item.GetString("D2T_Rarity", (string)null) ?? "";
		string remark = item.GetString("D2T_Desc", (string)null) ?? "";
		int num2 = item.GetInt("D2T_SaleValue", 0);
		if (num2 <= 0)
		{
			try
			{
				num2 = item.Value;
			}
			catch
			{
				num2 = 0;
			}
		}
		string displayName = item.GetString("D2T_Name", (string)null) ?? PrettyName(item);
		string text = "[D2T]" + Convert.ToBase64String(Encoding.UTF8.GetBytes(BuildMetaJson(pos, neg, displayName, remark, quality, displayQuality, num2, num, rarityId)));
		item.SetString("D2T_Meta", text, true);
	}

	private static bool RemoveVanillaAffix(Item item, int index)
	{
		//IL_02eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d2: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			ModifierDescriptionCollection modifiers = item.Modifiers;
			if ((Object)(object)modifiers == (Object)null)
			{
				return false;
			}
			List<AffixEntry> list = ReadAffixesSmart(item, positive: true, out var _);
			List<AffixEntry> list2 = ReadAffixesSmart(item, positive: false, out var _);
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			foreach (AffixEntry item2 in list)
			{
				if (!string.IsNullOrEmpty(item2.key))
				{
					hashSet.Add(item2.key);
				}
			}
			foreach (AffixEntry item3 in list2)
			{
				if (!string.IsNullOrEmpty(item3.key))
				{
					hashSet.Add(item3.key);
				}
			}
			ModifierDescription val = null;
			int num = 0;
			foreach (ModifierDescription item4 in modifiers)
			{
				if (item4 == null)
				{
					continue;
				}
				string text = "";
				try
				{
					text = item4.Key;
				}
				catch
				{
				}
				if (!string.IsNullOrEmpty(text) && !hashSet.Contains(text))
				{
					if (num == index)
					{
						val = item4;
						break;
					}
					num++;
				}
			}
			if (val == null)
			{
				return false;
			}
			try
			{
				string text2 = "";
				try
				{
					text2 = val.Key;
				}
				catch
				{
				}
				if (!string.IsNullOrEmpty(text2) && text2.StartsWith("D2T_", StringComparison.Ordinal))
				{
					string text3 = null;
					try
					{
						text3 = item.GetString("D2T_Meta", (string)null);
					}
					catch
					{
					}
					if (!string.IsNullOrEmpty(text3) && text3.StartsWith("[D2T]"))
					{
						List<AffixEntry> list3 = ReadAffixesSmart(item, positive: true, out var _);
						List<AffixEntry> list4 = ReadAffixesSmart(item, positive: false, out var _);
						for (int i = 0; i < list3.Count; i++)
						{
							if (string.Equals(list3[i].key, text2, StringComparison.OrdinalIgnoreCase))
							{
								return RemoveD2Affix(item, positive: true, i);
							}
						}
						for (int j = 0; j < list4.Count; j++)
						{
							if (string.Equals(list4[j].key, text2, StringComparison.OrdinalIgnoreCase))
							{
								return RemoveD2Affix(item, positive: false, j);
							}
						}
					}
				}
			}
			catch
			{
			}
			MethodInfo method = ((object)modifiers).GetType().GetMethod("Remove", new Type[1] { typeof(ModifierDescription) });
			if (method == null)
			{
				method = ((object)modifiers).GetType().GetMethod("Remove", new Type[1] { typeof(object) });
			}
			method?.Invoke(modifiers, new object[1] { val });
			((object)modifiers).GetType().GetMethod("ReapplyModifiers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(modifiers, null);
			try
			{
				string text4 = "";
				try
				{
					text4 = val.Key;
				}
				catch
				{
				}
				string text5 = "";
				try
				{
					text5 = ((object)val.Type/*cast due to constrained. prefix*/).ToString();
				}
				catch
				{
				}
				if (!string.IsNullOrEmpty(text4))
				{
					string text6 = item.GetString("TotemFusion_ModSnap", (string)null);
					if (!string.IsNullOrEmpty(text6))
					{
						VanillaModList vanillaModList = JsonConvert.DeserializeObject<VanillaModList>(text6);
						if (vanillaModList != null && vanillaModList.mods != null)
						{
							List<VanillaModJson> list5 = new List<VanillaModJson>();
							VanillaModJson[] mods = vanillaModList.mods;
							foreach (VanillaModJson vanillaModJson in mods)
							{
								if (vanillaModJson != null && (!string.Equals(vanillaModJson.key, text4, StringComparison.Ordinal) || !string.Equals(vanillaModJson.type, text5, StringComparison.OrdinalIgnoreCase)))
								{
									list5.Add(vanillaModJson);
								}
							}
							item.SetString("TotemFusion_ModSnap", SerializeModList(list5), true);
						}
					}
					else
					{
						List<VanillaModJson> list6 = ReadVanillaList(item) ?? new List<VanillaModJson>();
						bool flag = false;
						foreach (VanillaModJson item5 in list6)
						{
							if (item5 != null && string.Equals(item5.key, text4, StringComparison.Ordinal) && string.Equals(item5.type, text5, StringComparison.OrdinalIgnoreCase))
							{
								flag = true;
								break;
							}
						}
						if (!flag)
						{
							list6.Add(new VanillaModJson
							{
								key = text4,
								type = text5
							});
							WriteVanillaBlacklist(item, list6);
						}
					}
				}
			}
			catch
			{
			}
			try
			{
				string value = item.GetString("TotemFusion_ModSnap", (string)null);
				string text7 = item.GetString("TotemFusion_Van", (string)null) ?? "";
				if (text7.Length > 80)
				{
					text7 = text7.Substring(0, 80) + "...";
				}
				string text8 = "";
				try
				{
					text8 = ((val != null) ? val.Key : "");
				}
				catch
				{
				}
				string text9 = "";
				try
				{
					text9 = ((val != null) ? ((object)val.Type/*cast due to constrained. prefix*/).ToString() : "");
				}
				catch
				{
				}
				Debug.Log((object)("[TFix] RemoveVanillaAffix: 删key=" + text8 + " type=" + text9 + " 有KModSnap=" + !string.IsNullOrEmpty(value) + " KVanMods=" + text7));
			}
			catch
			{
			}
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] 移除原版属性失败: " + ex.Message));
			return false;
		}
	}

	private static bool RemoveEffect(Item item, int index)
	{
		try
		{
			List<Effect> effects = item.Effects;
			if (effects == null || index < 0 || index >= effects.Count)
			{
				return false;
			}
			object obj = null;
			int num = 0;
			foreach (Effect item2 in effects)
			{
				if (num == index)
				{
					obj = item2;
					break;
				}
				num++;
			}
			if (obj == null)
			{
				return false;
			}
			try
			{
				Effect val = (Effect)((obj is Effect) ? obj : null);
				if ((Object)(object)val != (Object)null)
				{
					string text = CleanEffectSig(EffectIdentity(val));
					string text2 = CleanEffectSig(EffectIdentityRemoval(val));
					bool flag = false;
					if (!string.IsNullOrEmpty(text))
					{
						AddRemovedEffect(item, text);
						flag = true;
					}
					if (!string.IsNullOrEmpty(text2))
					{
						AddRemovedEffect(item, text2);
						flag = true;
					}
					Debug.Log((object)("[TFix] RemoveEffect 记录签名: 精确=" + (text ?? "null") + " 稳定=" + (text2 ?? "null") + " 已记录=" + flag));
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning((object)("[TotemFusion] 记录已去除效果失败: " + ex.Message));
			}
			MethodInfo method = effects.GetType().GetMethod("Remove", new Type[1] { obj.GetType() });
			if (method == null)
			{
				method = effects.GetType().GetMethod("Remove", new Type[1] { typeof(object) });
			}
			method?.Invoke(effects, new object[1] { obj });
			try
			{
				Component val2 = (Component)((obj is Component) ? obj : null);
				if (val2 != null && (Object)(object)val2 != (Object)null)
				{
					Object.Destroy((Object)(object)val2);
				}
			}
			catch
			{
			}
			return true;
		}
		catch (Exception ex2)
		{
			Debug.LogWarning((object)("[TotemFusion] 移除效果失败: " + ex2.Message));
			return false;
		}
	}

	private static bool ResolveLocalization()
	{
		if (_locResolved)
		{
			return _locType != null;
		}
		_locResolved = true;
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly in assemblies)
		{
			Type type = null;
			try
			{
				type = assembly.GetType("SodaCraft.Localizations.LocalizationManager", throwOnError: false);
			}
			catch
			{
			}
			if (!(type == null))
			{
				_locType = type;
				try
				{
					_locSetOverride = type.GetMethod("SetOverrideText", BindingFlags.Static | BindingFlags.Public, null, new Type[2]
					{
						typeof(string),
						typeof(string)
					}, null);
				}
				catch
				{
				}
				try
				{
					_locGetPlain = type.GetMethod("GetPlainText", BindingFlags.Static | BindingFlags.Public, null, new Type[1] { typeof(string) }, null);
				}
				catch
				{
				}
				break;
			}
		}
		return _locType != null;
	}

	private static string GetPlainText(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return null;
		}
		if (PlainTextCache.TryGetValue(key, out var value))
		{
			return value;
		}
		string text = null;
		try
		{
			if (ResolveLocalization() && _locGetPlain != null)
			{
				text = _locGetPlain.Invoke(null, new object[1] { key }) as string;
			}
		}
		catch
		{
		}
		PlainTextCache[key] = text;
		return text;
	}

	private static string TranslateStat(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return null;
		}
		if (StatCN.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
		{
			return value;
		}
		return null;
	}

	private static string DisplayKey(string key)
	{
		string text = (key ?? "").Trim();
		if (text.Length == 0)
		{
			return "属性";
		}
		if (HasCJK(text))
		{
			return text;
		}
		string text2 = TranslateStat(text);
		if (!string.IsNullOrEmpty(text2))
		{
			return text2;
		}
		string plainText = GetPlainText(text);
		if (!string.IsNullOrEmpty(plainText))
		{
			string text3 = StripStars(plainText).Trim();
			if (text3.Length > 0 && text3 != text)
			{
				return text3;
			}
		}
		return text;
	}

	private static bool HasCJK(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return false;
		}
		foreach (char c in s)
		{
			if (c >= '一' && c <= '鿿')
			{
				return true;
			}
		}
		return false;
	}

	private static void RegisterOverride(string key, string text)
	{
		if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		try
		{
			if (ResolveLocalization() && !(_locType == null) && !(_locSetOverride == null))
			{
				_locSetOverride.Invoke(null, new object[2] { key, text });
			}
		}
		catch
		{
		}
	}

	public static bool IsD2Totem(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return false;
		}
		try
		{
			string text = item.GetString("D2T_Meta", (string)null);
			if (!string.IsNullOrEmpty(text) && (text.StartsWith("[D2T]") || text.TrimStart().StartsWith("{")))
			{
				return true;
			}
			if (item.GetInt("D2T_PosCount", 0) + item.GetInt("D2T_NegCount", 0) > 0)
			{
				return true;
			}
			if (item.GetInt("D2T_TypeID", 0) > 0)
			{
				return true;
			}
			string text2 = item.GetString("D2T_Name", (string)null);
			if (!string.IsNullOrEmpty(text2) && text2.StartsWith("D2T_"))
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	public static bool IsVanillaTotem(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return false;
		}
		try
		{
			if (VanillaTotemTypeIds.Contains(item.TypeID))
			{
				return true;
			}
			string displayName = item.DisplayName;
			if (!string.IsNullOrEmpty(displayName) && (displayName.IndexOf("图腾", StringComparison.OrdinalIgnoreCase) >= 0 || displayName.IndexOf("Totem", StringComparison.OrdinalIgnoreCase) >= 0))
			{
				return true;
			}
			string displayNameRaw = item.DisplayNameRaw;
			if (!string.IsNullOrEmpty(displayNameRaw) && displayNameRaw.IndexOf("Totem", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	public static bool IsTotem(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return false;
		}
		if (!IsD2Totem(item))
		{
			return IsVanillaTotem(item);
		}
		return true;
	}

	public static int PropertyCount(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return 0;
		}
		int num = 0;
		try
		{
			num = item.GetInt("D2T_PosCount", 0) + item.GetInt("D2T_NegCount", 0);
		}
		catch
		{
		}
		return num + CountVanillaModifiers(item);
	}

	public static int AffixCount(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return 0;
		}
		return PropertyCount(item) + CountEffects(item);
	}

	private static int CountVanillaModifiers(Item item)
	{
		try
		{
			ModifierDescriptionCollection modifiers = item.Modifiers;
			if ((Object)(object)modifiers == (Object)null)
			{
				return 0;
			}
			int num = 0;
			foreach (ModifierDescription item2 in modifiers)
			{
				if (item2 != null)
				{
					num++;
				}
			}
			return num;
		}
		catch
		{
			return 0;
		}
	}

	public static int CountEffects(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return 0;
		}
		try
		{
			List<Effect> effects = item.Effects;
			if (effects == null)
			{
				return 0;
			}
			int num = 0;
			foreach (Effect item2 in effects)
			{
				if ((Object)(object)item2 != (Object)null)
				{
					num++;
				}
			}
			return num;
		}
		catch
		{
			return 0;
		}
	}

	private static bool IsWeakSignature(string sig)
	{
		if (!string.IsNullOrEmpty(sig))
		{
			return sig.IndexOf("A:") < 0;
		}
		return true;
	}

	private static bool IsUselessSignature(string dec)
	{
		if (string.IsNullOrEmpty(dec))
		{
			return true;
		}
		if (dec.IndexOf("|N:") >= 0)
		{
			return true;
		}
		return dec.Replace("|", "").Replace(";", "").Replace("T:", "")
			.Replace("F:", "")
			.Replace("A:", "")
			.Length == 0;
	}

	private static string EffectDump(Effect e)
	{
		if ((Object)(object)e == (Object)null)
		{
			return "null";
		}
		string text = "";
		try
		{
			text = ((Object)((Component)e).gameObject).name;
		}
		catch
		{
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		try
		{
			num = ((e.Triggers != null) ? e.Triggers.Count : 0);
		}
		catch
		{
		}
		try
		{
			num2 = ((e.Filters != null) ? e.Filters.Count : 0);
		}
		catch
		{
		}
		try
		{
			num3 = ((e.Actions != null) ? e.Actions.Count : 0);
		}
		catch
		{
		}
		string text2 = "";
		try
		{
			text2 = FirstObjectName(e.Actions) ?? "";
		}
		catch
		{
		}
		string text3 = "";
		try
		{
			text3 = EffectSignature(e);
		}
		catch
		{
		}
		return string.Format("{0}[T{1}/F{2}/A{3}] buff={4} sig#{5}{6}", text, num, num2, num3, text2, Math.Abs(text3.GetHashCode() % 100000), IsWeakSignature(text3) ? "(弱签名)" : "");
	}

	private static string DescribeFields(object c)
	{
		if (c == null)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder();
		try
		{
			Type type = c.GetType();
			while (type != null && type != typeof(object) && type != typeof(Component) && type != typeof(MonoBehaviour))
			{
				FieldInfo[] fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (FieldInfo fieldInfo in fields)
				{
					object value;
					try
					{
						value = fieldInfo.GetValue(c);
					}
					catch
					{
						continue;
					}
					Object val = (Object)((value is Object) ? value : null);
					string text;
					if (val != (Object)null)
					{
						text = val.name;
					}
					else
					{
						text = ((value != null) ? Convert.ToString(value, CultureInfo.InvariantCulture) : "");
					}
					if (text != null && text.Length > 48)
					{
						text = text.Substring(0, 48);
					}
					stringBuilder.Append(fieldInfo.Name).Append('=').Append(text)
						.Append(',');
				}
				type = type.BaseType;
			}
		}
		catch
		{
		}
		return stringBuilder.ToString();
	}

	private static string EffectSignature(Effect e)
	{
		if ((Object)(object)e == (Object)null)
		{
			return "null";
		}
		StringBuilder stringBuilder = new StringBuilder();
		try
		{
			foreach (EffectTrigger trigger in e.Triggers)
			{
				stringBuilder.Append("T:").Append(((Object)(object)trigger == (Object)null) ? "?" : ((object)trigger).GetType().Name).Append(';');
			}
		}
		catch
		{
		}
		stringBuilder.Append('|');
		try
		{
			foreach (EffectFilter filter in e.Filters)
			{
				stringBuilder.Append("F:").Append(((Object)(object)filter == (Object)null) ? "?" : ((object)filter).GetType().Name).Append(';');
			}
		}
		catch
		{
		}
		stringBuilder.Append('|');
		try
		{
			foreach (EffectAction action in e.Actions)
			{
				stringBuilder.Append("A:").Append(((Object)(object)action == (Object)null) ? "?" : ((object)action).GetType().Name).Append('{')
					.Append(DescribeFields(action))
					.Append("};");
			}
		}
		catch
		{
		}
		return stringBuilder.ToString();
	}

	private static string StableEffectSignature(Effect e)
	{
		if ((Object)(object)e == (Object)null)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder();
		try
		{
			foreach (EffectTrigger trigger in e.Triggers)
			{
				stringBuilder.Append("T:").Append(((Object)(object)trigger == (Object)null) ? "?" : ((object)trigger).GetType().Name).Append(';');
			}
		}
		catch
		{
		}
		stringBuilder.Append('|');
		try
		{
			foreach (EffectFilter filter in e.Filters)
			{
				stringBuilder.Append("F:").Append(((Object)(object)filter == (Object)null) ? "?" : ((object)filter).GetType().Name).Append(';');
			}
		}
		catch
		{
		}
		stringBuilder.Append('|');
		try
		{
			foreach (EffectAction action in e.Actions)
			{
				stringBuilder.Append("A:").Append(((Object)(object)action == (Object)null) ? "?" : ((object)action).GetType().Name).Append('{')
					.Append(StableDescribeFields(action))
					.Append("};");
			}
		}
		catch
		{
		}
		return stringBuilder.ToString();
	}

	private static string StableDescribeFields(object c)
	{
		if (c == null)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder();
		try
		{
			Type type = c.GetType();
			while (type != null && type != typeof(object) && type != typeof(Component) && type != typeof(MonoBehaviour))
			{
				FieldInfo[] fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (FieldInfo fieldInfo in fields)
				{
					if (_runtimeRefFields.Contains(fieldInfo.Name))
					{
						continue;
					}
					object value;
					try
					{
						value = fieldInfo.GetValue(c);
					}
					catch
					{
						continue;
					}
					Object val = (Object)((value is Object) ? value : null);
					if (val != (Object)null)
					{
						string text = "";
						try
						{
							text = val.name;
						}
						catch
						{
						}
						if (!string.IsNullOrEmpty(text) && !IsInternalObjectName(text) && !string.Equals(text, fieldInfo.Name, StringComparison.Ordinal))
						{
							stringBuilder.Append(fieldInfo.Name).Append('=').Append(text)
								.Append(',');
						}
					}
					else
					{
						if (value == null)
						{
							continue;
						}
						string text2 = Convert.ToString(value, CultureInfo.InvariantCulture);
						if (text2 == null || text2.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase) < 0)
						{
							if (text2 != null && text2.Length > 48)
							{
								text2 = text2.Substring(0, 48);
							}
							stringBuilder.Append(fieldInfo.Name).Append('=').Append(text2)
								.Append(',');
						}
					}
				}
				type = type.BaseType;
			}
		}
		catch
		{
		}
		return stringBuilder.ToString();
	}

	private static HashSet<string> CollectEffectSignatures(Item item)
	{
		HashSet<string> hashSet = new HashSet<string>();
		if ((Object)(object)item == (Object)null)
		{
			return hashSet;
		}
		try
		{
			List<Effect> effects = item.Effects;
			if (effects == null)
			{
				return hashSet;
			}
			foreach (Effect item2 in effects)
			{
				if (!((Object)(object)item2 == (Object)null))
				{
					hashSet.Add(EffectIdentity(item2));
				}
			}
		}
		catch
		{
		}
		return hashSet;
	}

	private static string EffectIdentity(Effect e)
	{
		if ((Object)(object)e == (Object)null)
		{
			return null;
		}
		string text = StableEffectSignature(e);
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		if (text.Replace("|", "").Replace(";", "").Replace("T:", "")
			.Replace("F:", "")
			.Replace("A:", "")
			.Length == 0)
		{
			return null;
		}
		return text;
	}

	private static string EffectIdentityRemoval(Effect e)
	{
		if ((Object)(object)e == (Object)null)
		{
			return null;
		}
		string text = StableEffectSignatureStable(e);
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		if (text.Replace("|", "").Replace(";", "").Replace("T:", "")
			.Replace("F:", "")
			.Replace("A:", "")
			.Length == 0)
		{
			return null;
		}
		return text;
	}

	private static string StableEffectSignatureStable(Effect e)
	{
		if ((Object)(object)e == (Object)null)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder();
		try
		{
			foreach (EffectTrigger trigger in e.Triggers)
			{
				stringBuilder.Append("T:").Append(((Object)(object)trigger == (Object)null) ? "?" : ((object)trigger).GetType().Name).Append(';');
			}
		}
		catch
		{
		}
		stringBuilder.Append('|');
		try
		{
			foreach (EffectFilter filter in e.Filters)
			{
				stringBuilder.Append("F:").Append(((Object)(object)filter == (Object)null) ? "?" : ((object)filter).GetType().Name).Append(';');
			}
		}
		catch
		{
		}
		stringBuilder.Append('|');
		try
		{
			foreach (EffectAction action in e.Actions)
			{
				stringBuilder.Append("A:").Append(((Object)(object)action == (Object)null) ? "?" : ((object)action).GetType().Name).Append('{')
					.Append(StableDescribeFieldsStable(action))
					.Append("};");
			}
		}
		catch
		{
		}
		return stringBuilder.ToString();
	}

	private static string StableDescribeFieldsStable(object c)
	{
		if (c == null)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder();
		try
		{
			Type type = c.GetType();
			while (type != null && type != typeof(object) && type != typeof(Component) && type != typeof(MonoBehaviour))
			{
				FieldInfo[] fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (FieldInfo fieldInfo in fields)
				{
					if (_runtimeRefFields.Contains(fieldInfo.Name))
					{
						continue;
					}
					object value;
					try
					{
						value = fieldInfo.GetValue(c);
					}
					catch
					{
						continue;
					}
					Object val = (Object)((value is Object) ? value : null);
					if (val != (Object)null)
					{
						stringBuilder.Append(fieldInfo.Name).Append('=').Append(((object)val).GetType().Name)
							.Append(',');
					}
					else
					{
						if (value == null)
						{
							continue;
						}
						string text = Convert.ToString(value, CultureInfo.InvariantCulture);
						if (text == null || text.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase) < 0)
						{
							if (text != null && text.Length > 48)
							{
								text = text.Substring(0, 48);
							}
							stringBuilder.Append(fieldInfo.Name).Append('=').Append(text)
								.Append(',');
						}
					}
				}
				type = type.BaseType;
			}
		}
		catch
		{
		}
		return stringBuilder.ToString();
	}

	private static string CleanEffectSig(string sig)
	{
		if (string.IsNullOrEmpty(sig))
		{
			return sig;
		}
		try
		{
			string pattern = "(^|[,\\{])\\s*(?:_mainControl|master|selfBuff|owner|target|source|_owner|gameObject|buff|fx|buffs|transform|parent|originator|instigator|caster|_caster)=[^,}]*";
			sig = Regex.Replace(sig, pattern, (Match m) => (!m.Groups[1].Success || !(m.Groups[1].Value == "{")) ? "" : "{");
			string pattern2 = "(^|[,\\{])\\s*[A-Za-z_][A-Za-z0-9_]*=[^,}]*\\(Clone\\)[^,}]*";
			sig = Regex.Replace(sig, pattern2, (Match m) => (!m.Groups[1].Success || !(m.Groups[1].Value == "{")) ? "" : "{");
			sig = Regex.Replace(sig, "\\(Clone\\)", "");
			sig = Regex.Replace(sig, ",\\s*[,;]", ";");
			sig = Regex.Replace(sig, ";\\s*;", ";");
			sig = Regex.Replace(sig, "\\{\\s*,", "{");
			sig = Regex.Replace(sig, ",\\s*\\}", "}");
			sig = Regex.Replace(sig, "\\{\\s*\\}", "{}");
			sig = Regex.Replace(sig, "(^|\\|)N:[^|]*", "");
		}
		catch
		{
		}
		return sig;
	}

	private static HashSet<string> GetRemovedEffects(Item item)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		if ((Object)(object)item == (Object)null)
		{
			return hashSet;
		}
		string text = null;
		try
		{
			text = item.GetString("TotemFusion_RemovedFX", (string)null);
		}
		catch
		{
		}
		if (string.IsNullOrEmpty(text))
		{
			return hashSet;
		}
		string[] array = text.Split('|');
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = (array[i] ?? "").Trim();
			if (text2.Length != 0)
			{
				string sig;
				try
				{
					sig = Encoding.UTF8.GetString(Convert.FromBase64String(text2));
				}
				catch
				{
					sig = text2;
				}
				string text3 = CleanEffectSig(sig);
				if (!IsUselessSignature(text3))
				{
					hashSet.Add(text3);
				}
			}
		}
		return hashSet;
	}

	private static void AddRemovedEffect(Item item, string sig)
	{
		if ((Object)(object)item == (Object)null || string.IsNullOrEmpty(sig))
		{
			return;
		}
		HashSet<string> removedEffects = GetRemovedEffects(item);
		if (!removedEffects.Add(sig))
		{
			return;
		}
		try
		{
			List<string> list = new List<string>();
			foreach (string item2 in removedEffects)
			{
				list.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(item2)));
			}
			item.SetString("TotemFusion_RemovedFX", string.Join("|", list.ToArray()), true);
		}
		catch
		{
		}
	}

	private static bool EffectMatchesRemoved(Effect e, HashSet<string> removed)
	{
		if (removed == null || removed.Count == 0 || (Object)(object)e == (Object)null)
		{
			return false;
		}
		string text = null;
		string text2 = null;
		try
		{
			text = CleanEffectSig(EffectIdentity(e));
		}
		catch
		{
		}
		try
		{
			text2 = CleanEffectSig(EffectIdentityRemoval(e));
		}
		catch
		{
		}
		if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(text2))
		{
			return false;
		}
		if (text.IndexOf("A:") < 0 && text2.IndexOf("A:") < 0)
		{
			return false;
		}
		if (!string.IsNullOrEmpty(text) && removed.Contains(text))
		{
			return true;
		}
		if (!string.IsNullOrEmpty(text2) && removed.Contains(text2))
		{
			return true;
		}
		return false;
	}

	private static int PurgeRemovedEffects(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return 0;
		}
		int num = 0;
		try
		{
			HashSet<string> removedEffects = GetRemovedEffects(item);
			Debug.Log((object)("[TFix] PurgeRemovedEffects 入口: removed数=" + removedEffects.Count));
			if (removedEffects.Count == 0)
			{
				return 0;
			}
			List<Effect> effects = item.Effects;
			if (effects == null)
			{
				return 0;
			}
			for (int num2 = effects.Count - 1; num2 >= 0; num2--)
			{
				Effect val = effects[num2];
				if (!((Object)(object)val == (Object)null) && EffectMatchesRemoved(val, removedEffects))
				{
					try
					{
						effects.RemoveAt(num2);
					}
					catch
					{
					}
					try
					{
						Object.Destroy((Object)(object)((Component)val).gameObject);
					}
					catch
					{
					}
					num++;
				}
			}
			if (removedEffects.Count > 0 && num == 0)
			{
				try
				{
					List<string> list = new List<string>();
					foreach (Effect item2 in effects)
					{
						if ((Object)(object)item2 != (Object)null)
						{
							list.Add("精确:" + CleanEffectSig(EffectIdentity(item2)));
							list.Add("稳定:" + CleanEffectSig(EffectIdentityRemoval(item2)));
						}
					}
					Debug.Log((object)("[TFix][诊断] PurgeRemovedEffects 0删: 活体keys=[" + string.Join(" || ", list.ToArray()) + "]"));
					Debug.Log((object)("[TFix][诊断] PurgeRemovedEffects 0删: 待删set=[" + string.Join(" || ", removedEffects.ToArray()) + "]"));
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		Debug.Log((object)("[TFix] PurgeRemovedEffects 结果: 实际删除=" + num));
		return num;
	}

	public static int CloneEffectsInto(Item fused, Item src, HashSet<string> existing)
	{
		if ((Object)(object)fused == (Object)null || (Object)(object)src == (Object)null || fused == src)
		{
			return 0;
		}
		int num = 0;
		try
		{
			List<Effect> effects = src.Effects;
			if (effects == null)
			{
				return 0;
			}
			int num2 = 0;
			foreach (Effect item2 in effects)
			{
				num2++;
				if ((Object)(object)item2 == (Object)null)
				{
					continue;
				}
				string item = EffectIdentity(item2);
				bool flag = false;
				if (existing != null && !existing.Add(item))
				{
					flag = true;
				}
				if (flag)
				{
					Debug.Log((object)("[TotemFusion] 效果 #" + num2 + " " + EffectDump(item2) + " → 跳过（与已有重复）"));
					continue;
				}
				Debug.Log((object)("[TotemFusion] 效果 #" + num2 + " " + EffectDump(item2) + " → 克隆"));
				try
				{
					GameObject val = Object.Instantiate<GameObject>(((Component)item2).gameObject, ((Component)fused).transform, false);
					((Object)val).name = ((Object)((Component)item2).gameObject).name;
					val.SetActive(true);
					Effect component = val.GetComponent<Effect>();
					if ((Object)(object)component == (Object)null)
					{
						Object.Destroy((Object)(object)val);
						continue;
					}
					fused.AddEffect(component);
					num++;
				}
				catch (Exception ex)
				{
					Debug.LogWarning((object)("[TotemFusion] 克隆 Effect 失败: " + ex.Message));
				}
			}
			if (num > 0)
			{
				try
				{
					typeof(Item).GetMethod("NotifyItemTreeChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(fused, null);
				}
				catch
				{
				}
			}
		}
		catch (Exception ex2)
		{
			Debug.LogWarning((object)("[TotemFusion] 读取 Effect 列表失败: " + ex2.Message));
		}
		return num;
	}

	private static string BuildSourceIds(Item a, Item b)
	{
		HashSet<int> set = new HashSet<int>();
		Action<Item> action = (Item it) =>
		{
			if (!((Object)(object)it == (Object)null))
			{
				try
				{
					if (it.TypeID > 0)
					{
						set.Add(it.TypeID);
					}
				}
				catch
				{
				}
				string text = null;
				try
				{
					text = it.GetString("TotemFusion_Src", (string)null);
				}
				catch
				{
				}
				if (!string.IsNullOrEmpty(text))
				{
					string[] array = text.Split(',');
					for (int i = 0; i < array.Length; i++)
					{
						if (int.TryParse(array[i].Trim(), out var result) && result > 0)
						{
							set.Add(result);
						}
					}
				}
			}
		};
		action(a);
		action(b);
		return string.Join(",", set.Select((int x) => x.ToString(CultureInfo.InvariantCulture)));
	}

	private static int RemoveDeadEffects(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return 0;
		}
		int num = 0;
		try
		{
			List<Effect> effects = item.Effects;
			if (effects == null)
			{
				return 0;
			}
			for (int num2 = effects.Count - 1; num2 >= 0; num2--)
			{
				Effect val = effects[num2];
				if ((Object)(object)val == (Object)null)
				{
					try
					{
						effects.RemoveAt(num2);
					}
					catch
					{
					}
				}
				else
				{
					bool flag = false;
					try
					{
						int num3 = ((val.Triggers != null) ? val.Triggers.Count : 0);
						int num4 = ((val.Filters != null) ? val.Filters.Count : 0);
						flag = (val.Actions == null || val.Actions.Count == 0) && (num3 == 0 || num4 == 0);
					}
					catch
					{
					}
					if (flag)
					{
						try
						{
							effects.RemoveAt(num2);
						}
						catch
						{
						}
						try
						{
							Object.Destroy((Object)(object)((Component)val).gameObject);
						}
						catch
						{
						}
						num++;
					}
				}
			}
		}
		catch
		{
		}
		return num;
	}

	public static int EnsureEffects(Item fused, Item a, Item b)
	{
		if ((Object)(object)fused == (Object)null)
		{
			return 0;
		}
		try
		{
			int num = CountEffects(fused);
			int num2 = RepairFusedEffects(fused);
			int num3 = CountEffects(fused);
			if (num3 < num)
			{
				HashSet<string> existing = CollectEffectSignatures(fused);
				if ((Object)(object)a != (Object)null)
				{
					num3 += CloneEffectsInto(fused, a, existing);
				}
				if ((Object)(object)b != (Object)null)
				{
					num3 += CloneEffectsInto(fused, b, existing);
				}
				num3 = CountEffects(fused);
			}
			Debug.Log((object)$"[TotemFusion] 效果兜底：合成后 {num} 条，修复 {num2} 条，最终 {num3} 条");
			return num3;
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] 效果兜底失败: " + ex.Message));
			return 0;
		}
	}

	public static int RepairFusedEffects(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return 0;
		}
		try
		{
			RemoveDeadEffects(item);
		}
		catch
		{
		}
		try
		{
			string text = item.GetString("TotemFusion_Src", (string)null);
			if (string.IsNullOrEmpty(text))
			{
				return 0;
			}
			List<int> list = new List<int>();
			string[] array = text.Split(',');
			for (int i = 0; i < array.Length; i++)
			{
				if (int.TryParse(array[i].Trim(), out var result) && result > 0 && !list.Contains(result))
				{
					list.Add(result);
				}
			}
			if (list.Count == 0)
			{
				return 0;
			}
			HashSet<string> hashSet = CollectEffectSignatures(item);
			HashSet<string> removedEffects = GetRemovedEffects(item);
			if (removedEffects != null)
			{
				List<string> list2 = new List<string>();
				foreach (string item2 in removedEffects)
				{
					if (IsUselessSignature(item2))
					{
						list2.Add(item2);
					}
				}
				foreach (string item3 in list2)
				{
					removedEffects.Remove(item3);
				}
			}
			if (removedEffects != null)
			{
				foreach (string item4 in removedEffects)
				{
					hashSet.Add(item4);
				}
			}
			int num = 0;
			Debug.Log((object)("[TFix] RepairFusedEffects: KSourceIds='" + text + "' 源数=" + list.Count + " 已存在签名=" + hashSet.Count + " 已移除=" + (removedEffects?.Count ?? 0)));
			foreach (int item5 in list)
			{
				Item val = null;
				bool flag = false;
				try
				{
					val = ItemAssetsCollection.GetPrefab(item5);
				}
				catch
				{
				}
				if ((Object)(object)val == (Object)null)
				{
					try
					{
						val = ItemAssetsCollection.InstantiateSync(item5);
					}
					catch
					{
					}
					flag = (Object)(object)val != (Object)null;
				}
				int num2 = 0;
				try
				{
					num2 = (((Object)(object)val == (Object)null) ? (-1) : CountEffects(val));
				}
				catch
				{
				}
				Debug.Log((object)("[TotemFusion][诊断] 源#" + item5 + " GetPrefab/InstantiateSync结果=" + (((Object)(object)val == (Object)null) ? "NULL(跳过)" : ("OK, 含Effect=" + num2))));
				if ((Object)(object)val == (Object)null)
				{
					continue;
				}
				try
				{
					num += CloneEffectsInto(item, val, hashSet);
				}
				catch
				{
				}
				if (flag)
				{
					try
					{
						Object.Destroy((Object)(object)((Component)val).gameObject);
					}
					catch
					{
					}
				}
			}
			try
			{
				num -= PurgeRemovedEffects(item);
			}
			catch
			{
			}
			return num;
		}
		catch
		{
			return 0;
		}
	}

	private static string MergeDescriptionText(Item a, Item b)
	{
		string text = DescOf(a);
		string text2 = DescOf(b);
		if (text.Length == 0)
		{
			return StripTierRemarks(text2);
		}
		if (text2.Length == 0)
		{
			return StripTierRemarks(text);
		}
		if (text == text2 || text.Contains(text2))
		{
			return StripTierRemarks(text);
		}
		if (text2.Contains(text))
		{
			return StripTierRemarks(text2);
		}
		return StripTierRemarks(JoinDesc(text, text2));
	}

	private static string ComposeFusedDescription(Item baseItem, Item otherItem, List<AffixEntry> pos, List<AffixEntry> neg)
	{
		string text = MergeDescriptionText(baseItem, otherItem);
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		if (pos != null)
		{
			OrderAffixes(pos);
		}
		if (neg != null)
		{
			OrderAffixes(neg);
		}
		return BuildAffixDescription(pos, neg);
	}

	private static string JoinDesc(string a, string b)
	{
		string text = (a ?? "").TrimEnd();
		string text2 = (b ?? "").TrimStart();
		if (text.Length == 0)
		{
			return text2;
		}
		if (text2.Length == 0)
		{
			return text;
		}
		char c = text[text.Length - 1];
		if (c == '。' || c == '.' || c == '！' || c == '!' || c == '；' || c == ';' || c == '，' || c == ',' || c == '：' || c == ':' || c == '、')
		{
			return CollapseSeparators(text + text2);
		}
		return CollapseSeparators(text + "；" + text2);
	}

	private static string CollapseSeparators(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		string text2 = text;
		for (int i = 0; i < 8; i++)
		{
			string text3 = text2.Replace("。；", "。").Replace("。;", "。").Replace(".；", ".")
				.Replace(".;", ".")
				.Replace("！；", "！")
				.Replace("!；", "!")
				.Replace("！;", "！")
				.Replace("!;", "!")
				.Replace("；；", "；")
				.Replace("；;", "；")
				.Replace(";;", ";");
			if (text3 == text2)
			{
				break;
			}
			text2 = text3;
		}
		return text2;
	}

	public static string StripTierRemarks(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		int num;
		while ((num = text.IndexOf("D2T_TierRemark_", StringComparison.Ordinal)) >= 0)
		{
			int i;
			for (i = num; i < text.Length && (char.IsDigit(text[i]) || text[i] == '_'); i++)
			{
			}
			text = text.Substring(0, num) + text.Substring(i);
		}
		string text2 = text.Replace(';', '；').Replace('\r', '；').Replace('\n', '；');
		string text3 = text2.Replace("；", "").Trim();
		while (text3.EndsWith("。") || text3.EndsWith("."))
		{
			text3 = text3.Substring(0, text3.Length - 1).TrimEnd();
		}
		if (TierRemarks.Contains(text3))
		{
			return "";
		}
		string[] array = text2.Split('；');
		List<string> list = new List<string>();
		string[] array2 = array;
		foreach (string text4 in array2)
		{
			string text5 = ((text4 != null) ? text4.Trim() : "");
			if (text5.Length == 0)
			{
				continue;
			}
			string text6 = text5;
			while (text6.EndsWith("。") || text6.EndsWith(".") || text6.EndsWith("：") || text6.EndsWith(":"))
			{
				text6 = text6.Substring(0, text6.Length - 1).TrimEnd();
			}
			if (TierRemarks.Contains(text6))
			{
				continue;
			}
			bool flag = false;
			foreach (string tierRemark in TierRemarks)
			{
				if (text6.StartsWith(tierRemark, StringComparison.Ordinal))
				{
					if (text6.Substring(tierRemark.Length).TrimStart('：', ':', '·', '•', '-', '—', ' ').Trim('的', '级', '品', '质', '图', '腾', ' ')
						.Length == 0)
					{
						flag = true;
					}
					break;
				}
			}
			if (!flag)
			{
				list.Add(text5);
			}
		}
		if (list.Count == 0)
		{
			return "";
		}
		string text7 = list[0].Trim();
		for (int k = 1; k < list.Count; k++)
		{
			text7 = JoinDesc(text7, list[k]);
		}
		return CollapseSeparators(text7);
	}

	private static string DescOf(Item item)
	{
		string text = ResolveFromSources(item, null);
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		return NaturalDescription(item);
	}

	private static string ResolveFromSources(Item item)
	{
		return ResolveFromSources(item, null);
	}

	private static string ResolveFromSources(Item item, HashSet<int> visited)
	{
		if ((Object)(object)item == (Object)null)
		{
			return null;
		}
		if (visited == null)
		{
			visited = new HashSet<int>();
		}
		int num = -1;
		try
		{
			num = item.TypeID;
		}
		catch
		{
		}
		if (num > 0)
		{
			visited.Add(num);
		}
		string text = null;
		try
		{
			text = item.GetString("TotemFusion_Src", (string)null);
		}
		catch
		{
		}
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		List<int> list = new List<int>();
		string[] array = text.Split(',');
		for (int i = 0; i < array.Length; i++)
		{
			if (int.TryParse(array[i].Trim(), out var result) && result > 0)
			{
				list.Add(result);
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		List<string> list2 = new List<string>();
		List<Item> list3 = new List<Item>();
		foreach (int item2 in list)
		{
			if (item2 == num || !visited.Add(item2))
			{
				continue;
			}
			Item val = null;
			try
			{
				val = ItemAssetsCollection.GetPrefab(item2);
			}
			catch
			{
			}
			if ((Object)(object)val == (Object)null)
			{
				try
				{
					val = ItemAssetsCollection.InstantiateSync(item2);
				}
				catch
				{
				}
				if ((Object)(object)val != (Object)null)
				{
					list3.Add(val);
				}
			}
			if (!((Object)(object)val == (Object)null))
			{
				string text2 = ResolveFromSources(val, visited);
				if (!string.IsNullOrEmpty(text2))
				{
					list2.Add(text2);
				}
			}
		}
		foreach (Item item3 in list3)
		{
			try
			{
				if ((Object)(object)item3 != (Object)null && (Object)(object)((Component)item3).gameObject != (Object)null)
				{
					Object.Destroy((Object)(object)((Component)item3).gameObject);
				}
			}
			catch
			{
			}
		}
		if (list2.Count == 0)
		{
			return null;
		}
		string text3 = list2[0];
		for (int j = 1; j < list2.Count; j++)
		{
			text3 = JoinDesc(text3, list2[j]);
		}
		return StripTierRemarks(text3);
	}

	private static string NaturalDescription(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return "";
		}
		string text = ReadNatural(item, primary: true);
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		text = ReadNatural(item, primary: false);
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		string text2 = null;
		try
		{
			text2 = item.GetString("D2T_Desc", (string)null);
		}
		catch
		{
		}
		if (!string.IsNullOrEmpty(text2))
		{
			string plainText = GetPlainText(text2);
			if (!string.IsNullOrEmpty(plainText) && plainText != text2 && !IsKeyLike(plainText))
			{
				return plainText.Trim();
			}
		}
		return "";
	}

	private static string ReadNatural(Item item, bool primary)
	{
		string text = null;
		try
		{
			text = (primary ? StripStars(item.Description) : StripStars(item.DescriptionRaw));
		}
		catch
		{
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			return "";
		}
		text = text.Trim();
		if (IsKeyLike(text))
		{
			string plainText = GetPlainText(text);
			if (!string.IsNullOrEmpty(plainText) && plainText != text && !IsKeyLike(plainText))
			{
				return plainText.Trim();
			}
			return "";
		}
		return text;
	}

	private static bool IsKeyLike(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return false;
		}
		if (!s.StartsWith("Item_") && !s.StartsWith("D2T_") && !s.StartsWith("Buff_") && !s.EndsWith("_Desc"))
		{
			return s.EndsWith("_Name");
		}
		return true;
	}

	private static Type ItemUtilitiesType()
	{
		if (_itemUtilitiesResolved)
		{
			return _itemUtilitiesType;
		}
		_itemUtilitiesResolved = true;
		try
		{
			_itemUtilitiesType = typeof(ItemUtilities);
			if (_itemUtilitiesType != null)
			{
				return _itemUtilitiesType;
			}
		}
		catch
		{
		}
		try
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (int i = 0; i < assemblies.Length; i++)
			{
				Type type = assemblies[i].GetType("TeamSoda.Duckov.Core.ItemUtilities");
				if (type != null)
				{
					_itemUtilitiesType = type;
					break;
				}
			}
		}
		catch
		{
		}
		return _itemUtilitiesType;
	}

	private static bool UtilBool(string method, Item item)
	{
		try
		{
			Type type = ItemUtilitiesType();
			if (type == null)
			{
				return false;
			}
			MethodInfo method2 = type.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (method2 == null)
			{
				return false;
			}
			return (bool)method2.Invoke(null, new object[1] { item });
		}
		catch
		{
			return false;
		}
	}

	public static bool InPlayerStorage(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return false;
		}
		return UtilBool("IsInPlayerStorage", item);
	}

	public static bool InPlayerCharacter(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return false;
		}
		return UtilBool("IsInPlayerCharacter", item);
	}

	public static string GetLocation(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return "?";
		}
		try
		{
			if (InPlayerStorage(item))
			{
				return "仓库";
			}
			if (InPlayerCharacter(item))
			{
				try
				{
					if (item.IsInCharacterSlot())
					{
						return "装备";
					}
				}
				catch
				{
				}
				return "背包";
			}
		}
		catch
		{
		}
		return "其它";
	}

	public static bool IsOwnedByPlayer(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return false;
		}
		if (InPlayerCharacter(item))
		{
			return true;
		}
		if (InPlayerStorage(item))
		{
			return true;
		}
		return false;
	}

	public static void DumpInventoryToLog()
	{
		try
		{
			List<Item> list = ItemUtilities.FindAllBelongsToPlayer((Predicate<Item>)((Item x) => true));
			if (list == null)
			{
				Debug.Log((object)"[TotemFusion] 诊断：未取到玩家物品列表。");
				return;
			}
			Debug.Log((object)("[TotemFusion] ==== 诊断开始：玩家物品 " + list.Count + " 件 ===="));
			int num = Math.Min(list.Count, 80);
			for (int num2 = 0; num2 < num; num2++)
			{
				Item val = list[num2];
				if (!((Object)(object)val == (Object)null))
				{
					try
					{
						bool flag = InPlayerCharacter(val);
						bool flag2 = InPlayerStorage(val);
						Debug.Log((object)string.Format("[TotemFusion] 诊断 #{0}: TypeID={1} 名称={2} 属性={3}(D2 {4}/原版 {5}/其它模组 {6}) 效果={13} 位置={7} 图腾={8} 可合成={9} 角色={10} 仓库={11} slot={12}", num2, val.TypeID, val.DisplayName, AffixCount(val) + CountCustomVars(val), AffixCount(val), CountVanillaModifiers(val), CountCustomVars(val), GetLocation(val), IsTotem(val), IsFusable(val), flag, flag2, val.IsInCharacterSlot(), CountEffects(val)));
					}
					catch
					{
					}
				}
			}
			Debug.Log((object)"[TotemFusion] ==== 诊断结束 ====");
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] 诊断失败: " + ex.Message));
		}
	}

	private static List<AffixEntry> ReadAffixes(Item item, bool positive)
	{
		List<AffixEntry> list = new List<AffixEntry>();
		try
		{
			string format = (positive ? "D2T_Pos_{0}_id" : "D2T_Neg_{0}_id");
			string format2 = (positive ? "D2T_Pos_{0}_key" : "D2T_Neg_{0}_key");
			string format3 = (positive ? "D2T_Pos_{0}_val" : "D2T_Neg_{0}_val");
			string format4 = (positive ? "D2T_Pos_{0}_mod" : "D2T_Neg_{0}_mod");
			int num = item.GetInt(positive ? "D2T_PosCount" : "D2T_NegCount", 0);
			for (int i = 0; i < num; i++)
			{
				list.Add(new AffixEntry
				{
					id = (item.GetString(string.Format(format, i), (string)null) ?? ""),
					key = (item.GetString(string.Format(format2, i), (string)null) ?? ""),
					val = item.GetFloat(string.Format(format3, i), 0f),
					mod = (item.GetString(string.Format(format4, i), (string)null) ?? "")
				});
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] ReadAffixes 异常: " + ex.Message));
		}
		return list;
	}

	private static bool TryReadMeta(Item item, out D2MetaJson meta)
	{
		meta = null;
		try
		{
			string text = item.GetString("D2T_Meta", (string)null);
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			string text2 = null;
			if (text.StartsWith("[D2T]"))
			{
				string s = text.Substring("[D2T]".Length);
				text2 = Encoding.UTF8.GetString(Convert.FromBase64String(s));
			}
			else if (text.TrimStart().StartsWith("{"))
			{
				text2 = text;
			}
			if (string.IsNullOrEmpty(text2))
			{
				return false;
			}
			meta = JsonUtility.FromJson<D2MetaJson>(text2);
			return meta != null;
		}
		catch
		{
		}
		return false;
	}

	private static List<AffixEntry> ReadAffixesSmart(Item item, bool positive, out bool fromMeta)
	{
		fromMeta = false;
		List<AffixEntry> list = ReadAffixes(item, positive);
		if (!TryReadMeta(item, out var meta) || meta == null)
		{
			return list;
		}
		D2AffixJson[] array = (positive ? meta.positives : meta.negatives);
		if (array == null || array.Length == 0)
		{
			return list;
		}
		List<AffixEntry> list2 = new List<AffixEntry>();
		D2AffixJson[] array2 = array;
		foreach (D2AffixJson d2AffixJson in array2)
		{
			if (d2AffixJson != null)
			{
				list2.Add(new AffixEntry
				{
					id = (d2AffixJson.id ?? ""),
					key = (d2AffixJson.statKey ?? ""),
					val = d2AffixJson.value,
					mod = (d2AffixJson.modifier ?? "")
				});
			}
		}
		if (list2.Count == 0)
		{
			return list;
		}
		HashSet<string> hashSet = new HashSet<string>();
		foreach (AffixEntry item2 in list)
		{
			hashSet.Add(MergeKeyOf(item2));
		}
		List<AffixEntry> list3 = new List<AffixEntry>(list);
		foreach (AffixEntry item3 in list2)
		{
			if (hashSet.Add(MergeKeyOf(item3)))
			{
				list3.Add(item3);
			}
		}
		fromMeta = list3.Count > list.Count;
		return list3;
	}

	private static string MergeKeyOf(AffixEntry e)
	{
		string text = e.key ?? "";
		string text2 = e.mod ?? "";
		if (text.Length > 0)
		{
			return "K:" + text + "|M:" + text2;
		}
		return "I:" + e.id + "|M:" + text2;
	}

	private static List<AffixEntry> MergeAffixes(List<AffixEntry> a, List<AffixEntry> b, bool mergeSame)
	{
		List<AffixEntry> list = new List<AffixEntry>();
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		List<AffixEntry>[] array = new List<AffixEntry>[2] { a, b };
		foreach (List<AffixEntry> list2 in array)
		{
			if (list2 == null)
			{
				continue;
			}
			foreach (AffixEntry item in list2)
			{
				string key = MergeKeyOf(item);
				if (dictionary.TryGetValue(key, out var value))
				{
					AffixEntry value2 = list[value];
					value2.val += item.val;
					list[value] = value2;
				}
				else
				{
					dictionary[key] = list.Count;
					list.Add(item);
				}
			}
		}
		return list;
	}

	private static object GetField(object obj, string name)
	{
		try
		{
			return obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj);
		}
		catch
		{
			return null;
		}
	}

	private static void SetField(object obj, string name, object value)
	{
		try
		{
			obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(obj, value);
		}
		catch
		{
		}
	}

	private static ModifierDescription CreateModifier(ModifierDescriptionCollection collection, object target, string key, ModifierType type, float value, bool enableInInventory, bool display, bool overrideOrder, int order)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected Obj, but got Unknown
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			ModifierDescription val = (ModifierDescription)Activator.CreateInstance(typeof(ModifierDescription), nonPublic: true);
			if (val == null)
			{
				return null;
			}
			SetField(val, "collection", collection);
			SetField(val, "target", target);
			SetField(val, "key", key ?? "");
			SetField(val, "type", type);
			SetField(val, "value", value);
			SetField(val, "enableInInventory", enableInInventory);
			SetField(val, "display", display);
			SetField(val, "overrideOrder", overrideOrder);
			SetField(val, "order", order);
			return val;
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] 创建属性条目失败: " + ex.Message));
			return null;
		}
	}

	private static bool AddModifierToCollection(ModifierDescriptionCollection col, ModifierDescription md)
	{
		if ((Object)(object)col == (Object)null || md == null)
		{
			return false;
		}
		int num = 0;
		try
		{
			num = col.Count;
		}
		catch
		{
		}
		try
		{
			col.Add(md);
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] Modifiers.Add 失败，尝试直接写入底层 list: " + ex.Message));
		}
		int num2 = 0;
		try
		{
			num2 = col.Count;
		}
		catch
		{
		}
		if (num2 > num)
		{
			return true;
		}
		try
		{
			FieldInfo field = ((object)col).GetType().GetField("list", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null && field.GetValue(col) is IList list)
			{
				list.Add(md);
				return true;
			}
		}
		catch (Exception ex2)
		{
			Debug.LogWarning((object)("[TotemFusion] 直接写入 list 也失败: " + ex2.Message));
		}
		return false;
	}

	private static List<ModifierDescription> SnapshotModifiers(Item item)
	{
		List<ModifierDescription> list = new List<ModifierDescription>();
		if ((Object)(object)item == (Object)null)
		{
			return list;
		}
		try
		{
			ModifierDescriptionCollection modifiers = item.Modifiers;
			if ((Object)(object)modifiers != (Object)null)
			{
				foreach (ModifierDescription item2 in modifiers)
				{
					if (item2 != null)
					{
						list.Add(item2);
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	private static int MergeVanillaModifiers(Item fused, Item otherItem, List<ModifierDescription> baseMods, List<ModifierDescription> otherMods, bool mergeSame, HashSet<string> stackedKeys)
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0357: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		try
		{
			ModifierDescriptionCollection modifiers = fused.Modifiers;
			if ((Object)(object)modifiers == (Object)null)
			{
				return 0;
			}
			List<D2Sig> sigs = CollectD2AffixSignatures(fused);
			if (baseMods != null)
			{
				bool flag = default;
				bool flag2 = default;
				bool flag3 = default;
				foreach (ModifierDescription baseMod in baseMods)
				{
					try
					{
						if (baseMod == null)
						{
							continue;
						}
						string key = baseMod.Key;
						ModifierType type = baseMod.Type;
						float value = baseMod.Value;
						if (!string.IsNullOrEmpty(key) && !key.StartsWith("D2T_", StringComparison.OrdinalIgnoreCase) && modifiers.Find((Predicate<ModifierDescription>)((ModifierDescription x) =>
						{
							//IL_0017: Unknown result type (might be due to invalid IL or missing references)
							//IL_001d: Unknown result type (might be due to invalid IL or missing references)
							return x != null && x.Key == key && x.Type == type;
						})) == null)
						{
							object field = GetField(baseMod, "target");
							object field2 = GetField(baseMod, "enableInInventory");
							int num2;
							if (field2 is bool)
							{
								flag = (bool)field2;
								num2 = 1;
							}
							else
							{
								num2 = 0;
							}
							bool enableInInventory = (byte)((uint)num2 & (flag ? 1u : 0u)) != 0;
							field2 = GetField(baseMod, "display");
							int num3;
							if (field2 is bool)
							{
								flag2 = (bool)field2;
								num3 = 1;
							}
							else
							{
								num3 = 0;
							}
							bool display = (byte)((uint)num3 & (flag2 ? 1u : 0u)) != 0;
							field2 = GetField(baseMod, "overrideOrder");
							int num4;
							if (field2 is bool)
							{
								flag3 = (bool)field2;
								num4 = 1;
							}
							else
							{
								num4 = 0;
							}
							bool overrideOrder = (byte)((uint)num4 & (flag3 ? 1u : 0u)) != 0;
							int order = ((GetField(baseMod, "order") is int num5) ? num5 : 0);
							ModifierDescription val = CreateModifier(modifiers, field, key, type, value, enableInInventory, display, overrideOrder, order);
							if (val != null && AddModifierToCollection(modifiers, val))
							{
								num++;
							}
						}
					}
					catch (Exception ex)
					{
						Debug.LogWarning((object)("[TotemFusion] 合并主图腾属性条目失败，跳过: " + ex.Message));
					}
				}
			}
			IEnumerable<ModifierDescription> enumerable = ((otherMods != null && otherMods.Count > 0) ? otherMods : null);
			if (enumerable == null)
			{
				try
				{
					enumerable = (IEnumerable<ModifierDescription>)(((Object)(object)otherItem != (Object)null) ? otherItem.Modifiers : null);
				}
				catch
				{
				}
			}
			if (enumerable != null)
			{
				bool flag4 = default;
				bool flag5 = default;
				bool flag6 = default;
				foreach (ModifierDescription item in enumerable)
				{
					try
					{
						if (item == null)
						{
							continue;
						}
						string key2 = item.Key;
						ModifierType type2 = item.Type;
						float value2 = item.Value;
						if (string.IsNullOrEmpty(key2) || key2.StartsWith("D2T_", StringComparison.OrdinalIgnoreCase))
						{
							continue;
						}
						object field3 = GetField(item, "target");
						object field2 = GetField(item, "enableInInventory");
						int num6;
						if (field2 is bool)
						{
							flag4 = (bool)field2;
							num6 = 1;
						}
						else
						{
							num6 = 0;
						}
						bool enableInInventory2 = (byte)((uint)num6 & (flag4 ? 1u : 0u)) != 0;
						field2 = GetField(item, "display");
						int num7;
						if (field2 is bool)
						{
							flag5 = (bool)field2;
							num7 = 1;
						}
						else
						{
							num7 = 0;
						}
						bool display2 = (byte)((uint)num7 & (flag5 ? 1u : 0u)) != 0;
						field2 = GetField(item, "overrideOrder");
						int num8;
						if (field2 is bool)
						{
							flag6 = (bool)field2;
							num8 = 1;
						}
						else
						{
							num8 = 0;
						}
						bool overrideOrder2 = (byte)((uint)num8 & (flag6 ? 1u : 0u)) != 0;
						int order2 = ((GetField(item, "order") is int num9) ? num9 : 0);
						ModifierDescription val2 = modifiers.Find((Predicate<ModifierDescription>)((ModifierDescription x) =>
						{
							//IL_0017: Unknown result type (might be due to invalid IL or missing references)
							//IL_001d: Unknown result type (might be due to invalid IL or missing references)
							return x != null && x.Key == key2 && x.Type == type2;
						}));
						if (val2 != null)
						{
							float value3 = val2.Value;
							if (stackedKeys != null && SigMatches(sigs, key2, type2, value3))
							{
								stackedKeys.Add(key2 + "|" + ((object)type2/*cast due to constrained. prefix*/).ToString());
							}
							SetField(val2, "value", value3 + value2);
						}
						else
						{
							ModifierDescription val3 = CreateModifier(modifiers, field3, key2, type2, value2, enableInInventory2, display2, overrideOrder2, order2);
							if (val3 != null && AddModifierToCollection(modifiers, val3))
							{
								num++;
							}
						}
					}
					catch (Exception ex2)
					{
						Debug.LogWarning((object)("[TotemFusion] 合并材料属性条目失败，跳过: " + ex2.Message));
					}
				}
			}
			try
			{
				modifiers.ReapplyModifiers();
			}
			catch
			{
			}
		}
		catch (Exception ex3)
		{
			Debug.LogWarning((object)("[TotemFusion] 合并原版属性失败: " + ex3.Message));
		}
		return num;
	}

	private static string SerializeModList(List<VanillaModJson> list)
	{
		try
		{
			return JsonConvert.SerializeObject((object)new VanillaModList
			{
				mods = list.ToArray()
			});
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TFix] Newtonsoft 序列化 KModSnap 失败，回退手动拼 JSON: " + ex));
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("{\"mods\":[");
		for (int i = 0; i < list.Count; i++)
		{
			VanillaModJson vanillaModJson = list[i];
			if (i > 0)
			{
				stringBuilder.Append(',');
			}
			stringBuilder.Append('{');
			stringBuilder.Append("\"key\":\"").Append(EscapeJson(vanillaModJson.key)).Append('"');
			stringBuilder.Append(",\"type\":\"").Append(EscapeJson(vanillaModJson.type)).Append('"');
			stringBuilder.Append(",\"value\":").Append(vanillaModJson.value.ToString(CultureInfo.InvariantCulture));
			stringBuilder.Append(",\"target\":\"").Append(EscapeJson(vanillaModJson.target)).Append('"');
			stringBuilder.Append(",\"inv\":").Append(vanillaModJson.inv ? "true" : "false");
			stringBuilder.Append(",\"display\":").Append(vanillaModJson.display ? "true" : "false");
			stringBuilder.Append(",\"oo\":").Append(vanillaModJson.oo ? "true" : "false");
			stringBuilder.Append(",\"order\":").Append(vanillaModJson.order);
			stringBuilder.Append(",\"stacked\":").Append(vanillaModJson.stacked ? "true" : "false");
			stringBuilder.Append('}');
		}
		stringBuilder.Append("]}");
		return stringBuilder.ToString();
	}

	private static string EscapeJson(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder(s.Length + 8);
		foreach (char c in s)
		{
			switch (c)
			{
			case '"':
				stringBuilder.Append("\\\"");
				continue;
			case '\\':
				stringBuilder.Append("\\\\");
				continue;
			case '\n':
				stringBuilder.Append("\\n");
				continue;
			case '\r':
				stringBuilder.Append("\\r");
				continue;
			case '\t':
				stringBuilder.Append("\\t");
				continue;
			}
			if (c < ' ')
			{
				StringBuilder stringBuilder2 = stringBuilder.Append("\\u");
				int num = c;
				stringBuilder2.Append(num.ToString("x4"));
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private static List<D2Sig> CollectD2AffixSignatures(Item item)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		List<D2Sig> list = new List<D2Sig>();
		if ((Object)(object)item == (Object)null)
		{
			return list;
		}
		for (int i = 0; i < 2; i++)
		{
			bool fromMeta;
			foreach (AffixEntry item2 in ReadAffixesSmart(item, i == 0, out fromMeta))
			{
				if (!string.IsNullOrEmpty(item2.key))
				{
					ModifierType type;
					try
					{
						type = (ModifierType)Enum.Parse(typeof(ModifierType), item2.mod, ignoreCase: true);
					}
					catch
					{
						continue;
					}
					list.Add(new D2Sig
					{
						key = item2.key,
						type = type,
						val = item2.val
					});
				}
			}
		}
		return list;
	}

	private static bool SigMatches(List<D2Sig> sigs, string key, ModifierType type, float value)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		foreach (D2Sig sig in sigs)
		{
			if (sig.key == key && sig.type == type && Math.Abs(sig.val - value) < 0.001f)
			{
				return true;
			}
		}
		return false;
	}

	public static bool PlaceFusedItem(Item item, out string destination, out string err)
	{
		destination = null;
		err = null;
		if ((Object)(object)item == (Object)null)
		{
			err = "物品为空";
			return false;
		}
		ForceDetach(item);
		try
		{
			item.MaxStackCount = 1;
			item.StackCount = 1;
		}
		catch
		{
		}
		try
		{
			bool flag = ItemUtilities.SendToPlayerCharacterInventory(item, true);
			if (flag && IsAlive(item) && IsHeldByPlayer(item))
			{
				destination = "背包";
				return true;
			}
			Debug.LogWarning((object)("[TotemFusion] 放入背包失败（返回 " + flag + "）：" + PlaceDiagnostics(item)));
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] 放入背包异常: " + ex.Message));
		}
		try
		{
			if (!IsAlive(item))
			{
				err = "合成品在放入过程中被销毁";
				return false;
			}
			ItemUtilities.SendToPlayerStorage(item, false);
			if (IsAlive(item) && IsHeldByPlayer(item))
			{
				destination = "仓库";
				return true;
			}
			Debug.LogWarning((object)("[TotemFusion] 放入仓库失败：" + PlaceDiagnostics(item)));
		}
		catch (Exception ex2)
		{
			Debug.LogWarning((object)("[TotemFusion] 放入仓库异常: " + ex2.Message));
		}
		try
		{
			if (IsAlive(item))
			{
				ItemUtilities.SendToPlayer(item, true, true);
				if (IsAlive(item) && IsHeldByPlayer(item))
				{
					destination = "背包/身上";
					return true;
				}
			}
		}
		catch (Exception ex3)
		{
			Debug.LogWarning((object)("[TotemFusion] 兜底放置异常: " + ex3.Message));
		}
		err = "背包和仓库都放不下（请至少腾出一个空位）";
		return false;
	}

	private static void ForceDetach(Item item)
	{
		if (!((Object)(object)item == (Object)null))
		{
			SetField(item, "pluggedIntoSlot", null);
			SetField(item, "inventory", null);
			try
			{
				item.Detach();
			}
			catch
			{
			}
			SetField(item, "pluggedIntoSlot", null);
			SetField(item, "inventory", null);
		}
	}

	public static bool IsAlive(Item item)
	{
		try
		{
			return (Object)(object)item != (Object)null;
		}
		catch
		{
			return false;
		}
	}

	private static string PlaceDiagnostics(Item item)
	{
		try
		{
			return $"可堆叠={IsAlive(item) && item.Stackable} 上限={(IsAlive(item) ? item.MaxStackCount : (-1))} 数量={(IsAlive(item) ? item.StackCount : (-1))} 物品存活={IsAlive(item)} 已入容器={IsHeldByPlayer(item)}";
		}
		catch
		{
			return "（诊断失败）";
		}
	}

	private static bool IsHeldByPlayer(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return false;
		}
		try
		{
			if (item.PluggedIntoSlot != null)
			{
				return true;
			}
		}
		catch
		{
		}
		try
		{
			if ((Object)(object)item.InInventory != (Object)null)
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	private static void WriteVanillaBlacklist(Item item, List<VanillaModJson> list)
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (VanillaModJson item2 in list)
			{
				if (item2 != null && !string.IsNullOrEmpty(item2.key))
				{
					if (stringBuilder.Length > 0)
					{
						stringBuilder.Append('\n');
					}
					stringBuilder.Append(item2.key).Append('|').Append(item2.type ?? "");
				}
			}
			item.SetString("TotemFusion_Van", stringBuilder.ToString(), true);
		}
		catch
		{
		}
	}

	private static List<VanillaModJson> ReadVanillaList(Item item)
	{
		try
		{
			string text = item.GetString("TotemFusion_Van", (string)null);
			if (string.IsNullOrEmpty(text))
			{
				return null;
			}
			if (text.IndexOf('{') >= 0)
			{
				try
				{
					VanillaModList vanillaModList = JsonConvert.DeserializeObject<VanillaModList>(text);
					if (vanillaModList != null && vanillaModList.mods != null && vanillaModList.mods.Length != 0)
					{
						return new List<VanillaModJson>(vanillaModList.mods);
					}
				}
				catch
				{
				}
				return null;
			}
			List<VanillaModJson> list = new List<VanillaModJson>();
			string[] array = text.Split('\n');
			foreach (string text2 in array)
			{
				if (!string.IsNullOrEmpty(text2))
				{
					string[] array2 = text2.Split(new char[1] { '|' }, 2);
					list.Add(new VanillaModJson
					{
						key = array2[0],
						type = ((array2.Length > 1) ? array2[1] : "")
					});
				}
			}
			return (list.Count > 0) ? list : null;
		}
		catch
		{
			return null;
		}
	}

	private static object GetMember(object obj, string name)
	{
		if (obj == null)
		{
			return null;
		}
		Type type = obj.GetType();
		try
		{
			PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null && property.CanRead)
			{
				return property.GetValue(obj);
			}
		}
		catch
		{
		}
		try
		{
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				return field.GetValue(obj);
			}
		}
		catch
		{
		}
		string name2 = char.ToLowerInvariant(name[0]) + name.Substring(1);
		try
		{
			PropertyInfo property2 = type.GetProperty(name2, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property2 != null && property2.CanRead)
			{
				return property2.GetValue(obj);
			}
		}
		catch
		{
		}
		try
		{
			FieldInfo field2 = type.GetField(name2, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field2 != null)
			{
				return field2.GetValue(obj);
			}
		}
		catch
		{
		}
		return null;
	}

	private static object InvokeMethod(object obj, string name)
	{
		if (obj == null)
		{
			return null;
		}
		try
		{
			MethodInfo method = obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
			if (method != null)
			{
				return method.Invoke(obj, null);
			}
		}
		catch
		{
		}
		return null;
	}

	private static string TargetTokenOf(Item item, object md)
	{
		try
		{
			object member = GetMember(md, "target");
			if (member == null)
			{
				return "Self";
			}
			object obj = GetMember(md, "Master") ?? item;
			if (obj != null && member == obj)
			{
				return "Self";
			}
			object obj2 = ((obj != null) ? GetMember(obj, "ParentItem") : null);
			if (obj2 != null && member == obj2)
			{
				return "Parent";
			}
			object obj3 = ((obj != null) ? InvokeMethod(obj, "GetCharacterItem") : null);
			if (obj3 != null && member == obj3)
			{
				return "Character";
			}
			return "Self";
		}
		catch
		{
			return "Self";
		}
	}

	private static object ResolveTargetObject(Item item, string token)
	{
		try
		{
			if (token == "Parent")
			{
				object member = GetMember(item, "ParentItem");
				if (member != null)
				{
					return member;
				}
			}
			else if (token == "Character")
			{
				object obj = InvokeMethod(item, "GetCharacterItem");
				if (obj != null)
				{
					return obj;
				}
			}
		}
		catch
		{
		}
		return item;
	}

	private static ModifierType ParseModType(string s)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!string.IsNullOrEmpty(s))
			{
				return (ModifierType)Enum.Parse(typeof(ModifierType), s, ignoreCase: true);
			}
		}
		catch
		{
		}
		return (ModifierType)0;
	}

	private static bool VanillaExists(ModifierDescriptionCollection col, string key, ModifierType type, float value)
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			foreach (ModifierDescription item in col)
			{
				if (item == null)
				{
					continue;
				}
				string text = "";
				try
				{
					text = item.Key;
				}
				catch
				{
				}
				if (!(text != key))
				{
					ModifierType val = (ModifierType)0;
					try
					{
						val = item.Type;
					}
					catch
					{
					}
					float num = 0f;
					try
					{
						num = item.Value;
					}
					catch
					{
					}
					if (val == type && Math.Abs(num - value) < 0.001f)
					{
						return true;
					}
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static void WriteModifierSnapshot(Item fused, List<ModifierDescription> baseMods, List<ModifierDescription> otherMods)
	{
		try
		{
			Dictionary<string, VanillaModJson> dictionary = new Dictionary<string, VanillaModJson>(StringComparer.Ordinal);
			if (baseMods != null)
			{
				AccumulateFrom(dictionary, fused, baseMods);
			}
			if (otherMods != null)
			{
				AccumulateFrom(dictionary, fused, otherMods);
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("[TFix][写盘诊断] map=").Append(dictionary.Count).Append(":");
			foreach (KeyValuePair<string, VanillaModJson> item in dictionary)
			{
				stringBuilder.Append(" [").Append(item.Key).Append('=')
					.Append(item.Value.value)
					.Append(']');
			}
			string text = SerializeModList(new List<VanillaModJson>(dictionary.Values));
			stringBuilder.Append(" JSON=").Append(text);
			fused.SetString("TotemFusion_ModSnap", text, true);
			List<VanillaModJson> list = ReadModTarget(fused);
			stringBuilder.Append(" 读回=").Append(list?.Count ?? 0).Append(":");
			if (list != null)
			{
				foreach (VanillaModJson item2 in list)
				{
					stringBuilder.Append(" [").Append(item2.key ?? "?").Append('|')
						.Append(item2.type ?? "?")
						.Append('=')
						.Append(item2.value)
						.Append(']');
				}
			}
			Debug.Log((object)stringBuilder.ToString());
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TFix][写盘诊断] 异常: " + ex));
		}
	}

	private static void AccumulateModifiers(Dictionary<string, VanillaModJson> map, Item item)
	{
		if (!((Object)(object)item == (Object)null))
		{
			IEnumerable<ModifierDescription> mods = null;
			try
			{
				mods = (IEnumerable<ModifierDescription>)item.Modifiers;
			}
			catch
			{
			}
			AccumulateFrom(map, item, mods);
		}
	}

	private static void AccumulateFrom(Dictionary<string, VanillaModJson> map, Item owner, IEnumerable<ModifierDescription> mods)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (mods == null)
		{
			return;
		}
		try
		{
			bool flag = default;
			bool flag2 = default;
			bool flag3 = default;
			foreach (ModifierDescription mod in mods)
			{
				if (mod == null)
				{
					continue;
				}
				string text = "";
				try
				{
					text = mod.Key;
				}
				catch
				{
				}
				if (!string.IsNullOrEmpty(text) && !text.StartsWith("D2T_", StringComparison.OrdinalIgnoreCase))
				{
					ModifierType val = (ModifierType)0;
					try
					{
						val = mod.Type;
					}
					catch
					{
					}
					float num = 0f;
					try
					{
						num = mod.Value;
					}
					catch
					{
					}
					string target = TargetTokenOf(owner, mod);
					object member = GetMember(mod, "enableInInventory");
					int num2;
					if (member is bool)
					{
						flag = (bool)member;
						num2 = 1;
					}
					else
					{
						num2 = 0;
					}
					bool inv = (byte)((uint)num2 & (flag ? 1u : 0u)) != 0;
					member = GetMember(mod, "display");
					int num3;
					if (member is bool)
					{
						flag2 = (bool)member;
						num3 = 1;
					}
					else
					{
						num3 = 0;
					}
					bool display = (byte)((uint)num3 & (flag2 ? 1u : 0u)) != 0;
					member = GetMember(mod, "overrideOrder");
					int num4;
					if (member is bool)
					{
						flag3 = (bool)member;
						num4 = 1;
					}
					else
					{
						num4 = 0;
					}
					bool oo = (byte)((uint)num4 & (flag3 ? 1u : 0u)) != 0;
					int order = ((GetMember(mod, "order") is int num5) ? num5 : 0);
					string key = text + "|" + ((object)val/*cast due to constrained. prefix*/).ToString();
					if (map.TryGetValue(key, out var value))
					{
						value.value += num;
						continue;
					}
					map[key] = new VanillaModJson
					{
						key = text,
						type = ((object)val/*cast due to constrained. prefix*/).ToString(),
						value = num,
						target = target,
						inv = inv,
						display = display,
						oo = oo,
						order = order,
						stacked = false
					};
				}
			}
		}
		catch
		{
		}
	}

	private static int RepairModifiers(Item item)
	{
		//IL_0403: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_03eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c9: Unknown result type (might be due to invalid IL or missing references)
		List<VanillaModJson> list = ReadModTarget(item);
		if (list == null || list.Count == 0)
		{
			return 0;
		}
		ModifierDescriptionCollection modifiers = item.Modifiers;
		if ((Object)(object)modifiers == (Object)null)
		{
			return 0;
		}
		int num = 0;
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("[TotemFusion][诊断] RepairModifiers 快照(").Append(list.Count).Append("):");
			foreach (VanillaModJson item2 in list)
			{
				stringBuilder.Append(" [").Append((item2.key ?? "?") + "|" + (item2.type ?? "?") + "=" + item2.value.ToString("0.####")).Append("]");
			}
			stringBuilder.Append(" 实际Modifiers(").Append(CountVanillaModifiers(item)).Append("):");
			try
			{
				foreach (ModifierDescription modifier in item.Modifiers)
				{
					string text = "";
					ModifierType val = (ModifierType)0;
					float num2 = 0f;
					try
					{
						text = modifier.Key;
					}
					catch
					{
					}
					try
					{
						val = modifier.Type;
					}
					catch
					{
					}
					try
					{
						num2 = modifier.Value;
					}
					catch
					{
					}
					stringBuilder.Append(" [").Append((text ?? "?") + "|" + ((object)val/*cast due to constrained. prefix*/).ToString() + "=" + num2.ToString("0.####")).Append("]");
				}
			}
			catch
			{
			}
			Debug.Log((object)stringBuilder.ToString());
		}
		catch
		{
		}
		foreach (VanillaModJson item3 in list)
		{
			if (item3 == null || string.IsNullOrEmpty(item3.key) || item3.key.StartsWith("D2T_", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			ModifierType type = ParseModType(item3.type);
			ModifierDescription val2 = FindVanilla(modifiers, item3.key, type);
			if (val2 != null)
			{
				float num3 = 0f;
				try
				{
					num3 = val2.Value;
				}
				catch
				{
				}
				if (Math.Abs(num3 - item3.value) > 0.001f)
				{
					SetField(val2, "value", item3.value);
					num++;
				}
			}
			else
			{
				object target = ResolveTargetObject(item, item3.target);
				ModifierDescription val3 = CreateModifier(modifiers, target, item3.key, type, item3.value, item3.inv, item3.display, item3.oo, item3.order);
				if (val3 != null && AddModifierToCollection(modifiers, val3))
				{
					num++;
				}
			}
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (VanillaModJson item4 in list)
		{
			if (item4 != null && !string.IsNullOrEmpty(item4.key) && !item4.key.StartsWith("D2T_", StringComparison.OrdinalIgnoreCase))
			{
				hashSet.Add(item4.key + "|" + item4.type);
			}
		}
		List<ModifierDescription> list2 = new List<ModifierDescription>();
		foreach (ModifierDescription item5 in modifiers)
		{
			if (item5 == null)
			{
				continue;
			}
			string text2 = "";
			try
			{
				text2 = item5.Key;
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(text2) && !text2.StartsWith("D2T_", StringComparison.OrdinalIgnoreCase))
			{
				ModifierType val4 = (ModifierType)0;
				try
				{
					val4 = item5.Type;
				}
				catch
				{
				}
				if (!hashSet.Contains(text2 + "|" + ((object)val4).ToString()))
				{
					list2.Add(item5);
				}
			}
		}
		if (list2.Count > 0)
		{
			foreach (ModifierDescription item6 in list2)
			{
				bool flag = false;
				try
				{
					MethodInfo method = ((object)modifiers).GetType().GetMethod("Remove", new Type[1] { typeof(ModifierDescription) });
					if (method != null)
					{
						method.Invoke(modifiers, new object[1] { item6 });
						flag = true;
					}
				}
				catch
				{
				}
				if (flag)
				{
					num++;
				}
			}
		}
		if (num > 0)
		{
			try
			{
				modifiers.ReapplyModifiers();
			}
			catch
			{
			}
		}
		return num;
	}

	private static List<VanillaModJson> ReadModTarget(Item item)
	{
		try
		{
			string text = item.GetString("TotemFusion_ModSnap", (string)null);
			if (!string.IsNullOrEmpty(text))
			{
				try
				{
					VanillaModList vanillaModList = JsonConvert.DeserializeObject<VanillaModList>(text);
					if (vanillaModList != null && vanillaModList.mods != null)
					{
						return new List<VanillaModJson>(vanillaModList.mods);
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning((object)("[TFix] Newtonsoft 反序列化 KModSnap 失败，回退 JsonUtility: " + ex));
				}
				try
				{
					VanillaModList vanillaModList2 = JsonUtility.FromJson<VanillaModList>(text);
					if (vanillaModList2 != null && vanillaModList2.mods != null)
					{
						return new List<VanillaModJson>(vanillaModList2.mods);
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		try
		{
			string text2 = item.GetString("TotemFusion_Src", (string)null);
			if (!string.IsNullOrEmpty(text2))
			{
				Dictionary<string, VanillaModJson> dictionary = new Dictionary<string, VanillaModJson>(StringComparer.Ordinal);
				string[] array = text2.Split(',');
				for (int i = 0; i < array.Length; i++)
				{
					if (!int.TryParse(array[i].Trim(), out var result) || result <= 0)
					{
						continue;
					}
					Item val = null;
					bool flag = false;
					try
					{
						val = ItemAssetsCollection.GetPrefab(result);
					}
					catch
					{
					}
					if ((Object)(object)val == (Object)null)
					{
						try
						{
							val = ItemAssetsCollection.InstantiateSync(result);
							flag = true;
						}
						catch
						{
						}
					}
					if (!((Object)(object)val != (Object)null))
					{
						continue;
					}
					AccumulateModifiers(dictionary, val);
					if (flag)
					{
						try
						{
							Object.Destroy((Object)(object)((Component)val).gameObject);
						}
						catch
						{
						}
					}
				}
				if (dictionary.Count > 0)
				{
					return new List<VanillaModJson>(dictionary.Values);
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static ModifierDescription FindVanilla(ModifierDescriptionCollection col, string key, ModifierType type)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			foreach (ModifierDescription item in col)
			{
				if (item == null)
				{
					continue;
				}
				string text = "";
				try
				{
					text = item.Key;
				}
				catch
				{
				}
				if (!(text != key))
				{
					ModifierType val = (ModifierType)0;
					try
					{
						val = item.Type;
					}
					catch
					{
					}
					if (val == type)
					{
						return item;
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static void ReapplyVanillaRemovals(Item item)
	{
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			string value = item.GetString("TotemFusion_ModSnap", (string)null);
			int num = 0;
			try
			{
				num = ReadVanillaList(item)?.Count ?? 0;
			}
			catch
			{
			}
			int num2 = 0;
			try
			{
				num2 = CountVanillaModifiers(item);
			}
			catch
			{
			}
			Debug.Log((object)("[TFix] ReapplyVanillaRemovals 入口: 有KModSnap=" + !string.IsNullOrEmpty(value) + " 黑名单数=" + num + " 移除前Mods=" + num2));
			if (!string.IsNullOrEmpty(value))
			{
				return;
			}
			List<VanillaModJson> list = ReadVanillaList(item);
			if (list == null || list.Count == 0)
			{
				return;
			}
			ModifierDescriptionCollection modifiers = item.Modifiers;
			if ((Object)(object)modifiers == (Object)null)
			{
				return;
			}
			bool flag = false;
			foreach (VanillaModJson item2 in list)
			{
				if (item2 == null || string.IsNullOrEmpty(item2.key))
				{
					continue;
				}
				foreach (ModifierDescription item3 in modifiers)
				{
					if (item3 == null)
					{
						continue;
					}
					string text = "";
					try
					{
						text = item3.Key;
					}
					catch
					{
					}
					if (text != item2.key)
					{
						continue;
					}
					string a = "";
					try
					{
						a = ((object)item3.Type/*cast due to constrained. prefix*/).ToString();
					}
					catch
					{
					}
					if (!string.Equals(a, item2.type, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					try
					{
						MethodInfo method = ((object)modifiers).GetType().GetMethod("Remove", new Type[1] { ((object)item3).GetType() });
						if (method == null)
						{
							method = ((object)modifiers).GetType().GetMethod("Remove", new Type[1] { typeof(object) });
						}
						method?.Invoke(modifiers, new object[1] { item3 });
						flag = true;
					}
					catch
					{
					}
					break;
				}
			}
			if (flag)
			{
				try
				{
					modifiers.ReapplyModifiers();
				}
				catch
				{
				}
			}
			Debug.Log((object)("[TFix] ReapplyVanillaRemovals 结果: changed=" + flag + " 移除前Mods=" + num2 + " 移除后Mods=" + CountVanillaModifiers(item)));
		}
		catch
		{
		}
	}

	internal static bool SanitizeD2Meta(Item item)
	{
		try
		{
			List<VanillaModJson> list = ReadVanillaList(item);
			if (list == null || list.Count == 0)
			{
				Debug.Log((object)"[TFix][D2诊断] SanitizeD2Meta: 黑名单空，跳过");
				return false;
			}
			string text = null;
			try
			{
				text = item.GetString("D2T_Meta", (string)null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(text) || !text.StartsWith("[D2T]"))
			{
				string? text2;
				if (string.IsNullOrEmpty(text))
				{
					text2 = "null";
				}
				else
				{
					text2 = ((text.Length > 24) ? (text.Substring(0, 24) + "...") : text);
				}
				Debug.Log((object)("[TFix][D2诊断] SanitizeD2Meta: 无[D2T]meta，跳过 raw=" + text2));
				return false;
			}
			string s = text.Substring("[D2T]".Length);
			string text3 = null;
			try
			{
				text3 = Encoding.UTF8.GetString(Convert.FromBase64String(s));
			}
			catch
			{
				return false;
			}
			if (string.IsNullOrEmpty(text3))
			{
				return false;
			}
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (VanillaModJson item2 in list)
			{
				if (item2 != null && !string.IsNullOrEmpty(item2.key))
				{
					hashSet.Add(item2.key);
				}
			}
			if (hashSet.Count == 0)
			{
				return false;
			}
			int num = 0;
			try
			{
				JObject val = JObject.Parse(text3);
				string[] array = new string[2] { "positives", "negatives" };
				foreach (string text4 in array)
				{
					JToken val2 = val[text4];
					JArray val3 = (JArray)(object)((val2 is JArray) ? val2 : null);
					if (val3 == null)
					{
						continue;
					}
					for (int num2 = ((JContainer)val3).Count - 1; num2 >= 0; num2--)
					{
						JToken val4 = val3[num2];
						JObject val5 = (JObject)(object)((val4 is JObject) ? val4 : null);
						if (val5 != null)
						{
							string text5 = ((object)val5["statKey"])?.ToString();
							if (!string.IsNullOrEmpty(text5) && hashSet.Contains(text5))
							{
								val3.RemoveAt(num2);
								num++;
							}
						}
					}
				}
				if (num > 0)
				{
					string text6 = "[D2T]" + Convert.ToBase64String(Encoding.UTF8.GetBytes(((JToken)val).ToString((Formatting)0, Array.Empty<JsonConverter>())));
					item.SetString("D2T_Meta", text6, true);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning((object)("[TotemFusion] SanitizeD2Meta 解析失败(退回不改动): " + ex.Message));
				return false;
			}
			if (num > 0)
			{
				Debug.Log((object)("[TFix] SanitizeD2Meta: 从 D2T_Meta 剔除 " + num + " 个已删词条"));
			}
			return num > 0;
		}
		catch
		{
			return false;
		}
	}

	private static void ReapplyD2Removals(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return;
		}
		try
		{
			if (IsD2Totem(item))
			{
				try
				{
					ForceD2Reapply(item);
					return;
				}
				catch
				{
					return;
				}
			}
		}
		catch
		{
		}
	}

	public static void RepairFusedItem(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return;
		}
		try
		{
			string value = null;
			try
			{
				value = item.GetString("TotemFusion_Made", (string)null);
			}
			catch
			{
			}
			string value2 = null;
			try
			{
				value2 = item.GetString("TotemFusion_Src", (string)null);
			}
			catch
			{
			}
			try
			{
				item.GetString("TotemFusion_ModSnap", (string)null);
			}
			catch
			{
			}
			Debug.Log((object)("[TFix] RepairFusedItem 入口: made=" + (string.IsNullOrEmpty(value) ? "无(原版)" : "有(融合)") + " src=" + (string.IsNullOrEmpty(value2) ? "无" : "有") + " IsD2=" + IsD2Totem(item)));
			ReapplyVanillaRemovals(item);
			try
			{
				ReapplyD2Removals(item);
			}
			catch
			{
			}
			try
			{
				PurgeRemovedEffects(item);
			}
			catch
			{
			}
		}
		catch
		{
		}
		try
		{
			string value3 = null;
			try
			{
				value3 = item.GetString("TotemFusion_Made", (string)null);
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(value3))
			{
				int num = 0;
				try
				{
					num = CountVanillaModifiers(item);
				}
				catch
				{
				}
				int num2 = 0;
				try
				{
					num2 = CountEffects(item);
				}
				catch
				{
				}
				try
				{
					RepairModifiers(item);
				}
				catch (Exception ex)
				{
					Debug.LogWarning((object)("[TFix] RepairModifiers 异常: " + ex));
				}
				try
				{
					RepairFusedEffects(item);
				}
				catch (Exception ex2)
				{
					Debug.LogWarning((object)("[TFix] RepairFusedEffects 异常: " + ex2));
				}
				try
				{
					PurgeRemovedEffects(item);
				}
				catch (Exception ex3)
				{
					Debug.LogWarning((object)("[TFix] PurgeRemovedEffects 异常: " + ex3));
				}
				try
				{
					ReapplyIdentity(item);
				}
				catch (Exception ex4)
				{
					Debug.LogWarning((object)("[TFix] ReapplyIdentity 异常: " + ex4));
				}
				try
				{
					RepairPrice(item);
				}
				catch (Exception ex5)
				{
					Debug.LogWarning((object)("[TFix] RepairPrice 异常: " + ex5));
				}
				try
				{
					ForceNonStackable(item);
				}
				catch (Exception ex6)
				{
					Debug.LogWarning((object)("[TFix] ForceNonStackable 异常: " + ex6));
				}
				int num3 = 0;
				try
				{
					num3 = CountVanillaModifiers(item);
				}
				catch
				{
				}
				int num4 = 0;
				try
				{
					num4 = CountEffects(item);
				}
				catch
				{
				}
				bool flag = false;
				try
				{
					flag = !string.IsNullOrEmpty(item.GetString("TotemFusion_ModSnap", (string)null));
				}
				catch
				{
				}
				bool flag2 = false;
				try
				{
					flag2 = !string.IsNullOrEmpty(item.GetString("TotemFusion_RemovedFX", (string)null));
				}
				catch
				{
				}
				Debug.Log((object)("[TFix][汇总] RepairFusedItem 完成: modsBefore=" + num + " modsAfter=" + num3 + " fxBefore=" + num2 + " fxAfter=" + num4 + " 有KModSnap=" + flag + " 有已删FX=" + flag2));
			}
		}
		catch (Exception ex7)
		{
			Debug.LogWarning((object)("[TFix] RepairFusedItem 外层异常: " + ex7));
		}
	}

	private static int GetIntMember(Item item, string[] names, int def)
	{
		Type type = ((object)item).GetType();
		foreach (string name in names)
		{
			try
			{
				PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (property != null && property.CanRead && property.PropertyType == typeof(int))
				{
					return (int)property.GetValue(item, null);
				}
			}
			catch
			{
			}
			try
			{
				FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null && field.FieldType == typeof(int))
				{
					return (int)field.GetValue(item);
				}
			}
			catch
			{
			}
		}
		return def;
	}

	private static bool SetIntMember(Item item, string[] names, int value)
	{
		Type type = ((object)item).GetType();
		foreach (string name in names)
		{
			try
			{
				PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (property != null && property.CanWrite && property.PropertyType == typeof(int))
				{
					property.SetValue(item, value, null);
					return true;
				}
			}
			catch
			{
			}
			try
			{
				FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null && field.FieldType == typeof(int))
				{
					field.SetValue(item, value);
					return true;
				}
			}
			catch
			{
			}
		}
		return false;
	}

	private static void ForceNonStackable(Item item)
	{
		try
		{
			int intMember = GetIntMember(item, new string[4] { "MaxStackCount", "maxStackCount", "MaxStack", "StackLimit" }, 1);
			if (intMember > 1 && SetIntMember(item, new string[4] { "MaxStackCount", "maxStackCount", "MaxStack", "StackLimit" }, 1))
			{
				Debug.Log((object)("[TotemFusion][诊断] 强制图腾不可堆叠(上限): " + intMember + "→1"));
			}
			int intMember2 = GetIntMember(item, new string[4] { "StackCount", "stackCount", "Amount", "StackSize" }, 1);
			if (intMember2 > 1 && SetIntMember(item, new string[4] { "StackCount", "stackCount", "Amount", "StackSize" }, 1))
			{
				Debug.Log((object)("[TotemFusion][诊断] 强制图腾不可堆叠(当前数): " + intMember2 + "→1"));
			}
		}
		catch
		{
		}
	}

	private static void RepairPrice(Item item)
	{
		int num = 0;
		bool flag = false;
		try
		{
			num = item.GetInt("TotemFusion_Price", -1);
		}
		catch
		{
		}
		if (num <= 0)
		{
			try
			{
				num = item.GetInt("D2T_SaleValue", -1);
			}
			catch
			{
			}
		}
		if (num <= 0)
		{
			num = DerivePriceFromSources(item);
			flag = num > 0;
		}
		if (num <= 0)
		{
			return;
		}
		int num2 = 0;
		try
		{
			num2 = item.Value;
		}
		catch
		{
		}
		if (num2 == num)
		{
			if (flag)
			{
				Debug.Log((object)("[TotemFusion][诊断] RepairPrice: 无快照，已从源派生售价并已是 " + num));
			}
			return;
		}
		try
		{
			item.Value = num;
			Debug.Log((object)("[TotemFusion][诊断] RepairPrice: 还原售价 " + num2 + " → " + num + (flag ? " (派生自源)" : "")));
		}
		catch
		{
		}
	}

	private static int DerivePriceFromSources(Item item)
	{
		string text = null;
		try
		{
			text = item.GetString("TotemFusion_Src", (string)null);
		}
		catch
		{
		}
		if (string.IsNullOrEmpty(text))
		{
			return -1;
		}
		List<int> list = new List<int>();
		string[] array = text.Split(',');
		for (int i = 0; i < array.Length; i++)
		{
			if (!int.TryParse(array[i].Trim(), out var result) || result <= 0)
			{
				continue;
			}
			Item val = null;
			bool flag = false;
			try
			{
				val = ItemAssetsCollection.GetPrefab(result);
			}
			catch
			{
			}
			if ((Object)(object)val == (Object)null)
			{
				try
				{
					val = ItemAssetsCollection.InstantiateSync(result);
					flag = true;
				}
				catch
				{
				}
			}
			if ((Object)(object)val == (Object)null)
			{
				continue;
			}
			int num = SaleValueOf(val);
			if (flag)
			{
				try
				{
					Object.Destroy((Object)(object)((Component)val).gameObject);
				}
				catch
				{
				}
			}
			if (num > 0)
			{
				list.Add(num);
			}
		}
		if (list.Count < 2)
		{
			return -1;
		}
		int num2 = list[0];
		for (int j = 1; j < list.Count; j++)
		{
			int num3 = Math.Max(num2, list[j]);
			int num4 = Math.Min(num2, list[j]);
			if (num3 <= 0)
			{
				return 0;
			}
			num2 = num3 + (int)Mathf.Round((float)num4 * 0.5f);
		}
		if (num2 <= 0)
		{
			return -1;
		}
		return num2;
	}

	private static void ReapplyIdentity(Item item)
	{
		try
		{
			string text = null;
			try
			{
				text = item.GetString("TotemFusion_Name", (string)null);
			}
			catch
			{
			}
			if (!string.IsNullOrWhiteSpace(text))
			{
				try
				{
					item.DisplayNameRaw = text;
				}
				catch
				{
				}
				try
				{
					item.SetString("D2T_Name", text, true);
				}
				catch
				{
				}
			}
			string text2 = null;
			bool flag = false;
			try
			{
				flag = !string.IsNullOrEmpty(item.GetString("TotemFusion_Src", (string)null));
			}
			catch
			{
			}
			try
			{
				text2 = item.GetString("TotemFusion_Desc", (string)null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(text2) & flag)
			{
				try
				{
					text2 = ResolveFromSources(item);
				}
				catch
				{
				}
			}
			string text3 = (string.IsNullOrEmpty(text2) ? "" : StripTierRemarks(text2));
			if (flag && string.IsNullOrEmpty(text3))
			{
				try
				{
					List<AffixEntry> list = ReadAffixesSmart(item, positive: true, out var _);
					List<AffixEntry> list2 = ReadAffixesSmart(item, positive: false, out var _);
					OrderAffixes(list);
					OrderAffixes(list2);
					string text4 = BuildAffixDescription(list, list2);
					if (!string.IsNullOrEmpty(text4))
					{
						text3 = text4;
					}
				}
				catch
				{
				}
			}
			if (!string.IsNullOrEmpty(text3))
			{
				try
				{
					SetDescriptionRaw(item, text3);
				}
				catch
				{
				}
				try
				{
					item.SetString("D2T_Desc", text3, true);
					return;
				}
				catch
				{
					return;
				}
			}
		}
		catch
		{
		}
	}

	private static Dictionary<string, float> CollectD2BaseValues(Item item)
	{
		Dictionary<string, float> map = new Dictionary<string, float>();
		if ((Object)(object)item == (Object)null)
		{
			return map;
		}
		List<AffixEntry> list = ReadAffixesSmart(item, positive: true, out var _);
		List<AffixEntry> list2 = ReadAffixesSmart(item, positive: false, out var _);
		Action<AffixEntry> action = (AffixEntry e) =>
		{
			if (!string.IsNullOrEmpty(e.key))
			{
				map.TryGetValue(e.key, out var value);
				map[e.key] = value + e.val;
			}
		};
		if (list != null)
		{
			foreach (AffixEntry item2 in list)
			{
				action(item2);
			}
		}
		if (list2 != null)
		{
			foreach (AffixEntry item3 in list2)
			{
				action(item3);
			}
		}
		return map;
	}

	private static List<VanillaModJson> BuildVanillaSnapshot(Item baseItem, Item otherItem, bool mergeSame)
	{
		Dictionary<string, float> dictionary = new Dictionary<string, float>();
		foreach (KeyValuePair<string, float> item in CollectD2BaseValues(baseItem))
		{
			dictionary[item.Key] = item.Value;
		}
		foreach (KeyValuePair<string, float> item2 in CollectD2BaseValues(otherItem))
		{
			dictionary.TryGetValue(item2.Key, out var value);
			dictionary[item2.Key] = value + item2.Value;
		}
		List<VanillaModJson> list = new List<VanillaModJson>();
		HashSet<string> seen = new HashSet<string>();
		CollectInto(baseItem, list, seen, mergeSame);
		CollectInto(otherItem, list, seen, mergeSame);
		foreach (VanillaModJson item3 in list)
		{
			if (dictionary.ContainsKey(item3.key))
			{
				item3.stacked = true;
			}
		}
		return list;
	}

	private static void CollectInto(Item src, List<VanillaModJson> result, HashSet<string> seen, bool mergeSame)
	{
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)src == (Object)null)
		{
			return;
		}
		List<VanillaModJson> list = ReadVanillaList(src);
		if (list != null && list.Count > 0)
		{
			foreach (VanillaModJson m in list)
			{
				if (m != null && !string.IsNullOrEmpty(m.key))
				{
					string item = m.key + "|" + m.type;
					if (seen.Contains(item))
					{
						VanillaModJson vanillaModJson = result.Find((VanillaModJson x) => x.key == m.key && x.type == m.type);
						if (vanillaModJson != null)
						{
							vanillaModJson.value += m.value;
						}
					}
					else
					{
						seen.Add(item);
						result.Add(m);
					}
				}
			}
			return;
		}
		List<D2Sig> sigs = CollectD2AffixSignatures(src);
		ModifierDescriptionCollection modifiers = src.Modifiers;
		if ((Object)(object)modifiers == (Object)null)
		{
			return;
		}
		bool flag = default;
		bool flag2 = default;
		bool flag3 = default;
		foreach (ModifierDescription item3 in modifiers)
		{
			if (item3 == null)
			{
				continue;
			}
			string key = item3.Key;
			ModifierType type = item3.Type;
			float value = item3.Value;
			string item2 = key + "|" + ((object)type/*cast due to constrained. prefix*/).ToString();
			if (SigMatches(sigs, key, type, value))
			{
				continue;
			}
			if (seen.Contains(item2))
			{
				VanillaModJson vanillaModJson2 = result.Find((VanillaModJson x) => x.key == key && x.type == ((object)type/*cast due to constrained. prefix*/).ToString());
				if (vanillaModJson2 != null)
				{
					vanillaModJson2.value += value;
				}
				continue;
			}
			seen.Add(item2);
			VanillaModJson vanillaModJson3 = new VanillaModJson
			{
				key = key,
				type = ((object)type/*cast due to constrained. prefix*/).ToString(),
				value = value,
				target = TargetTokenOf(src, item3)
			};
			object field = GetField(item3, "enableInInventory");
			int num;
			if (field is bool)
			{
				flag = (bool)field;
				num = 1;
			}
			else
			{
				num = 0;
			}
			vanillaModJson3.inv = (byte)((uint)num & (flag ? 1u : 0u)) != 0;
			field = GetField(item3, "display");
			int num2;
			if (field is bool)
			{
				flag2 = (bool)field;
				num2 = 1;
			}
			else
			{
				num2 = 0;
			}
			vanillaModJson3.display = (byte)((uint)num2 & (flag2 ? 1u : 0u)) != 0;
			field = GetField(item3, "overrideOrder");
			int num3;
			if (field is bool)
			{
				flag3 = (bool)field;
				num3 = 1;
			}
			else
			{
				num3 = 0;
			}
			vanillaModJson3.oo = (byte)((uint)num3 & (flag3 ? 1u : 0u)) != 0;
			vanillaModJson3.order = ((GetField(item3, "order") is int num4) ? num4 : 0);
			vanillaModJson3.stacked = false;
			result.Add(vanillaModJson3);
		}
	}

	private static int MergeGenericVariables(Item fused, Item otherItem, bool mergeSame)
	{
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		try
		{
			CustomDataCollection variables = otherItem.Variables;
			CustomDataCollection variables2 = fused.Variables;
			if (variables == null || variables2 == null)
			{
				return 0;
			}
			foreach (CustomData item in variables)
			{
				try
				{
					if (item == null)
					{
						continue;
					}
					string key = item.Key;
					if (!IsMergeableVarKey(key))
					{
						continue;
					}
					string text = ((object)item.DataType/*cast due to constrained. prefix*/).ToString();
					CustomData val = null;
					try
					{
						val = variables2.GetEntry(key);
					}
					catch
					{
					}
					if (val != null && ((object)val.DataType/*cast due to constrained. prefix*/).ToString() == text)
					{
						if (text == "Float")
						{
							float num2 = val.GetFloat();
							float num3 = item.GetFloat();
							val.SetFloat(num2 + num3);
							num++;
						}
						else if (text == "Int")
						{
							int num4 = val.GetInt();
							int num5 = item.GetInt();
							val.SetInt(num4 + num5);
							num++;
						}
					}
					else if (val == null)
					{
						bool flag = false;
						try
						{
							flag = item.Display;
						}
						catch
						{
						}
						switch (text)
						{
						case "Float":
							fused.SetFloat(key, item.GetFloat(), true);
							break;
						case "Int":
							fused.SetInt(key, item.GetInt(), true);
							break;
						case "Bool":
							fused.SetBool(key, item.GetBool(), true);
							break;
						case "String":
							fused.SetString(key, item.GetString(), true);
							break;
						}
						try
						{
							variables2.SetDisplay(key, flag);
						}
						catch
						{
						}
						num++;
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning((object)("[TotemFusion] 合并自定义属性条目失败，跳过: " + ex.Message));
				}
			}
			try
			{
				((object)variables2).GetType().GetMethod("RebuildDictionary", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(variables2, null);
			}
			catch
			{
			}
		}
		catch (Exception ex2)
		{
			Debug.LogWarning((object)("[TotemFusion] 通用属性合并失败: " + ex2.Message));
		}
		return num;
	}

	private static string JsonStr(string s)
	{
		if (s == null)
		{
			return "\"\"";
		}
		StringBuilder stringBuilder = new StringBuilder(s.Length + 2);
		stringBuilder.Append('"');
		foreach (char c in s)
		{
			switch (c)
			{
			case '"':
				stringBuilder.Append("\\\"");
				continue;
			case '\\':
				stringBuilder.Append("\\\\");
				continue;
			case '\n':
				stringBuilder.Append("\\n");
				continue;
			case '\r':
				stringBuilder.Append("\\r");
				continue;
			case '\t':
				stringBuilder.Append("\\t");
				continue;
			}
			if (c < ' ')
			{
				StringBuilder stringBuilder2 = stringBuilder.Append("\\u");
				int num = c;
				stringBuilder2.Append(num.ToString("x4"));
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		stringBuilder.Append('"');
		return stringBuilder.ToString();
	}

	private static string JsonNum(float f)
	{
		if (float.IsNaN(f) || float.IsInfinity(f))
		{
			f = 0f;
		}
		return f.ToString("R", CultureInfo.InvariantCulture);
	}

	private static string AffixArrayJson(List<AffixEntry> list)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append('[');
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append(',');
				}
				AffixEntry affixEntry = list[i];
				stringBuilder.Append('{');
				stringBuilder.Append("\"id\":").Append(JsonStr(affixEntry.id ?? ""));
				stringBuilder.Append(",\"statKey\":").Append(JsonStr(affixEntry.key ?? ""));
				stringBuilder.Append(",\"value\":").Append(JsonNum(affixEntry.val));
				stringBuilder.Append(",\"modifier\":").Append(JsonStr(affixEntry.mod ?? ""));
				stringBuilder.Append('}');
			}
		}
		stringBuilder.Append(']');
		return stringBuilder.ToString();
	}

	private static string BuildMetaJson(List<AffixEntry> pos, List<AffixEntry> neg, string displayName, string remark, int quality, int displayQuality, int value, int typeId, string rarityId)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append('{');
		stringBuilder.Append("\"rarityId\":").Append(JsonStr(rarityId ?? ""));
		stringBuilder.Append(",\"quality\":").Append(quality.ToString(CultureInfo.InvariantCulture));
		stringBuilder.Append(",\"displayQuality\":").Append(displayQuality.ToString(CultureInfo.InvariantCulture));
		stringBuilder.Append(",\"displayName\":").Append(JsonStr(displayName ?? ""));
		stringBuilder.Append(",\"remark\":").Append(JsonStr(remark ?? ""));
		stringBuilder.Append(",\"value\":").Append(value.ToString(CultureInfo.InvariantCulture));
		stringBuilder.Append(",\"typeId\":").Append(typeId.ToString(CultureInfo.InvariantCulture));
		stringBuilder.Append(",\"positives\":").Append(AffixArrayJson(pos));
		stringBuilder.Append(",\"negatives\":").Append(AffixArrayJson(neg));
		stringBuilder.Append('}');
		return stringBuilder.ToString();
	}

	public static void ForceD2Reapply(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return;
		}
		try
		{
			Type type = null;
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies)
			{
				try
				{
					type = assembly.GetType("Diablo2Totem.Services.ItemPersistenceService", throwOnError: false);
				}
				catch
				{
				}
				if (type != null)
				{
					break;
				}
			}
			if (type == null)
			{
				return;
			}
			int instanceID = ((Object)item).GetInstanceID();
			string[] array = new string[4] { "_appliedGood", "_byInstance", "_noMetaWarned", "_pendingRetry" };
			foreach (string name in array)
			{
				try
				{
					FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					object obj2 = ((field != null) ? field.GetValue(null) : null);
					if (obj2 != null)
					{
						MethodInfo method = obj2.GetType().GetMethod("Remove", new Type[1] { typeof(int) });
						if (method != null)
						{
							method.Invoke(obj2, new object[1] { instanceID });
						}
					}
				}
				catch
				{
				}
			}
			string text = "";
			try
			{
				text = item.GetString("D2T_Meta", (string)null) ?? "";
			}
			catch
			{
			}
			if (text.Length > 90)
			{
				text = text.Substring(0, 90) + "...";
			}
			Debug.Log((object)("[TFix] ForceD2Reapply: 清缓存前 D2T_Meta=" + text));
			MethodInfo method2 = type.GetMethod("EnsureApplied", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (method2 == null)
			{
				return;
			}
			method2.Invoke(null, new object[1] { item });
			string text2 = "";
			try
			{
				text2 = item.GetString("D2T_Meta", (string)null) ?? "";
			}
			catch
			{
			}
			if (text2.Length > 90)
			{
				text2 = text2.Substring(0, 90) + "...";
			}
			Debug.Log((object)("[TFix] ForceD2Reapply: EnsureApplied后 D2T_Meta=" + text2));
			try
			{
				ModifierDescriptionCollection modifiers = item.Modifiers;
				if ((Object)(object)modifiers != (Object)null)
				{
					((object)modifiers).GetType().GetMethod("ReapplyModifiers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(modifiers, null);
				}
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	private static void WriteAffixes(Item item, List<AffixEntry> list, bool positive, int cleanupTo)
	{
		string format = (positive ? "D2T_Pos_{0}_id" : "D2T_Neg_{0}_id");
		string format2 = (positive ? "D2T_Pos_{0}_key" : "D2T_Neg_{0}_key");
		string format3 = (positive ? "D2T_Pos_{0}_val" : "D2T_Neg_{0}_val");
		string format4 = (positive ? "D2T_Pos_{0}_mod" : "D2T_Neg_{0}_mod");
		int count = list.Count;
		item.SetInt(positive ? "D2T_PosCount" : "D2T_NegCount", count, true);
		for (int i = 0; i < count; i++)
		{
			item.SetString(string.Format(format, i), list[i].id, true);
			item.SetString(string.Format(format2, i), list[i].key, true);
			item.SetFloat(string.Format(format3, i), list[i].val, true);
			item.SetString(string.Format(format4, i), list[i].mod, true);
		}
		for (int j = count; j < cleanupTo; j++)
		{
			item.SetString(string.Format(format, j), "", true);
			item.SetString(string.Format(format2, j), "", true);
			item.SetFloat(string.Format(format3, j), 0f, true);
			item.SetString(string.Format(format4, j), "", true);
		}
	}

	private static int SaleValueOf(Item item)
	{
		if ((Object)(object)item == (Object)null)
		{
			return 0;
		}
		int num = 0;
		try
		{
			num = item.GetInt("D2T_SaleValue", 0);
		}
		catch
		{
		}
		if (num <= 0)
		{
			try
			{
				num = item.Value;
			}
			catch
			{
			}
		}
		if (num <= 0)
		{
			return 0;
		}
		return num;
	}

	public static int FusedValue(Item a, Item b)
	{
		int val = SaleValueOf(a);
		int val2 = SaleValueOf(b);
		int num = Math.Max(val, val2);
		if (num <= 0)
		{
			return 0;
		}
		int num2 = Math.Min(val, val2);
		return num + (int)Mathf.Round((float)num2 * 0.5f);
	}

	public static FusePreview Preview(Item baseItem, Item otherItem, bool mergeSame)
	{
		FusePreview fusePreview = new FusePreview();
		if ((Object)(object)baseItem == (Object)null || (Object)(object)otherItem == (Object)null)
		{
			fusePreview.Error = "请放入两个图腾";
			return fusePreview;
		}
		if (baseItem == otherItem)
		{
			fusePreview.Error = "不能选择同一个图腾";
			return fusePreview;
		}
		if (IsFusedTotem(baseItem))
		{
			fusePreview.Error = "主槽位图腾已经合成过，不能再次参与合成";
			return fusePreview;
		}
		if (IsFusedTotem(otherItem))
		{
			fusePreview.Error = "材料槽位图腾已经合成过，不能再次参与合成";
			return fusePreview;
		}
		if (!IsFusable(baseItem))
		{
			fusePreview.Error = "主槽位物品没有可合并的属性";
			return fusePreview;
		}
		if (!IsFusable(otherItem))
		{
			fusePreview.Error = "材料槽位物品没有可合并的属性";
			return fusePreview;
		}
		try
		{
			fusePreview.Name = BuildFusedName(baseItem, otherItem);
			if (IsD2Totem(baseItem) || IsD2Totem(otherItem))
			{
				List<AffixEntry> a = ReadAffixesSmart(baseItem, positive: true, out var _);
				List<AffixEntry> a2 = ReadAffixesSmart(baseItem, positive: false, out var _);
				List<AffixEntry> b = ReadAffixesSmart(otherItem, positive: true, out var _);
				List<AffixEntry> b2 = ReadAffixesSmart(otherItem, positive: false, out var _);
				PreviewAffixDiff(fusePreview, a, b, mergeSame, positive: true);
				PreviewAffixDiff(fusePreview, a2, b2, mergeSame, positive: false);
			}
			PreviewVanillaDiff(fusePreview, ReadVanilla(baseItem), ReadVanilla(otherItem), mergeSame);
			int num = CountEffects(baseItem);
			CollectNewEffects(baseItem, otherItem, fusePreview);
			fusePreview.TotalEffects = num + fusePreview.AddedEffects;
			fusePreview.CustomTotal = UnionCustomVarCount(baseItem, otherItem);
			fusePreview.Value = FusedValue(baseItem, otherItem);
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] 预览计算失败: " + ex.Message));
			fusePreview.Error = "预览失败：" + ex.Message;
		}
		return fusePreview;
	}

	private static void PreviewAffixDiff(FusePreview p, List<AffixEntry> a, List<AffixEntry> b, bool mergeSame, bool positive)
	{
		Dictionary<string, float> dictionary = new Dictionary<string, float>();
		Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
		foreach (AffixEntry item in a)
		{
			string key = MergeKeyOf(item);
			if (dictionary.ContainsKey(key))
			{
				dictionary[key] += item.val;
				continue;
			}
			dictionary[key] = item.val;
			dictionary2[key] = DisplayKey(item.key);
		}
		int num = dictionary.Count;
		HashSet<string> hashSet = new HashSet<string>();
		foreach (AffixEntry item2 in b)
		{
			string text = MergeKeyOf(item2);
			if (!dictionary.ContainsKey(text))
			{
				dictionary[text] = item2.val;
				dictionary2[text] = DisplayKey(item2.key);
				num++;
				p.AddedProps++;
				if (p.Added.Count < 6)
				{
					p.Added.Add(AffixLabel(item2));
				}
				continue;
			}
			float num2 = dictionary[text];
			float v = (dictionary[text] = num2 + item2.val);
			if (hashSet.Add(text))
			{
				p.StackedProps++;
				if (p.Stacked.Count < 6)
				{
					p.Stacked.Add(dictionary2[text] + " " + SignedVal(num2) + " → " + SignedVal(v));
				}
			}
		}
		p.TotalProps += num;
	}

	private static int AffixCategory(string key)
	{
		if (AffixCategoryOrder.TryGetValue((key ?? "").Trim(), out var value))
		{
			return value;
		}
		return 99;
	}

	private static void OrderAffixes(List<AffixEntry> list)
	{
		list?.Sort((AffixEntry a, AffixEntry b) =>
		{
			int num = AffixCategory(a.key);
			int num2 = AffixCategory(b.key);
			if (num != num2)
			{
				return num.CompareTo(num2);
			}
			int num3 = string.Compare(a.key ?? "", b.key ?? "", StringComparison.OrdinalIgnoreCase);
			return (num3 != 0) ? num3 : a.val.CompareTo(b.val);
		});
	}

	private static string BuildAffixDescription(List<AffixEntry> pos, List<AffixEntry> neg)
	{
		if ((pos == null || pos.Count == 0) && (neg == null || neg.Count == 0))
		{
			return "";
		}
		List<string> list = new List<string>();
		if (pos != null)
		{
			foreach (AffixEntry po in pos)
			{
				list.Add(AffixLabel(po));
			}
		}
		if (neg != null)
		{
			foreach (AffixEntry item in neg)
			{
				list.Add(AffixLabel(item));
			}
		}
		if (list.Count == 0)
		{
			return "";
		}
		return string.Join("；", list);
	}

	private static string AffixLabel(AffixEntry e)
	{
		return DisplayKey(e.key) + " " + SignedVal(e.val, e.mod);
	}

	private static bool IsNegativeModifier(float value, string type)
	{
		if (!string.IsNullOrEmpty(type))
		{
			if (type.IndexOf("Subtract", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			if (type.IndexOf("Reduce", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			if (type.IndexOf("Minus", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}
		return value < 0f;
	}

	private static string SignedVal(float v, string mod = null)
	{
		float num = Math.Abs(v);
		string text = ((!string.IsNullOrEmpty(mod) && mod.IndexOf("Percent", StringComparison.OrdinalIgnoreCase) >= 0) ? (FmtVal(num * 100f) + "%") : FmtVal(num));
		return (IsNegativeModifier(v, mod) ? "-" : "+") + text;
	}

	private static string FmtVal(float v)
	{
		if (Math.Abs(v - Mathf.Round(v)) < 0.0001f)
		{
			return Mathf.Round(v).ToString(CultureInfo.InvariantCulture);
		}
		return v.ToString("0.#", CultureInfo.InvariantCulture);
	}

	private static List<VmRow> ReadVanilla(Item item)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		List<VmRow> list = new List<VmRow>();
		if ((Object)(object)item == (Object)null)
		{
			return list;
		}
		try
		{
			ModifierDescriptionCollection modifiers = item.Modifiers;
			if ((Object)(object)modifiers == (Object)null)
			{
				return list;
			}
			foreach (ModifierDescription item2 in modifiers)
			{
				if (item2 != null)
				{
					list.Add(new VmRow
					{
						key = item2.Key,
						type = ((object)item2.Type/*cast due to constrained. prefix*/).ToString(),
						value = item2.Value
					});
				}
			}
		}
		catch
		{
		}
		return list;
	}

	private static void PreviewVanillaDiff(FusePreview p, List<VmRow> a, List<VmRow> b, bool mergeSame)
	{
		Dictionary<string, float> dictionary = new Dictionary<string, float>();
		Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
		Dictionary<string, string> dictionary3 = new Dictionary<string, string>();
		foreach (VmRow item in a)
		{
			string key = item.key + "|" + item.type;
			if (dictionary.ContainsKey(key))
			{
				dictionary[key] += item.value;
				continue;
			}
			dictionary[key] = item.value;
			dictionary2[key] = DisplayKey(item.key);
			dictionary3[key] = item.type;
		}
		int num = dictionary.Count;
		HashSet<string> hashSet = new HashSet<string>();
		foreach (VmRow item2 in b)
		{
			string text = item2.key + "|" + item2.type;
			string text2 = DisplayKey(item2.key);
			if (dictionary.ContainsKey(text))
			{
				float num2 = dictionary[text];
				float v = (dictionary[text] = num2 + item2.value);
				if (!(Math.Abs(item2.value) < 0.0001f) && hashSet.Add(text))
				{
					p.StackedProps++;
					if (p.Stacked.Count < 6)
					{
						p.Stacked.Add(text2 + " " + ModVal(num2, item2.type) + " → " + ModVal(v, item2.type));
					}
				}
				continue;
			}
			if (!dictionary.ContainsKey(text))
			{
				dictionary[text] = item2.value;
				num++;
				p.AddedProps++;
				if (p.Added.Count < 6)
				{
					p.Added.Add(text2 + " " + ModVal(item2.value, item2.type));
				}
				continue;
			}
			float num4 = dictionary[text];
			dictionary[text] = num4 + item2.value;
			if (hashSet.Add(text + "#dup"))
			{
				p.StackedProps++;
				if (p.Stacked.Count < 6)
				{
					p.Stacked.Add(text2 + " " + ModVal(num4, item2.type) + " → " + ModVal(dictionary[text], item2.type));
				}
			}
		}
		p.TotalProps += num;
	}

	private static string ModVal(float v, string typeName)
	{
		bool num = !string.IsNullOrEmpty(typeName) && typeName.IndexOf("Percent", StringComparison.OrdinalIgnoreCase) >= 0;
		string text = (IsNegativeModifier(v, typeName) ? "-" : "+") + FmtVal(Math.Abs(v));
		if (!num)
		{
			return text;
		}
		return text + "%";
	}

	private static void CollectNewEffects(Item baseItem, Item otherItem, FusePreview p)
	{
		if ((Object)(object)otherItem == (Object)null)
		{
			return;
		}
		HashSet<string> hashSet = CollectEffectSignatures(baseItem);
		try
		{
			List<Effect> effects = otherItem.Effects;
			if (effects == null)
			{
				return;
			}
			foreach (Effect item2 in effects)
			{
				if ((Object)(object)item2 == (Object)null)
				{
					continue;
				}
				string item = EffectIdentity(item2);
				if (!hashSet.Contains(item))
				{
					hashSet.Add(item);
					p.AddedEffects++;
					if (p.Added.Count < 6)
					{
						p.Added.Add(EffectLabel(item2));
					}
				}
			}
		}
		catch
		{
		}
	}

	private static string EffectLabel(Effect e)
	{
		try
		{
			FieldInfo field = typeof(Effect).GetField("description", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				string text = CleanKeyText(field.GetValue(e) as string);
				if (!string.IsNullOrEmpty(text) && !IsUselessLabel(text) && !IsInternalObjectName(text))
				{
					return text;
				}
			}
		}
		catch
		{
		}
		Item master = null;
		try
		{
			master = e.Item;
		}
		catch
		{
		}
		string text2 = LabelFromItemRefs(e.Actions, master) ?? LabelFromItemRefs(e.Triggers, master);
		if (!string.IsNullOrEmpty(text2) && !IsUselessLabel(text2) && !IsInternalObjectName(text2))
		{
			return text2;
		}
		try
		{
			string s = FirstObjectName(e.Actions) ?? FirstObjectName(e.Triggers) ?? FirstObjectName(e.Filters);
			s = CleanDefaultName(s);
			if (!string.IsNullOrEmpty(s))
			{
				string text3 = ResolveName(s);
				if (!string.IsNullOrEmpty(text3) && !IsUselessLabel(text3) && !IsInternalObjectName(text3))
				{
					return text3;
				}
			}
			string text4 = ComponentShortName(e.Actions) ?? ComponentShortName(e.Triggers) ?? ComponentShortName(e.Filters);
			if (!string.IsNullOrEmpty(text4))
			{
				string text5 = ResolveName(text4);
				if (!string.IsNullOrEmpty(text5) && !IsUselessLabel(text5) && !IsInternalObjectName(text5))
				{
					return text5;
				}
				if (!IsUselessLabel(text4) && !IsInternalObjectName(text4))
				{
					return text4;
				}
			}
		}
		catch
		{
		}
		try
		{
			string text6 = BuffLabelFromEffect(e);
			if (!string.IsNullOrEmpty(text6))
			{
				return text6;
			}
		}
		catch
		{
		}
		return "特殊效果";
	}

	private static string BuffLabelFromEffect(Effect e)
	{
		try
		{
			string text = EffectSignature(e);
			if (string.IsNullOrEmpty(text))
			{
				return null;
			}
			string[] array = new string[3] { "buffPfb=", "selfBuff=", "buff=" };
			foreach (string text2 in array)
			{
				for (int num = text.IndexOf(text2, StringComparison.OrdinalIgnoreCase); num >= 0; num = text.IndexOf(text2, num + 1, StringComparison.OrdinalIgnoreCase))
				{
					int num2 = num + text2.Length;
					int j;
					for (j = num2; j < text.Length && text[j] != ',' && text[j] != '}' && text[j] != ';'; j++)
					{
					}
					string text3 = text.Substring(num2, j - num2).Trim().Trim('"');
					if (!string.IsNullOrEmpty(text3) && text3.IndexOf("buff", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						string text4 = ResolveName(text3);
						if (!string.IsNullOrEmpty(text4) && !IsUselessLabel(text4) && !IsInternalObjectName(text4))
						{
							return text4;
						}
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static bool IsUselessLabel(string s)
	{
		if (string.IsNullOrWhiteSpace(s))
		{
			return true;
		}
		string text = (s ?? "").Trim().TrimEnd('。', '.', ' ', '\u3000', '*', '：', ':');
		if (UselessLabels.Contains(text))
		{
			return true;
		}
		if (text.IndexOf("未定义", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}
		if (text.IndexOf("undefined", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}
		if (text.IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}
		if (text.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}
		return false;
	}

	private static bool IsEffectInternalPlaceholder(string rname)
	{
		if (string.IsNullOrWhiteSpace(rname))
		{
			return true;
		}
		string text = rname.Trim();
		if (text.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase))
		{
			text = text.Substring(0, text.Length - "(Clone)".Length).Trim();
		}
		return FusionPlaceholderNames.Contains(text);
	}

	private static bool IsInternalObjectName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return true;
		}
		if (name.IndexOf("(clone)", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}
		string item = name.Trim();
		if (InternalObjNames.Contains(item))
		{
			return true;
		}
		if (DefaultObjNames.Contains(item))
		{
			return true;
		}
		return false;
	}

	private static string CleanKeyText(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return null;
		}
		s = StripStars(s).Trim();
		if (s.Length == 0)
		{
			return null;
		}
		if (!HasCJK(s))
		{
			string plainText = GetPlainText(s);
			if (!string.IsNullOrEmpty(plainText))
			{
				string text = StripStars(plainText).Trim();
				if (text.Length > 0 && text != s && (HasCJK(text) || text != s))
				{
					return text;
				}
			}
		}
		return s;
	}

	private static string CleanDefaultName(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return null;
		}
		s = s.Trim();
		if (s.Length == 0)
		{
			return null;
		}
		if (DefaultObjNames.Contains(s))
		{
			return null;
		}
		bool flag = true;
		string text = s;
		for (int i = 0; i < text.Length; i++)
		{
			if (!char.IsDigit(text[i]))
			{
				flag = false;
				break;
			}
		}
		if (!flag)
		{
			return s;
		}
		return null;
	}

	private static string ResolveName(string raw)
	{
		if (string.IsNullOrEmpty(raw))
		{
			return null;
		}
		if (BuffLabels.TryGetValue(raw, out var value))
		{
			return value;
		}
		if (BuffLabels.TryGetValue(raw.Replace("Buff_", ""), out value))
		{
			return value;
		}
		string text = CleanKeyText(raw);
		if (!string.IsNullOrEmpty(text) && HasCJK(text))
		{
			return text;
		}
		List<string> list = new List<string>();
		string text2 = raw;
		int i;
		for (i = 0; i < text2.Length && char.IsDigit(text2[i]); i++)
		{
		}
		if (i > 0 && i < text2.Length && text2[i] == '_')
		{
			text2 = text2.Substring(i + 1);
		}
		if (text2 != raw)
		{
			list.Add(text2);
		}
		string text3 = text2.ToLowerInvariant();
		if (text3.StartsWith("buff_equip_"))
		{
			list.Add(text2.Substring(11));
		}
		if (text3.StartsWith("buff_"))
		{
			list.Add(text2.Substring(5));
		}
		if (text3.StartsWith("item_"))
		{
			list.Add(text2.Substring(5));
		}
		foreach (string item in list)
		{
			if (string.IsNullOrEmpty(item))
			{
				continue;
			}
			string plainText = GetPlainText(item);
			if (!string.IsNullOrEmpty(plainText))
			{
				string text4 = StripStars(plainText).Trim();
				if (text4.Length > 0 && text4 != item)
				{
					return text4;
				}
			}
			if (BuffLabels.TryGetValue(item, out value))
			{
				return value;
			}
		}
		if (!(text2 != raw))
		{
			return text;
		}
		return text2;
	}

	private static string LabelFromItemRefs(IEnumerable comps, Item master)
	{
		if (comps == null)
		{
			return null;
		}
		foreach (object comp in comps)
		{
			if (comp == null)
			{
				continue;
			}
			try
			{
				Type type = comp.GetType();
				while (type != null && type != typeof(object) && type != typeof(Component) && type != typeof(MonoBehaviour))
				{
					FieldInfo[] fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					foreach (FieldInfo fieldInfo in fields)
					{
						object value;
						try
						{
							value = fieldInfo.GetValue(comp);
						}
						catch
						{
							continue;
						}
						Item val = (Item)((value is Item) ? value : null);
						if ((Object)(object)val == (Object)null && value != null && !(value is Object) && !(value is string) && value is IEnumerable<Item> enumerable)
						{
							foreach (Item item in enumerable)
							{
								if ((Object)(object)item != (Object)null)
								{
									val = item;
									break;
								}
							}
						}
						if ((Object)(object)val == (Object)null || ((Object)(object)master != (Object)null && val == master))
						{
							continue;
						}
						string text = null;
						try
						{
							text = val.DisplayName;
						}
						catch
						{
						}
						if (string.IsNullOrWhiteSpace(text))
						{
							try
							{
								text = val.DisplayNameRaw;
							}
							catch
							{
							}
						}
						text = CleanDefaultName(text);
						if (!string.IsNullOrEmpty(text))
						{
							string text2 = ResolveName(text);
							if (!string.IsNullOrEmpty(text2))
							{
								return text2;
							}
						}
					}
					type = type.BaseType;
				}
			}
			catch
			{
			}
		}
		return null;
	}

	private static string ComponentShortName(IEnumerable comps)
	{
		if (comps == null)
		{
			return null;
		}
		foreach (object comp in comps)
		{
			if (comp == null)
			{
				continue;
			}
			string name = comp.GetType().Name;
			string[] array = new string[4] { "Action", "Trigger", "Filter", "Effect" };
			foreach (string text in array)
			{
				if (name.EndsWith(text, StringComparison.OrdinalIgnoreCase) && name.Length > text.Length)
				{
					return name.Substring(0, name.Length - text.Length);
				}
			}
			return name;
		}
		return null;
	}

	private static string FirstObjectName(IEnumerable comps)
	{
		if (comps == null)
		{
			return null;
		}
		foreach (object comp in comps)
		{
			if (comp == null)
			{
				continue;
			}
			try
			{
				Type type = comp.GetType();
				while (type != null && type != typeof(object) && type != typeof(Component) && type != typeof(MonoBehaviour))
				{
					FieldInfo[] fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					foreach (FieldInfo fieldInfo in fields)
					{
						object value;
						try
						{
							value = fieldInfo.GetValue(comp);
						}
						catch
						{
							continue;
						}
						Object val = (Object)((value is Object) ? value : null);
						if (val != (Object)null && !string.IsNullOrEmpty(val.name) && val.name != comp.GetType().Name && !IsInternalObjectName(val.name))
						{
							return val.name;
						}
					}
					type = type.BaseType;
				}
			}
			catch
			{
			}
		}
		return null;
	}

	private static int UnionCustomVarCount(Item a, Item b)
	{
		HashSet<string> hashSet = new HashSet<string>();
		Item[] array = new Item[2] { a, b };
		foreach (Item val in array)
		{
			if ((Object)(object)val == (Object)null)
			{
				continue;
			}
			try
			{
				CustomDataCollection variables = val.Variables;
				if (variables == null)
				{
					continue;
				}
				foreach (CustomData item in variables)
				{
					if (item != null && IsMergeableVarKey(item.Key))
					{
						hashSet.Add(item.Key);
					}
				}
			}
			catch
			{
			}
		}
		return hashSet.Count;
	}

	public static Item Fuse(Item baseItem, Item otherItem, bool mergeSame, out string error)
	{
		error = null;
		try
		{
			if ((Object)(object)baseItem == (Object)null || (Object)(object)otherItem == (Object)null)
			{
				error = "请先选择两个图腾";
				return null;
			}
			if (baseItem == otherItem)
			{
				error = "不能选择同一个图腾进行合成";
				return null;
			}
			if (IsFusedTotem(baseItem))
			{
				error = "主槽位图腾已经合成过，不能再次参与合成";
				return null;
			}
			if (IsFusedTotem(otherItem))
			{
				error = "材料槽位图腾已经合成过，不能再次参与合成";
				return null;
			}
			if (!IsFusable(baseItem))
			{
				error = "主槽位物品没有任何可合并的属性";
				return null;
			}
			if (!IsFusable(otherItem))
			{
				error = "材料槽位物品没有任何可合并的属性";
				return null;
			}
			bool flag = IsD2Totem(baseItem) || IsD2Totem(otherItem);
			int num = CountVanillaModifiers(baseItem);
			int num2 = CountVanillaModifiers(otherItem);
			bool flag2 = num > 0 || num2 > 0;
			int num3 = CountCustomVars(baseItem);
			int num4 = CountCustomVars(otherItem);
			bool flag3 = num3 > 0 || num4 > 0;
			int num5 = CountEffects(baseItem);
			int num6 = CountEffects(otherItem);
			bool flag4 = num5 > 0 || num6 > 0;
			List<ModifierDescription> list = SnapshotModifiers(baseItem);
			List<ModifierDescription> list2 = SnapshotModifiers(otherItem);
			Item val = baseItem.CreateInstance();
			if ((Object)(object)val == (Object)null)
			{
				error = "生成合成图腾失败";
				return null;
			}
			string text = null;
			Debug.Log((object)$"[TotemFusion] 合成输入：主={PrettyName(baseItem)}(属性{PropertyCount(baseItem)}/效果{CountEffects(baseItem)}) 材料={PrettyName(otherItem)}(属性{PropertyCount(otherItem)}/效果{CountEffects(otherItem)})");
			int num7 = FusedValue(baseItem, otherItem);
			string text2 = BuildFusedName(baseItem, otherItem);
			string text3 = BuildFusedRawName(baseItem, otherItem, text2);
			try
			{
				val.DisplayNameRaw = text2;
			}
			catch (Exception ex)
			{
				Debug.LogWarning((object)("[TotemFusion] 设置合成图腾名字失败: " + ex.Message));
			}
			RegisterOverride(text3, text2);
			if (text2 != text3)
			{
				RegisterOverride(text2, text2);
			}
			try
			{
				val.SetString("TotemFusion_Name", text2, true);
			}
			catch
			{
			}
			Debug.Log((object)("[TotemFusion] 合成图腾命名：" + text2 + "（写入值 " + text3 + "）"));
			if (flag)
			{
				List<AffixEntry> list3 = ReadAffixesSmart(baseItem, positive: true, out var fromMeta);
				List<AffixEntry> list4 = ReadAffixesSmart(baseItem, positive: false, out var fromMeta2);
				List<AffixEntry> list5 = ReadAffixesSmart(otherItem, positive: true, out var fromMeta3);
				List<AffixEntry> list6 = ReadAffixesSmart(otherItem, positive: false, out var fromMeta4);
				List<AffixEntry> list7 = MergeAffixes(list3, list5, mergeSame);
				List<AffixEntry> list8 = MergeAffixes(list4, list6, mergeSame);
				int num8 = 0;
				if (flag2)
				{
					foreach (VanillaModJson item in BuildVanillaSnapshot(baseItem, otherItem, mergeSame))
					{
						if (item == null || string.IsNullOrEmpty(item.key))
						{
							continue;
						}
						int num9 = -1;
						for (int i = 0; i < list7.Count; i++)
						{
							if (list7[i].key == item.key && list7[i].mod == item.type)
							{
								num9 = i;
								break;
							}
						}
						if (num9 >= 0)
						{
							AffixEntry value = list7[num9];
							value.val += item.value;
							list7[num9] = value;
							continue;
						}
						list7.Add(new AffixEntry
						{
							id = item.key,
							key = item.key,
							val = item.value,
							mod = item.type
						});
						num8++;
					}
				}
				WriteAffixes(val, list7, positive: true, list3.Count + list5.Count);
				WriteAffixes(val, list8, positive: false, list4.Count + list6.Count);
				text = ComposeFusedDescription(baseItem, otherItem, list7, list8);
				if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(text3))
				{
					RegisterOverride(text3 + "_Desc", text);
					if (text2 != text3)
					{
						RegisterOverride(text2 + "_Desc", text);
					}
					try
					{
						val.SetString("TotemFusion_Desc", text, true);
					}
					catch
					{
					}
					try
					{
						val.SetString("D2T_Desc", text, true);
					}
					catch
					{
					}
					try
					{
						SetDescriptionRaw(val, text);
					}
					catch
					{
					}
				}
				int quality = Math.Max(baseItem.GetInt("D2T_Quality", 0), otherItem.GetInt("D2T_Quality", 0));
				int displayQuality = Math.Max(baseItem.GetInt("D2T_DisplayQuality", 0), otherItem.GetInt("D2T_DisplayQuality", 0));
				int value2 = FusedValue(baseItem, otherItem);
				string text4 = null;
				try
				{
					text4 = baseItem.GetString("D2T_Rarity", (string)null);
				}
				catch
				{
				}
				string text5 = text ?? "";
				int num10 = baseItem.GetInt("D2T_TypeID", 0);
				if (num10 <= 0)
				{
					num10 = baseItem.TypeID;
				}
				string displayName = text2;
				try
				{
					string s = BuildMetaJson(list7, list8, displayName, text5 ?? "", quality, displayQuality, value2, num10, text4 ?? "");
					string text6 = "[D2T]" + Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
					val.SetString("D2T_Meta", text6, true);
				}
				catch (Exception ex2)
				{
					Debug.LogWarning((object)("[TotemFusion] 写入 D2T_Meta 失败（回退到分散字段）: " + ex2.Message));
					try
					{
						val.SetString("D2T_Meta", "", true);
					}
					catch
					{
					}
				}
				Debug.Log((object)$"[TotemFusion] D2 词缀合并：正 {list3.Count}+{list5.Count}→{list7.Count}，负 {list4.Count}+{list6.Count}→{list8.Count}，来源(Meta)=主{fromMeta | fromMeta2}/材{fromMeta3 | fromMeta4}，原版并入Meta={num8}");
			}
			if (!flag)
			{
				text = MergeDescriptionText(baseItem, otherItem);
				if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(text3))
				{
					RegisterOverride(text3 + "_Desc", text);
					if (text2 != text3)
					{
						RegisterOverride(text2 + "_Desc", text);
					}
					try
					{
						val.SetString("TotemFusion_Desc", text, true);
					}
					catch
					{
					}
					try
					{
						val.SetString("D2T_Desc", text, true);
					}
					catch
					{
					}
					try
					{
						SetDescriptionRaw(val, text);
					}
					catch
					{
					}
				}
			}
			HashSet<string> stackedKeys = new HashSet<string>();
			if (flag2)
			{
				int num11 = MergeVanillaModifiers(val, otherItem, list, list2, mergeSame, stackedKeys);
				Debug.Log((object)$"[TotemFusion] 原版属性合并：主 {num} 条 + 材料 {num2} 条，新增 {num11} 条，最终 {CountVanillaModifiers(val)} 条");
			}
			if (flag4)
			{
				HashSet<string> existing = CollectEffectSignatures(val);
				int num12 = CloneEffectsInto(val, otherItem, existing);
				Debug.Log((object)$"[TotemFusion] 效果(Effect)合并：主 {num5} 条 + 材料 {num6} 条，新增 {num12} 条，最终 {CountEffects(val)} 条");
				if (string.IsNullOrEmpty(text))
				{
					try
					{
						string text7 = MergeDescriptionText(baseItem, otherItem);
						if (!string.IsNullOrEmpty(text7) && !string.IsNullOrEmpty(text3))
						{
							RegisterOverride(text3 + "_Desc", text7);
							if (text2 != text3)
							{
								RegisterOverride(text2 + "_Desc", text7);
							}
							try
							{
								val.SetString("TotemFusion_Desc", text7, true);
							}
							catch
							{
							}
							try
							{
								val.SetString("D2T_Desc", text7, true);
							}
							catch
							{
							}
						}
					}
					catch
					{
					}
				}
			}
			if (flag3)
			{
				int num13 = MergeGenericVariables(val, otherItem, mergeSame);
				Debug.Log((object)$"[TotemFusion] 通用属性合并：主 {num3} 条 + 材料 {num4} 条，变动 {num13} 条，最终 {CountCustomVars(val)} 条");
			}
			if (flag)
			{
				string text8 = text2;
				val.SetString("D2T_Name", text8, true);
				val.SetInt("D2T_Quality", Math.Max(baseItem.GetInt("D2T_Quality", 0), otherItem.GetInt("D2T_Quality", 0)), true);
				val.SetInt("D2T_DisplayQuality", Math.Max(baseItem.GetInt("D2T_DisplayQuality", 0), otherItem.GetInt("D2T_DisplayQuality", 0)), true);
				val.SetInt("D2T_TypeID", baseItem.GetInt("D2T_TypeID", 0), true);
				val.SetInt("D2T_SaleValue", num7, true);
				val.SetString("D2T_Rarity", baseItem.GetString("D2T_Rarity", (string)null) ?? "", true);
				if (string.IsNullOrEmpty(val.GetString("D2T_Desc", (string)null)))
				{
					val.SetString("D2T_Desc", baseItem.GetString("D2T_Desc", (string)null) ?? "", true);
				}
			}
			if (num7 > 0)
			{
				try
				{
					val.Value = num7;
				}
				catch (Exception ex3)
				{
					Debug.LogWarning((object)("[TotemFusion] 设置成品价值失败: " + ex3.Message));
				}
				try
				{
					val.SetInt("TotemFusion_Price", num7, true);
				}
				catch
				{
				}
			}
			try
			{
				val.SetString("TotemFusion_Made", "1", true);
			}
			catch
			{
			}
			try
			{
				val.SetString("TotemFusion_Src", BuildSourceIds(baseItem, otherItem), true);
			}
			catch
			{
			}
			try
			{
				WriteModifierSnapshot(val, list, list2);
			}
			catch
			{
			}
			try
			{
				Func<ModifierDescription, string> func = (ModifierDescription m) =>
				{
					//IL_0010: Unknown result type (might be due to invalid IL or missing references)
					//IL_0025: Unknown result type (might be due to invalid IL or missing references)
					//IL_002a: Unknown result type (might be due to invalid IL or missing references)
					if (m == null)
					{
						return "?";
					}
					string text9 = "";
					ModifierType val2 = (ModifierType)0;
					float num14 = 0f;
					try
					{
						text9 = m.Key;
					}
					catch
					{
					}
					try
					{
						val2 = m.Type;
					}
					catch
					{
					}
					try
					{
						num14 = m.Value;
					}
					catch
					{
					}
					return (text9 ?? "?") + "|" + ((object)val2/*cast due to constrained. prefix*/).ToString() + "=" + num14.ToString("0.####");
				};
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append("[TotemFusion][诊断] 合并校验 mergeSame=").Append(mergeSame).Append(" baseMods(")
					.Append(list?.Count ?? 0)
					.Append("):");
				if (list != null)
				{
					foreach (ModifierDescription item2 in list)
					{
						stringBuilder.Append(" [").Append(func(item2)).Append("]");
					}
				}
				stringBuilder.Append(" otherMods(").Append(list2?.Count ?? 0).Append("):");
				if (list2 != null)
				{
					foreach (ModifierDescription item3 in list2)
					{
						stringBuilder.Append(" [").Append(func(item3)).Append("]");
					}
				}
				stringBuilder.Append(" fused实时(").Append(CountVanillaModifiers(val)).Append("):");
				try
				{
					foreach (ModifierDescription modifier in val.Modifiers)
					{
						stringBuilder.Append(" [").Append(func(modifier)).Append("]");
					}
				}
				catch
				{
				}
				List<VanillaModJson> list9 = ReadModTarget(val);
				stringBuilder.Append(" KModSnap(").Append(list9?.Count ?? 0).Append("):");
				if (list9 != null)
				{
					foreach (VanillaModJson item4 in list9)
					{
						stringBuilder.Append(" [").Append((item4.key ?? "?") + "|" + (item4.type ?? "?") + "=" + item4.value.ToString("0.####")).Append("]");
					}
				}
				Debug.Log((object)stringBuilder.ToString());
			}
			catch
			{
			}
			Debug.Log((object)$"[TotemFusion] Fuse 成功：D2词缀={flag} 原版属性={flag2}(主{num}/材料{num2}) 通用属性={flag3}(主{num3}/材料{num4}) 效果={flag4}(主{num5}/材料{num6}) 最终属性条数={AffixCount(val)}");
			return val;
		}
		catch (Exception ex4)
		{
			error = "写入失败: " + ex4.Message;
			Debug.LogError((object)("[TotemFusion] Fuse 异常:\n" + ex4));
			return null;
		}
	}

	public static void InstallTotemOptions()
	{
		try
		{
			InteractCrafter[] array = Object.FindObjectsOfType<InteractCrafter>();
			int num = 0;
			InteractCrafter[] array2 = array;
			foreach (InteractCrafter val in array2)
			{
				if (!((Object)(object)val == (Object)null) && ((InteractableBase)val).interactableGroup && IsSupportedCrafter(val) && !AlreadyHasTotemOption(val))
				{
					AddTotemOptionToBench(val);
					num++;
				}
			}
			Debug.Log((object)("[TotemFusion] 安装‘图腾合成’选项完成，本次新增 " + num + " 个工作台。"));
		}
		catch (Exception ex)
		{
			Debug.LogError((object)("[TotemFusion] InstallTotemOptions 失败: " + ex));
		}
	}

	/// <summary>
	/// 只允许工作台（含高级工作台）注入图腾功能。
	/// 医疗台（MedicStation）、打印机（Printer）、灶台（Cook）等其它制作台一律跳过。
	/// </summary>
	private static bool IsSupportedCrafter(InteractCrafter crafter)
	{
		if (crafter == null)
			return false;

		string tag = crafter.requireTag;
		return tag == "WorkBench" || tag == "WorkBenchAdvanced";
	}

	private static bool AlreadyHasTotemOption(InteractCrafter owner)
	{
		FieldInfo groupField = GetGroupField();
		if (groupField != null && groupField.GetValue(owner) is List<InteractableBase> source && source.OfType<InteractCrafter>().Any((InteractCrafter c) => c.requireTag == "TotemFusionMod" || c.requireTag == "TotemFusionPurge"))
		{
			return true;
		}
		MultiInteraction component = ((Component)owner).gameObject.GetComponent<MultiInteraction>();
		if ((Object)(object)component != (Object)null && typeof(MultiInteraction).GetField("interactables", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(component) is IList list)
		{
			foreach (object item in list)
			{
				InteractCrafter val = (InteractCrafter)((item is InteractCrafter) ? item : null);
				if (val != null && (val.requireTag == "TotemFusionMod" || val.requireTag == "TotemFusionPurge"))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static void AddTotemOptionToBench(InteractCrafter owner)
	{
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		GameObject gameObject = ((Component)owner).gameObject;
		InteractCrafter val;
		try
		{
			val = gameObject.AddComponent<InteractCrafter>();
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[TotemFusion] AddComponent 时被其它补丁打断: " + ex));
			val = gameObject.GetComponent<InteractCrafter>();
			if ((Object)(object)val == (Object)null || (val.requireTag != "TotemFusionMod" && val.requireTag != "TotemFusionPurge"))
			{
				Debug.LogWarning((object)"[TotemFusion] 该制作台注入失败，跳过。");
				return;
			}
		}
		val.requireTag = "TotemFusionMod";
		((InteractableBase)val).InteractName = "图腾合成";
		((InteractableBase)val).interactMarkerOffset = Vector3.zero;
		Collider component = gameObject.GetComponent<Collider>();
		if ((Object)(object)component != (Object)null)
		{
			try
			{
				((InteractableBase)val).interactCollider = component;
			}
			catch
			{
			}
		}
		AddToGroup((InteractableBase)(object)owner, (InteractableBase)(object)val);
		MultiInteraction component2 = gameObject.GetComponent<MultiInteraction>();
		if ((Object)(object)component2 != (Object)null)
		{
			AddToMultiInteraction(component2, (InteractableBase)(object)val);
		}
		Debug.Log((object)"[TotemFusion] 已在制作台注入‘图腾合成’交互选项。");
		InteractCrafter val2;
		try
		{
			val2 = gameObject.AddComponent<InteractCrafter>();
		}
		catch (Exception ex2)
		{
			Debug.LogWarning((object)("[TotemFusion] 注入‘图腾词条去除’时被其它补丁打断: " + ex2));
			val2 = gameObject.GetComponents<InteractCrafter>().FirstOrDefault((InteractCrafter c) => c.requireTag == "TotemFusionPurge");
			if ((Object)(object)val2 == (Object)null)
			{
				return;
			}
		}
		val2.requireTag = "TotemFusionPurge";
		((InteractableBase)val2).InteractName = "图腾词条去除";
		((InteractableBase)val2).interactMarkerOffset = Vector3.zero;
		if ((Object)(object)component != (Object)null)
		{
			try
			{
				((InteractableBase)val2).interactCollider = component;
			}
			catch
			{
			}
		}
		AddToGroup((InteractableBase)(object)owner, (InteractableBase)(object)val2);
		if ((Object)(object)component2 != (Object)null)
		{
			AddToMultiInteraction(component2, (InteractableBase)(object)val2);
		}
		Debug.Log((object)"[TotemFusion] 已在制作台注入‘图腾词条去除’交互选项（与图腾合成并列）。");
	}

	private static FieldInfo GetGroupField()
	{
		if (_groupField == null)
		{
			_groupField = typeof(InteractableBase).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((FieldInfo f) => f.FieldType == typeof(List<InteractableBase>) && f.Name == "otherInterablesInGroup");
		}
		return _groupField;
	}

	private static void AddToGroup(InteractableBase owner, InteractableBase member)
	{
		FieldInfo groupField = GetGroupField();
		if (!(groupField == null))
		{
			List<InteractableBase> list = groupField.GetValue(owner) as List<InteractableBase>;
			if (list == null)
			{
				list = new List<InteractableBase>();
				groupField.SetValue(owner, list);
			}
			if (!list.Contains(member))
			{
				list.Add(member);
			}
		}
	}

	private static void AddToMultiInteraction(MultiInteraction mi, InteractableBase member)
	{
		FieldInfo field = typeof(MultiInteraction).GetField("interactables", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (!(field == null) && field.GetValue(mi) is IList list && !list.Contains(member))
		{
			list.Add(member);
		}
	}
}
