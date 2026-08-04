#!/usr/bin/env python3
"""Round のリリース手順（実装フロー ステップ3〜5）を固定化するスクリプト。

`.agents/skills/implement/references/release.md` の手順を、順序と検証ごとコード化する。
手で叩くと落としやすい次の3点を機械的に担保することが目的である。

- **submodule を push する前に親の gitlink を更新しない。** push 前の gitlink は他の開発者が解決できない
- **gitlink は `origin/develop` から到達可能なコミットを指す。** feature ブランチのコミットを指すと、
  squash マージやブランチ削除で到達不能になり、新規クローンの `git submodule update` が失敗する
- **親リポジトリへ `git add -A` しない。** 無関係な未コミット変更を巻き込む

フェーズ
--------
preflight : 検証のみ。git の状態は一切変更しない
commit    : preflight → submodule へコミット → push →（任意で）PR 作成
finalize  : マージ後。gitlink の到達可能性を確認 → 親リポジトリをコミット → push

**マージとブランチ削除はこのスクリプトが行わない。** 承認を挟む余地を残すため意図的に対象外にしている。

Exit codes
----------
0 : 成功
1 : 検証の失敗、または git 操作の失敗
2 : 引数の誤りなど、実行前に判明した問題

Usage
-----
    python scripts/release_round.py preflight
    python scripts/release_round.py commit --message "[add]説明" --issue 119 --pr --pr-body-file body.md
    python scripts/release_round.py finalize --paths Documentation/Designs/Foo.md
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path


WORKSPACE_ROOT = Path(__file__).resolve().parents[1]
SUBMODULE_PATH = "Assets/SymphonyFrameWork"
SUBMODULE_ROOT = WORKSPACE_ROOT / SUBMODULE_PATH
INTEGRATION_BRANCH = "origin/develop"

# この名前のブランチへ直接コミットしない。実装フローは feature ブランチを前提にする。
PROTECTED_BRANCHES = frozenset({"develop", "main", "master"})

# `.meta` を必要としない領域。Unity の Asset Import 対象外。
META_EXEMPT_PREFIXES = ("Documentation~/",)

COMMIT_MESSAGE_PATTERN = re.compile(r"^\[(add|update|fix)\][^\s].*$")
CHANGELOG_HEADING_PATTERN = re.compile(r"^## \[([^\]]+)\]", re.MULTILINE)
COMMENT_LINE_PATTERN = re.compile(r"^\s*(//|/\*|\*)")

BOM = b"\xef\xbb\xbf"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    subparsers = parser.add_subparsers(dest="phase", required=True)

    subparsers.add_parser("preflight", help="検証のみ行い、gitの状態を変更しない")

    commit_parser = subparsers.add_parser("commit", help="submoduleへコミットしpushする")
    commit_parser.add_argument(
        "--message",
        required=True,
        help="コミットメッセージ。[add]/[update]/[fix] で始まる1行",
    )
    commit_parser.add_argument("--issue", help="対応するIssue番号。指定するとトレーラへ入る")
    commit_parser.add_argument("--pr", action="store_true", help="push後にPullRequestを作成する")
    commit_parser.add_argument("--pr-base", default="develop", help="PRのベースブランチ")
    commit_parser.add_argument("--pr-body-file", help="PR本文のファイルパス")
    commit_parser.add_argument(
        "--skip-preflight",
        action="store_true",
        help="検証を飛ばす。検証済みの内容を再コミットする場合だけ使う",
    )

    finalize_parser = subparsers.add_parser(
        "finalize", help="マージ後に親リポジトリのgitlinkを更新する"
    )
    finalize_parser.add_argument(
        "--paths",
        nargs="*",
        default=[],
        help="gitlinkと一緒にコミットする親リポジトリのパス（設計書など）",
    )
    finalize_parser.add_argument(
        "--message", help="親リポジトリのコミットメッセージ。省略時は自動生成"
    )
    finalize_parser.add_argument("--parent-branch", help="pushする親のブランチ。省略時は現在のブランチ")
    finalize_parser.add_argument(
        "--no-push", action="store_true", help="コミットまで行い、pushしない"
    )

    args = parser.parse_args()

    if args.phase == "preflight":
        return 0 if run_preflight() else 1
    if args.phase == "commit":
        return run_commit(args)
    if args.phase == "finalize":
        return run_finalize(args)

    return 2


# ---------------------------------------------------------------- git helpers


def git(*arguments: str, cwd: Path = WORKSPACE_ROOT, check: bool = True) -> str:
    """git を実行して標準出力を返す。"""
    completed = subprocess.run(
        ["git", *arguments],
        cwd=cwd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if check and completed.returncode != 0:
        raise RuntimeError(
            f"git {' '.join(arguments)} が失敗しました。\n{completed.stderr.strip()}"
        )
    return completed.stdout.strip()


def submodule_git(*arguments: str, check: bool = True) -> str:
    """submodule に対して git を実行する。"""
    return git(*arguments, cwd=SUBMODULE_ROOT, check=check)


def current_branch(cwd: Path) -> str:
    return git("branch", "--show-current", cwd=cwd)


# ------------------------------------------------------------------ preflight


def run_preflight() -> bool:
    """コミット前の検証をまとめて実行する。"""
    print("== preflight ==")
    failures: list[str] = []

    failures.extend(check_branch())
    failures.extend(check_version_matches_changelog())
    failures.extend(check_bom())
    failures.extend(check_meta_pairs())
    failures.extend(check_runtime_editor_references())
    failures.extend(check_test_assembly_constraints())

    print("")
    if failures:
        print(f"NG: {len(failures)}件の問題があります。")
        for failure in failures:
            print(f"  - {failure}")
        return False

    print("OK: すべての検証を通過しました。")
    return True


def check_branch() -> list[str]:
    branch = current_branch(SUBMODULE_ROOT)
    if not branch:
        return ["submoduleがdetached HEADです。feature ブランチを作成してください。"]
    if branch in PROTECTED_BRANCHES:
        return [f"submoduleが'{branch}'です。feature ブランチを作成してください。"]
    print(f"[branch] OK: {branch}")
    return []


def check_version_matches_changelog() -> list[str]:
    """package.json の version と CHANGELOG の先頭見出しの一致を確認する。"""
    package_path = SUBMODULE_ROOT / "package.json"
    changelog_path = SUBMODULE_ROOT / "CHANGELOG.md"

    version = json.loads(package_path.read_text(encoding="utf-8"))["version"]
    match = CHANGELOG_HEADING_PATTERN.search(changelog_path.read_text(encoding="utf-8"))

    if match is None:
        return ["CHANGELOG.mdにバージョン見出しが見つかりません。"]

    heading = match.group(1)
    if version != heading:
        return [f"package.jsonの'{version}'とCHANGELOGの'{heading}'が一致しません。"]

    print(f"[version] OK: {version}")
    return []


def changed_files(pattern: str) -> list[str]:
    """統合ブランチとの差分と、未追跡ファイルを合わせて返す。"""
    tracked = submodule_git(
        "diff", "--name-only", "--diff-filter=d", INTEGRATION_BRANCH, "--", pattern
    )
    untracked = submodule_git("ls-files", "-o", "--exclude-standard", "--", pattern)
    names = {name for name in (tracked + "\n" + untracked).splitlines() if name}
    return sorted(names)


def check_bom() -> list[str]:
    """この Round で触った .cs が UTF-8 BOM付きかを確認する。

    BOM が無くてもコンパイルは通るため、検索しない限り気づけない。
    書き込みツールの多くは BOM を付けないので、毎回機械的に確認する。
    """
    failures = []
    targets = changed_files("*.cs")
    for name in targets:
        path = SUBMODULE_ROOT / name
        if not path.exists():
            continue
        if path.read_bytes()[:3] != BOM:
            failures.append(f"UTF-8 BOMがありません: {name}")

    if not failures:
        print(f"[bom] OK: {len(targets)}件")
    return failures


def check_meta_pairs() -> list[str]:
    """新規追加ファイルに .meta が対で存在するかを確認する。"""
    failures = []
    untracked = [
        name
        for name in submodule_git("ls-files", "-o", "--exclude-standard").splitlines()
        if name and not name.endswith(".meta")
    ]

    checked = 0
    for name in untracked:
        if name.startswith(META_EXEMPT_PREFIXES):
            continue
        checked += 1
        if not (SUBMODULE_ROOT / f"{name}.meta").exists():
            failures.append(f".metaがありません: {name}")

    if not failures:
        print(f"[meta] OK: {checked}件")
    return failures


def check_runtime_editor_references() -> list[str]:
    """Runtime と Core から UnityEditor を参照していないかを確認する。

    **この Round で触った .cs だけを対象にする。** パッケージには `#if UNITY_EDITOR` で
    囲んだ既存の例外（`Core/SymphonyConstant.cs`）が残っており、全体を毎回検査すると
    同じ既存違反を報告し続けることになる。読まれなくなる検査は無いのと同じである。

    Core/Editor/ は Editor 専用の共有基盤なので対象外にする。
    コメント行はコードではないため除外する。
    """
    failures = []
    checked = 0
    for name in changed_files("*.cs"):
        if not name.startswith(("Runtime/", "Core/")) or name.startswith("Core/Editor/"):
            continue

        path = SUBMODULE_ROOT / name
        if not path.exists():
            continue

        checked += 1
        for number, line in enumerate(
            path.read_text(encoding="utf-8", errors="replace").splitlines(), start=1
        ):
            if "UnityEditor" not in line and "EditorPrefs" not in line:
                continue
            if COMMENT_LINE_PATTERN.match(line):
                continue
            failures.append(f"UnityEditorを参照しています: {name}:{number}")

    if not failures:
        print(f"[layer] OK: Runtime/Coreの変更{checked}件にUnityEditor参照なし")
    return failures


def check_test_assembly_constraints() -> list[str]:
    """テスト用 asmdef に UNITY_INCLUDE_TESTS があるかを確認する。

    無いと nunit を参照するアセンブリが Player ビルドへ入る。
    """
    failures = []
    checked = 0
    for path in (SUBMODULE_ROOT / "Tests").rglob("*.asmdef"):
        checked += 1
        definition = json.loads(path.read_text(encoding="utf-8-sig"))
        if "UNITY_INCLUDE_TESTS" not in (definition.get("defineConstraints") or []):
            relative = path.relative_to(SUBMODULE_ROOT).as_posix()
            failures.append(f"UNITY_INCLUDE_TESTSがありません: {relative}")

    if not failures:
        print(f"[asmdef] OK: {checked}件")
    return failures


# --------------------------------------------------------------------- commit


def run_commit(args: argparse.Namespace) -> int:
    if not COMMIT_MESSAGE_PATTERN.match(args.message):
        print(
            "NG: コミットメッセージは'[add]'/'[update]'/'[fix]'で始まる1行にしてください。"
            f"\n  受け取った値: {args.message!r}"
        )
        return 2

    if not args.skip_preflight and not run_preflight():
        print("\nNG: 検証が通らなかったためコミットしません。")
        return 1

    print("\n== commit ==")
    if not submodule_git("status", "--porcelain"):
        print("NG: submoduleにコミットする変更がありません。")
        return 1

    submodule_git("add", "-A")

    body_lines = []
    if args.issue:
        body_lines.append(f"Issue: #{args.issue}")
    body_lines.append("Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>")
    message = args.message + "\n\n" + "\n".join(body_lines) + "\n"

    submodule_git("commit", "-m", message)
    head = submodule_git("rev-parse", "--short", "HEAD")
    print(f"[commit] OK: {head} {args.message}")

    branch = current_branch(SUBMODULE_ROOT)
    submodule_git("push", "-u", "origin", branch)
    print(f"[push] OK: origin/{branch}")

    if args.pr:
        return create_pull_request(args, branch)

    print("\n次: PRを作成し、マージしてから finalize を実行してください。")
    return 0


def create_pull_request(args: argparse.Namespace, branch: str) -> int:
    command = [
        "gh", "pr", "create",
        "--base", args.pr_base,
        "--head", branch,
        "--title", args.message,
    ]
    if args.pr_body_file:
        command += ["--body-file", args.pr_body_file]
    else:
        body = f"Issue: #{args.issue}\n" if args.issue else ""
        command += ["--body", body]

    completed = subprocess.run(
        command, cwd=SUBMODULE_ROOT, capture_output=True, text=True,
        encoding="utf-8", errors="replace",
    )
    if completed.returncode != 0:
        print(f"NG: PRの作成に失敗しました。\n{completed.stderr.strip()}")
        return 1

    print(f"[pr] OK: {completed.stdout.strip()}")
    print("\n次: PRをマージしてから finalize を実行してください。")
    return 0


# ------------------------------------------------------------------- finalize


def run_finalize(args: argparse.Namespace) -> int:
    print("== finalize ==")

    if submodule_git("status", "--porcelain"):
        print("NG: submoduleに未コミットの変更があります。先にコミットしてください。")
        return 1

    submodule_git("fetch", "origin", "--prune")
    head = submodule_git("rev-parse", "HEAD")

    # gitlink が develop から到達できることを確認する。ここを飛ばすと、
    # squash マージやブランチ削除で新規クローンの submodule 解決が壊れる。
    completed = subprocess.run(
        ["git", "merge-base", "--is-ancestor", head, INTEGRATION_BRANCH],
        cwd=SUBMODULE_ROOT, capture_output=True, text=True,
    )
    if completed.returncode != 0:
        print(
            f"NG: submoduleのHEAD({head[:7]})が{INTEGRATION_BRANCH}から到達できません。"
            "\n  PRのマージを待ってから再実行してください。"
        )
        return 1
    print(f"[gitlink] OK: {head[:7]} は {INTEGRATION_BRANCH} から到達可能")

    # 親リポジトリへは明示したパスだけを staging する。`git add -A` は使わない。
    paths = [SUBMODULE_PATH, *args.paths]
    git("add", "--", *paths)

    staged = git("diff", "--cached", "--name-only")
    if not staged:
        print("NG: 親リポジトリにコミットする変更がありません。")
        return 1
    print(f"[stage] OK:\n  " + "\n  ".join(staged.splitlines()))

    message = args.message or default_parent_message()
    git("commit", "-m", message)
    print(f"[commit] OK: {git('rev-parse', '--short', 'HEAD')} {message.splitlines()[0]}")

    if args.no_push:
        print("\n--no-push が指定されたためpushしていません。")
        return 0

    branch = args.parent_branch or current_branch(WORKSPACE_ROOT)
    git("push", "origin", branch)
    print(f"[push] OK: origin/{branch}")
    return 0


def default_parent_message() -> str:
    """submodule の直近の実作業コミットから親のメッセージを組み立てる。

    `--no-merges` を付けるのは、マージ後の HEAD がマージコミットになっており、
    そのままでは「Merge pull request #NNN from ...を取り込み」という
    意味を持たない件名になるためである。
    """
    subject = submodule_git("log", "-1", "--no-merges", "--format=%s")
    body = re.sub(r"^\[(add|update|fix)\]", "", subject)
    return (
        f"[update]{body}を取り込み\n\n"
        "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>\n"
    )


if __name__ == "__main__":
    try:
        sys.exit(main())
    except RuntimeError as error:
        print(f"NG: {error}")
        sys.exit(1)
