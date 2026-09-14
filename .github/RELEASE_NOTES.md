**NonToon Light Limit 1.0.0** —— 给 NonToon 加上亮度上下限与亮度倍数，并支持一个全局变量统一控制，做 Light Limit Changer 式的亮度调节。

## 安装

### VCC / ALCOM（推荐）

1. 打开 VCC（或 ALCOM）→ **Settings → Packages → Add Repository**
2. 填入仓库地址：

   ```
   https://njsgdd10086.github.io/NonToonLightLimit/index.json
   ```

3. 回到工程的 **Manage Project**，在 `NonToon Light Limit` 上点 **Install**。

### 手动

下载下方附件 `com.atrinaxu.nontoon.lightlimit-1.0.0.zip`，解压到工程的
`Packages/com.atrinaxu.nontoon.lightlimit/`。

## 依赖

| 包 | 用途 |
| --- | --- |
| `jp.lilxyzw.shadercore` | 模块系统（本插件以 Shader Core 模块形式注入） |
| `jp.lilxyzw.nontoon` | 目标 shader |

## 用法

1. 安装后第一次打开工程会自动登记模块，Console 会打印 `[NonToon 亮度控制] 已把模块登记到 ...`；
2. 打开任意 NonToon 材质，找到 **NonToon Light Limit** 一组参数：
   - `Min Brightness` / `Max Brightness`：亮度上下限；
   - `Brightness`：亮度倍数；
   - `Use Global Control` + `Mask Channel`：是否接受全局倍数、用共享遮罩哪个通道限制范围。
3. 全局控制用脚本设置：

   ```csharp
   Shader.SetGlobalFloat("_NonToonLightLimit_Global", 1.3f);   // 全局亮度倍数
   Shader.SetGlobalFloat("_NonToonLightLimit_Envelope", 0.6f); // 0..1 全局包络
   ```

完整说明见 [README](https://github.com/njsgdd10086/NonToonLightLimit#readme)。

## 本次内容

- 亮度上下限 + 亮度倍数 + 遮罩范围 + 全局控制，全部以 Shader Core 模块实现，不改 NonToon 本体；
- 模块排在 NonToon 自带 Light Boost 之后，两者可以叠加；
- 自动把模块登记进 NonToon 的模块列表并落盘（重启编辑器仍生效），只追加不删除；
- 菜单 `Tools → NonToon 亮度控制` 可手动重新登记 / 移除；
- 同时支持 NonToon 与 NonToonFur。
