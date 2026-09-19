// 与「NonToon Modules」模块包之间的桥。
//
// 从这一版起，亮度模块（LightLimit）**不再放在本插件里**，而是放在独立的模块包 com.nontoon.modules
// （本插件通过 VPM 依赖「订阅」它）。这样模块可以单独安装使用（Tools/NonToon 模块），插件里也不再包含
// 任何 Shader Core 模块文件。
//
// 这里用反射调用模块包的公共 API（NonToonModules.NonToonModuleRegistry），所以：
//   · 模块包没装时本插件仍能编译；
//   · 找不到模块包时会回退到插件自带的老实现（见 LightLimitModuleInstaller.Register）。

using System;
using System.Reflection;
using UnityEngine;

namespace AtriNaxu.NonToonLightLimit
{
    internal static class NonToonModulesBridge
    {
        private const string RegistryTypeName = "NonToonModules.NonToonModuleRegistry";

        /// <summary>模块包里的 Fabric 模块 id 与 LightLimit 模块 id（供插件自动勾选用）。</summary>
        public const string LightLimitModuleId = "com.atrinaxu.nontoon.lightlimit";
        public const string ModulesPackageName = "com.nontoon.modules";

        private static Type RegistryType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(RegistryTypeName, false);
                if (type != null) return type;
            }
            return null;
        }

        /// <summary>模块包装了没有。</summary>
        public static bool Present() => RegistryType() != null;

        /// <summary>模块有没有被勾选。</summary>
        public static bool IsEnabled()
        {
            var type = RegistryType();
            var method = type?.GetMethod("IsEnabled", BindingFlags.Public | BindingFlags.Static);
            if (method == null) return false;
            try { return (bool)method.Invoke(null, new object[] { LightLimitModuleId }); }
            catch (Exception) { return false; }
        }

        /// <summary>确保亮度模块被勾选（幂等）。模块包不在时返回 false，让调用方回退。</summary>
        public static bool TryEnsureEnabled(out bool succeeded)
        {
            succeeded = false;
            var type = RegistryType();
            var method = type?.GetMethod("EnsureEnabled", BindingFlags.Public | BindingFlags.Static);
            if (method == null) return false;
            try
            {
                succeeded = (bool)method.Invoke(null, new object[] { LightLimitModuleId, "亮度上下限模块" });
            }
            catch (Exception exception)
            {
                var inner = exception.InnerException ?? exception;
                Debug.LogWarning("[NonToon 亮度控制] 勾选亮度模块时出错：" + inner.Message);
            }
            return true;
        }

        /// <summary>取消勾选（卸载用）。</summary>
        public static bool TrySetEnabled(bool enabled, out bool succeeded)
        {
            succeeded = false;
            var type = RegistryType();
            var method = type?.GetMethod("SetEnabled", BindingFlags.Public | BindingFlags.Static);
            if (method == null) return false;
            try
            {
                succeeded = (bool)method.Invoke(null, new object[] { LightLimitModuleId, enabled, true });
            }
            catch (Exception exception)
            {
                var inner = exception.InnerException ?? exception;
                Debug.LogWarning("[NonToon 亮度控制] 设置亮度模块状态时出错：" + inner.Message);
            }
            return true;
        }
    }
}
