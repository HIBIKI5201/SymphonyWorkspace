using System;

namespace SymphonyFrameWork.Core
{
    /// <summary>
    ///     UnityEngine.Objectを遅延生成し、破棄後は自動で生成し直す参照を保持する。
    ///     System.Lazy&lt;T&gt;はUnity側での破棄を検知できず、
    ///     Domain Reloadを無効にした再生では破棄済みインスタンスを保持したままになるため、
    ///     フレームワーク内でUnityEngine.Objectを遅延生成する場合はこのクラスを使用する。
    /// </summary>
    /// <typeparam name="T"> 遅延生成するUnityEngine.Objectの型。 </typeparam>
    internal sealed class SymphonyLazyObject<T> where T : UnityEngine.Object
    {
        /// <summary>
        ///     生成処理と破棄処理を指定して初期化する。
        /// </summary>
        /// <param name="factory"> 対象を生成する処理。 </param>
        /// <param name="destroyer">
        ///     対象を破棄する処理。
        ///     ComponentのようにGameObjectごと破棄したい場合に指定する。
        ///     省略した場合はObject.Destroyで対象自身のみを破棄する。
        /// </param>
        public SymphonyLazyObject(Func<T> factory, Action<T> destroyer = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _destroyer = destroyer;
        }

        /// <summary> 生成済みで、かつ破棄されていない場合はtrue。 </summary>
        public bool IsAlive => _instance;

        /// <summary> 生成済みの対象。未生成または破棄済みの場合は生成し直す。 </summary>
        public T Value
        {
            get
            {
                // 破棄済みのUnityEngine.Objectはbool変換でfalseになるため、生成し直す。
                if (!_instance)
                {
                    _instance = _factory();
                }

                return _instance;
            }
        }

        /// <summary>
        ///     生成せずに現在の対象を取得する。
        /// </summary>
        /// <param name="value"> 生存している場合は対象、それ以外はnull。 </param>
        /// <returns> 生存している対象を取得できた場合はtrue。 </returns>
        public bool TryGetValue(out T value)
        {
            bool isAlive = _instance;
            value = isAlive ? _instance : null;
            return isAlive;
        }

        /// <summary>
        ///     生成済みの対象を破棄し、未生成の状態へ戻す。
        ///     破棄済み、または未生成の場合は何もしない。
        /// </summary>
        public void Destroy()
        {
            if (_instance)
            {
                if (_destroyer != null)
                {
                    _destroyer(_instance);
                }
                else
                {
                    UnityEngine.Object.Destroy(_instance);
                }
            }

            _instance = null;
        }

        private readonly Func<T> _factory;
        private readonly Action<T> _destroyer;

        private T _instance;
    }
}
