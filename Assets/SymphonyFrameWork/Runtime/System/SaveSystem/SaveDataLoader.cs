using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SymphonyFrameWork.System.SaveSystem
{
    /// <summary>
    ///     セーブデータのライフサイクルを保証し、派生クラスをJSONの変換と永続化処理に限定します。
    ///     利用側は本クラスを継承して保存先を差し替えますが、呼び出しは
    ///     <see cref="SaveDataRegistry" />が行うため、ここで宣言する操作は`internal`です。
    /// </summary>
    [Serializable]
    public abstract class SaveDataLoader
    {
        /// <summary> 指定した型の永続化データが存在するか確認する。 </summary>
        /// <param name="dataType"> 確認するセーブデータ型。 </param>
        /// <returns> 永続化データが存在する場合はtrue。 </returns>
        internal bool Exists(Type dataType)
        {
            ValidateDataType(dataType);
            return ExistsCore(dataType);
        }

        /// <summary> 永続化されたJSONを指定インスタンスへ復元する。 </summary>
        /// <param name="dataType"> 復元するセーブデータ型。 </param>
        /// <param name="data"> 復元結果を上書きするインスタンス。 </param>
        /// <param name="token"> 処理を中断するためのトークン。 </param>
        internal async ValueTask LoadAsync(
            Type dataType,
            SaveDataContent data,
            CancellationToken token = default)
        {
            Validate(dataType, data, token);
            string json = await LoadJsonAsync(dataType, token);

            if (string.IsNullOrEmpty(json))
            {
                ResetToDefault(dataType, data);
                Debug.Log($"[{GetType().Name}]\n{dataType.Name} のデータが見つからないので生成しました。");
                return;
            }

            try
            {
                ResetToDefault(dataType, data);
                OverwriteFromJson(dataType, json, data);
            }
            catch (Exception ex)
            {
                ResetToDefault(dataType, data);
                Debug.LogWarning(
                    $"[{GetType().Name}]\n{dataType.Name} の本体データ復元に失敗しました。新たなインスタンス状態へ戻します。\n{ex.Message}");
            }
        }

        /// <summary> 指定インスタンスをJSONへ変換して永続化する。 </summary>
        /// <param name="dataType"> 保存するセーブデータ型。 </param>
        /// <param name="data"> 保存するインスタンス。 </param>
        /// <param name="token"> 処理を中断するためのトークン。 </param>
        internal async ValueTask SaveAsync(
            Type dataType,
            SaveDataContent data,
            CancellationToken token = default)
        {
            Validate(dataType, data, token);

            string previousSaveDate = data.SaveDate;
            data.UpdateSaveDate();

            try
            {
                string json = SerializeToJson(dataType, data);
                await SaveJsonAsync(dataType, json, token);
            }
            catch
            {
                data.SaveDate = previousSaveDate;
                throw;
            }

            Debug.Log($"[{GetType().Name}]\nデータをセーブしました。 date : {data.SaveDate}\n{data}");
        }

        /// <summary> 指定した型の永続化データを削除する。 </summary>
        /// <param name="dataType"> 削除するセーブデータ型。 </param>
        /// <param name="token"> 処理を中断するためのトークン。 </param>
        internal ValueTask DeleteAsync(Type dataType, CancellationToken token = default)
        {
            ValidateDataType(dataType);
            token.ThrowIfCancellationRequested();
            return DeleteCoreAsync(dataType, token);
        }

        /// <summary> 派生ローダー固有の保存先にデータが存在するか確認する。 </summary>
        protected abstract bool ExistsCore(Type dataType);

        /// <summary> 派生ローダー固有の保存先からJSONを読み込む。 </summary>
        protected abstract ValueTask<string> LoadJsonAsync(Type dataType, CancellationToken token);

        /// <summary> 派生ローダー固有の保存先へJSONを書き込む。 </summary>
        protected abstract ValueTask SaveJsonAsync(Type dataType, string json, CancellationToken token);

        /// <summary> 派生ローダー固有の保存先からデータを削除する。 </summary>
        protected abstract ValueTask DeleteCoreAsync(Type dataType, CancellationToken token);

        /// <summary> 指定インスタンスを派生ローダーのJSON形式へ変換する。 </summary>
        protected abstract string SerializeToJson(Type dataType, SaveDataContent data);

        /// <summary> JSONの内容を既存のセーブデータへ上書きする。 </summary>
        protected abstract void OverwriteFromJson(Type dataType, string json, SaveDataContent data);

        /// <summary> 対象インスタンスを指定型のデフォルト状態へ戻す。 </summary>
        private void ResetToDefault(Type dataType, SaveDataContent target)
        {
            SaveDataContent defaultData = (SaveDataContent)Activator.CreateInstance(dataType);

            try
            {
                string defaultJson = SerializeToJson(dataType, defaultData);
                OverwriteFromJson(dataType, defaultJson, target);
                target.ClearSaveDate();
            }
            finally
            {
                defaultData.Dispose();
            }
        }

        /// <summary> セーブデータ型、インスタンス、キャンセル状態を検証する。 </summary>
        private static void Validate(Type dataType, SaveDataContent data, CancellationToken token)
        {
            ValidateDataType(dataType);

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (!dataType.IsInstanceOfType(data))
            {
                throw new ArgumentException($"{dataType.Name} のインスタンスを指定してください。", nameof(data));
            }

            token.ThrowIfCancellationRequested();
        }

        /// <summary> セーブ対象として生成可能な具象型であることを検証する。 </summary>
        private static void ValidateDataType(Type dataType)
        {
            if (dataType == null)
            {
                throw new ArgumentNullException(nameof(dataType));
            }

            if (!dataType.IsClass
                || dataType.IsAbstract
                || dataType.IsGenericTypeDefinition
                || dataType.GetConstructor(Type.EmptyTypes) == null
                || !typeof(SaveDataContent).IsAssignableFrom(dataType))
            {
                throw new ArgumentException(
                    $"セーブ対象は {nameof(SaveDataContent)} を継承したデフォルトコンストラクタ付き具象クラスにしてください。",
                    nameof(dataType));
            }
        }
    }
}
