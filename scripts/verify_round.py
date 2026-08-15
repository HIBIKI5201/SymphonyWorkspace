#!/usr/bin/env python3
"""Unity側の検証を1コマンドへまとめる。

`implement` スキルのステップ3で毎回同じ順序で叩いていた一連の uloop 呼び出しを、
1プロセスへ固定する。**手で叩くと往復が10回を超え、そのほとんどが待ちと再取得である。**

固定しているのは次の3点で、いずれも手順書に書いても抜ける類のものである。

1. **Domain Reload 中の応答を待って取り直す。** uloop は reload 中に
   「Unity is reloading」を返して終わる。手で叩くと sleep を挟んで再実行することになり、
   待ち時間の見積もりが毎回変わる。
2. **確定前の値を信用しない。** compile 直後の集計は確定前の値を返すことがあり、
   実際に「警告12件」が取り直すと0件になっている。compile は必ず2回問い合わせ、
   2回目の値だけを採用する。
3. **PlayMode を2往復させる。** Domain Reload 無効のため、static 状態のゴースト参照は
   1回では出ない。あわせて Enter Play Mode Options が書き戻されていないかも見る。

使い方:

    python scripts/verify_round.py                # 全部
    python scripts/verify_round.py --skip-playmode # EditModeまで
    python scripts/verify_round.py --json          # 機械可読な要約だけ
"""

from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

WORKSPACE_ROOT = Path(__file__).resolve().parents[1]


def resolve_npx() -> str:
    """npx の実体を解決する。

    **Windows の `npx` は `.cmd` シムで、`subprocess` は PATH 上の拡張子を補わない。**
    `shutil.which` に解決させ、見つからない場合だけ素の名前へ落とす。
    """
    return shutil.which("npx") or "npx"


ULOOP = [resolve_npx(), "--yes", "uloop-cli@2.2.0"]

# Domain Reload の待ち。1回あたりの間隔と、諦めるまでの回数。
RELOAD_WAIT_SECONDS = 10
RELOAD_MAX_RETRIES = 18

# uloop が結果を返せない一時状態。**どちらもJSONを返さず終了する。**
# 「reloading」だけを見ていると、コンパイル中に来た応答をパース失敗として報告してしまう。
BUSY_MARKERS = ("Unity is reloading", "Unity is compiling")


class VerifyError(RuntimeError):
    """検証を続行できない状態を表す。"""


def run_uloop(*arguments: str, timeout: int = 900) -> dict:
    """uloop を実行し、Domain Reload 中なら収まるまで待って取り直す。

    **戻り値のJSONだけを返す。** uloop は JSON の前後に npm の通知を混ぜるため、
    最初の `{` から最後の `}` までを取り出す。
    """
    for attempt in range(RELOAD_MAX_RETRIES):
        completed = subprocess.run(
            [*ULOOP, *arguments],
            cwd=WORKSPACE_ROOT,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout,
        )
        output = f"{completed.stdout}\n{completed.stderr}"

        # reload中・コンパイル中は結果が返らない。待って同じ要求をやり直す。
        if any(marker in output for marker in BUSY_MARKERS):
            time.sleep(RELOAD_WAIT_SECONDS)
            continue

        match = re.search(r"\{.*\}", output, re.S)
        if match is None:
            raise VerifyError(
                f"uloopの応答をJSONとして読めません: {' '.join(arguments)}\n{output.strip()[:800]}"
            )

        return json.loads(match.group(0))

    raise VerifyError(
        f"Domain Reloadが{RELOAD_MAX_RETRIES * RELOAD_WAIT_SECONDS}秒たっても終わりません:"
        f" {' '.join(arguments)}"
    )


def clear_console() -> None:
    """古いログが今回の結果を隠さないよう、先に消す。"""
    run_uloop("clear-console", timeout=300)


def compile_project() -> dict:
    """コンパイルし、確定後の集計だけを採用する。

    force-recompile の直後は確定前の値が返るため、**必ずもう一度問い合わせる。**
    2回目は再コンパイルを起こさない（`--force-recompile false`）。
    """
    run_uloop("compile", "--force-recompile", "true")
    settled = run_uloop("compile", "--force-recompile", "false")
    return {
        "errors": settled.get("ErrorCount", -1),
        "warnings": settled.get("WarningCount", -1),
        "errorList": settled.get("Errors") or [],
        "warningList": settled.get("Warnings") or [],
    }


def run_tests(mode: str) -> dict:
    """指定モードのテストを実行する。"""
    result = run_uloop("run-tests", "--test-mode", mode)
    return {
        "mode": mode,
        "total": result.get("TestCount", -1),
        "passed": result.get("PassedCount", -1),
        "failed": result.get("FailedCount", -1),
        "skipped": result.get("SkippedCount", -1),
        "failedTests": result.get("FailedTests") or [],
    }


def console_problems() -> dict:
    """Console に残ったエラーと警告を数える。

    **テストを走らせる前に呼ぶこと。** 隔離を検証するテストは `LogAssert.Expect` と対で
    意図的に例外ログを出すため、テスト後に数えると期待どおりの動作を失敗として報告する。
    実際に「subscriber failure」「view failure」を検証している既存テストがある。
    テスト自体の成否は `run_tests` の結果で見る。
    """
    result = run_uloop("get-logs", "--log-type", "All", "--max-count", "200", timeout=300)
    errors = []
    warnings = []
    for entry in result.get("Logs") or []:
        if entry.get("Type") == "Error":
            errors.append(entry.get("Message", ""))
        elif entry.get("Type") == "Warning":
            warnings.append(entry.get("Message", ""))

    return {"errors": errors, "warnings": warnings}


def check_enter_play_mode_options() -> str | None:
    """Enter Play Mode Options が書き戻されていないかを見る。

    **PlayMode テストの実行で 3 から 1 へ戻る。** Console にもテスト結果にも出ないため、
    ここで見ないと気づけない。
    """
    settings_path = WORKSPACE_ROOT / "ProjectSettings" / "EditorSettings.asset"
    text = settings_path.read_text(encoding="utf-8")
    options = re.search(r"^\s*m_EnterPlayModeOptions:\s*(\d+)", text, re.MULTILINE)

    # 1=DisableDomainReload、2=DisableSceneReload。両方無効の 3 が前提。
    if options is None or options.group(1) != "3":
        found = options.group(1) if options else "不明"
        return (
            f"Enter Play Mode Options が {found} です（期待は 3）。"
            " PlayModeテストの実行で書き戻されています。Unity側で戻してください。"
        )

    return None


def build_summary(args: argparse.Namespace) -> dict:
    """検証を順に実行し、結果をまとめる。"""
    summary: dict = {"ok": True, "steps": []}

    clear_console()

    compile_result = compile_project()
    summary["compile"] = compile_result
    if compile_result["errors"] != 0 or compile_result["warnings"] != 0:
        summary["ok"] = False

    # テストが意図的に出す例外ログと混ざらないよう、テストを走らせる前に見る。
    problems = console_problems()
    summary["console"] = problems
    if problems["errors"]:
        summary["ok"] = False

    test_results = []
    if not args.skip_editmode:
        test_results.append(run_tests("EditMode"))

    # Domain Reload 無効のため、staticのゴースト参照は1往復では出ない。
    if not args.skip_playmode:
        for _ in range(args.playmode_cycles):
            test_results.append(run_tests("PlayMode"))

    summary["tests"] = test_results
    for result in test_results:
        if result["failed"] != 0 or result["passed"] != result["total"]:
            summary["ok"] = False

    play_mode_issue = check_enter_play_mode_options()
    summary["enterPlayModeOptions"] = play_mode_issue or "OK"
    if play_mode_issue:
        summary["ok"] = False

    return summary


def print_summary(summary: dict) -> None:
    """人が読む形で結果を出す。"""
    print("== verify ==")

    compile_result = summary["compile"]
    print(f"[compile] エラー{compile_result['errors']}件 / 警告{compile_result['warnings']}件")
    for entry in compile_result["errorList"][:10]:
        print(f"  ERROR: {entry}")
    for entry in compile_result["warningList"][:10]:
        print(f"  WARN : {entry}")

    console = summary["console"]
    print(
        f"[console] コンパイル後 エラー{len(console['errors'])}件"
        f" / 警告{len(console['warnings'])}件（テスト実行前の時点）"
    )
    for message in console["errors"][:10]:
        print(f"  ERROR: {message.splitlines()[0][:160]}")

    for result in summary["tests"]:
        print(
            f"[{result['mode']}] {result['passed']}/{result['total']} 成功"
            f"（失敗{result['failed']} / スキップ{result['skipped']}）"
        )
        for failed in result["failedTests"][:10]:
            print(f"  FAILED: {failed}")

    print(f"[playmode] {summary['enterPlayModeOptions']}")

    print("")
    print("OK: 検証を通過しました。" if summary["ok"] else "NG: 検証に失敗があります。")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--skip-editmode", action="store_true", help="EditModeテストを飛ばす")
    parser.add_argument("--skip-playmode", action="store_true", help="PlayModeテストを飛ばす")
    parser.add_argument(
        "--playmode-cycles",
        type=int,
        default=2,
        help="PlayModeテストの往復回数（既定2。Domain Reload無効のため1回では足りない）",
    )
    parser.add_argument("--json", action="store_true", help="要約をJSONで出す")
    args = parser.parse_args()

    try:
        summary = build_summary(args)
    except VerifyError as error:
        print(f"NG: {error}")
        return 2
    except subprocess.TimeoutExpired as error:
        print(f"NG: uloopがタイムアウトしました: {error}")
        return 2

    if args.json:
        print(json.dumps(summary, ensure_ascii=False, indent=2))
    else:
        print_summary(summary)

    return 0 if summary["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
