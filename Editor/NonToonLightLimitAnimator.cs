// 一键生成「全局亮度动画 + 菜单」。
//
// 背景：Unity 的动画只能驱动「渲染器上的材质属性」，驱动不了 shader 全局变量，
// 所以头像里想用一个菜单滑条控制所有材质的亮度，必须给每个材质都写上动画曲线。
// 这个小工具就是把这个体力活自动化：
//
//   1. 找出 avatar 下所有带亮度模块属性的材质（NonToon / NonToonFur）；
//   2. 生成两条常量动画：最暗一条、最亮一条，每条都覆盖上面所有渲染器；
//   3. 生成一个只有一层的 AnimatorController，层里是一个 1D 混合树
//      （参数 0 → 最暗，参数 1 → 最亮），用 Float 参数驱动；
//   4. 生成一个 VRChat 表情菜单资源（径向滑块）；
//   5. 在 avatar 下建一个 _NonToonLightLimit 物体，挂上 Modular Avatar 的
//      Merge Animator / Menu Installer / Parameters，把上面三样接进去。
//
// 上传时 Modular Avatar 会把动画层合并进 FX、把菜单挂到表情菜单根上。
//
// 关于绑定写法：材质属性动画要写成 material.<属性名>，类型用渲染器自己的类型
// （MeshRenderer / SkinnedMeshRenderer）。实测这种写法是通过 MaterialPropertyBlock
// 生效的，会作用在该渲染器的所有材质槽上；而 m_Materials.Array.data[i].<属性名>
// 只对「换材质」那种对象引用曲线有效，写属性值不生效。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AtriNaxu.NonToonLightLimit
{
    internal sealed class LightLimitAnimationRequest
    {
        public GameObject AvatarRoot;
        public string ParameterName = "NonToonBrightness";
        public string MenuLabel = "亮度";
        public float MinMultiplier = 0.25f;
        public float MaxMultiplier = 1.75f;
        public float DefaultMultiplier = 1f;
        public string OutputFolder = "Assets/NonToonLightLimit";

        /// <summary>
        /// 是否把「当前还挂着 lilToon 材质」的渲染器也算进来。
        /// 用一键切换开关（MA Material Setter）时，渲染器上平时是 lilToon，
        /// 切换后才换成 NonToon，所以要把这些槽也写上动画，切换后才受滑块控制。
        /// </summary>
        public bool IncludeLilToon;
    }

    internal static class NonToonLightLimitAnimator
    {
        internal const string ContainerName = "_NonToonLightLimit";
        internal const string BrightnessProperty = "_com_atrinaxu_nontoon_lightlimit_Brightness";
        internal const string DefaultOutputFolder = "Assets/NonToonLightLimit";

        // ------------------------------------------------------------------ 对外入口

        /// <summary>选中对象所在 avatar 的根物体（需要有 VRCAvatarDescriptor）。</summary>
        internal static GameObject FindAvatar(GameObject selected)
        {
            var descriptorType = FindType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");
            if (descriptorType == null || selected == null) return null;

            var current = selected.transform;
            while (current != null)
            {
                if (current.GetComponent(descriptorType) != null) return current.gameObject;
                current = current.parent;
            }
            return null;
        }

        /// <summary>能用这个工具的前提：VRChat SDK + Modular Avatar 都在。</summary>
        internal static bool CheckDependencies(out string message)
        {
            var missing = new List<string>();
            if (FindType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor") == null) missing.Add("VRChat SDK3 Avatars");
            if (FindType("VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu") == null) missing.Add("VRChat SDK3 Avatars");
            if (FindType("nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator") == null) missing.Add("Modular Avatar");
            if (FindType("nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller") == null) missing.Add("Modular Avatar");
            if (FindType("nadena.dev.modular_avatar.core.ModularAvatarParameters") == null) missing.Add("Modular Avatar");

            message = missing.Count == 0 ? "" : "缺少依赖：" + string.Join(" / ", missing.Distinct().ToArray());
            return missing.Count == 0;
        }

        /// <summary>找出所有需要写动画的渲染器（材质里有亮度模块属性的）。</summary>
        internal static List<Renderer> CollectTargets(GameObject avatarRoot, bool includeLilToon)
        {
            var result = new List<Renderer>();
            if (avatarRoot == null) return result;

            foreach (var renderer in avatarRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                if (IsInsideContainer(renderer.transform, avatarRoot.transform)) continue;
                if (!IsTarget(renderer, includeLilToon)) continue;
                result.Add(renderer);
            }
            return result;
        }

        /// <summary>生成（或重新生成）整套东西。</summary>
        internal static bool Generate(LightLimitAnimationRequest request)
        {
            var log = new StringBuilder();
            try
            {
                if (!CheckDependencies(out var missing))
                {
                    Debug.LogError("[NonToon 亮度控制] " + missing);
                    return false;
                }

                if (request.AvatarRoot == null)
                {
                    Debug.LogError("[NonToon 亮度控制] 没有指定 avatar，请选中 avatar 本体后再生成。");
                    return false;
                }

                if (string.IsNullOrEmpty(request.ParameterName))
                {
                    Debug.LogError("[NonToon 亮度控制] 参数名不能为空。");
                    return false;
                }

                var min = Mathf.Max(0f, request.MinMultiplier);
                var max = Mathf.Max(0f, request.MaxMultiplier);
                if (Mathf.Approximately(min, max))
                {
                    Debug.LogError("[NonToon 亮度控制] 最暗倍数和最亮倍数不能一样，否则滑块没有效果。");
                    return false;
                }
                if (min > max) (min, max) = (max, min);

                var targets = CollectTargets(request.AvatarRoot, request.IncludeLilToon);
                if (targets.Count == 0)
                {
                    Debug.LogError("[NonToon 亮度控制] 在 " + request.AvatarRoot.name +
                                   " 下没有找到带亮度模块属性的材质。\n" +
                                   "请确认：1) avatar 上有 NonToon 材质；2) 模块已经登记（菜单 Tools/NonToon 亮度控制/重新登记到模块列表）。\n" +
                                   "如果渲染器上目前还是 lilToon 材质（配合一键切换开关用），请勾上「包含 lilToon 材质槽」。");
                    return false;
                }

                var avatarName = Sanitize(request.AvatarRoot.name);
                var folder = (string.IsNullOrEmpty(request.OutputFolder) ? DefaultOutputFolder : request.OutputFolder)
                    .Replace('\\', '/').TrimEnd('/') + "/" + avatarName;
                PrepareFolder(folder);

                var minClipPath = folder + "/" + avatarName + "_Brightness_Min.anim";
                var maxClipPath = folder + "/" + avatarName + "_Brightness_Max.anim";
                var controllerPath = folder + "/" + avatarName + "_Brightness.controller";
                var menuPath = folder + "/" + avatarName + "_Brightness_Menu.asset";

                var minClip = BuildClip(request.AvatarRoot, targets, min, minClipPath);
                var maxClip = BuildClip(request.AvatarRoot, targets, max, maxClipPath);
                if (minClip == null || maxClip == null) return false;

                var defaultSlider = Mathf.Clamp01(max - min > 1e-6f ? (request.DefaultMultiplier - min) / (max - min) : 0.5f);
                var controller = BuildController(request.ParameterName, minClip, maxClip, defaultSlider, controllerPath);
                if (controller == null) return false;

                var menu = BuildMenu(request.MenuLabel, request.ParameterName, menuPath);
                if (menu == null) return false;

                var container = BuildContainer(request, controller, menu, defaultSlider);
                if (container == null) return false;

                AssetDatabase.SaveAssets();
                if (!Application.isBatchMode) SceneView.RepaintAll();

                log.AppendLine("目标 avatar    : " + request.AvatarRoot.name);
                log.AppendLine("生效渲染器    : " + targets.Count + " 个（材质属性 " + BrightnessProperty +
                               (request.IncludeLilToon ? "，含仍是 lilToon 的材质槽" : "") + "）");
                log.AppendLine("滑块范围      : 参数 " + request.ParameterName + "  0 → 亮度 ×" + min.ToString("0.##") +
                               "，1 → 亮度 ×" + max.ToString("0.##") + "，默认值 " + defaultSlider.ToString("0.###"));
                log.AppendLine("生成资源      : " + folder);
                log.AppendLine("场景物体      : " + request.AvatarRoot.name + "/" + ContainerName +
                               "（MA Merge Animator + Menu Installer + Parameters）");
                log.AppendLine("用法          : 上传后表情菜单里会出现「" + request.MenuLabel + "」滑块，" +
                               "拖动即可统一调整所有 NonToon 材质的亮度；逐材质的亮度上下限仍然生效。");
                Debug.Log("[NonToon 亮度控制] 全局亮度动画已生成。\n" + log);
                Selection.activeGameObject = container;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[NonToon 亮度控制] 生成失败：" + exception);
                return false;
            }
        }

        /// <summary>删掉生成的东西（场景物体 + 资源文件夹）。</summary>
        internal static bool Delete(LightLimitAnimationRequest request)
        {
            var removedSomething = false;
            try
            {
                if (request.AvatarRoot != null)
                {
                    // 全部删掉：用户可能把生成的物体挪到别处，或者不小心复制出了好几份，
                    // 留着多份会让 MA 重复挂菜单 / 重复声明参数，上传会出问题。
                    foreach (var container in FindContainers(request.AvatarRoot.transform))
                    {
                        UnityEngine.Object.DestroyImmediate(container);
                        removedSomething = true;
                    }
                }

                var folder = (string.IsNullOrEmpty(request.OutputFolder) ? DefaultOutputFolder : request.OutputFolder)
                    .Replace('\\', '/').TrimEnd('/');
                var avatarName = request.AvatarRoot != null ? Sanitize(request.AvatarRoot.name) : null;
                var target = avatarName != null && AssetDatabase.IsValidFolder(folder + "/" + avatarName)
                    ? folder + "/" + avatarName
                    : null;
                if (target != null)
                {
                    AssetDatabase.DeleteAsset(target);
                    removedSomething = true;
                }

                if (AssetDatabase.IsValidFolder(folder) && AssetDatabase.GetSubFolders(folder).Length == 0)
                    AssetDatabase.DeleteAsset(folder);

                AssetDatabase.SaveAssets();
                if (!Application.isBatchMode) SceneView.RepaintAll();

                if (removedSomething) Debug.Log("[NonToon 亮度控制] 已删除生成的全局亮度动画与菜单。");
                else Debug.Log("[NonToon 亮度控制] 没有找到生成的东西，无需删除。");
                return removedSomething;
            }
            catch (Exception exception)
            {
                Debug.LogError("[NonToon 亮度控制] 删除失败：" + exception);
                return false;
            }
        }

        // ------------------------------------------------------------------ 生成各部件的实现

        private static AnimationClip BuildClip(GameObject avatarRoot, List<Renderer> targets, float multiplier, string path)
        {
            // 先在内存里把曲线写好再落盘：CreateAsset 之后就修改的话，
            // 可能被排队中的重导入用「还没有曲线」的磁盘版本覆盖掉。
            var clip = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(path), frameRate = 60f };

            foreach (var renderer in targets)
            {
                var relativePath = RelativePath(avatarRoot.transform, renderer.transform);
                if (relativePath == null) continue;

                // 材质属性动画的标准写法：material.<属性名>，类型用渲染器自己的类型
                var binding = EditorCurveBinding.FloatCurve(relativePath, renderer.GetType(), "material." + BrightnessProperty);
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 1f, multiplier));
            }

            AssetDatabase.CreateAsset(clip, path);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController BuildController(string parameterName, AnimationClip minClip, AnimationClip maxClip,
                                                          float defaultSlider, string path)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            if (controller == null) return null;

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = parameterName,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = defaultSlider,
            });

            var tree = new BlendTree
            {
                name = parameterName + "_Blend",
                hideFlags = HideFlags.HideInHierarchy,
                blendType = BlendTreeType.Simple1D,
                blendParameter = parameterName,
                useAutomaticThresholds = false,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.children = new[]
            {
                new ChildMotion { motion = minClip, threshold = 0f, timeScale = 1f, cycleOffset = 0f, position = Vector2.zero },
                new ChildMotion { motion = maxClip, threshold = 1f, timeScale = 1f, cycleOffset = 0f, position = Vector2.zero },
            };

            var machine = new AnimatorStateMachine { name = parameterName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(machine, controller);

            var state = machine.AddState(parameterName);
            state.motion = tree;
            state.writeDefaultValues = true;
            state.hideFlags = HideFlags.HideInHierarchy;
            machine.defaultState = state;

            controller.layers = new[]
            {
                new AnimatorControllerLayer
                {
                    name = parameterName,
                    defaultWeight = 1f,
                    stateMachine = machine,
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    iKPass = false,
                },
            };

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static ScriptableObject BuildMenu(string label, string parameterName, string path)
        {
            var menuType = FindType("VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu");
            if (menuType == null) return null;

            var menu = ScriptableObject.CreateInstance(menuType);
            menu.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(menu, path);

            var controlType = menuType.GetNestedType("Control");
            if (controlType == null)
            {
                Debug.LogError("[NonToon 亮度控制] 找不到 VRCExpressionsMenu.Control，VRChat SDK 版本可能不兼容。");
                return null;
            }

            var control = Activator.CreateInstance(controlType);
            SetMember(control, "name", label);
            SetEnumMember(control, "type", "RadialPuppet");

            var parameterType = controlType.GetNestedType("Parameter");
            if (parameterType != null)
            {
                var parameter = Activator.CreateInstance(parameterType);
                SetMember(parameter, "name", parameterName);
                SetMember(control, "parameter", parameter);
            }
            SetMember(control, "value", 1f);

            var controlsField = menuType.GetField("controls", BindingFlags.Public | BindingFlags.Instance);
            if (controlsField?.GetValue(menu) is IList controls)
            {
                controls.Clear();
                controls.Add(control);
            }

            EditorUtility.SetDirty(menu);
            return menu;
        }

        private static GameObject BuildContainer(LightLimitAnimationRequest request, AnimatorController controller,
                                                 ScriptableObject menu, float defaultSlider)
        {
            var mergeType = FindType("nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator");
            var installerType = FindType("nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller");
            var parametersType = FindType("nadena.dev.modular_avatar.core.ModularAvatarParameters");
            if (mergeType == null || installerType == null || parametersType == null) return null;

            // 之前生成的全都清掉（可能不止一个：被挪走或复制过），再重建一个干净的
            foreach (var existing in FindContainers(request.AvatarRoot.transform))
                UnityEngine.Object.DestroyImmediate(existing);

            var container = new GameObject(ContainerName);
            container.transform.SetParent(request.AvatarRoot.transform, false);

            // 1) 把动画层合并进 FX（路径按 avatar 根目录算）
            var merge = container.AddComponent(mergeType);
            SetMember(merge, "animator", controller);
            SetEnumMember(merge, "layerType", "FX");
            SetEnumMember(merge, "pathMode", "Absolute");
            SetMember(merge, "deleteAttachedAnimator", true);
            SetMember(merge, "matchAvatarWriteDefaults", true);

            // 2) 把菜单挂到表情菜单根上
            var installer = container.AddComponent(installerType);
            SetMember(installer, "menuToAppend", menu);

            // 3) 声明一个同步的 Float 参数
            var parameters = container.AddComponent(parametersType);
            var listField = parametersType.GetField("parameters", BindingFlags.Public | BindingFlags.Instance);
            if (listField?.GetValue(parameters) is IList list)
            {
                var configType = FindType("nadena.dev.modular_avatar.core.ParameterConfig");
                if (configType != null)
                {
                    var config = Activator.CreateInstance(configType);
                    SetMember(config, "nameOrPrefix", request.ParameterName);
                    SetEnumMember(config, "syncType", "Float");
                    SetMember(config, "defaultValue", defaultSlider);
                    SetMember(config, "saved", true);
                    SetMember(config, "hasExplicitDefaultValue", true);
                    list.Clear();
                    list.Add(config);
                }
            }

            EditorUtility.SetDirty(container);
            if (!Application.isBatchMode && container.scene.IsValid()) EditorSceneManager.MarkSceneDirty(container.scene);
            return container;
        }

        // ------------------------------------------------------------------ 小工具

        private static bool HasBrightnessProperty(Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            if (materials == null) return false;
            foreach (var material in materials)
            {
                if (material == null) continue;
                if (material.HasProperty(BrightnessProperty)) return true;
            }
            return false;
        }

        /// <summary>这个渲染器要不要写动画：有模块属性，或者（勾选了的话）还有 lilToon 材质。</summary>
        private static bool IsTarget(Renderer renderer, bool includeLilToon)
        {
            if (HasBrightnessProperty(renderer)) return true;
            if (!includeLilToon) return false;

            var materials = renderer.sharedMaterials;
            if (materials == null) return false;
            foreach (var material in materials)
            {
                if (material == null || material.shader == null) continue;
                if (material.shader.name.IndexOf("liltoon", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static bool IsInsideContainer(Transform transform, Transform avatarRoot)
        {
            for (var current = transform; current != null && current != avatarRoot; current = current.parent)
            {
                if (current.name == ContainerName) return true;
            }
            return false;
        }

        private static GameObject FindContainer(Transform avatarRoot)
        {
            return FindContainers(avatarRoot).FirstOrDefault();
        }

        /// <summary>
        /// avatar 下所有叫 _NonToonLightLimit 的物体（递归找，而且返回全部）。
        /// 生成时会把它们都清掉再重建：重复的 Merge Animator / Menu Installer / Parameters
        /// 会让 Modular Avatar 重复挂菜单、重复声明参数，上传阶段会直接报错。
        /// </summary>
        private static List<GameObject> FindContainers(Transform avatarRoot)
        {
            var result = new List<GameObject>();
            if (avatarRoot == null) return result;
            foreach (var transform in avatarRoot.GetComponentsInChildren<Transform>(true))
            {
                if (transform != avatarRoot && transform.name == ContainerName) result.Add(transform.gameObject);
            }
            return result;
        }

        private static string RelativePath(Transform root, Transform target)
        {
            if (target == root) return "";
            var parts = new List<string>();
            var current = target;
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            if (current != root) return null;
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static void PrepareFolder(string folder)
        {
            // 先整个删掉再重建：同一个路径上重复创建资源，AssetDatabase 容易残留旧对象。
            // 这里不调用 Refresh —— 排队中的重导入会在新资源刚建好时用空文件覆盖它。
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);

            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Avatar";
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(name.Length);
            foreach (var c in name) builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return builder.ToString().Trim();
        }

        private static void SetMember(object target, string name, object value)
        {
            if (target == null) return;
            var type = target.GetType();
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && value != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(target, value);
                return;
            }
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null && property.CanWrite && value != null && property.PropertyType.IsInstanceOfType(value))
                property.SetValue(target, value, null);
        }

        private static void SetEnumMember(object target, string name, string enumValue)
        {
            if (target == null) return;
            var field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null || !field.FieldType.IsEnum) return;
            try { field.SetValue(target, Enum.Parse(field.FieldType, enumValue)); }
            catch (Exception exception)
            {
                Debug.LogWarning("[NonToon 亮度控制] 设置 " + name + " = " + enumValue + " 失败：" + exception.Message);
            }
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
