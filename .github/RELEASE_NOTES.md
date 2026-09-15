**NonToon Light Limit 1.1.3** —— 修掉「生成物被挪进 prefab 实例后清理不掉」导致菜单挂错位置 / 构建报错的问题。

## 修复内容

- **清理生成物改用 `Undo.DestroyObjectImmediate`**：之前用 `DestroyImmediate`，如果 `_NonToonLightLimit`
  被挪进了服装的「装饰开关」之类属于 **prefab 实例** 的物体里，就删不掉，会留下重复的
  **MA Merge Animator / Menu Installer / Menu Install Target**，于是：
  - Modular Avatar 把菜单挂到了别的位置 → 游戏 / Gesture Manager 里看不到亮度滑块；
  - 构建阶段报 `Index was outside the bounds of the array`。
- 现在生成/删除时还会：
  - 把被挪到别处的同名物体删掉，并在 Console 里说明它在哪；
  - 发现同名物体上挂着 MA 的「菜单安装点」（Menu Install Target）时一并清掉；
  - 始终把新建的 `_NonToonLightLimit` 放在 **avatar 根目录**下。

## 建议操作

1. ALCOM 更新到 1.1.3；
2. 打开 **Tools → NonToon 亮度控制 → 生成全局亮度动画 + 菜单**；
3. 先点 **删除已生成**（会清掉所有同名物体，包括被挪走的和带 Menu Install Target 的），
   再点 **生成 / 更新**；
4. 生成的 `_NonToonLightLimit` **不要再拖进服装的装饰开关里**（保持在 avatar 根目录），然后 Build & Test。

## 安装 / 升级

VCC / ALCOM 仓库地址（总仓库，本插件与 LilToNonToon Switcher 都在这份索引里）：

```
https://njsgdd10086.github.io/vpm-listing/index.json
```
