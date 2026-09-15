// NonToon 亮度控制（亮度上下限 + 亮度倍数 + 遮罩范围）
//
// 挂载点：Shader Core 的 modifylight 阶段，并声明在 NonToon 自带的 Lighten 之后执行
// （.scmodule 里 afters: ["Lighten"]），所以 _LightBoost 先算完，这里再做限制，
// 两者可以叠加。
//
// 关于写法（很重要）：Shader Core 的 phase 代码是**原样插进函数体里**的
// （NonToon 的 urp.hlsl:127 / birp.hlsl:129 都在片段着色函数内部），所以这里只能写
// 「语句 + 局部变量」：
//   ✗ 不能声明全局变量（所以没法用 Shader.SetGlobalFloat 做全局控制）
//   ✗ 不能定义函数
//   ✗ 不能用 static const
// 需要额外变量就在块里定义局部变量（下面 ll 前缀的都是）。
//
// 为什么上限要在这里改：NonToon 自带 Light Boost 只能提亮，而且
// urp.hlsl / birp.hlsl 里 sd.lightColor = min(env + lightSum, 1) 把上限压死在 1，
// 想压暗或限制上限，只能在这一阶段直接改 sd.lightColor。
//
// 逐材质参数（自动带 _com_atrinaxu_nontoon_lightlimit_ 前缀）：
//   _Min / _Max            亮度上下限
//   _Brightness            亮度倍数（动画 / 菜单滑块改的就是它）
//   _UseSharedMask         是否用共享遮罩限制生效范围（默认关闭 = 整块材质都生效）
//   _GlobalMaskChannel     生效范围用共享遮罩的哪个通道

{
    // 生效范围：默认不限制（整块材质都生效）。
    // 之前这里是「没勾选就直接读 A 通道」，结果转换插件把别的遮罩（描边 / 高光 / 材质捕获…）
    // 烘焙进 A 通道以后，那些材质会莫名其妙地「亮度调不动」——所以改成显式开关。
    half llMask = _UseSharedMask != 0 ? saturate(sd.mask[_GlobalMaskChannel]) : 1.0;

    if (llMask > 0)
    {
        half llLo = min(_Min, _Max);
        half llHi = max(_Min, _Max);

        // 亮度倍数
        half3 llColor = sd.lightColor * max(_Brightness, 0.0);

        // 按亮度归一到 [下限, 上限] 再乘回去：暗部抬高、高光压住，
        // 颜色之间的明暗比例保持不变（不会把阴影拍平）
        half3 llLumaWeight = half3(0.2126, 0.7152, 0.0722);
        half llLuma = max(dot(llColor, llLumaWeight), 1e-5);
        llColor *= clamp(llLuma, llLo, llHi) / llLuma;

        sd.lightColor = lerp(sd.lightColor, llColor, llMask);
    }
}
