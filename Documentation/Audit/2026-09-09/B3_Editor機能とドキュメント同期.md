# B3. Editor機能とドキュメント同期

Editor配下11ディレクトリ、`EditorTools.md`の一覧、各モジュール文書を突き合わせた。

## 【確定】Administratorのパネル数が本文と一覧で食い違う

**場所**: [EditorTools.md:86](../../../Assets/SymphonyFrameWork/Documentation~/EditorTools.md#L86)

本文は「6つのパネル」と書くが、直前の表にはService Locate、Scene Load、Scene Block、Save Data、Pause、Debug HUD、Auto Enum Generatorの7件がある。

**修正方針**: 固定件数を7へ直すか、表を正本として「各パネル」に言い換える。
