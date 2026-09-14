**NonToon Light Limit 1.1.0** —— NonToon 的亮度上下限 + 亮度倍数 + 全局控制，这一版加上了「一键生成全局亮度动画 + 表情菜单滑块」。

## 安装

### VCC / ALCOM（推荐）

1. 打开 VCC（或 ALCOM）→ **Settings → Packages → Add Repository**
2. 填入仓库地址：

   ```
   https://njsgdd10086.github.io/NonToonLightLimit/index.json
   ```

3. 回到工程的 **Manage Project**，在 `NonToon Light Limit` 上点 **Install**。

### 手动

下载下方附件 `com.atrinaxu.nontoon.lightlimit-1.1.0.zip`，解压到工程的
`Packages/com.atrinaxu.nontoon.lightlimit/`。

## 依赖

| 包 | 用途 |
| --- | --- |
| `jp.lilxyzw.shadercore` | 模块系统（本插件以 Shader Core 模块形式注入） |
| `jp.lilxyzw.nontoon` | 目标 shader |
| VRChat SDK3 Avatars + Modular Avatar | 只有「一键生成全局动画 + 菜单」这个小工具需要 |

## 本次更新

- 新增 **一键生成全局亮度动画 + 菜单**（`Tools → NonToon 亮度控制 → 生成全局亮度动画 + 菜单`）：
  - 自动找出 avatar 下所有 NonToon 材质，生成两条动画（最暗 / 最亮）+ 单层 AnimatorController（1D 混合树）+ 一个表情菜单径向滑块；
  - 在 avatar 下建 `_NonToonLightLimit` 物体，挂 Modular Avatar 的 Merge Animator / Menu Installer / Parameters，上传时自动接好；
  - 可调参数名、菜单名、最暗/最亮倍数、默认倍数、输出文件夹；
  - 「包含 lilToon 材质槽」选项：配合 shader 一键切换开关（MA Material Setter）时勾上，切换成 NonToon 后同样受滑块控制；
  - 重复生成会覆盖上一次，也可一键删除（不动材质本身）。
- 亮度模块本身没有变化：上下限、亮度倍数、遮罩范围照旧，逐材质的 Min / Max 在滑块拖动时仍然生效。

完整说明见 [README](https://github.com/njsgdd10086/NonToonLightLimit#readme)。
