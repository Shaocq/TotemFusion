# 更新日志

本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/) 与 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) 格式。

## [1.0.1] - 2026-10-23

首次开源发布。当前版本包含的全部功能：

### 新增

- **图腾合成**：两个图腾合成一个，相同属性数值相加（+1 与 +2 → +3），名字按 `+N` 递增
- **词缀合并**：自动合并原版词缀、Diablo2Totem 词缀与效果、自定义变量
- **图腾词条去除**：花钱移除不需要的词条，费用随词条数量递增
- **制作台入口**：在基地制作台新增「图腾合成」与「图腾词条去除」两个页签
- **UI 组件**：`PanelDragHandler`（面板拖动）、`SelfDrawnButton` + `UiFactory`（自绘圆角按钮）、`TotemSlotClick`（槽位交互）

### 变更

- 限制版：**合成过的图腾不能再次参与合成**，避免无限叠加

### 修复

- 针对 Diablo2Totem 做兼容与存档修复（`D2EffectGuard` / `D2OnLoadGuard`），避免读档时词缀丢失或重复

### 说明

- 依赖前置 HarmonyLib（工坊 `3589088839`），不内嵌 Harmony
