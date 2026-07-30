using System.Runtime.CompilerServices;

// Core/Internal/ のヘルパーはフレームワーク専用のため internal のままにするが、
// アセンブリが分かれているRuntimeとEditorから利用できるようフレンド指定する。
[assembly: InternalsVisibleTo("SymphonyFrameWork")]
[assembly: InternalsVisibleTo("SymphonyFrameWork.Editor")]
