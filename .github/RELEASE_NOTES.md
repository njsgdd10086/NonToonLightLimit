**NonToon Light Limit 1.1.6** —— 修掉「生成菜单和参数」后上传失败（`Index was outside the bounds of the array`）的问题。

## 修复内容

- **菜单改用 Modular Avatar 自己的组件来挂**，和 MA 的 `GameObject → Modular Avatar → Create Toggle`
  完全同一条路：同一个物体上放

  * **MA Menu Item**（`RadialPuppet` 控件 + 参数名，`isSynced / isSaved / isDefault` 都开）
  * **MA Menu Installer**（`Menu to Append` 留空）

  菜单由 Modular Avatar 在构建时生成。之前的写法是插件自己 `CreateInstance` 造一个
  `VRCExpressionsMenu` 资源再挂到 `Menu to Append` 上，有用户的工程在这种写法下上传会失败
  （`BuilderException: Index was outside the bounds of the array`），换成 MA 的标准写法后恢复正常。
- 因此**不再生成 `*_Brightness_Menu.asset`**，输出文件夹里只剩两条动画 + 一个 AnimatorController。
- 另外这一版把生成内容拆成两个可单独勾选的部分（动画层 / 菜单和参数），方便只想用一半的情况。

## 已经验证过

- 生成后组件为：`MA Merge Animator` + `MA Menu Item`（RadialPuppet / NonToonBrightness）+ `MA Menu Installer` + `MA Parameters`；
- 完整构建（Build & Test 的导出路径）通过，构建产物里的合并菜单包含
  `NonToon亮度菜单 / type 203 / NonToonBrightness`；
- 动画层单独生成时上传也正常（用户实测）。

## 升级后请这样做

1. ALCOM 更新到 1.1.6；
2. 工具窗口点 **删除已生成**（清掉旧写法留下的菜单资源引用），再点 **生成 / 更新**；
3. 旧版本生成的 `*_Brightness_Menu.asset` 已经没用了，可以连同 `Assets/NonToonLightLimit` 里那一份一起删掉；
4. 然后 Build & Test。

## 安装 / 升级

VCC / ALCOM 仓库地址（总仓库，本插件与 LilToNonToon Switcher 都在这份索引里）：

```
https://njsgdd10086.github.io/vpm-listing/index.json
```
