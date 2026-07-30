using SymphonyFrameWork.System.SceneLoad;
using SymphonyFrameWork.System.ServiceLocate;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SymphonyFrameWork.Samples.ServiceLocatorSample
{
    /// <summary> Service Locatorの取得、待機、シーン遷移時の保持を順に実演する。 </summary>
    public sealed class ServiceLocatorSample_Sequences : MonoBehaviour
    {
        /// <summary> Service Locatorのサンプルシーケンスを開始する。 </summary>
        private void Start()
        {
            _ = Sequence();
        }

        /// <summary> 登録取得、Single相当のシーン再読込、遅延登録待機を実行する。 </summary>
        private async ValueTask Sequence()
        {
            StringBuilder logBuilder = new StringBuilder();
            string currentSceneName = SceneManager.GetActiveScene().name;

            Camera camera = ServiceLocator.GetRequiredInstance<Camera>();
            ServiceLocatorSample_1 serviceLocatorSample_1 = ServiceLocator.GetRequiredInstance<ServiceLocatorSample_1>();

            logBuilder.AppendLine($"Camera instance retrieved from ServiceLocator | name: {camera.name}, id: {camera.GetInstanceID()}");
            logBuilder.AppendLine($"ServiceLocatorSample_1 instance retrieved from ServiceLocator | name: {serviceLocatorSample_1.name}, id: {serviceLocatorSample_1.GetInstanceID()}");
            Debug.Log(logBuilder.ToString());
            logBuilder.Clear();

            await Awaitable.WaitForSecondsAsync(3f, destroyCancellationToken);

            Debug.Log("Reloading the current scene...");
            await SceneLoader.UnloadScene(currentSceneName);
            await SceneLoader.LoadScene(currentSceneName, mode: LoadSceneMode.Single, priority: 1);
            Debug.Log("Reloading done.");

            camera = ServiceLocator.GetRequiredInstance<Camera>();

            logBuilder.AppendLine($"Camera instance retrieved from ServiceLocator | name: {camera.name}, id: {camera.GetInstanceID()}");
            logBuilder.AppendLine($"ServiceLocatorSample_1 still exists because it is a singleton. | name: {serviceLocatorSample_1.name}, id: {serviceLocatorSample_1.GetInstanceID()}");
            Debug.Log(logBuilder.ToString());
            logBuilder.Clear();

            Debug.Log($"wait for register ServiceLocatorSample_2");
            ServiceLocatorSample_2 serviceLocatorSample_2 = await ServiceLocator.GetInstanceAsync<ServiceLocatorSample_2>(10, destroyCancellationToken);
            Debug.Log($"ServiceLocatorSample_2 was registered | {serviceLocatorSample_2.name}, id: {serviceLocatorSample_2.GetInstanceID()}");
        }
    }
}
