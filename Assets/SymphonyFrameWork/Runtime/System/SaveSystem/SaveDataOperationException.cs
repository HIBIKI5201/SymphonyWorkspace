using System;

namespace SymphonyFrameWork.System.SaveSystem
{
    /// <summary> セーブデータ操作がローダーまたは保存先のエラーで失敗した場合に発生する例外。 </summary>
    public sealed class SaveDataOperationException : Exception
    {
        /// <summary> 失敗した操作と対象を指定して例外を生成する。 </summary>
        /// <param name="operation"> 失敗した操作。 </param>
        /// <param name="dataType"> 操作対象のセーブデータ型。 </param>
        /// <param name="loaderType"> 操作に使用したローダー型。 </param>
        /// <param name="innerException"> 原因となった例外。 </param>
        public SaveDataOperationException(
            SaveDataOperation operation,
            Type dataType,
            Type loaderType,
            Exception innerException)
            : base(
                $"[{nameof(SaveDataRegistry)}] {dataType?.FullName ?? "(null)"} の{GetOperationName(operation)}に失敗しました。"
                + $" Loader: {loaderType?.FullName ?? "(null)"}",
                innerException)
        {
            Operation = operation;
            DataType = dataType;
            LoaderType = loaderType;
        }

        /// <summary> 失敗した操作。 </summary>
        public SaveDataOperation Operation { get; }

        /// <summary> 操作対象のセーブデータ型。 </summary>
        public Type DataType { get; }

        /// <summary> 操作に使用したローダー型。 </summary>
        public Type LoaderType { get; }

        /// <summary> 操作名を例外メッセージ向けの日本語へ変換する。 </summary>
        /// <param name="operation"> 変換する操作。 </param>
        /// <returns> 例外メッセージに使用する操作名。 </returns>
        private static string GetOperationName(SaveDataOperation operation)
        {
            return operation switch
            {
                SaveDataOperation.Exists => "存在確認",
                SaveDataOperation.Load => "読み込み",
                SaveDataOperation.Save => "保存",
                SaveDataOperation.Delete => "削除",
                _ => operation.ToString()
            };
        }
    }
}
