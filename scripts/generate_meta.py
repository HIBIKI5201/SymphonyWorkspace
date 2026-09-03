#!/usr/bin/env python3
"""Unity Editor が無い環境で、新規ファイルの `.meta` を生成する。

**この手段は Unity Editor へフォーカスを当てられない実行環境（リモートコンテナなど）
専用の例外である。** Unity が使えるなら Editor に生成させるほうが常に正しい
（→ `Documentation/CONTRIBUTING.md` §2）。

`release_round.py preflight` は `.meta` の欠落で必ず止まるため、Unity が無い環境では
ここを人が手で埋めることになる。手で書くと次の3つが抜ける。

1. **既存アセットの GUID を作り直してしまう。** 一度でも `.meta` が履歴にあるファイルは、
   利用側プロジェクトの参照とシリアライズ済みデータがその GUID で繋がっている。
   新しい値を振ると参照が切れる。**このスクリプトは履歴に `.meta` を持つパスを拒否する。**
2. **GUID が重複する。** 手で数字を並べると衝突に気づけない。生成後に必ず全走査する。
3. **拡張子ごとの importer ブロックが違う。** `.cs` は MonoImporter、`.asset` は
   NativeFormatImporter、フォルダは `folderAsset: yes` を伴う DefaultImporter である。

使い方:

    python scripts/generate_meta.py --check     # 欠落を一覧するだけ。生成しない
    python scripts/generate_meta.py             # 生成する
    python scripts/generate_meta.py --root Assets/SymphonyFrameWork

Exit codes
----------
0 : 欠落が無い、または生成に成功した
1 : `--check` で欠落を検出した、または生成を拒否・中断した
2 : 引数の誤り
"""

from __future__ import annotations

import argparse
import subprocess
import sys
import uuid
from collections import defaultdict
from pathlib import Path

WORKSPACE_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_ROOT = "Assets/SymphonyFrameWork"

# Unity が Asset Import の対象にしないもの。`.meta` は不要で、作ると逆に汚れる。
# 先頭が `.`、末尾が `~`、`cvs`（大小無視）、`.tmp` は Unity 自身の除外規則である。
EXCLUDED_DIR_NAMES = frozenset({"cvs"})
EXCLUDED_SUFFIXES = (".tmp", ".meta")

# `~` で終わるが、**中身は** `.meta` を必要とするディレクトリ。
# **`Samples~` 自身は `.meta` を持たない**（`~` は Unity に取り込ませないための印である）が、
# 中のファイルとフォルダは持つ。Package Manager がサンプル導入時に `.meta` ごとコピーし、
# 利用側で GUID を引き継ぐためである。実際に既存のサンプルもその形になっている。
# `release_round.py` の preflight も `Documentation~/` だけを免除しており、そこと揃える。
META_REQUIRED_TILDE_DIRS = frozenset({"Samples~"})

# 拡張子ごとの importer ブロック。**末尾の空白は Unity の出力そのままで、消さない。**
# 既存の `.meta` と diff を取ったときに、内容の違いではなく整形の違いで差分が出るのを防ぐ。
MONO_IMPORTER = """MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

DEFAULT_IMPORTER = """DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_IMPORTER = """folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

NATIVE_FORMAT_IMPORTER = """NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""

TEXT_SCRIPT_IMPORTER = """TextScriptImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

ASSEMBLY_DEFINITION_IMPORTER = """AssemblyDefinitionImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

# UIToolkit の `.uxml` / `.uss` は ScriptedImporter で、内蔵 importer の fileID が異なる。
UXML_IMPORTER = """ScriptedImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 2
  userData:
  assetBundleName:
  assetBundleVariant:
  script: {fileID: 13804, guid: 0000000000000000e000000000000000, type: 0}
"""

USS_IMPORTER = """ScriptedImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 2
  userData:
  assetBundleName:
  assetBundleVariant:
  script: {fileID: 12385, guid: 0000000000000000e000000000000000, type: 0}
  disableValidation: 0
"""

IMPORTER_BY_SUFFIX = {
    ".cs": MONO_IMPORTER,
    ".asmdef": ASSEMBLY_DEFINITION_IMPORTER,
    ".asmref": ASSEMBLY_DEFINITION_IMPORTER,
    ".asset": NATIVE_FORMAT_IMPORTER,
    ".preset": NATIVE_FORMAT_IMPORTER,
    ".uxml": UXML_IMPORTER,
    ".uss": USS_IMPORTER,
    ".md": TEXT_SCRIPT_IMPORTER,
    ".json": TEXT_SCRIPT_IMPORTER,
    ".txt": TEXT_SCRIPT_IMPORTER,
    ".xml": TEXT_SCRIPT_IMPORTER,
}

# 対応表に無い拡張子は DefaultImporter で作る。**Unity が後で正しい importer へ書き換える。**
# GUID さえ確定していれば参照は繋がるため、ここで拡張子を網羅する必要はない。
FALLBACK_IMPORTER = DEFAULT_IMPORTER


def run_git(*arguments: str, cwd: Path) -> subprocess.CompletedProcess:
    """git を実行する。失敗しても例外にせず、呼び出し側が returncode を見る。"""
    return subprocess.run(
        ["git", *arguments],
        cwd=cwd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )


def is_excluded(name: str) -> bool:
    """Unity が Asset Import の対象にしない名前かを判定する。"""
    if name.startswith("."):
        return True
    if name.endswith("~"):
        return True
    if name.lower() in EXCLUDED_DIR_NAMES:
        return True
    if any(name.endswith(suffix) for suffix in EXCLUDED_SUFFIXES):
        return True
    return False


def is_traversable(name: str) -> bool:
    """中を走査してよい名前かを判定する。

    **除外されるディレクトリでも、中身が `.meta` を必要とする場合がある。**
    `Samples~` は自身が `.meta` を持たない一方、中のファイルは持つ。
    """
    if name in META_REQUIRED_TILDE_DIRS:
        return True

    return not is_excluded(name)


def collect_import_targets(root: Path) -> list[Path]:
    """`root` 配下で `.meta` を持つべきパスを、ファイルとフォルダの両方について集める。"""
    targets: list[Path] = []
    for path in sorted(root.rglob("*")):
        parts = path.relative_to(root).parts

        # 走査できない親を持つパスは、その中身ごと対象から外す。
        if any(not is_traversable(name) for name in parts[:-1]):
            continue

        # 自身が除外対象なら、走査は続けても `.meta` は要らない。
        if is_excluded(path.name):
            continue

        targets.append(path)
    return targets


def has_meta_in_history(relative_path: str, repository_root: Path) -> bool:
    """そのパスの `.meta` が HEAD に存在するかを見る。

    **存在するなら GUID は既に配布済みであり、作り直してはいけない。**
    作業ツリーから消えているだけなので、`git checkout` で戻すのが正しい。
    """
    completed = run_git("cat-file", "-e", f"HEAD:{relative_path}.meta", cwd=repository_root)
    return completed.returncode == 0


def find_repository_root(path: Path) -> Path:
    """`path` が属する git リポジトリのルートを返す。submodule なら submodule 側を返す。"""
    completed = run_git("rev-parse", "--show-toplevel", cwd=path)
    if completed.returncode != 0:
        return path
    return Path(completed.stdout.strip())


def collect_existing_guids(root: Path) -> dict[str, list[Path]]:
    """`root` 配下の全 `.meta` から GUID を読み、値ごとに出現箇所を集める。"""
    guids: dict[str, list[Path]] = defaultdict(list)
    for meta_path in root.rglob("*.meta"):
        try:
            for line in meta_path.read_text(encoding="utf-8", errors="replace").splitlines():
                if line.startswith("guid: "):
                    guids[line[len("guid: "):].strip()].append(meta_path)
                    break
        except OSError:
            continue
    return guids


def build_meta_content(target: Path, guid: str) -> str:
    """`target` の種別に応じた `.meta` の中身を組み立てる。"""
    if target.is_dir():
        body = FOLDER_IMPORTER
    else:
        body = IMPORTER_BY_SUFFIX.get(target.suffix.lower(), FALLBACK_IMPORTER)
    return f"fileFormatVersion: 2\nguid: {guid}\n{body}"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument(
        "--root",
        default=DEFAULT_ROOT,
        help=f"走査するディレクトリ。ワークスペースからの相対パス（既定: {DEFAULT_ROOT}）",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="欠落を一覧するだけで生成しない。欠落があれば exit 1",
    )
    arguments = parser.parse_args()

    root = (WORKSPACE_ROOT / arguments.root).resolve()
    if not root.is_dir():
        print(f"NG: ディレクトリが見つかりません: {root}")
        return 2

    repository_root = find_repository_root(root)
    targets = collect_import_targets(root)
    missing = [target for target in targets if not Path(f"{target}.meta").exists()]

    if not missing:
        print(f"OK: `.meta` の欠落はありません（{len(targets)}件を走査）")
        return 0

    # 履歴に `.meta` を持つパスは、GUID を作り直さず復元させる。
    protected: list[Path] = []
    creatable: list[Path] = []
    for target in missing:
        relative_path = target.relative_to(repository_root).as_posix()
        if has_meta_in_history(relative_path, repository_root):
            protected.append(target)
        else:
            creatable.append(target)

    if protected:
        print("NG: 履歴に `.meta` があるパスの `.meta` が作業ツリーから消えています。")
        print("    GUIDを作り直すと利用側の参照が切れます。次で復元してください。")
        for target in protected:
            relative_path = target.relative_to(repository_root).as_posix()
            print(f"    git -C {repository_root} checkout -- '{relative_path}.meta'")
        return 1

    if arguments.check:
        print(f"NG: `.meta` が欠落しています（{len(creatable)}件）")
        for target in creatable:
            print(f"    {target.relative_to(WORKSPACE_ROOT).as_posix()}")
        return 1

    existing_guids = collect_existing_guids(root)
    used = set(existing_guids)
    created: list[tuple[Path, str]] = []
    for target in creatable:
        guid = uuid.uuid4().hex
        while guid in used:
            guid = uuid.uuid4().hex
        used.add(guid)
        Path(f"{target}.meta").write_text(
            build_meta_content(target, guid), encoding="utf-8", newline="\n"
        )
        created.append((target, guid))

    duplicated = {
        guid: paths for guid, paths in collect_existing_guids(root).items() if len(paths) > 1
    }
    if duplicated:
        print("NG: GUIDが重複しています。生成した `.meta` を破棄して調べ直してください。")
        for guid, paths in duplicated.items():
            print(f"    {guid}")
            for path in paths:
                print(f"        {path.relative_to(WORKSPACE_ROOT).as_posix()}")
        return 1

    print(f"OK: `.meta` を {len(created)}件生成しました。GUIDの重複はありません。")
    print("    Unity Editor が無い環境の例外です。**PR説明とコミット報告へその旨を明記し、**")
    print("    後でUnityが再生成・差分を出さないことを依頼者の確認項目として提示してください。")
    for target, guid in created:
        print(f"    {target.relative_to(WORKSPACE_ROOT).as_posix()}.meta  {guid}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
