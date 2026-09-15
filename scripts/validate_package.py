#!/usr/bin/env python3
"""校验 Unity 包结构与版本号。

用法：
    python scripts/validate_package.py               # 只校验结构
    python scripts/validate_package.py --tag v1.0.0  # 同时校验标签与版本一致
    python scripts/validate_package.py --github-output  # 额外输出 version / zipname 到 GITHUB_OUTPUT

被 .github/workflows/validate.yml 与 release.yml 共用，本地也可以直接跑来验证。
"""

from __future__ import annotations

import argparse
import json
import os
import pathlib
import re
import sys

REQUIRED_FIELDS = ("name", "version", "displayName", "unity", "license")
REQUIRED_PATHS = ("README.md", "LICENSE", "CHANGELOG.md", "package.json", "Editor", "Shaders")

# .scmodule 里的 uniqueID 必须和安装器里的常量一致，否则登记进去的模块名对不上
MODULE_FILE = "Shaders/Modules/LightLimit/com.atrinaxu.nontoon.lightlimit.scmodule"
INSTALLER_FILE = "Editor/LightLimitModuleInstaller.cs"


def fail(message: str) -> None:
    print(f"[失败] {message}", file=sys.stderr)
    sys.exit(1)


def check_meta(root: pathlib.Path, paths: list[pathlib.Path], what: str) -> None:
    missing = [p.name for p in paths if not p.with_name(p.name + ".meta").exists()]
    if missing:
        fail(f"以下{what}缺少 .meta：" + ", ".join(missing))


def strip_comments(code: str) -> str:
    code = re.sub(r"/\*.*?\*/", "", code, flags=re.S)
    return re.sub(r"//[^\n]*", "", code)


# phase 代码会被 Shader Core 原样插进片段着色函数体里，所以只能是一段语句块：
# 不能声明全局变量、不能定义函数、不能用 static（1.1.0 就是因为这个把 NonToon 编译搞挂了）。
FUNCTION_PATTERN = re.compile(
    r"^\s*(?:static\s+)?(?:inline\s+)?(?:half|float|double|void|int|uint|bool"
    r"|half[234]|float[234]|half[234]x[234]|float[234]x[234])\s+\w+\s*\(",
    re.M,
)


def check_phase_file(path: pathlib.Path, root: pathlib.Path) -> None:
    relative = path.relative_to(root).as_posix()
    code = strip_comments(path.read_text(encoding="utf-8"))

    lines = [line.strip() for line in code.splitlines() if line.strip()]
    if not lines:
        fail(f"{relative} 是空的")

    depth = 0
    blocks = 0
    for number, line in enumerate(lines, start=1):
        if depth == 0:
            if line == "{":
                blocks += 1
                if blocks > 1:
                    fail(f"{relative} 顶层只能有一个 {{ }} 块，第 {number} 行还有别的内容：{line}")
            elif not line.startswith("{"):
                fail(f"{relative} 第 {number} 行不在 {{ }} 块里：{line}\n"
                     f"（phase 代码会被插进函数体，只能写语句和局部变量）")
        depth += line.count("{") - line.count("}")
        if depth < 0:
            fail(f"{relative} 第 {number} 行花括号数量不对：{line}")

    if depth != 0:
        fail(f"{relative} 花括号不匹配")
    if blocks == 0:
        fail(f"{relative} 里没有 {{ }} 语句块")

    if re.search(r"\bstatic\b", code):
        fail(f"{relative} 不能出现 static（phase 代码在函数体里）")
    if FUNCTION_PATTERN.search(code):
        fail(f"{relative} 不能定义函数（phase 代码在函数体里，需要计算就写局部变量）")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".", help="包根目录")
    parser.add_argument("--tag", default="", help="要校验的标签，例如 v1.0.0")
    parser.add_argument("--github-output", action="store_true", help="把结果写入 GITHUB_OUTPUT")
    args = parser.parse_args()

    root = pathlib.Path(args.root).resolve()
    manifest = root / "package.json"
    if not manifest.exists():
        fail(f"找不到 {manifest}")

    try:
        # utf-8-sig 兼容带 BOM 的 JSON（例如用 PowerShell 写出来的文件）
        data = json.loads(manifest.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as exc:
        fail(f"package.json 不是合法 JSON: {exc}")

    for field in REQUIRED_FIELDS:
        if not data.get(field):
            fail(f"package.json 缺少字段 {field}")

    for path in REQUIRED_PATHS:
        if not (root / path).exists():
            fail(f"缺少 {path}")

    version = data["version"]
    name = data["name"]

    sources = sorted((root / "Editor").glob("*.cs"))
    if not sources:
        fail("Editor/ 下没有 .cs 文件")

    # 每个源码 / 着色器文件都要有对应的 .meta，否则编辑器导入时 GUID 会变
    check_meta(root, sources, "脚本")
    check_meta(root, sorted(root.glob("Shaders/**/*.hlsl")), "着色器")
    check_meta(root, sorted(root.glob("Shaders/**/*.scmodule")), "模块定义")

    skip_dirs = {".git", ".github", "scripts", "__pycache__"}
    directories = sorted(
        path for path in root.rglob("*")
        if path.is_dir()
        and not any(part in skip_dirs or part.startswith(".") for part in path.relative_to(root).parts)
    )
    check_meta(root, directories, "目录")

    for path in sources:
        text = path.read_text(encoding="utf-8")
        if text.count("{") != text.count("}"):
            fail(f"{path.name} 花括号不匹配（{{={text.count('{')} }}={text.count('}')}）")

    # phase 文件结构检查（插进函数体，只能写语句块）
    phase_files = sorted(root.glob("Shaders/Modules/*/phase_*.hlsl"))
    if not phase_files:
        fail("Shaders/Modules/*/ 下没有 phase_*.hlsl")
    for path in phase_files:
        check_phase_file(path, root)

    # .scmodule 必须是合法 JSON，且 uniqueID 与 C# 常量一致
    module_path = root / MODULE_FILE
    if not module_path.exists():
        fail(f"找不到模块定义 {MODULE_FILE}")
    try:
        module = json.loads(module_path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as exc:
        fail(f"{MODULE_FILE} 不是合法 JSON: {exc}")

    unique_id = module.get("uniqueID", "")
    if not unique_id:
        fail(f"{MODULE_FILE} 缺少 uniqueID")

    installer = (root / INSTALLER_FILE).read_text(encoding="utf-8")
    match = re.search(r'ModuleId\s*=\s*"([^"]+)"', installer)
    if not match:
        fail(f"{INSTALLER_FILE} 里找不到 ModuleId 常量")
    if match.group(1) != unique_id:
        fail(f"ModuleId（{match.group(1)}）与 .scmodule 的 uniqueID（{unique_id}）不一致")

    # 相位必须挂在 Shader Core 的 modifylight 上，否则改不到 sd.lightColor
    phases = module.get("phases") or []
    if not any(p.get("phase") == "modifylight" for p in phases):
        fail(f"{MODULE_FILE} 没有挂在 modifylight 阶段")

    if not data.get("vpmDependencies", {}).get("jp.lilxyzw.shadercore"):
        fail("package.json 缺少 jp.lilxyzw.shadercore 依赖")
    if not data.get("vpmDependencies", {}).get("jp.lilxyzw.nontoon"):
        fail("package.json 缺少 jp.lilxyzw.nontoon 依赖")

    tag = args.tag
    if tag.startswith("v") and tag[1:] != version:
        fail(f"标签 {tag} 与 package.json 的版本 {version} 不一致")

    zip_name = f"{name}-{version}.zip"
    print(f"[通过] {name} {version}（Unity {data['unity']}，{data['license']}），"
          f"模块 {unique_id}，源码 {len(sources)} 个，标签 {tag or '未校验'}，压缩包名 {zip_name}")

    if args.github_output:
        output_path = os.environ.get("GITHUB_OUTPUT")
        if output_path:
            with open(output_path, "a", encoding="utf-8") as handle:
                handle.write(f"version={version}\n")
                handle.write(f"zipname={zip_name}\n")

    return 0


if __name__ == "__main__":
    sys.exit(main())
