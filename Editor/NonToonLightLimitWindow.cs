// 「生成全局亮度动画 + 菜单」小工具的窗口。
//
// 只做一件事：给 avatar 下所有 NonToon 材质写一套亮度动画，并生成一个表情菜单滑块，
// 让一根滑块控制全部材质的亮度。逐材质的亮度上下限不受影响。

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AtriNaxu.NonToonLightLimit
{
    internal sealed class NonToonLightLimitWindow : EditorWindow
    {
        private const string Prefix = "AtriNaxu.NonToonLightLimit.Anim.";
        private const string MenuPath = "Tools/NonToon 亮度控制/生成全局亮度动画 + 菜单";

        private GameObject avatar;
        private string parameterName;
        private string menuLabel;
        private float minMultiplier;
        private float maxMultiplier;
        private float defaultMultiplier;
        private string outputFolder;
        private bool includeLilToon;
        private Vector2 scroll;

        private int targetCount;
        private string dependencyError;

        [MenuItem(MenuPath, false, 1)]
        private static void Open()
        {
            var window = GetWindow<NonToonLightLimitWindow>(true, "NonToon 全局亮度动画", true);
            window.minSize = new Vector2(420f, 340f);
            window.Show();
        }

        private void OnEnable()
        {
            parameterName = EditorPrefs.GetString(Prefix + "ParameterName", "NonToonBrightness");
            menuLabel = EditorPrefs.GetString(Prefix + "MenuLabel", "亮度");
            minMultiplier = EditorPrefs.GetFloat(Prefix + "Min", 0.25f);
            maxMultiplier = EditorPrefs.GetFloat(Prefix + "Max", 1.75f);
            defaultMultiplier = EditorPrefs.GetFloat(Prefix + "Default", 1f);
            outputFolder = EditorPrefs.GetString(Prefix + "OutputFolder", NonToonLightLimitAnimator.DefaultOutputFolder);
            includeLilToon = EditorPrefs.GetBool(Prefix + "IncludeLilToon", false);

            if (NonToonLightLimitAnimator.CheckDependencies(out var message)) dependencyError = null;
            else dependencyError = message;

            var selected = Selection.activeGameObject;
            avatar = NonToonLightLimitAnimator.FindAvatar(selected);
            RefreshCount();
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(Prefix + "ParameterName", parameterName);
            EditorPrefs.SetString(Prefix + "MenuLabel", menuLabel);
            EditorPrefs.SetFloat(Prefix + "Min", minMultiplier);
            EditorPrefs.SetFloat(Prefix + "Max", maxMultiplier);
            EditorPrefs.SetFloat(Prefix + "Default", defaultMultiplier);
            EditorPrefs.SetString(Prefix + "OutputFolder", outputFolder);
            EditorPrefs.SetBool(Prefix + "IncludeLilToon", includeLilToon);
        }

        private void RefreshCount()
        {
            targetCount = NonToonLightLimitAnimator.CollectTargets(avatar, includeLilToon).Count;
            Repaint();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("把 avatar 下所有 NonToon 材质的亮度接到一根菜单滑块上。", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (!string.IsNullOrEmpty(dependencyError))
            {
                EditorGUILayout.HelpBox(dependencyError + "\n这个工具需要 VRChat SDK3 Avatars 与 Modular Avatar。", MessageType.Error);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUI.BeginChangeCheck();
            avatar = (GameObject)EditorGUILayout.ObjectField("目标 Avatar", avatar, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck()) RefreshCount();

            if (avatar == null)
            {
                EditorGUILayout.HelpBox("选中 avatar 本体（带 VRC Avatar Descriptor 的那个物体）再打开这个窗口。", MessageType.Warning);
            }

            EditorGUILayout.Space();
            parameterName = EditorGUILayout.TextField("参数名", parameterName);
            menuLabel = EditorGUILayout.TextField("菜单名", menuLabel);

            EditorGUILayout.Space();
            minMultiplier = EditorGUILayout.FloatField("最暗倍数（滑块 0）", minMultiplier);
            maxMultiplier = EditorGUILayout.FloatField("最亮倍数（滑块 1）", maxMultiplier);
            defaultMultiplier = EditorGUILayout.FloatField("默认倍数", defaultMultiplier);
            if (minMultiplier <= 0f && maxMultiplier <= 0f)
                EditorGUILayout.HelpBox("两个倍数都是 0 的话，拖滑块只会让画面全黑。", MessageType.Warning);

            EditorGUILayout.Space();
            outputFolder = EditorGUILayout.TextField("输出文件夹", outputFolder);

            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            includeLilToon = EditorGUILayout.ToggleLeft("包含 lilToon 材质槽（配合 shader 一键切换开关时勾上）", includeLilToon);
            if (EditorGUI.EndChangeCheck()) RefreshCount();

            EditorGUILayout.Space();
            var hasAvatar = avatar != null;
            var countText = hasAvatar
                ? "会写动画的渲染器：" + targetCount + " 个"
                : "会写动画的渲染器：—";
            EditorGUILayout.LabelField(countText, EditorStyles.boldLabel);

            if (hasAvatar && targetCount == 0)
            {
                EditorGUILayout.HelpBox("这个 avatar 下没有找到带亮度模块属性的材质。\n" +
                                        "先确认材质是 NonToon，并且模块已经登记（菜单 Tools/NonToon 亮度控制/重新登记到模块列表）。",
                                        MessageType.Warning);
            }

            // 同步参数预算：滑块本身要占一个 8 bit 的同步 Float 参数
            if (hasAvatar)
            {
                var budget = NonToonLightLimitAnimator.DescribeParameterBudget(avatar, out _, out var syncedCount, out var remainingBits);
                if (budget != null)
                {
                    EditorGUILayout.LabelField("参数预算：" + budget, EditorStyles.miniLabel);
                    if (remainingBits < 8)
                    {
                        EditorGUILayout.HelpBox(
                            "同步参数预算不够了：这个滑块要再加一个 8 bit 的同步 Float 参数。\n" +
                            "VRChat 上限是 3200 bit / 256 个同步参数，超了上传会失败（有的 SDK 版本会直接报 " +
                            "Index was outside the bounds of the array）。\n" +
                            "可以先删掉没用的同步参数（Modular Avatar 的 Show Modular Avatar Information 窗口里有明细），" +
                            "或者不用滑块，只逐材质调 Brightness。",
                            MessageType.Warning);
                    }
                    else if (syncedCount >= 250)
                    {
                        EditorGUILayout.HelpBox("同步参数个数已经 " + syncedCount + " 个（上限 256），再加一个可能会超。",
                                                MessageType.Warning);
                    }
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!hasAvatar || targetCount == 0))
                {
                    if (GUILayout.Button("生成 / 更新", GUILayout.Height(28f)))
                    {
                        SavePrefs();
                        NonToonLightLimitAnimator.Generate(BuildRequest());
                        RefreshCount();
                    }
                }

                using (new EditorGUI.DisabledScope(!hasAvatar))
                {
                    if (GUILayout.Button("删除已生成", GUILayout.Height(28f)))
                    {
                        if (EditorUtility.DisplayDialog("NonToon 全局亮度动画",
                                "删除 " + avatar.name + " 下生成的 " + NonToonLightLimitAnimator.ContainerName +
                                " 物体，以及输出文件夹里对应的资源？\n（不会动材质本身）", "删除", "取消"))
                        {
                            NonToonLightLimitAnimator.Delete(BuildRequest());
                            RefreshCount();
                        }
                    }
                }

                if (GUILayout.Button("重新检测", GUILayout.Height(28f), GUILayout.Width(80f))) RefreshCount();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "生成内容：\n" +
                "· " + NonToonLightLimitAnimator.ContainerName + " 物体（MA Merge Animator 把动画层合并进 FX；MA Menu Installer 挂菜单；MA Parameters 声明参数）\n" +
                "· 两条动画（最暗 / 最亮）覆盖上面所有渲染器的 material." + NonToonLightLimitAnimator.BrightnessProperty + "\n" +
                "· 一个单层 AnimatorController（1D 混合树，参数 0 = 最暗，1 = 最亮）\n" +
                "· 一个表情菜单资源（径向滑块）\n\n" +
                "注意：滑块会统一写出亮度倍数，所以材质的 Brightness 数值在拖动范围内由滑块决定（默认位置 = 1.0，即不改变）；" +
                "逐材质的亮度上下限、遮罩范围照旧生效。",
                MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        private LightLimitAnimationRequest BuildRequest()
        {
            return new LightLimitAnimationRequest
            {
                AvatarRoot = avatar,
                ParameterName = parameterName,
                MenuLabel = menuLabel,
                MinMultiplier = minMultiplier,
                MaxMultiplier = maxMultiplier,
                DefaultMultiplier = defaultMultiplier,
                OutputFolder = outputFolder,
                IncludeLilToon = includeLilToon,
            };
        }

        /// <summary>给批处理 / 其它脚本用的入口：直接按默认参数生成。</summary>
        internal static void GenerateForSelection()
        {
            var root = NonToonLightLimitAnimator.FindAvatar(Selection.activeGameObject);
            if (root == null)
            {
                Debug.LogError("[NonToon 亮度控制] 请先选中 avatar 本体。");
                return;
            }

            NonToonLightLimitAnimator.Generate(new LightLimitAnimationRequest
            {
                AvatarRoot = root,
                ParameterName = EditorPrefs.GetString(Prefix + "ParameterName", "NonToonBrightness"),
                MenuLabel = EditorPrefs.GetString(Prefix + "MenuLabel", "亮度"),
                MinMultiplier = EditorPrefs.GetFloat(Prefix + "Min", 0.25f),
                MaxMultiplier = EditorPrefs.GetFloat(Prefix + "Max", 1.75f),
                DefaultMultiplier = EditorPrefs.GetFloat(Prefix + "Default", 1f),
                OutputFolder = EditorPrefs.GetString(Prefix + "OutputFolder", NonToonLightLimitAnimator.DefaultOutputFolder),
                IncludeLilToon = EditorPrefs.GetBool(Prefix + "IncludeLilToon", false),
            });
        }
    }
}
