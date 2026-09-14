SC_Box
SC_float(_Min, 0.0, [SCRange(0,1)], "Min Brightness", "亮度下限：环境再暗也不会低于这个亮度（0 = 不限制）")
SC_float(_Max, 1.0, [SCRange(0,2)], "Max Brightness", "亮度上限：环境再亮也不会高于这个亮度（1 = NonToon 原本的上限）")
SC_BoxEnd

SC_float(_Brightness, 1.0, [SCRange(0,4)], "Brightness", "本材质的亮度倍数，1 = 不改变；想整体调亮调暗就改这里")
SC_uint(_Global, 0, [SCToggle], "Use Global Control", "开启后额外乘上全局倍数 _NonToonLightLimit_Global")
SC_uint(_GlobalMaskChannel, 3, [SCMaskChannel], "__MaskChannel", "共享遮罩里控制生效范围的通道（默认 A，没有遮罩时全生效）")
