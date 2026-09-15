**NonToon Light Limit 1.1.1** —— 修复 1.1.0 装上后 NonToon 编译报错、角色变粉的问题。

## 修复内容

- **NonToon 编译报错（角色变粉）**：Shader Core 的 phase 代码是原样插进片段着色函数体里的
  （NonToon 的 `urp.hlsl:127` / `birp.hlsl:129` 都在函数内部），之前的 `phase_modifylight.hlsl`
  在文件里声明了全局变量和辅助函数，插进函数体后是非法 HLSL，会报
  `syntax error: unexpected token '('` 和 `undeclared identifier 'color'`。
  现在整段改成只含语句和局部变量的 `{ }` 块。
- 顺带去掉 shader 全局变量（`_NonToonLightLimit_Global` / `_NonToonLightLimit_Envelope`）与材质上的
  `Use Global Control`：Shader Core 的模块没法声明全局变量，这部分本来就编译不过。
  要「一根滑块控制所有材质」请用 **一键生成全局亮度动画 + 菜单**。
- 材质参数：`Min Brightness` / `Max Brightness` / `Brightness` / `Mask Channel`。

## 已经验证过

- 展开后的 NonToon 源码里，本模块是一段插在函数体内的 `{ }` 语句块，和 NonToon 自带 Lighten 的写法一致；
- Direct3D11 下实际渲染：没有色偏（不是编译失败的洋红），并且
  `Brightness = 0.1` 明显变暗、`Max = 0.2` 压暗、`Max = 2 + Brightness = 2` 变亮、`Min = 0.9` 抬亮；
- 强制编译 Forward / Outline 变体（含 `DIRECTIONAL` `LIGHTPROBE_SH` `SHADOWS_SCREEN` `VERTEXLIGHT_ON`）无 shader 报错。

## 安装 / 升级

VCC / ALCOM 仓库地址（总仓库，本插件与 LilToNonToon Switcher 都在这份索引里）：

```
https://njsgdd10086.github.io/vpm-listing/index.json
```

升级到 1.1.1 后 NonToon 会重新导入一次，粉色就会恢复。
