// NonToon 亮度控制（亮度上下限 + 亮度倍数 + 全局控制）
//
// 挂载点：Shader Core 的 modifylight 阶段，并声明在 NonToon 自带的 Lighten 之后执行
// （.scmodule 里 afters: ["Lighten"]），所以 _LightBoost 先算完，这里再做限制，
// 两者可以叠加。
//
// 为什么需要这个模块：
//   NonToon 自带的 Light Boost 只能提亮，而且 urp.hlsl / birp.hlsl 里
//   sd.lightColor = min(env + lightSum, 1)，上限被硬性压在 1，想压暗或者限制上限
//   只能在这一阶段直接改 sd.lightColor。
//
// 逐材质参数（自动带 _com_atrinaxu_nontoon_lightlimit_ 前缀）：
//   _Min / _Max            亮度上下限
//   _Brightness            亮度倍数
//   _Global                是否接受全局倍数
//   _GlobalMaskChannel     生效范围用共享遮罩的哪个通道
//
// 全局参数（脚本 / 编辑器 / 世界用 Shader.SetGlobalFloat 设置，材质不需要带这些属性）：
//   _NonToonLightLimit_Global    全局亮度倍数，默认 1（不设置就等于关闭）
//   _NonToonLightLimit_Envelope  全局包络 0..1，默认 0；> 0 时改用「0 = 压到下限、1 = 放开到上限」

// Shader Core 不会替模块声明全局变量，所以这里自己声明
float _NonToonLightLimit_Global = 1.0;
float _NonToonLightLimit_Envelope = 0.0;

static const half3 LL_LUMA = half3(0.2126, 0.7152, 0.0722);

// 按亮度归一到目标区间再乘回去：暗部被抬高、高光被压住，
// 但颜色之间的明暗比例保持不变（不会把阴影拍平成一坨）。
half3 LLApplyLimit(half3 color, half lo, half hi)
{
    half luma = max(dot(color, LL_LUMA), 1e-5);
    return color * (clamp(luma, lo, hi) / luma);
}

{
    // 共享遮罩里用来限制生效范围的通道；没设遮罩时该通道为 1，即整体生效
    half llMask = saturate(sd.mask[_GlobalMaskChannel]);

    if (llMask > 0)
    {
        half llLo = min(_Min, _Max);
        half llHi = max(_Min, _Max);

        // 亮度倍数：本材质倍数始终生效，开了全局控制再乘全局倍数
        half llMul = max(_Brightness, 0.0);
        if (_Global) llMul *= max(_NonToonLightLimit_Global, 0.0);

        half3 llColor = sd.lightColor * llMul;

        // 全局包络：用 0..1 一个值同时驱动所有开了全局控制的材质
        if (_Global && _NonToonLightLimit_Envelope > 0)
        {
            half llTarget = max(lerp(llLo, llHi, saturate(_NonToonLightLimit_Envelope)), 1e-5);
            llColor = LLApplyLimit(llColor, 0.0, llTarget);
        }

        // 逐材质的上下限始终生效
        llColor = LLApplyLimit(llColor, llLo, llHi);

        sd.lightColor = lerp(sd.lightColor, llColor, llMask);
    }
}
