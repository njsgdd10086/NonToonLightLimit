# 更新日志

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
