**NonToon Light Limit 1.1.2** —— 生成/删除小工具会清理掉所有重复的 `_NonToonLightLimit` 物体。

## 修复内容

- **重复的生成物体**：之前只查找 avatar 根目录下的第一个 `_NonToonLightLimit`，
  如果把它挪到别处、或者复制出了多份，再次生成就会留下多个同名的
  **MA Merge Animator / Menu Installer / Parameters**。
  重复挂菜单、重复声明参数会让上传阶段出错。
  现在改成在整个 avatar 下**递归查找并全部清掉**，再在 avatar 根目录下建一个干净的。

## 建议操作

1. 在 ALCOM 里更新到 1.1.2；
2. 打开 **Tools → NonToon 亮度控制 → 生成全局亮度动画 + 菜单**，点一次 **删除已生成**，再点 **生成 / 更新**；
3. 如果你的场景里还有手改过 / 复制出来的同名 `_NonToonLightLimit`（例如只有 `MA Menu Install Target` 的那种），
   一并删掉再生成。

## 安装 / 升级

VCC / ALCOM 仓库地址（总仓库，本插件与 LilToNonToon Switcher 都在这份索引里）：

```
https://njsgdd10086.github.io/vpm-listing/index.json
```
