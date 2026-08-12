#!/usr/bin/env python3
"""コメントを除いた差分だけを抽出する。

`Documentation/Designs/CodeCommentConventions.md` の Round ごとの検証で使う。

コメント規約をコードへ適用する改修では、差分の大半が `//` と `///` の追加になる。
そのまま `git diff` を読むと、`var` の置き換えや波括弧の補完といった
**振る舞いを変えうる変更がコメントに埋もれる**。このスクリプトは
コメントと空白を落としたうえで差分を取り、人が読むべき行だけを残す。

コメント追加だけの Round なら出力は空になる。

Usage
-----
    python scripts/comment_only_diff.py
    python scripts/comment_only_diff.py --ref HEAD --path Core
    python scripts/comment_only_diff.py --stat
"""

from __future__ import annotations

import argparse
import difflib
from collections import Counter
import subprocess
import sys
from pathlib import Path

WORKSPACE_ROOT = Path(__file__).resolve().parent.parent
SUBMODULE_ROOT = WORKSPACE_ROOT / "Assets" / "SymphonyFrameWork"


def strip_comments(source: str) -> str:
    """C#ソースからコメントを除去する。

    文字列リテラルの中の `//` を落とさないよう、文字列・逐語文字列・文字リテラルを
    読み飛ばしながら走査する。`"https://example.com"` を壊さないために必要。
    """
    out: list[str] = []
    i = 0
    length = len(source)

    while i < length:
        char = source[i]
        pair = source[i:i + 2]

        # 行コメント。改行は残して行番号の対応を保つ。
        if pair == "//":
            while i < length and source[i] != "\n":
                i += 1
            continue

        # ブロックコメント。中の改行だけを残す。
        if pair == "/*":
            i += 2
            while i < length and source[i:i + 2] != "*/":
                if source[i] == "\n":
                    out.append("\n")
                i += 1
            i += 2
            continue

        # 逐語文字列（@"..." と $@"..." / @$"..."）。"" だけがエスケープ。
        if char == "@" or (char in "$" and source[i:i + 2] in ('$@', '@$')):
            verbatim_start = source.find('"', i, i + 3)
            if verbatim_start != -1 and source[i:verbatim_start].strip("$@") == "":
                out.append(source[i:verbatim_start + 1])
                i = verbatim_start + 1
                while i < length:
                    if source[i] == '"':
                        if source[i:i + 2] == '""':
                            out.append('""')
                            i += 2
                            continue
                        out.append('"')
                        i += 1
                        break
                    out.append(source[i])
                    i += 1
                continue

        # 通常の文字列と文字リテラル。バックスラッシュでエスケープする。
        if char in '"\'':
            quote = char
            out.append(char)
            i += 1
            while i < length:
                if source[i] == "\\":
                    out.append(source[i:i + 2])
                    i += 2
                    continue
                out.append(source[i])
                if source[i] == quote:
                    i += 1
                    break
                i += 1
            continue

        out.append(char)
        i += 1

    return "".join(out)


def normalize(source: str) -> list[str]:
    """コメントとregionを落とし、空行と余分な空白を潰した行の一覧を返す。

    先頭のBOMを必ず落とす。比較元は `git show` の生の出力、比較先はファイル読み込みであり、
    片方だけBOMが残ると全ファイルの1行目が差分として出てしまう。

    `#region` と `#endregion` も落とす。コンパイル結果に影響しない指示子であり、
    本改修では全ファイルへ追加されるため、残すと本当に見るべき行が埋もれる。
    """
    lines = []
    for raw in strip_comments(source.lstrip("﻿")).splitlines():
        line = " ".join(raw.split())
        if not line:
            continue
        if line.startswith("#region") or line.startswith("#endregion"):
            continue
        lines.append(line)
    return lines


def git_submodule(*arguments: str, check: bool = True) -> str:
    result = subprocess.run(
        ["git", *arguments],
        cwd=SUBMODULE_ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if check and result.returncode != 0:
        print(result.stderr, file=sys.stderr)
        raise SystemExit(f"git {' '.join(arguments)} が失敗しました。")
    return result.stdout


def changed_cs_files(ref: str, path_filter: str | None) -> list[str]:
    """ref と作業ツリーの間で変更された .cs を返す。"""
    output = git_submodule("diff", "--name-only", ref, "--", "*.cs")
    files = [line.strip() for line in output.splitlines() if line.strip()]
    if path_filter:
        files = [f for f in files if f.startswith(path_filter)]
    return sorted(files)


def before_source(ref: str, relative_path: str) -> str:
    return git_submodule("show", f"{ref}:{relative_path}", check=False)


def after_source(relative_path: str) -> str:
    target = SUBMODULE_ROOT / relative_path
    if not target.is_file():
        return ""
    return target.read_text(encoding="utf-8-sig", errors="replace")


BRACE_ONLY = {"{", "}", "};"}


def net_changes(before: list[str], after: list[str]) -> tuple[list[str], list[str], int]:
    """行の多重集合を比較し、移動を相殺した正味の増減を返す。

    region の二分割はメンバーの並べ替えを伴い、行単位の差分では
    移動が「削除＋追加」として二重に出る。**並べ替えは規約が認めている**ため、
    それを差分として読まされると、本当に変わった行が埋もれる。

    同じ内容の行が前後で同数あるなら、それは移動であって変更ではない。
    ここで消え残るのが、実際に追加・削除・書き換えされた行である。

    単独の波括弧だけの行は件数だけ返し、一覧からは外す。Allman形式から
    1行形式への統一で必ず増減するうえ、それ自体は何の情報も持たない。
    **波括弧の増減だけで壊れる変更はコンパイルが通らない**ため、
    ここで見逃しても後段のコンパイルとテストが捕まえる。
    """
    before_counts = Counter(before)
    after_counts = Counter(after)
    added = sorted((after_counts - before_counts).elements())
    removed = sorted((before_counts - after_counts).elements())

    brace_count = sum(1 for line in added + removed if line in BRACE_ONLY)
    added = [line for line in added if line not in BRACE_ONLY]
    removed = [line for line in removed if line not in BRACE_ONLY]
    return added, removed, brace_count


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ref", default="HEAD", help="比較元のリビジョン（既定 HEAD）")
    parser.add_argument("--path", help="submodule ルートからの前方一致で対象を絞る（例 Core）")
    parser.add_argument("--stat", action="store_true", help="ファイルごとの件数だけを出す")
    parser.add_argument(
        "--positional", action="store_true",
        help="移動を相殺せず、行の位置差分をそのまま出す",
    )
    args = parser.parse_args(argv)

    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass

    files = changed_cs_files(args.ref, args.path)
    if not files:
        print("変更された .cs がありません。")
        return 0

    total_changed_lines = 0
    files_with_changes = 0
    total_brace_lines = 0

    for relative_path in files:
        before = normalize(before_source(args.ref, relative_path))
        after = normalize(after_source(relative_path))

        if args.positional:
            body = [
                line for line in difflib.unified_diff(
                    before, after,
                    fromfile=f"a/{relative_path}",
                    tofile=f"b/{relative_path}",
                    lineterm="",
                    n=1,
                )
            ]
            changed = [
                l for l in body
                if l[:1] in "+-" and not l.startswith(("+++", "---"))
            ]
        else:
            added, removed, braces = net_changes(before, after)
            changed = [f"-{line}" for line in removed] + [f"+{line}" for line in added]
            head = f"--- a/{relative_path}\n+++ b/{relative_path}"
            if braces:
                head += f"\n（単独の波括弧 {braces} 行は省略）"
            body = [head, *changed]
            total_brace_lines += braces

        if not changed:
            continue

        files_with_changes += 1
        total_changed_lines += len(changed)

        if args.stat:
            print(f"{len(changed):4d} 行  {relative_path}")
        else:
            print("\n".join(body))
            print()

    print("=" * 60)
    print(f"対象 {len(files)} ファイル中、コメント以外の変更があるのは {files_with_changes} ファイル")
    print(f"コメント以外の変更行数: {total_changed_lines}"
          f"（別に単独の波括弧 {total_brace_lines} 行）")
    if files_with_changes == 0:
        print("→ コメントの追加・書式変更だけの差分です。")
    else:
        print("→ 上記の行は振る舞いを変えうる変更です。1行ずつ確認してください。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
