# 独特角色换色（UniqueLookColor）子系统

为**复用独特 Figure / 换皮 Rig 的角色**提供 **发色 / 瞳色 / 服色** 换色，并配套换皮（Reskin）接线。

## 一、已接线的换皮家族与物种

| 物种 | Figure | RigPrefix | 素材目录（ASCII 名） |
|---|---|---|---|
| Golem 魔像 | `EFigure.Woman` | —（无换皮） | 原版 `woman_*`（基础女性外观） |
| Genasi 元素裔 | `EFigure.Aure` | `GolemRig` | `*_GolemRig_*` |
| Angel 天使 | `EFigure.Bella` | `angel` | `angel_*` |
| FallenAngel 堕天使 | `EFigure.Bella` | `fallen` | `fallen_*` |
| ArcticWerewolf 雪狼人 | `EFigure.Karen` | `arcticwolf` | `arcticwolf_*` |
| MoonElf 月精灵 | `EFigure.Claire` | `moonelf` | `moonelf_*` |
| Dairycow 奶牛 | `EFigure.Sylvia` | `dairycow` | `dairycow_*` |
| Weretiger 虎人 | `EFigure.Nero` | `weretiger` | `weretiger_*`（Nero 目录） |
| MagicWererabbit 魔兔人 | `EFigure.Vivi` | `wererabbit` | `wererabbit_*`（vivi 目录） |
| Devil 魔鬼 | `EFigure.Anna` | `devil` | `devil_*`（Anna 目录） |

> `SpeciesRegistry` 中已为上述物种写入 `Figure` 与 `RigPrefix`（**Golem 魔像除外**：
> 已取消独特角色外观，改用原版基础女性 `EFigure.Woman`，不设 `RigPrefix`，故不参与换皮）；
> `ReskinSystem` 会把资产名中的**家族 token**（aure/bella/nero/karen/claire/vivi/anna/sylvia）
> 替换为 `RigPrefix`（`bella_stand → angel_stand`、`bed_nero_1 → bed_weretiger_1`）。

### 换皮「确定性改名」——修复渲染混乱（角色变成别人 / 眼睛腿部缺失）
之前用全局 `CurrentCharacter` 猜"正在给谁渲染"，在**存档重载**、多角色同帧刷新时会串味。
现在改为确定性判定：

1. **主路径**：`Unit` / `UpdaterSpineCharacter` / `UpdaterSpinePrefab` 的 `SpineData` setter。
   赋值时都能拿到**确切角色**（Unit 自身，或 Updater 的 `Female` / `Male` / `Unit`），再经
   `ReskinSystem.TryGetRigInfo()` 取「RigPrefix + 本 Figure 的家族 token」，
   **只有两者同时有效才改名**；并且只替换含该 token 的资产名 →
   不同家族（aure/bella/nero/…）之间**永不互相串味**。
2. **渲染前兜底（修复 move 等动作回退原版贴图）**：`UpdaterSpineCharacter.Display()`
   结束时的 Postfix。世界角色的骨架是在 `Display()` 里定下来的：先清空 `Female/Male/SpineData`，
   再由 `Reference` 逐条重新赋值，最后 `m_SkeletonDataAsset = SpineData.SkeletonDataAsset`。
   若某条路径赋值 `SpineData` 时拿不到角色（`Female`/`Male` 为空），名字就不会被换，
   **移动（move）/拖拽等动作会回退成原版家族贴图**。
   此 Postfix 在 `Display()` 结束后，用**该渲染器自己的角色**（`Female` / `Male` / `Unit`）
   再校验一次：若 `SpineData` 仍是原版家族资产名，就地换成换皮克隆，并同步
   `m_SkeletonDataAsset`（否则紧随其后的 `OnChangeSpine()` 仍用旧骨架）。
   归因只用实例自己的角色，不依赖全局 `CurrentCharacter`，因此不会串味。
3. **兜底路径**：`SeqAsset.GetSkeletonDataAsset(...)` 只在
   `ReskinSystem.IsAssetNameOfCharacter(character, name)` 成立（该名字确实属于这个角色当前的
   `SpineData`）时才改名 → 不会再误改同 Figure 的**原版独特 NPC**或其它角色的资产。
   被拦下的疑似本角色改名会记一条 `[CBR-DIAG] 兜底改名被拦: …`（仅日志开关开启时）。
4. **已移除** `SpineData.GetSpineData(...)` 的 Postfix 改名：该路径无法确定目标角色
   （原版 Bella 与我们重皮的天使 Figure 相同，仅凭 Figure 名无法区分），
   是"变成别人/部件缺失"的主要来源；而游戏中全部 **27 处** `GetSpineData` 调用
   都紧接着赋值给上述已打补丁的 setter，因此该兜底是冗余且有害的。

> 排障：打开模组菜单 `UniqueLook_EnableLogging`，日志里会出现
> `[CBR-DIAG] <渲染器> 赋值 name='bella_move_1' state=[Move] … | char=angel(angel/Bella) | → 'angel_move_1'`，
> 可据此确认「哪个渲染器、哪个角色、什么动作、原版名 → 换皮名」是否都正确。

## 二、动作登记（*.animation）

目前每个换皮前缀都已登记 **33 个动作 = 原版家族的全部资产名**（共 8 个新前缀 × 33 = 264 条，加 GolemRig 原有 33 条）：

```
Female 常规：stand / card / dead / drain / milk / move_1 / move_2 / rest
             birth_1 / birth_2 / big_birth_1 / big_birth_2 / capsule
卧姿（差分）：bed_1 / bed_2 / bed_right_2
性爱（差分）：goblin_1 / goblin_2 / orc_1 / orc_2 / werewolf_1 / werewolf_2
             minotaur_1 / minotaur_2 / salamander_1 / salamander_2
             horse / man_1 / man_2 / man_3 / man_4 / tentacle_1 / tentacle_2
```

登记位置（与素材所在目录对应，`Path = "%<folder>\<asset>"` 相对清单自身目录解析）：
- 共用 Female 组：`Animation/{Stand,Card,Dead,Drain,Milk,Move_1,Move_2,Rest,Birth_1,Birth_2,Big_Birth_1,Big_Birth_2}/*.animation`、`DLC/Animation/Capsule/capsule.animation`
- 卧姿：`Animation/Bed_1/bed_1.animation`、`Animation/Bed_2/bed_2.animation`
- 性爱：`Animation/{Goblin_1,Goblin_2,Orc_1,Orc_2,Werewolf_1,Werewolf_2,Minotaur_1,Minotaur_2,Salamander_1,Salamander_2,Horse,Man_1..Man_4}/*.animation`、`DLC/Animation/Tentacle_1|Tentacle_2/*.animation`
- 虎人：`DLC/Animation/Nero/nero.animation`；魔兔人：`Animation/vivi/vivi.animation`；魔鬼：`DLC/Animation/Anna/anna.animation`

> ✅ 现在 9 个家族 token → 前缀 的映射均为 **33/33 命中、0 回退**：
> 任何动作（含各种发型/差分动作）都会加载对应新种族的骨架与贴图，
> **不会再回退成原版 NPC 的旧材质**。

> 说明：`*_right_2`（卧姿右向差分）没有独立 `.png`，其 `.atlas.txt` 直接引用同目录的 `_2.png`
> （GolemRig 原本即如此），属正常结构。

原始清单备份：
- `游戏目录/备份/animation_backup_<时间戳>/`（首次 16 个文件）
- `游戏目录/备份/animation_backup2_<时间戳>/`（本次补齐 160 条，22 个清单）

### ⚠ 追加 *.animation 条目的坑（曾导致启动 NRE）
部分原版清单（`vivi.animation` / `nero.animation` / `anna.animation` 等）以 **CRLF + 尾逗号**（`},`）结尾。
若直接“在末尾 `]` 前插入”新条目，会得到 `,,`；Newtonsoft 会把该空元素解析成 **null 元素**，
随后 `PlayerController.CoroutineLoadAnimationData` 遍历时 `animationData.Name` 抛
`NullReferenceException`（日志里表现为该协程 NRE）。
追加条目时请务必：
1. 去掉原有尾逗号，或插入前先规范化；
2. 用游戏自带 `Newtonsoft.Json` 复验：`JsonConvert.DeserializeObject<AnimationData[]>(text.Replace("\\","/"))`
   应无异常且 **数组内无 null 元素**。

## 三、换色渲染

渲染补丁在 `UpdaterSpineCharacter.OnChangeSpine` 之后，按**槽位名前缀**扫描 `Skeleton.Slots` 乘染：

- 头发（`HairColor`）：`hair_`、`hairbundle`、`longhair`、`head_back`、`s_hair_`、`s_hairbundle`、
  `s_longhair`、`s_head_back`、`pussyhair`、`s_pussyhair`
- 眼睛（`EyeColor`）：`eyeball`、`s_eyeball`、`eyeheart`、`s_eyeheart`
- 角 / 尾巴 / 兽耳（`HornColor`，即 `Item_CosmeticPill` / `Item_SlaveCosmeticPill` 的「角颜色」框；
  语义同原版 `SlaveHornColorPartList`）：`horn`、`s_horn`、`tail`、`s_tail`、`beastear`、`s_beastear`
- 服色（本模组扩展字段，见下）：`cloth_`、`s_cloth_`（**全部**衣物部件，含 body/tits/hand/bone 等）
- 有意排除：`eye_back`（眼白）、`eyeblink`/`eyehair`/`s_eyehair`（睫毛/眉）、
  `hairband`/`s_hairband`/`s_cloth_hairband`（发饰不参与上述任何一类的前缀判定除外——
  注意：`s_cloth_hairband` 归属于 `s_cloth_` 服色范围）、`chair*`（产椅）、
  `con_*`/`ik_*`/`ilk_*`（控制骨）、`m_*`/`armhair*`/`m_dickhair*`（雄性部件）
- 被 `HidePartList` 置为透明的部件会跳过，避免把隐藏部件重新显形。

> 服色采用「**设置过才染 + 取消时补一帧白色还原**」策略：
> 从未设置过服色的角色不会被本模组改动 `cloth_*` 槽位颜色（避免覆盖原版自身的染色）；
> 把服色改回白色时会补一次白色写入，确保旧染色不残留。

> **兽系特例（仅雪狼人 / 魔兔人，不影响其它角色）**：
> 这两个种族因种族原因**没有「角颜色」换色框**，且其衣物极简、用 `cloth_` 染服色非常不协调，
> 因此它们使用独立定义 `UniqueLookDefs.BeastTailLook`：
> * **停用** `cloth_` / `s_cloth_` 渲染；
> * 把「服色槽」改为渲染 **尾巴 / 兽耳**（`tail`、`s_tail`、`beastear`、`s_beastear`）；
> * 该槽标签会随当前角色**动态显示为「独特毛色 / Unique Fur Color」**（普通角色仍显示「独特服色」）；
> * 仅保留 `horn` / `s_horn` 由 `HornColor` 驱动（这两个骨架本无角，若有动作骨架带角仍走原版行为）。

## 四、UI 与数据

- UI **仅注入「女性信息窗口 → 扩展区」**：独特发色 / 独特瞳色 / 独特服色 三个颜色槽（带弹出取色面板），
  仅对可换色独特角色可见。**不向画廊注入任何 UI**（避免画廊超框）。
- 发色 / 瞳色：游戏自带 `Character.HairColor` / `Character.EyeColor`，随原版存档。
- 角 / 尾巴 / 兽耳：游戏自带 `Character.HornColor` —— 由 `Item_CosmeticPill` / `Item_SlaveCosmeticPill`
  自带的「角颜色」换色框驱动，**不额外添加任何 UI**，随原版存档。
- 服色：扩展字段 `UniqueLookExtra.ClothesColor`（默认白色=不覆盖），
  经 `ModSaveData`（键 `UniqueLookExtraList`）按存档槽位持久化，读档后 `PlayData.OnEnable` 套用。
  另有「是否被显式设置过」标记 `UniqueLookExtra.ClothesColorSet`：显式设为白色（=取消染色）同样会存档，
  因此不会被「物种初始服色」重新填上。

### 物种「独特服色」初始值（`ISpeciesData.ClothesColor`）
物种可声明创建时的初始服色，写入角色的服色扩展字段（服色为**乘染**语义，白色=不染）：

| 物种 | 初始服色 | 乘染后的实际观感（布料基准均值≈0.67 灰） |
|---|---|---|
| Angel 天使 | 金色 `#FFD700` | ≈ (0.67,0.57,0.0) —— 明显金色 |
| FallenAngel 堕天使 | 深紫色 `#6A0DAD` | ≈ (0.28,0.03,0.46) —— 明显深紫（比 `#4B0082` 更不易糊成黑紫） |

套用入口 `UniqueLookColorSystem.ApplySpeciesClothesColor()`（都是「物种被套用到角色」的时刻）：
- `FemaleSpeciesSetup.SetSpecies` —— 角色创建（猎手营地俘虏 / 职业生成傀儡等）
- `BreedingSystem.ApplyFetus` —— 后代出生的女胎
- `FemaleSpeciesSetup.RefreshSpeciesAppearance` —— 成人外观刷新
- `SkinColorSystem.EnforcePalette` —— 旧存档安全网（每角色每次运行补一次）

只有在该角色**尚未显式设置过服色**时才写入 → 不覆盖玩家在信息面板里改的颜色，
也不覆盖存档中已有的服色记录。

## 五、代码结构

```
UniqueLookColor/
├─ UniqueLookDefs.cs                家族槽位前缀表（各族共用 UniqueFigure）+ 前缀/Figure 映射
├─ UniqueLookColorSystem.cs         判定 / 解析 / 渲染 / 服色读写 / 日志
├─ UniqueLookColorConfig.cs         ModSettings 开关（UniqueLook_*）
├─ UlcLoc.cs                        轻量中/英文案
├─ Extra/                           服色扩展字段 + 存档
├─ UIComponents/                    ReferenceUniqueLook / InteractionUniqueLook / UpdaterUniqueLookColorPicker
├─ UI/                              信息窗口三色槽 + 注册入口
└─ Patches/UniqueLookColorPatches.cs 渲染 + 初始化注入 UI + PlayData 套用服色
```

换皮侧代码（`ComplexBreedingRedux`）：
- `Features/ReskinSystem.cs`：家族 token → 前缀映射（`MapAssetName`）、前缀解析、克隆缓存、诊断描述
- `Patches/AppearancePatches/GolemReskinPatches.cs`：
  三个 `SpineData` setter + `UpdaterSpineCharacter.Display` 渲染前兜底 + `SeqAsset.GetSkeletonDataAsset` 兜底

## 六、配置与调试

| 键 | 默认 | 说明 |
|---|---|---|
| `UniqueLook_Enable` | true | 渲染染槽 + 信息窗口 UI |
| `UniqueLook_RecolorVanillaFigure` | false | 是否连原版独特 Figure（无 RigPrefix）一起染 |
| `UniqueLook_EnableLogging` | false | 调试日志（命中槽位名 / 未命中候选） |

## ⚠ 注意（乘染特性）

Spine `slot.SetColor` 是**乘染**（不可提亮）：贴图中被烘焙成深色的区域换色后仍偏暗。
若希望换色明显，建议把各骨架的发/眼/胸衣区域重绘为**中性浅灰**。
