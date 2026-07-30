using UnityEngine;

namespace SymphonyFrameWork
{
    /// <summary>
    ///     コンポーネントに関するユーティリティクラス。
    /// </summary>
    public static class SymphonyComponentUtil
    {
        /// <summary>
        ///     コンポーネントを取得、なければ追加します。
        /// </summary>
        /// <typeparam name="T"> 取得または追加するComponentの型。 </typeparam>
        /// <param name="gameObject"> Componentを検索するGameObject。 </param>
        /// <returns> 既存または新たに追加したComponent。 </returns>
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }
            return component;
        }

        /// <summary>
        ///     自身を除く子オブジェクトからコンポーネントを取得します。
        /// </summary>
        /// <typeparam name="T"> 検索するComponentの型。 </typeparam>
        /// <param name="self"> 検索起点から除外するTransform。 </param>
        /// <param name="includeInactive"> 非アクティブな子も検索する場合はtrue。 </param>
        /// <returns> 子階層で最初に見つかったComponent。 </returns>
        public static T GetComponentInChildrenExcludeSelf<T>(this Transform self,
            bool includeInactive = false) 
            where T : Component
        {
            // Transformを直接たどる方が明確
            foreach (Transform child in self)
            {
                // 子オブジェクトからコンポーネントを検索する。
                T component = child.GetComponentInChildren<T>(includeInactive);

                if (component != null)
                {
                    return component;
                }
            }

            // 見つからなければnull。
            return null;
        }

        /// <summary>
        ///     親を辿ってコンポーネントを取得します。
        /// </summary>
        /// <typeparam name="T"> 検索するComponentの型。 </typeparam>
        /// <param name="transform"> 親方向への検索を開始するTransform。 </param>
        /// <returns> 親階層で最初に見つかったComponent。 </returns>
        public static T GetComponentInParents<T>(this Transform transform)
            where T : Component
        {
            Transform parent = transform.parent;

            // 親を辿ってコンポーネントを探す。
            while (parent != null)
            {
                T component = parent.GetComponent<T>();
                if (component != null)
                {
                    return component; // 見つかったら返す。
                }

                parent = parent.parent; // 次の親へ移動。
            }

            return null;
        }
    }
}
