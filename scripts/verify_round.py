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
   1回では出ない。
4. **Enter Play Mode Options を元へ戻す。** PlayMode テストの実行は、この設定を
   3（Domain Reload と Scene Reload の両方を無効）から 1 へ書き戻す。**毎回必ず戻るのに、
   戻す操作は毎回手で叩いていた。** 検出したら Unity へ設定し直し、確定後の値で成否を見る。

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


# 進行中のコンパイルへ重ねて要求したときに返る文言。**コードのエラーではない。**
# 件数1のエラーとしてJSONに載るため、そのまま採用すると健全なコードを失敗と報告する。
COMPILE_BUSY_MESSAGE = "Compilation is already in progress"


def compile_project() -> dict:
    """コンパイルし、確定後の集計だけを採用する。

    force-recompile の直後は確定前の値が返るため、**必ずもう一度問い合わせる。**
    2回目は再コンパイルを起こさない（`--force-recompile false`）。

    大量のアセットを動かした直後などは、要求時点で既にコンパイルが走っていることがある。
    そのときの応答はコードのエラーではないため、収まるまで待って取り直す。
    """
    for _ in range(RELOAD_MAX_RETRIES):
        run_uloop("compile", "--force-recompile", "true")
        settled = run_uloop("compile", "--force-recompile", "false")

        if not is_compile_busy(settled):
            return {
                "errors": settled.get("ErrorCount", -1),
                "warnings": settled.get("WarningCount", -1),
                "errorList": settled.get("Errors") or [],
                "warningList": settled.get("Warnings") or [],
            }

        time.sleep(RELOAD_WAIT_SECONDS)

    raise VerifyError(
        f"コンパイルが{RELOAD_MAX_RETRIES * RELOAD_WAIT_SECONDS}秒たっても開始できません。"
    )


def is_compile_busy(result: dict) -> bool:
    """コンパイル結果が「既に進行中」の応答かを判定する。"""
    return any(
        COMPILE_BUSY_MESSAGE in str(entry.get("Message", entry))
        for entry in result.get("Errors") or []
    )


def run_tests(mode: str) -> dict:
    """指定モードのテストを実行する。

    **実行そのものを拒否された場合、uloop は件数0の成功形と同じ形で返す。**
    `Success` と `Message` を落とすと「テストが1件も実行されていません」としか分からず、
    理由の書かれた行が捨てられる。実際に「未保存のシーンがあるため実行できない」が
    この形で返り、原因の特定に往復を要した。
    """
    result = run_uloop("run-tests", "--test-mode", mode)
    return {
        "mode": mode,
        "total": result.get("TestCount", -1),
        "passed": result.get("PassedCount", -1),
        "failed": result.get("FailedCount", -1),
        "skipped": result.get("SkippedCount", -1),
        "failedTests": result.get("FailedTests") or [],
        # **`Success` はテストが1件でも落ちれば false になる。** 実行を拒否されたことの判定には
        # 使えないため、「拒否」は件数0と併せて判定する。落ちた分は下の件数チェックが拾う。
        "accepted": result.get("Success", True) or result.get("TestCount", 0) > 0,
        "message": result.get("Message") or "",
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


# 1=DisableDomainReload、2=DisableSceneReload。両方無効の 3 がこのプロジェクトの前提。
EXPECTED_ENTER_PLAY_MODE_OPTIONS = "3"

# Unity側で Enter Play Mode Options を前提値へ戻す。ファイルを直接書いても実行中のEditorが
# メモリ上の値で上書きするため、**Editorへ設定させてから SaveAssets で確定させる。**
RESTORE_ENTER_PLAY_MODE_OPTIONS_CODE = (
    "UnityEditor.EditorSettings.enterPlayModeOptionsEnabled = true;"
    " UnityEditor.EditorSettings.enterPlayModeOptions ="
    " UnityEditor.EnterPlayModeOptions.DisableDomainReload"
    " | UnityEditor.EnterPlayModeOptions.DisableSceneReload;"
    " UnityEditor.AssetDatabase.SaveAssets();"
    " return ((int)UnityEditor.EditorSettings.enterPlayModeOptions).ToString();"
)


def read_enter_play_mode_options() -> str:
    """`ProjectSettings/EditorSettings.asset` に記録されている値を読む。"""
    settings_path = WORKSPACE_ROOT / "ProjectSettings" / "EditorSettings.asset"
    text = settings_path.read_text(encoding="utf-8")
    options = re.search(r"^\s*m_EnterPlayModeOptions:\s*(\d+)", text, re.MULTILINE)

    return options.group(1) if options else "不明"


def check_enter_play_mode_options(skipped_playmode: bool) -> tuple[str, bool]:
    """Enter Play Mode Options が書き戻されていれば、その場で戻す。

    **PlayMode テストの実行で 3 から 1 へ戻る。** Console にもテスト結果にも出ないため、
    ここで見ないと気づけない。**毎回必ず戻るのに、戻す操作は毎回手で叩いていた。**
    検出したら Unity へ設定し直し、確定後の値で成否を判断する。

    人へ報告する意味があるのは「戻せなかった」場合だけなので、復元できたら成功として扱う。
    ただし何が起きたかは要約へ残す。
    """
    found = read_enter_play_mode_options()
    if found == EXPECTED_ENTER_PLAY_MODE_OPTIONS:
        return "OK", True

    # PlayMode を回していないのに崩れているなら、原因はこの検証の外にある。戻すだけにして報告する。
    cause = "PlayModeテストの実行で書き戻されています" if not skipped_playmode else "この検証の外で変更されています"

    result = run_uloop("execute-dynamic-code", "--code", RESTORE_ENTER_PLAY_MODE_OPTIONS_CODE, timeout=300)
    if not result.get("Success"):
        return (
            f"NG: Enter Play Mode Options が {found} です（期待は"
            f" {EXPECTED_ENTER_PLAY_MODE_OPTIONS}）。{cause}。"
            f" 自動復元に失敗しました: {result.get('ErrorMessage') or result.get('Error')}"
        ), False

    restored = read_enter_play_mode_options()
    if restored != EXPECTED_ENTER_PLAY_MODE_OPTIONS:
        return (
            f"NG: Enter Play Mode Options を復元しましたが、ファイル上は {restored} のままです。"
            " Unity側で確認してください。"
        ), False

    return f"復元: {found} → {restored}（{cause}）", True


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
        # 実行を拒否された場合は、その理由をそのまま出す。0件の報告より原因が分かる。
        if not result["accepted"]:
            result["note"] = f"テストを実行できませんでした: {result['message']}"
            summary["ok"] = False
            continue

        # **0件を成功として扱わない。** `failed == 0 and passed == total` は 0/0 でも成立するため、
        # テストが1件も走らなかった実行（アセンブリが読めていない、フィルタが効きすぎている等）を
        # 見逃す。実際に PlayMode が 0/0 のまま「成功」と報告された。
        if result["total"] <= 0:
            result["note"] = "テストが1件も実行されていません"
            summary["ok"] = False
            continue

        if result["failed"] != 0 or result["passed"] != result["total"]:
            summary["ok"] = False

    play_mode_options, play_mode_ok = check_enter_play_mode_options(args.skip_playmode)
    summary["enterPlayModeOptions"] = play_mode_options
    if not play_mode_ok:
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
        note = result.get("note")
        print(
            f"[{result['mode']}] {result['passed']}/{result['total']}"
            f"（失敗{result['failed']} / スキップ{result['skipped']}）"
            + (f" NG: {note}" if note else " 成功")
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
