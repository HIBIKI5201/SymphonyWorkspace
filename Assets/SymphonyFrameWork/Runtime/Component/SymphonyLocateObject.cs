using SymphonyFrameWork.System.ServiceLocate;
using System.Threading;
using System.Threading.Tasks;

namespace SymphonyFrameWork.Utility
{
    /// <summary>
    ///     ServiceLocatorに登録されているインスタンスを保持する。
    /// </summary>
    public sealed class SymphonyLocateObject<T> where T : class
    {
        /// <summary>
        ///     インスタンスを初期化して生成する。
        /// </summary>
        public SymphonyLocateObject() => _instance = null;

        /// <summary>
        ///     初期インスタンスをセットして生成する。
        /// </summary>
        /// <param name="instance"> 初期キャッシュとして保持するインスタンス。 </param>
        public SymphonyLocateObject(T instance) => _instance = instance;

        /// <summary>
        ///     取得する。
        /// </summary>
        /// <returns> キャッシュまたはService Locatorから取得したインスタンス。 </returns>
        public T GetInstance()
        {
            T instance = _instance;

            // インスタンスがキャッシュされていなければ取得。
            if (instance == null)
            {
                instance = ServiceLocator.GetInstance<T>();
                _instance = instance; //キャッシュする。
            }

            return instance;
        }

        /// <summary>
        ///     非同期で取得する。
        /// </summary>
        /// <param name="grace"> 登録を待機する最大秒数。 </param>
        /// <param name="token"> 待機を中断するためのトークン。 </param>
        /// <returns> キャッシュまたは待機後に取得したインスタンス。 </returns>
        public async ValueTask<T> GetInstanceAsync(byte grace = 120, CancellationToken token = default)
        {
            T instance = _instance;

            // インスタンスがキャッシュされていなければ取得。
            if (instance == null)
            {
                instance = await ServiceLocator.GetInstanceAsync<T>(grace, token);
                _instance = instance; //キャッシュする。
            }

            return instance;
        }

        /// <summary>
        ///     インスタンスの取得を試みる。
        /// </summary>
        /// <param name="instance"> 取得できたインスタンス。 </param>
        /// <returns> インスタンスを取得できた場合はtrue。 </returns>
        public bool TryGetInstance(out T instance)
        {
            instance = _instance;

            // インスタンスがキャッシュされていなければ取得。
            if (instance == null && ServiceLocator.TryGetInstance(out instance))
            {
                _instance = instance; // キャッシュする。
            }

            return instance != null;
        }

        /// <summary> キャッシュされる値 </summary>
        private T _instance;
    }
}
