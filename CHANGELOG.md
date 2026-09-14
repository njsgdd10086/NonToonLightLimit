# 更新日志

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
