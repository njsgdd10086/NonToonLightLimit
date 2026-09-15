**NonToon Light Limit 1.1.7** —— 修掉「退出菜单数值归零 / 滑过一半跳回 0 / 部分部件不跟着走」的问题。

## 根因

VRChat 官方文档（[Expressions Menu and Controls](https://creators.vrchat.com/avatars/expression-menu-and-controls/)）规定：
**Puppet 类控件的 `Parameter` 字段不是数值参数，而是「这个 puppet 是否打开」的开关**（打开时=1，**退出菜单时清零**）；
真正的数值要写在 **`Sub-Parameters`** 里（径向就是 0..1 的那一个）。

之前把数值参数写进了 `Parameter`，于是：

- 拖动时客户端往这个字段写 0/1 → 动画只收到跳变；
- **退出菜单时该字段按语义清零** → 你看到的"退出变 0"；
- 滑过一半触发状态翻转 → "滑到 50 以上变回 0"；
- 数值参数从未真正被写入 → 部分材质看起来"不跟着走"。

## 现在改成

```yaml
type: 203                                     # RadialPuppet
parameter:  { name: "" }                      # 开关字段留空，不额外占参数
subParameters:
- { name: NonToonBrightness }                 # 0..1 的数值写到这里
```

## 已验证（从构建产物 .vrca 里读出来的）

```
参数: NonToonBrightness  valueType=Float  saved=True  defaultValue=0.5714286  synced=False
菜单项: type=RadialPuppet  "NonToon亮度菜单"
    parameter     = ""
    subParameters = NonToonBrightness
```

> `synced=False` 是正常的：Puppet 参数在 VRChat 里本来就是"打开时用 IK 同步"的本地参数，
> 官方文档里也写了 Puppet 的参数不走普通 Playable 同步。

## 升级后请这样做

1. ALCOM 更新到 1.1.7；
2. 工具窗口点 **删除已生成** → **生成 / 更新**；
3. 上传，在真机里确认：拖动滑块 → 亮度连续变化；**退出菜单后数值保留**。

## 安装 / 升级

VCC / ALCOM 仓库地址（总仓库，本插件与 LilToNonToon Switcher 都在这份索引里）：

```
https://njsgdd10086.github.io/vpm-listing/index.json
```
