# 更新日志

## 1.1.6

### 修复

- **菜单改成用 Modular Avatar 自己的组件来挂**（和 MA 的 `GameObject → Modular Avatar → Create Toggle` 完全同一条路）：
  同一个物体上放 **MA Menu Item**（`RadialPuppet` 控件 + 参数名 + `isSynced/isSaved/isDefault`）+ **MA Menu Installer**，
  `menuToAppend` 留空 —— 菜单由 Modular Avatar 在构建时生成。
  之前是插件自己 `CreateInstance` 造一个 `VRCExpressionsMenu` 资源再挂到 `menuToAppend` 上；
  有用户的工程在这种写法下上传会失败（`Index was outside the bounds of the array`），换掉后恢复正常流程。
- 因此**不再生成 `*_Brightness_Menu.asset`**，输出文件夹里只剩两条动画和一个 AnimatorController。

## 1.1.5

### 变更

- **拆成两个可单独生成的部分**（窗口里两个勾选项），方便定位问题、也方便只想用一半的人：
  - **生成动画层**：动画 + 控制器 + MA Merge Animator；
  - **生成菜单和参数**：菜单资源 + MA Menu Installer + MA Parameters。
- **参数预算只在「真的不够」时提示**：不再是常驻读数（VRCFury 会自动压缩参数，常显没意义），
  只有剩余空间不足 8 bit 时才在窗口里提醒一次。

## 1.1.4

### 新增 / 排查

- **窗口里显示同步参数预算**：用 Modular Avatar 的 `ParameterInfo.ForUI` 读这个 avatar 的同步参数用量
  （和 MA 的 `Show Modular Avatar Information` 窗口同一份数据），显示「已用 X / 3200 bit · 同步参数 N 个」。
  滑块要再加一个 8 bit 的同步 Float，预算不够（< 8 bit）或参数个数 ≥ 250 时会直接给出警告 ——
  VRChat 的上限是 3200 bit / 256 个同步参数，超了上传会失败，部分 SDK 版本会报
  `Index was outside the bounds of the array`。
- 生成时也会把参数预算写进 Console 日志。

## 1.1.3

### 修复

- **生成物被挪进 prefab 实例（比如服装的「装饰开关」）后清理不掉**：配置 `_NonToonLightLimit` 里的
  Merge Animator / Menu Installer / Parameters 时用的是 `DestroyImmediate`，在 prefab 实例里删不动，
  会留下重复的菜单安装点，导致 Modular Avatar 把菜单挂到别处（游戏里菜单里看不到滑块）、
  上传/构建阶段报错。现在改用 `Undo.DestroyObjectImmediate`，并且会：
  - 把被挪走的同名物体删掉并在日志里说明；
  - 发现它上面挂着 Modular Avatar 的「菜单安装点」（Menu Install Target）时一并清掉；
  - 重建时始终放在 **avatar 根目录**下。

## 1.1.2

### 修复

- **生成/删除时会清理掉所有 `_NonToonLightLimit` 物体**（原来是只找 avatar 根目录下的第一个）：
  如果把生成的物体挪到别处、或者复制出了好几份，再次生成会留下一堆同名的
  Merge Animator / Menu Installer / Parameters；重复挂菜单、重复声明参数会让上传阶段出错。
  现在改成在整个 avatar 下递归查找并全部清掉，再在 avatar 根目录下建一个干净的。

## 1.1.1

### 修复

- **修掉 NonToon 编译报错（装完插件角色变粉）**：Shader Core 的 phase 代码是**原样插进片段着色函数体里**的
  （NonToon 的 `urp.hlsl:127` / `birp.hlsl:129` 都在函数内部），之前 `phase_modifylight.hlsl` 在文件里
  声明了全局变量和辅助函数，插进函数体后是非法 HLSL，会报
  `syntax error: unexpected token '('` 与 `undeclared identifier 'color'`。
  现在整段改成只含语句和局部变量的 `{ }` 块。
- 顺带**去掉 shader 全局变量**（`_NonToonLightLimit_Global` / `_NonToonLightLimit_Envelope`）和材质上的
  `Use Global Control`：Shader Core 的模块没法声明全局变量，这部分本来就编译不过。
  要「一根滑块控制所有材质」请用**一键生成全局亮度动画 + 菜单**（它驱动每块材质的 Brightness）。
- 材质参数简化为：`Min Brightness` / `Max Brightness` / `Brightness` / `Mask Channel`。

## 1.1.0

### 新增

- **一键生成全局亮度动画 + 菜单**（可选小工具，菜单 `Tools → NonToon 亮度控制 → 生成全局亮度动画 + 菜单`）：
  给 avatar 下所有 NonToon 材质写一套亮度动画，并生成一个表情菜单径向滑块，
  一根滑块控制全部材质的亮度（Light Limit Changer 的「一括調整」效果）。
  - 生成两条动画（最暗 / 最亮）+ 单层 AnimatorController（1D 混合树）+ VRChat 菜单资源；
  - 在 avatar 下建 `_NonToonLightLimit` 物体，挂 Modular Avatar 的 Merge Animator /
    Menu Installer / Parameters，上传时自动接好；
  - 可调参数名、菜单名、最暗/最亮倍数、默认倍数、输出文件夹；
  - 有「包含 lilToon 材质槽」选项：配合一键切换开关（MA Material Setter）使用时勾上，
    切换成 NonToon 后同样受滑块控制；
  - 重复生成会覆盖上一次的结果，也可以一键删除（不动材质本身）。
- 需要 VRChat SDK3 Avatars 与 Modular Avatar；缺依赖时窗口会提示，模块本身仍然不需要它们。

## 1.0.0

首个版本。

### 新增

- **亮度下限 / 亮度上限**：材质面板上的 `Min Brightness` / `Max Brightness`，
  按亮度归一后夹取，暗部提亮、高光压住，颜色之间的明暗比例保持不变。
- **亮度倍数**：`Brightness`，每个材质单独的整体明暗，1 = 不改变。
- **遮罩范围**：`Mask Channel`，用共享遮罩的某个通道限制生效范围（默认 A）。
- **全局控制**：`Use Global Control` 打开后，由 shader 全局变量
  `_NonToonLightLimit_Global`（倍数，默认 1）与 `_NonToonLightLimit_Envelope`
  （0..1 包络，默认 0）统一驱动。
- **自动登记**：安装后自动把模块登记进 NonToon 的 Shader Core 模块列表并落盘，
  重启编辑器仍然生效；只追加不删除，NonToon 自带的模块不受影响。
- 菜单：`Tools → NonToon 亮度控制 → 重新登记到模块列表 / 从模块列表移除`。
- 支持 NonToon 与 NonToonFur 两个 shader。

### 说明

- 模块挂在 Shader Core 的 `modifylight` 阶段，并声明 `afters: ["Lighten"]`，
  在 NonToon 自带的 Light Boost 之后执行，两者可以叠加。
- 不做动画生成工具：VRChat 头像里请用逐材质的 `Brightness` / `Min` / `Max`
  （普通材质属性，可以被动画和菜单驱动）；全局变量适合编辑器预览、脚本与世界。
