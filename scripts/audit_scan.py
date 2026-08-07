#!/usr/bin/env python3
"""Mechanical scanner for the SymphonyFramework audit.

Collects the machine-checkable half of the audit perspectives defined in
`.agents/skills/audit/references/perspectives.md` and emits them as JSON or as
Markdown appendices (`file:line` enumerations) ready to be embedded into
`Documentation/Audit/`.

This script only reports locations. It never judges severity: assigning
"確定 / 要検証 / 設計指摘" requires reading the code and is the auditor's job.
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from dataclasses import dataclass, asdict
from pathlib import Path


WORKSPACE_ROOT = Path(__file__).resolve().parents[1]
PACKAGE_ROOT = WORKSPACE_ROOT / "Assets" / "SymphonyFrameWork"

# Directories that ship to users. Rules are strictest here.
SHIPPED_RUNTIME_DIRS = ("Runtime", "Core")
# Editor code also ships, but Debug.Log / LINQ / reflection are acceptable there.
SHIPPED_DIRS = ("Runtime", "Core", "Editor")
# Not shipped as package code, scanned only where explicitly noted.
AUXILIARY_DIRS = ("Tests", "Samples")

LONG_FILE_THRESHOLD = 300
LONG_METHOD_THRESHOLD = 60

# Numbers that carry no domain meaning and would only add noise.
BENIGN_NUMBERS = {"0", "1", "2", "-1", "0f", "1f", "0.5f", "100", "1000"}


@dataclass(frozen=True)
class Finding:
    category: str
    path: str
    line: int
    detail: str


@dataclass
class SourceFile:
    path: Path
    rel: str
    text: str
    lines: list[str]
    has_bom: bool

    @property
    def top_dir(self) -> str:
        return Path(self.rel).parts[3] if len(Path(self.rel).parts) > 3 else ""


def load_sources(dirs: tuple[str, ...]) -> list[SourceFile]:
    sources: list[SourceFile] = []
    for name in dirs:
        root = PACKAGE_ROOT / name
        if not root.is_dir():
            continue
        for path in sorted(root.rglob("*.cs")):
            raw = path.read_bytes()
            has_bom = raw.startswith(b"\xef\xbb\xbf")
            text = raw.decode("utf-8-sig", errors="replace")
            rel = path.relative_to(WORKSPACE_ROOT).as_posix()
            sources.append(
                SourceFile(path=path, rel=rel, text=text, lines=text.splitlines(), has_bom=has_bom)
            )
    return sources


def in_dirs(source: SourceFile, dirs: tuple[str, ...]) -> bool:
    prefix = "Assets/SymphonyFrameWork/"
    if not source.rel.startswith(prefix):
        return False
    return source.rel[len(prefix):].split("/", 1)[0] in dirs


def strip_line_comment(line: str) -> str:
    """Remove a trailing // comment, ignoring // inside string literals."""
    in_string = False
    quote = ""
    index = 0
    while index < len(line) - 1:
        char = line[index]
        if in_string:
            if char == "\\":
                index += 2
                continue
            if char == quote:
                in_string = False
        elif char in ('"', "'"):
            in_string = True
            quote = char
        elif char == "/" and line[index + 1] == "/":
            return line[:index]
        index += 1
    return line


# ---------------------------------------------------------------------------
# 01 イベント購読解除漏れ
# ---------------------------------------------------------------------------

# デリゲート購読は「識別子またはメンバーアクセスだけを足して文終わり」の形を取る。
# `timer += Time.deltaTime;` のような算術との区別は構文だけでは付かないため、
# 数値であることが明らかな右辺を除外する。
DELEGATE_RHS_RE = re.compile(r"^(?:[A-Za-z_][\w.]*|this\.[\w.]+|new\s+\w+[\w<>,\s]*\([^;]*\))$")
NUMERIC_RHS_RE = re.compile(
    r"^(?:Time|Mathf|Screen|Physics)\.\w+$|\.(?:Length|Count|deltaTime|time|magnitude)$"
)
LAMBDA_SUBSCRIBE_RE = re.compile(r"=\s*(?:\([^)]*\)|[A-Za-z_]\w*)\s*=>|=\s*(?:async\s+)?delegate\b")


def classify_delegate_assignment(code: str, operator: str) -> str | None:
    """Classify `+=` / `-=` as a delegate subscription.

    Returns "method" for a method-group subscription, "lambda" for an inline
    lambda, or None when the statement is arithmetic rather than a subscription.
    """
    index = code.find(operator)
    if index < 0:
        return None
    # `++` `--` `>=` `<=` `!=` `==` の一部を拾わないようにする。
    if index > 0 and code[index - 1] in "+-=!<>":
        return None
    right = code[index + 2 :].strip()
    if not right:
        return None
    if LAMBDA_SUBSCRIBE_RE.search(code[index:]):
        return "lambda"
    if not right.endswith(";"):
        return None
    right = right[:-1].strip()
    if not DELEGATE_RHS_RE.match(right) or NUMERIC_RHS_RE.search(right):
        return None
    return "method"


def check_event_unsubscribe(sources: list[SourceFile]) -> list[Finding]:
    findings: list[Finding] = []
    for source in sources:
        subscribes: list[tuple[int, str]] = []
        unsubscribes: list[int] = []
        for number, line in enumerate(source.lines, start=1):
            code = strip_line_comment(line)
            kind = classify_delegate_assignment(code, "+=")
            if kind:
                subscribes.append((number, code.strip()[:80]))
                if kind == "lambda":
                    findings.append(
                        Finding(
                            "01_lambda_subscribe",
                            source.rel,
                            number,
                            "ラムダ購読のため -= が書けない可能性: " + code.strip()[:80],
                        )
                    )
            if classify_delegate_assignment(code, "-="):
                unsubscribes.append(number)
        if subscribes and len(unsubscribes) < len(subscribes):
            findings.append(
                Finding(
                    "01_subscribe_imbalance",
                    source.rel,
                    subscribes[0][0],
                    f"+= {len(subscribes)}件 / -= {len(unsubscribes)}件 — "
                    + "; ".join(text for _, text in subscribes[:3]),
                )
            )
    return findings


# ---------------------------------------------------------------------------
# 03 static 状態と ResetRuntimeState
# ---------------------------------------------------------------------------

STATIC_DECL_RE = re.compile(
    r"^\s*(?:public|internal|private|protected)?\s*static\s+(?P<rest>.+)$"
)
RESET_DECL_RE = re.compile(r"\bvoid\s+ResetRuntimeState\s*\(")


def is_static_mutable_declaration(code: str) -> bool:
    """True for static fields / auto-properties / events that hold mutable state.

    Methods, operators and constants are excluded: they are not state, and
    matching them buried the real findings under `operator ==` noise.
    """
    match = STATIC_DECL_RE.match(code)
    if not match:
        return False
    rest = match.group("rest").strip()
    if any(
        keyword in rest
        for keyword in ("const ", "readonly ", "operator", "class ", "struct ",
                        "interface ", "enum ", "void ", "delegate ")
    ):
        return False
    if rest.startswith("event "):
        return True
    if "(" in rest:
        return False
    return rest.endswith(";") or "{ get" in rest or "{get" in rest or re.search(r"=[^=>]", rest) is not None


def check_static_state(sources: list[SourceFile]) -> list[Finding]:
    orchestrator = next(
        (s for s in sources if s.rel.endswith("Runtime/Orchestrator/Internal/SymphonyOrchestrator.cs")),
        None,
    )
    registered = set()
    if orchestrator is not None:
        registered = set(re.findall(r"(\w+)\.ResetRuntimeState", orchestrator.text))

    findings: list[Finding] = []
    for source in sources:
        if not in_dirs(source, SHIPPED_RUNTIME_DIRS):
            continue
        static_lines = [
            number
            for number, line in enumerate(source.lines, start=1)
            if is_static_mutable_declaration(strip_line_comment(line))
        ]
        if not static_lines:
            continue
        has_reset = bool(RESET_DECL_RE.search(source.text))
        type_name = Path(source.rel).stem
        if not has_reset:
            findings.append(
                Finding(
                    "03_static_without_reset",
                    source.rel,
                    static_lines[0],
                    f"static な可変状態 {len(static_lines)}件 / ResetRuntimeState 未定義",
                )
            )
        elif type_name not in registered:
            findings.append(
                Finding(
                    "03_reset_not_registered",
                    source.rel,
                    static_lines[0],
                    f"ResetRuntimeState はあるが SymphonyOrchestrator へ未登録（{type_name}）",
                )
            )
    return findings


# ---------------------------------------------------------------------------
# 11 非同期処理
# ---------------------------------------------------------------------------

ASYNC_VOID_RE = re.compile(r"\basync\s+void\s+\w+\s*\(")
ASYNC_METHOD_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*(?:public|internal|private|protected)?[\w\s]*\basync\s+"
    r"(?:Awaitable|Task|ValueTask|UniTask)(?:<[^>]+>)?\s+(\w+)\s*\(([^)]*)"
)


def check_async(sources: list[SourceFile]) -> list[Finding]:
    findings: list[Finding] = []
    for source in sources:
        for number, line in enumerate(source.lines, start=1):
            code = strip_line_comment(line)
            if ASYNC_VOID_RE.search(code):
                findings.append(
                    Finding("11_async_void", source.rel, number, code.strip()[:110])
                )
            match = ASYNC_METHOD_RE.match(code)
            if match:
                name, params = match.group(1), match.group(2)
                # Signature may wrap; look ahead until the closing paren.
                cursor = number
                while ")" not in params and cursor < len(source.lines):
                    params += source.lines[cursor]
                    cursor += 1
                if "CancellationToken" not in params:
                    findings.append(
                        Finding(
                            "11_missing_cancellation_token",
                            source.rel,
                            number,
                            f"async {name}(...) に CancellationToken 引数が無い",
                        )
                    )
    return findings


# ---------------------------------------------------------------------------
# 12 ログ運用
# ---------------------------------------------------------------------------

RAW_DEBUG_LOG_RE = re.compile(r"(?<!\w)(?<!Symphony)Debug\.Log(?:Warning|Error|Format)?\s*\(")


def iter_with_editor_guard(source: SourceFile):
    """Yield (line number, code, inside `#if UNITY_EDITOR`) for each line."""
    depth = 0
    for number, line in enumerate(source.lines, start=1):
        stripped = line.strip()
        if stripped.startswith("#if") and "UNITY_EDITOR" in stripped:
            depth += 1
        elif stripped.startswith("#endif") and depth > 0:
            depth -= 1
        yield number, strip_line_comment(line), depth > 0


def check_logging(sources: list[SourceFile]) -> list[Finding]:
    findings: list[Finding] = []
    for source in sources:
        if not in_dirs(source, SHIPPED_RUNTIME_DIRS):
            continue
        conditional = "[Conditional(\"UNITY_EDITOR\")]" in source.text
        for number, code, guarded in iter_with_editor_guard(source):
            if not RAW_DEBUG_LOG_RE.search(code):
                continue
            # `#if UNITY_EDITOR` の内側ならビルドへ残らない。分類を分けないと、
            # 実際に出荷されるログが正しく囲まれたログに埋もれる。
            category = "12_editor_only_debug_log" if guarded else "12_raw_debug_log"
            note = "文字列補間あり: " if '$"' in code else ""
            if not guarded and conditional:
                note += "（同ファイルに[Conditional]付きAPIあり）"
            findings.append(Finding(category, source.rel, number, note + code.strip()[:100]))
    return findings


# ---------------------------------------------------------------------------
# 16 SRP（ファイル長・メソッド長）
# ---------------------------------------------------------------------------

METHOD_SIGNATURE_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*(?:public|internal|private|protected)\s+"
    r"(?![\w<>]*\s*=\s)(?:static\s+|async\s+|virtual\s+|override\s+|sealed\s+|extern\s+|unsafe\s+|new\s+)*"
    r"[\w<>,\[\]\.\?]+\s+(\w+)\s*\("
)


def measure_methods(source: SourceFile) -> list[tuple[str, int, int]]:
    """Return (method name, start line, body length) triples via brace matching."""
    results: list[tuple[str, int, int]] = []
    index = 0
    while index < len(source.lines):
        match = METHOD_SIGNATURE_RE.match(strip_line_comment(source.lines[index]))
        if not match:
            index += 1
            continue
        # Find the opening brace of the body (may be on a later line).
        cursor = index
        while cursor < len(source.lines) and "{" not in strip_line_comment(source.lines[cursor]):
            if ";" in strip_line_comment(source.lines[cursor]) or "=>" in strip_line_comment(
                source.lines[cursor]
            ):
                cursor = -1
                break
            cursor += 1
        if cursor == -1 or cursor >= len(source.lines):
            index += 1
            continue
        depth = 0
        end = cursor
        for scan in range(cursor, len(source.lines)):
            code = strip_line_comment(source.lines[scan])
            depth += code.count("{") - code.count("}")
            if depth <= 0:
                end = scan
                break
        results.append((match.group(1), index + 1, end - cursor + 1))
        index = max(end, index + 1)
    return results


def check_size(sources: list[SourceFile]) -> list[Finding]:
    findings: list[Finding] = []
    for source in sources:
        if len(source.lines) > LONG_FILE_THRESHOLD:
            findings.append(
                Finding("16_long_file", source.rel, 1, f"{len(source.lines)}行")
            )
        for name, start, length in measure_methods(source):
            if length > LONG_METHOD_THRESHOLD:
                findings.append(
                    Finding("16_long_method", source.rel, start, f"{name}() {length}行")
                )
    return findings


# ---------------------------------------------------------------------------
# B. フレームワーク固有
# ---------------------------------------------------------------------------

PUBLIC_MEMBER_RE = re.compile(
    r"^\s*public\s+(?!class\b|struct\b|interface\b|enum\b|delegate\b|record\b|abstract\s+class\b|"
    r"sealed\s+class\b|static\s+class\b|partial\b|readonly\s+struct\b)"
)
PUBLIC_TYPE_RE = re.compile(
    r"^\s*public\s+(?:abstract\s+|sealed\s+|static\s+|partial\s+|readonly\s+|ref\s+)*"
    r"(class|struct|interface|enum|delegate|record)\s+(\w+)"
)
UNITY_EDITOR_RE = re.compile(r"\b(UnityEditor|EditorPrefs|EditorApplication|AssetDatabase)\b")
OBSOLETE_RE = re.compile(r"\[Obsolete")
DECL_TYPE_NAME_RE = re.compile(r"\b(?:class|struct|interface|enum|delegate|record)\s+(\w+)")
DECL_METHOD_NAME_RE = re.compile(r"\b(\w+)\s*(?:<[^<>()]*>)?\s*\(")
DECL_MEMBER_NAME_RE = re.compile(r"\b(\w+)\s*(?:\{|;|=>|=[^=])")
IDENTIFIER_RE = re.compile(r"\b(\w+)\b")


def extract_declared_symbol(lines: list[str], attribute_line: int) -> str:
    """Find the symbol an attribute at `attribute_line` (1-based) is attached to.

    Attributes may span several lines and may be followed by further attributes,
    so the bracket balance has to be tracked; otherwise the `nameof(...)` inside
    the `[Obsolete]` message gets mistaken for the declaration.
    """
    depth = 0
    for index in range(attribute_line - 1, min(attribute_line + 20, len(lines))):
        code = strip_line_comment(lines[index]).strip()
        if depth > 0 or code.startswith("["):
            depth += code.count("[") - code.count("]")
            continue
        if not code or code.startswith(("///", "//", "#")):
            continue
        for pattern in (DECL_TYPE_NAME_RE, DECL_METHOD_NAME_RE, DECL_MEMBER_NAME_RE):
            match = pattern.search(code)
            if match:
                return match.group(1)
        identifiers = IDENTIFIER_RE.findall(code)
        return identifiers[-1] if identifiers else ""
    return ""


def check_public_surface(sources: list[SourceFile]) -> list[Finding]:
    findings: list[Finding] = []
    for source in sources:
        if not in_dirs(source, SHIPPED_RUNTIME_DIRS):
            continue
        if "/Internal/" in source.rel:
            # Internal は非公開前提。public が出ていること自体が指摘対象。
            for number, line in enumerate(source.lines, start=1):
                if PUBLIC_TYPE_RE.match(strip_line_comment(line)):
                    findings.append(
                        Finding(
                            "B_public_in_internal",
                            source.rel,
                            number,
                            "Internal/ 配下に public 型",
                        )
                    )
            continue
        for number, line in enumerate(source.lines, start=1):
            code = strip_line_comment(line)
            if not PUBLIC_MEMBER_RE.match(code):
                continue
            # XML ドキュメントが直前にあるか
            documented = False
            for back in range(number - 2, max(number - 12, -1), -1):
                previous = source.lines[back].strip()
                if previous.startswith("///"):
                    documented = True
                    break
                if previous and not previous.startswith("[") and not previous.startswith("//"):
                    break
            if not documented:
                findings.append(
                    Finding(
                        "B_public_without_xmldoc",
                        source.rel,
                        number,
                        code.strip()[:110],
                    )
                )
    return findings


def check_assembly_boundary(sources: list[SourceFile]) -> list[Finding]:
    findings: list[Finding] = []
    for source in sources:
        if not in_dirs(source, SHIPPED_RUNTIME_DIRS):
            continue
        guard_depth = 0
        for number, line in enumerate(source.lines, start=1):
            stripped = line.strip()
            if stripped.startswith("#if") and "UNITY_EDITOR" in stripped:
                guard_depth += 1
            elif stripped.startswith("#endif") and guard_depth > 0:
                guard_depth -= 1
            code = strip_line_comment(line)
            if UNITY_EDITOR_RE.search(code):
                # `#if UNITY_EDITOR` で囲んであっても違反（CONTRIBUTING の規約）。
                # ただし判断材料になるので、囲みの有無は残す。
                guard = "#if UNITY_EDITOR 内 — " if guard_depth else "無防備 — "
                findings.append(
                    Finding(
                        "B_unity_editor_in_runtime",
                        source.rel,
                        number,
                        guard + code.strip()[:100],
                    )
                )
    return findings


def check_obsolete_sync(sources: list[SourceFile]) -> list[Finding]:
    deprecations = PACKAGE_ROOT / "Documentation~" / "Deprecations.md"
    documented = deprecations.read_text(encoding="utf-8-sig") if deprecations.is_file() else ""
    findings: list[Finding] = []
    for source in sources:
        for number, line in enumerate(source.lines, start=1):
            if not OBSOLETE_RE.search(strip_line_comment(line)):
                continue
            symbol = extract_declared_symbol(source.lines, number)
            listed = bool(symbol) and symbol in documented
            findings.append(
                Finding(
                    "B_obsolete" if listed else "B_obsolete_undocumented",
                    source.rel,
                    number,
                    f"{symbol or '(不明)'}"
                    + ("" if listed else " — Deprecations.md に記載なし"),
                )
            )
    return findings


DEPRECATION_ROW_RE = re.compile(r"^\|\s*`([^`]+)`\s*\|")


def deprecations_listed_symbols() -> dict[str, int]:
    """Symbols listed in Deprecations.md's 削除予定の一覧, mapped to line numbers."""
    doc = PACKAGE_ROOT / "Documentation~" / "Deprecations.md"
    if not doc.is_file():
        return {}
    lines = doc.read_text(encoding="utf-8-sig").splitlines()
    listed: dict[str, int] = {}
    inside = False
    for number, line in enumerate(lines, start=1):
        if line.startswith("## "):
            inside = line.startswith("## 削除予定の一覧")
            continue
        if line.startswith("### "):
            # 「Combineの削除手順」など、節内の補足表は対象外。
            inside = False
            continue
        if not inside:
            continue
        match = DEPRECATION_ROW_RE.match(line)
        if not match:
            continue
        entry = match.group(1).split("(")[0].split("<")[0].strip()
        symbol = entry.split(".")[-1]
        if symbol:
            listed.setdefault(symbol, number)
    return listed


def check_deprecations_reverse(sources: list[SourceFile]) -> list[Finding]:
    """Deprecations.md に載っているが、コードから消えたシンボルを探す。

    `[Obsolete]` を付けたときの記載漏れは check_obsolete_sync が拾う。こちらは
    その逆で、削除が済んだのに `## 削除済み` へ移されていない行を拾う。
    コード側からは検出できないため、この方向の検査が要る。
    """
    in_code = {
        extract_declared_symbol(source.lines, number)
        for source in sources
        for number, line in enumerate(source.lines, start=1)
        if OBSOLETE_RE.search(strip_line_comment(line))
    }
    in_code.discard("")
    return [
        Finding(
            "B_deprecation_stale",
            "Assets/SymphonyFrameWork/Documentation~/Deprecations.md",
            line,
            f"{symbol} — コードに [Obsolete] が無い。削除済みなら `## 削除済み` へ移す",
        )
        for symbol, line in sorted(deprecations_listed_symbols().items(), key=lambda kv: kv[1])
        if symbol not in in_code
    ]


def check_line_endings() -> list[Finding]:
    """リポジトリ側の改行コードを検査する。

    `core.autocrlf=true` 前提でワーキングツリーはCRLFになるため、ファイルの
    バイト列を見ても判断できない。indexに何が入っているかを見る必要がある。
    """
    try:
        completed = subprocess.run(
            ["git", "-C", str(PACKAGE_ROOT), "ls-files", "--eol", "--", "*.cs"],
            capture_output=True,
            text=True,
            check=True,
        )
    except (OSError, subprocess.CalledProcessError) as error:
        return [
            Finding(
                "B_line_ending_uncheckable",
                "Assets/SymphonyFrameWork",
                1,
                f"git ls-files --eol を実行できなかった: {error}",
            )
        ]

    findings: list[Finding] = []
    for line in completed.stdout.splitlines():
        # 例: "i/lf    w/crlf  attr/                 \tRuntime/Foo.cs"
        parts = line.split("\t", 1)
        if len(parts) != 2:
            continue
        index_eol = parts[0].split()[0]
        if index_eol not in ("i/lf", "i/-text"):
            findings.append(
                Finding(
                    "B_line_ending",
                    f"Assets/SymphonyFrameWork/{parts[1].strip()}",
                    1,
                    f"リポジトリ側が {index_eol}。CONTRIBUTING §3 はLF格納を定めている",
                )
            )
    return findings


def check_encoding(sources: list[SourceFile]) -> list[Finding]:
    return [
        Finding("B_missing_bom", source.rel, 1, "UTF-8 BOM 無し")
        for source in sources
        if not source.has_bom
    ]


# ---------------------------------------------------------------------------
# その他（09 / 14 / 18 / LINQ / IEquatable）
# ---------------------------------------------------------------------------

CONST_RE = re.compile(r"\b(const|static\s+readonly)\b")
NUMBER_RE = re.compile(r"(?<![\w.])(-?\d+(?:\.\d+)?[fdmul]?)(?![\w.])")
STRING_RE = re.compile(r"(?:@?\$?\"(?:[^\"\\]|\\.)*\")|'(?:[^'\\]|\\.)'")


def check_magic_numbers(sources: list[SourceFile]) -> list[Finding]:
    findings: list[Finding] = []
    for source in sources:
        if not in_dirs(source, SHIPPED_RUNTIME_DIRS):
            continue
        for number, line in enumerate(source.lines, start=1):
            code = STRING_RE.sub('""', strip_line_comment(line))
            if CONST_RE.search(code) or "[" in code and "]" in code and "=" not in code:
                continue
            literals = [m for m in NUMBER_RE.findall(code) if m not in BENIGN_NUMBERS]
            if literals:
                findings.append(
                    Finding(
                        "09_magic_number",
                        source.rel,
                        number,
                        f"{', '.join(sorted(set(literals)))} — {code.strip()[:80]}",
                    )
                )
    return findings


INTERFACE_RE = re.compile(r"^\s*(?:public|internal)\s+(?:partial\s+)?interface\s+(I\w+)")
STRUCT_RE = re.compile(r"^\s*(?:public|internal)\s+(?:readonly\s+|ref\s+|partial\s+)*struct\s+(\w+)")


def check_abstraction(sources: list[SourceFile]) -> list[Finding]:
    all_text = "\n".join(source.text for source in sources)
    findings: list[Finding] = []
    for source in sources:
        for number, line in enumerate(source.lines, start=1):
            interface = INTERFACE_RE.match(strip_line_comment(line))
            if interface:
                name = interface.group(1)
                implementations = len(
                    re.findall(rf"[:,]\s*{re.escape(name)}\b(?!\s*\{{)", all_text)
                )
                if implementations <= 1:
                    findings.append(
                        Finding(
                            "14_single_implementation_interface",
                            source.rel,
                            number,
                            f"{name} — 実装 {implementations}件",
                        )
                    )
            struct_decl = STRUCT_RE.match(strip_line_comment(line))
            if struct_decl:
                name = struct_decl.group(1)
                declaration = "\n".join(source.lines[number - 1 : number + 2])
                if "IEquatable" not in declaration:
                    findings.append(
                        Finding(
                            "06_struct_without_iequatable",
                            source.rel,
                            number,
                            f"struct {name}",
                        )
                    )
    return findings


TODO_RE = re.compile(r"//.*\b(TODO|FIXME|HACK|XXX)\b")
COMMENTED_CODE_RE = re.compile(r"^\s*//\s*[\w\.\]\)]+.*[;{}]\s*$")
LINQ_RE = re.compile(r"^\s*using\s+System\.Linq\s*;")


def check_hygiene(sources: list[SourceFile]) -> list[Finding]:
    findings: list[Finding] = []
    for source in sources:
        for number, line in enumerate(source.lines, start=1):
            if TODO_RE.search(line):
                findings.append(Finding("18_todo", source.rel, number, line.strip()[:110]))
            if COMMENTED_CODE_RE.match(line) and not line.strip().startswith("///"):
                findings.append(
                    Finding("18_commented_code", source.rel, number, line.strip()[:110])
                )
            if LINQ_RE.match(line) and in_dirs(source, SHIPPED_RUNTIME_DIRS):
                findings.append(
                    Finding("05_linq_in_runtime", source.rel, number, "using System.Linq")
                )
    return findings


# ---------------------------------------------------------------------------
# 実行
# ---------------------------------------------------------------------------

CATEGORY_LABELS = {
    "01_subscribe_imbalance": "01 購読(+=)より解除(-=)が少ないファイル",
    "01_lambda_subscribe": "01 ラムダ購読（解除不能の疑い）",
    "03_static_without_reset": "03 static 可変状態があり ResetRuntimeState が無い型",
    "03_reset_not_registered": "03 ResetRuntimeState が Orchestrator へ未登録",
    "05_linq_in_runtime": "05 Runtime/Core での System.Linq 使用",
    "06_struct_without_iequatable": "06 IEquatable 未宣言の struct",
    "09_magic_number": "09 定数化されていない数値リテラル",
    "11_async_void": "11 async void",
    "11_missing_cancellation_token": "11 CancellationToken 引数を持たない非同期メソッド",
    "12_raw_debug_log": "12 Runtime/Core での素の Debug.Log（ビルドへ残る）",
    "12_editor_only_debug_log": "12 `#if UNITY_EDITOR` で囲まれた Debug.Log（参考）",
    "14_single_implementation_interface": "14 実装が1件以下のインターフェース",
    "16_long_file": f"16 {LONG_FILE_THRESHOLD}行超のファイル",
    "16_long_method": f"16 {LONG_METHOD_THRESHOLD}行超のメソッド",
    "18_todo": "18 TODO / FIXME / HACK",
    "18_commented_code": "18 コメントアウトされたコード",
    "B_public_without_xmldoc": "B XMLドキュメントの無い public メンバー",
    "B_public_in_internal": "B Internal/ 配下の public 型",
    "B_unity_editor_in_runtime": "B Runtime/Core からの UnityEditor 参照",
    "B_obsolete": "B [Obsolete]（Deprecations.md 記載済み）",
    "B_obsolete_undocumented": "B [Obsolete] だが Deprecations.md に記載が無い",
    "B_deprecation_stale": "B Deprecations.md に残っているが コードから消えたシンボル",
    "B_missing_bom": "B UTF-8 BOM の無い .cs",
    "B_line_ending": "B リポジトリ側の改行コードがLFでない .cs",
    "B_line_ending_uncheckable": "B 改行コードを検査できなかった",
}


def run_all(sources: list[SourceFile]) -> list[Finding]:
    findings: list[Finding] = []
    findings += check_event_unsubscribe(sources)
    findings += check_static_state(sources)
    findings += check_async(sources)
    findings += check_logging(sources)
    findings += check_size(sources)
    findings += check_public_surface(sources)
    findings += check_assembly_boundary(sources)
    findings += check_obsolete_sync(sources)
    findings += check_deprecations_reverse(sources)
    findings += check_encoding(sources)
    findings += check_line_endings()
    findings += check_magic_numbers(sources)
    findings += check_abstraction(sources)
    findings += check_hygiene(sources)
    return findings


def render_markdown(findings: list[Finding], sources: list[SourceFile]) -> str:
    by_category: dict[str, list[Finding]] = {}
    for finding in findings:
        by_category.setdefault(finding.category, []).append(finding)

    total_lines = sum(len(source.lines) for source in sources)
    out: list[str] = []
    out.append("# 機械走査の結果（audit_scan.py）")
    out.append("")
    out.append(
        f"対象: `Assets/SymphonyFrameWork/` の {', '.join(SHIPPED_DIRS)} "
        f"— {len(sources)}ファイル / {total_lines}行"
    )
    out.append("")
    out.append(
        "**この出力は場所の列挙に過ぎない。** 確度（確定 / 要検証 / 設計指摘）は"
        "コードを読んで付けること。正規表現による検出のため誤検出を含む。"
    )
    out.append("")
    out.append("## 件数サマリ")
    out.append("")
    out.append("| 分類 | 件数 |")
    out.append("| --- | --- |")
    for category in sorted(by_category, key=lambda key: CATEGORY_LABELS.get(key, key)):
        label = CATEGORY_LABELS.get(category, category)
        out.append(f"| {label} | {len(by_category[category])} |")
    out.append("")

    for category in sorted(by_category, key=lambda key: CATEGORY_LABELS.get(key, key)):
        label = CATEGORY_LABELS.get(category, category)
        entries = by_category[category]
        out.append(f"## 付録: {label}（全{len(entries)}件）")
        out.append("")
        out.append("| 場所 | 内容 |")
        out.append("| --- | --- |")
        for entry in entries:
            detail = entry.detail.replace("|", "\\|")
            out.append(f"| `{entry.path}:{entry.line}` | {detail} |")
        out.append("")
    return "\n".join(out)


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--format", choices=("markdown", "json"), default="markdown", help="出力形式"
    )
    parser.add_argument("--out", type=Path, help="出力先。省略時は標準出力")
    parser.add_argument(
        "--category", action="append", help="この分類だけを出力する（複数指定可）"
    )
    args = parser.parse_args(argv)

    if not PACKAGE_ROOT.is_dir():
        print(f"パッケージが見つからない: {PACKAGE_ROOT}", file=sys.stderr)
        return 1

    sources = load_sources(SHIPPED_DIRS)
    findings = run_all(sources)
    if args.category:
        wanted = set(args.category)
        findings = [f for f in findings if f.category in wanted]

    if args.format == "json":
        payload = {
            "fileCount": len(sources),
            "lineCount": sum(len(s.lines) for s in sources),
            "findings": [asdict(f) for f in findings],
        }
        rendered = json.dumps(payload, ensure_ascii=False, indent=2)
    else:
        rendered = render_markdown(findings, sources)

    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(rendered + "\n", encoding="utf-8")
        print(f"{len(findings)}件を {args.out} へ書き出した")
    else:
        print(rendered)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
