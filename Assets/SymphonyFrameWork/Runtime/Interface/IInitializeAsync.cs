using System.Threading.Tasks;

namespace SymphonyFrameWork
{
    /// <summary>
    ///     非同期で初期化するインターフェース
    /// </summary>
    public interface IInitializeAsync
    {
        /// <summary> 実行中または完了済みの初期化処理。 </summary>
        public Task InitializeTask { get; protected set; }

        /// <summary> 初期化処理が完了しているかを示す。 </summary>
        public bool IsDone => InitializeTask != null ? InitializeTask.IsCompleted : false;

        /// <summary>
        ///     初期化を開始する
        /// </summary>
        /// <returns> 初期化処理を表すTask。 </returns>
        public async Task DoInitialize()
        {
            if (IsDone) return; //再初期化をブロック

            InitializeTask = InitializeAsync();
            await InitializeTask;

            InitializeTask = Task.CompletedTask; //軽量なTaskに変更
        }

        /// <summary>
        ///     初期化のフローを実装する
        /// </summary>
        /// <returns> 実装固有の初期化処理を表すTask。 </returns>
        protected Task InitializeAsync();
    }
}
