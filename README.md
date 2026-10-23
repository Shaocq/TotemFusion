# 图腾合成（TotemFusion）

[![游戏](https://img.shields.io/badge/游戏-Escape%20from%20Duckov-blue)](https://store.steampowered.com/app/3167020/)
[![协议](https://img.shields.io/badge/协议-MIT-yellow)](LICENSE)
[![版本](https://img.shields.io/badge/版本-1.0.1-brightgreen)](CHANGELOG.md)

> 在制作台新增「图腾合成」与「图腾词条去除」：两个图腾合成一个，相同属性数值相加（+1 与 +2 → +3），自动合并原版词缀与 Diablo2Totem 词缀。

| 项目 | 信息 |
| --- | --- |
| 游戏 | 逃离鸭科夫（Escape from Duckov，AppID `3167020`） |
| 框架 | `Duckov.Modding.ModBehaviour` |
| 前置 | HarmonyLib（工坊 `3589088839`），必须置于本 mod **之前**加载 |
| 创意工坊 | `3822330169` |
| 当前版本 | 1.0.1 |
| 开源协议 | [MIT](LICENSE) |

## 功能特性

| 功能 | 说明 |
| --- | --- |
| 图腾合成 | 把两个图腾合成一个，**相同属性数值相加**（+1 与 +2 → +3），名字按 `+N` 递增 |
| 词缀合并 | 自动合并原版词缀、Diablo2Totem 词缀与效果，以及自定义变量，尽量不丢词条 |
| 词条去除 | 花钱去掉图腾上不需要的词条，费用随词条数量递增 |
| Diablo2Totem 兼容 | 已针对 Diablo2Totem 做兼容与存档修复 |
| 合成限制 | **限制版**：合成过的图腾不能再次参与合成，避免无限叠加 |

## 使用方式

1. 在基地制作台打开「图腾合成」页签；
2. 放入两个图腾，界面会预览合并后的属性与名称；
3. 点击合成，产物优先进入角色背包，背包满时送到基地的自提点；
4. 「图腾词条去除」页可选择要移除的词条并支付对应金额。

## 安装

1. 订阅前置 **HarmonyLib**（工坊 `3589088839`），并确保它排在本 mod **之前**加载；
2. 在创意工坊订阅「图腾合成」，或把 `TotemFusion` 文件夹放入 `Escape from Duckov\Duckov_Data\Mods\`。

## 从源码构建

```bash
dotnet build TotemFusion.csproj -c Release
```

游戏不在默认路径时加 `-p:DuckovPath="你的游戏目录"`。

> `lib/0Harmony.dll` 不在仓库内。编译前请从创意工坊前置 HarmonyLib 目录复制一份到 `lib/`。

## 兼容性

- 与 Diablo2Totem 配套使用时会合并其词缀与效果；
- 不修改原版图腾数据，只在制作台新增入口。

## 更新日志

完整版本记录见 [CHANGELOG.md](CHANGELOG.md)。

## 许可证

本项目以 [MIT 协议](LICENSE) 开源，可自由使用、修改与再发布，保留版权声明即可。

## 致谢

- 游戏与 mod 框架：TeamSoda《逃离鸭科夫》
- Harmony 补丁框架：[Harmony](https://github.com/pardeike/Harmony)（MIT）
- 感谢所有在创意工坊反馈问题与建议的玩家
