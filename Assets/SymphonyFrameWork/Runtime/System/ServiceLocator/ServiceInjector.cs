using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace SymphonyFrameWork.System.ServiceLocate
{
    /// <summary>
    ///     IInjectableへServiceLocator登録済みインスタンスを注入する補助Facadeです。
    ///     SceneLoaderは、シーンロード時にこのクラスを介して自動注入します。
    /// </summary>
    public static class ServiceInjector
    {
        /// <summary> Service Locatorから1件の依存関係を解決して対象へ注入する。 </summary>
        /// <exception cref="ArgumentNullException"> 注入対象がnullの場合。 </exception>
        /// <exception cref="ServiceNotRegisteredException"> 必須サービスが未登録の場合。 </exception>
        public static void Inject<T0>(IInjectable<T0> target)
            where T0 : class
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            target.Inject(ServiceLocator.GetRequiredInstance<T0>());
        }

        /// <summary> Service Locatorから2件の依存関係を解決して対象へ注入する。 </summary>
        /// <exception cref="ArgumentNullException"> 注入対象がnullの場合。 </exception>
        /// <exception cref="ServiceNotRegisteredException"> 必須サービスが未登録の場合。 </exception>
        public static void Inject<T0, T1>(IInjectable<T0, T1> target)
            where T0 : class
            where T1 : class
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            target.Inject(
                ServiceLocator.GetRequiredInstance<T0>(),
                ServiceLocator.GetRequiredInstance<T1>());
        }

        /// <summary> Service Locatorから3件の依存関係を解決して対象へ注入する。 </summary>
        /// <exception cref="ArgumentNullException"> 注入対象がnullの場合。 </exception>
        /// <exception cref="ServiceNotRegisteredException"> 必須サービスが未登録の場合。 </exception>
        public static void Inject<T0, T1, T2>(IInjectable<T0, T1, T2> target)
            where T0 : class
            where T1 : class
            where T2 : class
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            target.Inject(
                ServiceLocator.GetRequiredInstance<T0>(),
                ServiceLocator.GetRequiredInstance<T1>(),
                ServiceLocator.GetRequiredInstance<T2>());
        }

        /// <summary> Service Locatorから4件の依存関係を解決して対象へ注入する。 </summary>
        /// <exception cref="ArgumentNullException"> 注入対象がnullの場合。 </exception>
        /// <exception cref="ServiceNotRegisteredException"> 必須サービスが未登録の場合。 </exception>
        public static void Inject<T0, T1, T2, T3>(IInjectable<T0, T1, T2, T3> target)
            where T0 : class
            where T1 : class
            where T2 : class
            where T3 : class
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            target.Inject(
                ServiceLocator.GetRequiredInstance<T0>(),
                ServiceLocator.GetRequiredInstance<T1>(),
                ServiceLocator.GetRequiredInstance<T2>(),
                ServiceLocator.GetRequiredInstance<T3>());
        }

        private static readonly Dictionary<Type, MethodInfo> _injectMethods = BuildInjectMethodMap();

        /// <summary>
        ///     IInjectable&lt;...&gt;の各アリティと、対応するInjectメソッドの対応表を構築する。
        /// </summary>
        private static Dictionary<Type, MethodInfo> BuildInjectMethodMap()
        {
            Dictionary<Type, MethodInfo> map = new();

            foreach (MethodInfo method in typeof(ServiceInjector).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != nameof(Inject))
                {
                    continue;
                }

                Type parameterType = method.GetParameters()[0].ParameterType;
                map[parameterType.GetGenericTypeDefinition()] = method;
            }

            return map;
        }

        /// <summary>
        ///     targetが実装している実際の <see cref="IInjectable{T0}"/> 系interfaceを判定し、対応するInjectを呼び出す。
        /// </summary>
        /// <param name="target"> 注入対象。 </param>
        /// <returns> 注入を実行できた場合はtrue。targetがどのIInjectable系interfaceも実装していない場合はfalse。 </returns>
        internal static bool TryAutoInject(IInjectable target)
        {
            if (target == null)
            {
                return false;
            }

            foreach (Type interfaceType in target.GetType().GetInterfaces())
            {
                if (!interfaceType.IsGenericType)
                {
                    continue;
                }

                if (!_injectMethods.TryGetValue(interfaceType.GetGenericTypeDefinition(), out MethodInfo method))
                {
                    continue;
                }

                try
                {
                    method.MakeGenericMethod(interfaceType.GetGenericArguments()).Invoke(null, new object[] { target });
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    throw;
                }

                return true;
            }

            return false;
        }
    }
}
