using System;
using System.Threading;

namespace SymphonyFrameWork.System.SaveSystem
{
    /// <summary>
    ///     CoreSystemが所有するライフタイムにSaveDataRegistryを連動させます。
    /// </summary>
    internal static class SaveSystem
    {
        /// <summary> セーブデータレジストリをシステムのライフタイムへ関連付ける。 </summary>
        internal static void Initialize(
            CancellationToken destroyCancellationToken,
            Func<SaveDataLoader> loaderResolver)
        {
            _destroyRegistration.Dispose();
            SaveDataRegistry.ConfigureLoaderResolver(loaderResolver);
            _destroyRegistration = destroyCancellationToken.Register(SaveDataRegistry.ResetRuntimeState);
        }

        private static CancellationTokenRegistration _destroyRegistration;
    }
}
