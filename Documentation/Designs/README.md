# Designs

SymphonyFramework 本体へ機能を追加・変更する前に書く設計書の置き場です。

- 1機能につき1ファイル。ファイル名は PascalCase（例: `EventBus.md`、`SceneTransitionEffect.md`）。
- 実装後も**設計判断の記録として残します**。実装が終わったら消す、という運用はしません。
- 設計と実装が食い違ったまま放置しない。実装側を直すか、設計書を現状に合わせて更新します。

構成と書き方、および設計書を起点とした実装フロー（設計書 → ワーカーによる実装 → 確認 → バージョン更新 → コミット）は [`.agents/skills/implement/SKILL.md`](../../.agents/skills/implement/SKILL.md) にあります。Claude Code、Codex、Gemini CLI で同じ skill を利用できます。

判断基準は [DesignPhilosophy.md](../DesignPhilosophy.md)（レイヤー・依存方向・公開範囲・バージョニング）と [CodeGuidelines.md](../CodeGuidelines.md)（名前空間とフォルダの対応）を参照してください。
