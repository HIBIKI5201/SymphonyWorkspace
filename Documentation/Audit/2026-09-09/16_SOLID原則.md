# 16. SOLID（特にSRP）

300行超21ファイル、60行超13メソッドを責務単位で読解した。

## 【設計指摘・高】SaveDataWindowが1,387行の単一ファイルである

**場所**: [SaveDataWindow.cs:18](../../../Assets/SymphonyFrameWork/Editor/Administrator/UITK/CS/SaveDataWindow.cs#L18)

詳細と分割単位は[観点15](15_クラス種別と責務の一致.md)。

## 【設計指摘】SymphonyAwaitableが1,081行の公開ユーティリティである

**場所**: [SymphonyAwaitable.cs:18](../../../Assets/SymphonyFrameWork/Runtime/Utility/SymphonyAwaitable.cs#L18)

完了値、WhenAll、条件待機、timeout、Task bridge、キャンセル所有が1ファイルにある。現行ガイドラインは大きい公開エントリポイントを`partial`で関心事別ファイルへ分割すると明記する。

**修正方針**: 公開型とシグネチャを変えず、`.Composition.cs`、`.Timeout.cs`、`.TaskBridge.cs`等へ分割する。

## 【設計指摘】公開Facade 3型が関心事別ファイル分割の基準を超えている

**場所**: [PauseManager.cs:1](../../../Assets/SymphonyFrameWork/Runtime/Service/Pause/PauseManager.cs#L1)、[SceneLoader.cs:1](../../../Assets/SymphonyFrameWork/Runtime/Service/SceneLoader/SceneLoader.cs#L1)、[ServiceLocator.cs:1](../../../Assets/SymphonyFrameWork/Runtime/Service/ServiceLocator/ServiceLocator.cs#L1)

676／528／743行。公開型を増やさず、操作群ごとの`partial`分割が規約に沿う。内部Serviceは型分割できるが、行数だけを理由に分けない。

