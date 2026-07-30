using System;

namespace SymphonyFrameWork.System.ServiceLocate
{
    /// <summary> 必須サービスがService Locatorへ登録されていない場合に発生する例外。 </summary>
    public sealed class ServiceNotRegisteredException : Exception
    {
        /// <summary> 未登録だったサービス型を指定して例外を生成する。 </summary>
        /// <param name="serviceType"> 取得を要求されたサービス型。 </param>
        public ServiceNotRegisteredException(Type serviceType)
            : base($"[{nameof(ServiceLocator)}] 必須サービス {serviceType?.FullName ?? "(null)"} が登録されていません。")
        {
            ServiceType = serviceType;
        }

        /// <summary> 取得を要求された未登録のサービス型。 </summary>
        public Type ServiceType { get; }
    }
}
