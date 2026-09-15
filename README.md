# NonToon 亮度控制（NonToon Light Limit）

给 [NonToon](https://github.com/lilxyzw/NonToon) 加上 **亮度上下限** 和 **亮度倍数**，并支持用一个全局变量统一控制所有材质 —— 也就是 [Light Limit Changer](https://booth.pm/ja/items/4864776) 那种「环境再暗也不会黑、再亮也不会过曝」的效果。

实现方式是给 Shader Core 写一个外挂模块，**不改 NonToon 本体**，NonToon 更新后重新导入即可，也不影响 NonToon 自带的 Light Boost、Shade、RimShade 等模块。

> 配套插件：[LilToNonToon Switcher](https://github.com/njsgdd10086/LilToNonToonSwitcher)（把 lilToon 材质转成 NonToon 并生成一键切换开关）。

---

## 特性

| 功能 | 说明 |
| --- | --- |
| 亮度下限 | 环境再暗也不会低于设定值，暗处不会糊成一团黑 |
| 亮度上限 | 环境再亮也不会高于设定值，避免高光过曝 |
| 亮度倍数 | 每个材质单独的整体明暗，1 = 不改变 |
| 等比保持 | 按亮度（luma）归一后夹取，暗部/亮部之间的比例不变，不会把阴影拍平 |
| 遮罩范围 | 用共享遮罩（Shared Mask）的某个通道控制生效范围，比如只提亮脸部 |
| 全局控制 | 用一个全局变量同时驱动所有开了「Use Global Control」的材质 |
| 一键全局动画（可选） | 小工具给所有 NonToon 材质生成亮度动画 + 表情菜单滑块，一根滑块控制全部材质 |
| 与 Light Boost 叠加 | 模块排在 NonToon 自带 Lighten 之后执行，先提亮再限制，两者可以一起用 |

## 安装

### 方式一：VCC / ALCOM（推荐）

1. 打开 VCC（或 ALCOM），进入 **Settings → Packages → Add Repository**；
2. 填入总仓库地址（**一个链接就够**，本插件和 [LilToNonToon Switcher](https://github.com/njsgdd10086/LilToNonToonSwitcher) 都在这份索引里）：

   ```
   https://njsgdd10086.github.io/vpm-listing/index.json
   ```

3. 在包列表里找到 **NonToon Light Limit**，点 Install。
4. 依赖的 `jp.lilxyzw.shadercore` 与 `jp.lilxyzw.nontoon` 会自动装上（如果项目里还没有）。

> 这个索引是「**ATRI_NAIXU VPM Packages**」总仓库（独立的
> [vpm-listing](https://github.com/njsgdd10086/vpm-listing) 仓库，由 Actions 自动从各插件仓库的
> Release 生成），包含本插件与 [LilToNonToon Switcher](https://github.com/njsgdd10086/LilToNonToonSwitcher)，
> 按需勾选安装即可。

### 方式二：手动放进 Packages

把本仓库整个文件夹复制到工程的 `Packages/com.atrinaxu.nontoon.lightlimit`（或直接把 Release 里的 zip 解压进去）。需要工程里已经有 Shader Core 和 NonToon。

## 使用

### 1. 确认模块已经登记

安装后第一次打开工程，插件会自动把模块登记进 NonToon 的模块列表并重新导入 NonToon，Console 里会看到：

```
[NonToon 亮度控制] 已把模块登记到 ... ，NonToon 正在重新导入。
```

如果没看到或者属性没出现，手动执行菜单 **Tools → NonToon 亮度控制 → 重新登记到模块列表**。
（也可以直接在 `NonToon.scshader` 的模块列表里勾选 `NonToon Light Limit (com.atrinaxu.nontoon.lightlimit)`。）

### 2. 调材质

材质面板上会多出 `NonToon Light Limit` 一组参数（属性名带 `_com_atrinaxu_nontoon_lightlimit_` 前缀）：

| 参数 | 默认 | 作用 |
| --- | --- | --- |
| **Min Brightness** | 0 | 亮度下限。0.2 表示最暗的地方也按 0.2 的亮度渲染 |
| **Max Brightness** | 1 | 亮度上限。NonToon 原本在 shader 里把上限压死在 1，调大可以放开 |
| **Brightness** | 1 | 亮度倍数，1 = 不改变；想做整体调亮/调暗就改这里（菜单滑块驱动的也是它） |
| **Mask Channel** | A | 用共享遮罩的哪个通道限制生效范围；没设共享遮罩时全生效 |

想批量改，可以直接框选多个 `.mat` 在 Inspector 里改，这些参数都是可动画的材质属性。

### 3. 全局控制（一个滑块控制所有材质）

Shader Core 的模块代码是插在函数体里的，**声明不了 shader 全局变量**，所以「全局控制」不是在 shader 里做的，
而是用下面第 4 节的小工具：它给所有材质的 `Brightness` 写同一条动画，再用一个表情菜单滑块驱动，
**一根滑块控制全部材质**。

不用那个小工具也可以，只要自己驱动这些材质属性就行：

* 在动画里逐材质改 **Brightness** / **Min** / **Max**——它们是普通材质属性，动画、菜单、
  MA / VRCFury 的材质属性动作都能驱动；
* 只想让一部分生效（比如不动脸），用 **Mask Channel** 选一个特定通道；
* 想让某些材质完全不受影响：把它的 **Mask Channel** 指向一个全 0 的通道，或者干脆不把它写进动画。

世界（Udon）里也可以用 `Shader.SetGlobalFloat` 驱动。

> **关于 VRChat 头像的运行时控制**：Unity 的动画系统只能动画「渲染器上的材质属性」，**不能驱动 shader 全局变量**，所以头像里想让一个菜单滑条控制所有材质，只能靠给每个材质生成动画。下面的小工具就是干这个的。

### 4. 一键生成全局亮度动画 + 菜单（可选）

菜单：**Tools → NonToon 亮度控制 → 生成全局亮度动画 + 菜单**

给 avatar 下所有 NonToon 材质写一套亮度动画，并生成一个表情菜单滑块，**一根滑块控制全部材质的亮度**（就是 Light Limit Changer 那个「一括調整」的效果）。

窗口里可调：

| 选项 | 默认 | 说明 |
| --- | --- | --- |
| 目标 Avatar | 当前选中对象所在的 avatar | 需要有 VRC Avatar Descriptor |
| 参数名 | `NonToonBrightness` | 同步的 Float 参数 |
| 菜单名 | `亮度` | 表情菜单里显示的名字 |
| 最暗倍数 / 最亮倍数 | 0.25 / 1.75 | 滑块两端对应的亮度倍数 |
| 默认倍数 | 1.0 | 进游戏时的初始亮度，1 = 不改变 |
| 输出文件夹 | `Assets/NonToonLightLimit` | 生成的动画、控制器、菜单资源放这里 |

点「生成 / 更新」后会在 avatar 下建一个 `_NonToonLightLimit` 物体，挂着 Modular Avatar 的
**Merge Animator**（把一层 FX 动画合并进去）、**Menu Installer**（挂表情菜单）、
**Parameters**（声明同步参数），并生成两条动画（最暗 / 最亮）+ 一个单层 AnimatorController
（1D 混合树，参数 0 = 最暗、1 = 最亮）+ 一个菜单资源（径向滑块）。上传时 MA 会自动接好。

要点：

* 需要 **VRChat SDK3 Avatars** 与 **Modular Avatar**；缺依赖时窗口会直接提示。
* 滑块是通过材质属性动画（MaterialPropertyBlock）生效的，**不会修改材质资产**；
  拖动范围内材质的 **Brightness** 数值由滑块统一决定（默认位置 = 1.0，即不改变），
  逐材质的 **Min / Max 上下限**、遮罩范围照旧生效。
* 用的是一键切换开关（MA Material Setter）时，渲染器上平时还是 lilToon 材质，
  这时勾上「包含 lilToon 材质槽」再生成，切换成 NonToon 后同样受滑块控制。
* 重复生成会先清掉上一次生成的东西；「删除已生成」可以完全撤销（不动材质本身）。

## 和 Light Limit Changer 的关系

|  | Light Limit Changer | 本插件 |
| --- | --- | --- |
| 支持的 shader | lilToon / Poiyomi / … | 只做 NonToon |
| 逐材质上下限 | ✅ | ✅ |
| 全局一个滑块控制全部 | ✅（生成动画 + 菜单） | ✅（模块 + 附带的一键生成小工具） |
| 需要生成动画/菜单的工具 | 需要 | 可选，不生成也能用模块 |
| 对 NonToon 的影响 | 改材质参数 | 追加一个 Shader Core 模块 |

## 原理

* 模块以 `.scmodule` + `properties.hlsl` + `phase_modifylight.hlsl` 的形式挂在 Shader Core 的 `modifylight` 阶段，并声明 `afters: ["Lighten"]`，所以它在 NonToon 的 Light Boost 之后执行；
* NonToon 的 `urp.hlsl` / `birp.hlsl` 里有 `sd.lightColor = min(env + lightSum, 1)`，上限被硬性压死，因此压暗、限制上限这类操作必须在 `sd.lightColor` 被使用之前改，`modifylight` 正好是那个位置；
* Shader Core 只编译「白名单里的模块」（`SCShaderImporter` 里 `shaderModules.Contains(uniqueID)`），白名单存在 `ProjectSettings/jp.lilxyzw.shadercore.asset`，而 Shader Core 只会在导入 scshader 时把 **该 shader 目录下** 的模块写进去 —— 外挂包不在那个目录，所以插件里带了一个安装器：调用 Shader Core 自己的入口取出该 shader 的模块列表，追加本模块后落盘并重新导入。只追加不删除，幂等。

## 常见问题

**材质面板上没有出现参数？**
说明模块没被编译进 NonToon。执行菜单 **Tools → NonToon 亮度控制 → 重新登记到模块列表**，或检查 `NonToon.scshader` 的模块列表里有没有 `com.atrinaxu.nontoon.lightlimit`。

**重启 Unity 后失效？**
正常情况下不会 —— 登记结果写在 `ProjectSettings/jp.lilxyzw.shadercore.asset`。如果那个文件被删掉或回滚了，重新执行一次菜单即可。

**想彻底关掉自动登记？**
把 EditorPrefs 的 `AtriNaxu.NonToonLightLimit.Disabled` 设为 `true`，然后只用菜单手动登记。

**会不会破坏 NonToon 自带的模块？**
不会。登记只做「往列表里追加一个 ID」，NonToon 自带的 11 个模块原样保留（可以在设置文件里核对）。

## 许可

MIT License，见 [LICENSE](LICENSE)。
