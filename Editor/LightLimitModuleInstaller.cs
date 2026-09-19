// 把 Light Limit 模块登记进 NonToon 的模块列表。
//
// Shader Core 的模块是「按 shader 白名单」加载的：SCShaderImporter 里只有
// shaderModules.Contains(uniqueID) 的模块才会被编译进 shader。这份白名单存在
// ProjectSettings/jp.lilxyzw.shadercore.asset，而 Shader Core 只会在「导入 scshader 时」
// 顺手把该 shader 目录下的模块写进去 —— 外挂包里的模块不在那个目录里，所以必须自己登记。
//
// 这里做两件事：
//   1. 调用 Shader Core 自己的 ProjectSettings.GetShaderModules()，让白名单按它的规则建立；
//   2. 把本模块的 uniqueID 追加进去并落盘，然后重新导入 NonToon 的 scshader。
// 只追加、不删除，所以是幂等的；NonToon 自带的模块一个都不会动。

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AtriNaxu.NonToonLightLimit
{
    [InitializeOnLoad]
    internal static class LightLimitModuleInstaller
    {
        /// <summary>本模块在 .scmodule 里声明的 uniqueID。</summary>
        internal const string ModuleId = "com.atrinaxu.nontoon.lightlimit";

        /// <summary>Shader Core 的工程设置文件（相对工程根目录）。</summary>
        private const string SettingsRelativePath = "ProjectSettings/jp.lilxyzw.shadercore.asset";

        /// <summary>禁止自动登记（设成 true 后只能从菜单手动登记）。</summary>
        private const string OptOutKey = "AtriNaxu.NonToonLightLimit.Disabled";

        /// <summary>本次编辑器会话是否已经检查过。</summary>
        private const string SessionKey = "AtriNaxu.NonToonLightLimit.Checked";

        static LightLimitModuleInstaller()
        {
            // delayCall 常常被 isCompiling / isUpdating 挡掉，所以改成轮询等编辑器就绪
            EditorApplication.update += WaitForIdle;
        }

        private static void WaitForIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitForIdle;
            AutoRegister();
        }

        private static void AutoRegister()
        {
            if (EditorPrefs.GetBool(OptOutKey, false)) return;
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            var pending = FindUnregisteredShaders();
            if (pending.Count == 0) return;

            if (Register())
            {
                Debug.Log("[NonToon 亮度控制] 已把模块登记到 " + string.Join(" / ", pending) +
                          "，NonToon 正在重新导入。\n" +
                          "想手动管理可以在 NonToon.scshader 的模块列表里勾选；" +
                          "想关闭自动登记，把 EditorPrefs 的 " + OptOutKey + " 设为 true。");
            }
            else
            {
                Debug.LogWarning("[NonToon 亮度控制] 自动登记没成功，请在 NonToon.scshader 的模块列表里手动勾选 " +
                                 ModuleId + "（菜单：Tools/NonToon 亮度控制/重新登记到模块列表）。");
            }
        }

        [MenuItem("Tools/NonToon 亮度控制/重新登记到模块列表", false, 10)]
        private static void RegisterFromMenu()
        {
            if (Register()) Debug.Log("[NonToon 亮度控制] 模块列表已更新，NonToon 正在重新导入。");
            else if (FindUnregisteredShaders().Count == 0) Debug.Log("[NonToon 亮度控制] NonToon 已经加载了该模块，无需重复登记。");
            else Debug.LogWarning("[NonToon 亮度控制] 登记失败，请在 NonToon.scshader 的模块列表里手动勾选 " + ModuleId + "。");
        }

        [MenuItem("Tools/NonToon 亮度控制/从模块列表移除", false, 11)]
        private static void UnregisterFromMenu()
        {
            if (SetRegistered(false)) Debug.Log("[NonToon 亮度控制] 已从模块列表移除，NonToon 正在重新导入。");
            else Debug.Log("[NonToon 亮度控制] 模块本来就没有登记。");
        }

        // ------------------------------------------------------------------ 登记 / 移除

        /// <summary>把本模块补进 NonToon 的模块列表。返回是否真的有改动（幂等：已登记时返回 false）。</summary>
        /// <summary>
        /// 登记到 NonToon 的模块列表。
        /// 优先交给模块包 com.nontoon.modules（模块本体已经搬到那里了，本插件通过 VPM 依赖订阅它）；
        /// 模块包没装时回退到本插件自带的老实现，保证功能不中断。
        /// </summary>
        internal static bool Register()
        {
            if (NonToonModulesBridge.TryEnsureEnabled(out var ok)) return ok;
            return SetRegistered(true);
        }

        /// <summary>哪些目标 shader 还没有登记本模块。</summary>
        internal static List<string> FindUnregisteredShaders()
        {
            var result = new List<string>();
            try
            {
                if (!TryGetSettings(out var instance, out var shaderSettings)) return result;
                var getModules = GetShaderModulesMethod(instance.GetType());
                if (getModules == null) return result;

                foreach (var path in FindNonToonShaderPaths())
                {
                    var modules = GetModules(instance, getModules, path) ?? FindModules(shaderSettings, ShaderNameOf(path));
                    if (modules == null) { result.Add(path); continue; }
                    if (!modules.Contains(ModuleId)) result.Add(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NonToon 亮度控制] 读取 Shader Core 设置失败：" + exception.Message);
            }
            return result;
        }

        private static bool SetRegistered(bool registered)
        {
            try
            {
                if (!TryGetSettings(out var instance, out var list))
                {
                    Debug.LogWarning("[NonToon 亮度控制] 找不到 Shader Core 的设置对象，请确认已经安装 jp.lilxyzw.shadercore。");
                    return false;
                }

                var getModules = GetShaderModulesMethod(instance.GetType());
                var paths = FindNonToonShaderPaths();
                if (paths.Count == 0)
                {
                    Debug.LogWarning("[NonToon 亮度控制] 没有找到 NonToon 的 .scshader，请确认已经安装 jp.lilxyzw.nontoon。");
                    return false;
                }

                var changed = false;
                foreach (var path in paths)
                {
                    // 优先走 Shader Core 自己的入口：它会按官方规则建立条目（缺失时自动补上并落盘）
                    var modules = GetModules(instance, getModules, path);
                    if (modules == null) modules = FindModules(list, ShaderNameOf(path));
                    if (modules == null)
                    {
                        modules = CreateShaderSettings(list, path);
                        if (modules == null)
                        {
                            Debug.LogWarning("[NonToon 亮度控制] 无法为 " + path + " 建立 Shader Core 模块设置条目。");
                            continue;
                        }
                        // 新建条目本身就是要落盘的改动（Shader Core 建立条目时也会立刻 Save）
                        changed = true;
                        if (modules.Contains(ModuleId) == registered) continue;
                    }

                    if (registered)
                    {
                        if (modules.Contains(ModuleId)) continue;
                        modules.Add(ModuleId);
                    }
                    else
                    {
                        if (!modules.Contains(ModuleId)) continue;
                        modules.Remove(ModuleId);
                    }
                    changed = true;
                }

                if (!changed) return false;

                if (!SaveSettings(instance))
                {
                    Debug.LogWarning("[NonToon 亮度控制] Shader Core 的设置没能落盘，本次会话仍然生效；" +
                                     "如果重启 Unity 后失效，请在 NonToon.scshader 的模块列表里手动勾选 " + ModuleId + "。");
                }
                ReimportTargetShaders();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[NonToon 亮度控制] 登记失败：" + exception);
                return false;
            }
        }

        /// <summary>目标 shader 的 asset 路径（Packages / Assets 下所有含 nontoon 的 .scshader）。</summary>
        private static List<string> FindNonToonShaderPaths()
        {
            var result = new List<string>();
            foreach (var root in new[] { "Packages", "Assets" })
            {
                if (!Directory.Exists(root)) continue;
                foreach (var path in Directory.GetFiles(root, "*.scshader", SearchOption.AllDirectories))
                {
                    var normalized = path.Replace('\\', '/');
                    if (normalized.IndexOf("nontoon", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    result.Add(normalized);
                }
            }
            return result;
        }

        /// <summary>复刻 Shader Core 取 shader 名的方式，用于在设置文件里定位条目。</summary>
        private static string ShaderNameOf(string shaderPath)
        {
            try
            {
                foreach (var line in File.ReadLines(shaderPath))
                {
                    var trimmed = line.TrimStart();
                    if (!trimmed.StartsWith("Shader", StringComparison.Ordinal)) continue;
                    var first = trimmed.IndexOf('"');
                    var last = trimmed.LastIndexOf('"');
                    if (first >= 0 && last > first) return trimmed.Substring(first + 1, last - first - 1);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NonToon 亮度控制] 解析 " + shaderPath + " 的 shader 名失败：" + exception.Message);
            }
            return null;
        }

        /// <summary>
        /// 兜底：Shader Core 的 GetShaderModules 不存在时（换版本）自己补一个条目。
        /// 默认列表 = 该 shader 所在目录下的全部非 multi 模块，与 Shader Core 首次导入时的行为一致。
        /// </summary>
        private static List<string> CreateShaderSettings(IList list, string shaderPath)
        {
            try
            {
                var shaderName = ShaderNameOf(shaderPath);
                if (string.IsNullOrEmpty(shaderName)) return null;

                var listType = list.GetType();
                var entryType = listType.IsGenericType ? listType.GetGenericArguments()[0] : null;
                if (entryType == null) return null;

                var entry = Activator.CreateInstance(entryType);
                var nameField = entryType.GetField("shadername");
                var modulesField = entryType.GetField("modules");
                if (nameField == null || modulesField == null) return null;

                nameField.SetValue(entry, shaderName);
                modulesField.SetValue(entry, FindModulesInDirectory(Path.GetDirectoryName(shaderPath)?.Replace('\\', '/')));

                var multiField = entryType.GetField("multiModules");
                if (multiField != null && multiField.GetValue(entry) == null)
                    multiField.SetValue(entry, Activator.CreateInstance(multiField.FieldType));

                list.Add(entry);
                return modulesField.GetValue(entry) as List<string>;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NonToon 亮度控制] 建立 Shader Core 设置条目失败：" + exception.Message);
                return null;
            }
        }

        private static List<string> FindModulesInDirectory(string directory)
        {
            var ids = new List<string>();
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return ids;
            foreach (var path in Directory.GetFiles(directory, "*.scmodule", SearchOption.AllDirectories))
            {
                var id = ModuleIdOf(path);
                if (id == null || ids.Contains(id)) continue;
                // 带 properties_multi.hlsl 的属于 multi 模块，不进 modules 列表
                if (File.Exists(Path.Combine(Path.GetDirectoryName(path) ?? "", "properties_multi.hlsl"))) continue;
                ids.Add(id);
            }
            return ids;
        }

        private static string ModuleIdOf(string scmodulePath)
        {
            try
            {
                foreach (var line in File.ReadLines(scmodulePath))
                {
                    var index = line.IndexOf("\"uniqueID\"", StringComparison.Ordinal);
                    if (index < 0) continue;
                    var first = line.IndexOf('"', index + 10);
                    var last = line.IndexOf('"', first + 1);
                    if (first >= 0 && last > first) return line.Substring(first + 1, last - first - 1);
                }
            }
            catch
            {
                // 读不到就当没有这个模块
            }
            return null;
        }

        private static void ReimportTargetShaders()
        {
            AssetDatabase.Refresh();
            foreach (var path in FindNonToonShaderPaths())
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        // ------------------------------------------------------------------ 反射访问 Shader Core 的内部设置

        private static bool TryGetSettings(out object instance, out IList shaderSettings)
        {
            instance = null;
            shaderSettings = null;

            var type = FindType("jp.lilxyzw.shadercore.ProjectSettings");
            if (type == null) return false;

            var property = type.GetProperty("instance",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (property != null) instance = property.GetValue(null, null);
            if (instance == null)
            {
                foreach (var obj in Resources.FindObjectsOfTypeAll(type))
                {
                    if (obj == null) continue;
                    instance = obj;
                    break;
                }
            }
            if (instance == null) return false;

            var field = type.GetField("shaderSettings", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return false;
            shaderSettings = field.GetValue(instance) as IList;
            return shaderSettings != null;
        }

        /// <summary>Shader Core 的 ProjectSettings.GetShaderModules(path, out modules, out multiModules)。</summary>
        private static MethodInfo GetShaderModulesMethod(Type settingsType)
        {
            foreach (var method in settingsType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (method.Name != "GetShaderModules") continue;
                var parameters = method.GetParameters();
                if (parameters.Length == 3 && parameters[0].ParameterType == typeof(string)) return method;
            }
            return null;
        }

        /// <summary>用 Shader Core 的入口取（必要时建立）某个 shader 的模块列表。</summary>
        private static List<string> GetModules(object instance, MethodInfo getModules, string shaderPath)
        {
            if (getModules == null) return null;
            var arguments = new object[] { shaderPath, null, null };
            getModules.Invoke(instance, arguments);
            return arguments[1] as List<string>;
        }

        private static List<string> FindModules(IList shaderSettings, string shaderName)
        {
            if (shaderSettings == null || string.IsNullOrEmpty(shaderName)) return null;
            foreach (var entry in shaderSettings)
            {
                if (entry == null) continue;
                var type = entry.GetType();
                var nameField = type.GetField("shadername");
                if (nameField == null || (string)nameField.GetValue(entry) != shaderName) continue;
                var modulesField = type.GetField("modules");
                return modulesField?.GetValue(entry) as List<string>;
            }
            return null;
        }

        /// <summary>调用 Shader Core 自己的 Save()；万一没写出文件就按它的格式补一份。</summary>
        private static bool SaveSettings(object instance)
        {
            var type = instance.GetType();
            var save = type.GetMethod("Save", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (save != null)
            {
                try { save.Invoke(instance, null); }
                catch (Exception exception) { Debug.LogWarning("[NonToon 亮度控制] 调用 Shader Core 的 Save() 失败：" + exception.Message); }
            }

            var path = SettingsFilePath();
            if (File.Exists(path)) return true;

            if (instance is UnityEngine.Object unityObject)
            {
                EditorUtility.SetDirty(unityObject);
                AssetDatabase.SaveAssets();
                if (File.Exists(path)) return true;
            }

            return WriteSettingsFile(instance, path);
        }

        private static string SettingsFilePath()
        {
            var root = Directory.GetParent(Application.dataPath);
            return Path.Combine(root?.FullName ?? ".", SettingsRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// 极端兜底：按 Unity 的 ScriptableSingleton 文本格式把设置写出去。
        /// 只在 Shader Core 的 Save() 没生成文件时使用；字段结构读不出来就放弃（下次开编辑器它会自己重建）。
        /// </summary>
        private static bool WriteSettingsFile(object instance, string path)
        {
            try
            {
                var type = instance.GetType();
                var listField = type.GetField("shaderSettings", BindingFlags.NonPublic | BindingFlags.Instance);
                var list = listField?.GetValue(instance) as IList;
                if (list == null) return false;

                var guid = ScriptGuid();
                if (string.IsNullOrEmpty(guid)) return false;

                var builder = new StringBuilder();
                builder.AppendLine("%YAML 1.1");
                builder.AppendLine("%TAG !u! tag:unity3d.com,2011:");
                builder.AppendLine("--- !u!114 &1");
                builder.AppendLine("MonoBehaviour:");
                builder.AppendLine("  m_ObjectHideFlags: 53");
                builder.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
                builder.AppendLine("  m_PrefabInstance: {fileID: 0}");
                builder.AppendLine("  m_PrefabAsset: {fileID: 0}");
                builder.AppendLine("  m_GameObject: {fileID: 0}");
                builder.AppendLine("  m_Enabled: 1");
                builder.AppendLine("  m_EditorHideFlags: 0");
                builder.AppendLine("  m_Script: {fileID: 11500000, guid: " + guid + ", type: 3}");
                builder.AppendLine("  m_Name: ");
                builder.AppendLine("  m_EditorClassIdentifier: ");
                builder.AppendLine("  shaderSettings:");

                foreach (var entry in list)
                {
                    if (entry == null) continue;
                    var entryType = entry.GetType();
                    builder.AppendLine("  - shadername: " + ((string)entryType.GetField("shadername")?.GetValue(entry) ?? ""));
                    builder.AppendLine("    modules:");
                    if (entryType.GetField("modules")?.GetValue(entry) is IEnumerable modules)
                        foreach (var module in modules) builder.AppendLine("    - " + module);

                    var multi = entryType.GetField("multiModules")?.GetValue(entry) as IEnumerable;
                    var multiLines = new List<string>();
                    if (multi != null)
                    {
                        foreach (var item in multi)
                        {
                            if (item == null) continue;
                            var itemType = item.GetType();
                            multiLines.Add("    - name: " + (itemType.GetField("name")?.GetValue(item) ?? ""));
                            multiLines.Add("      count: " + (itemType.GetField("count")?.GetValue(item) ?? 0));
                        }
                    }
                    if (multiLines.Count == 0) builder.AppendLine("    multiModules: []");
                    else
                    {
                        builder.AppendLine("    multiModules:");
                        foreach (var line in multiLines) builder.AppendLine(line);
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
                AssetDatabase.Refresh();
                Debug.Log("[NonToon 亮度控制] Shader Core 的 Save() 没有生成设置文件，已按同样格式补写：" + path);
                return File.Exists(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NonToon 亮度控制] 补写 Shader Core 设置文件失败：" + exception.Message);
                return false;
            }
        }

        /// <summary>Shader Core 的 ProjectSettings.cs 的脚本 GUID。</summary>
        private static string ScriptGuid()
        {
            foreach (var root in new[] { "Packages", "Assets" })
            {
                if (!Directory.Exists(root)) continue;
                foreach (var meta in Directory.GetFiles(root, "ProjectSettings.cs.meta", SearchOption.AllDirectories))
                {
                    var normalized = meta.Replace('\\', '/');
                    if (normalized.IndexOf("shadercore", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    foreach (var line in File.ReadLines(normalized))
                    {
                        var trimmed = line.Trim();
                        if (!trimmed.StartsWith("guid:", StringComparison.Ordinal)) continue;
                        return trimmed.Substring(5).Trim();
                    }
                }
            }
            return null;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
