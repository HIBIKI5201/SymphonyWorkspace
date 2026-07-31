using System;

using SymphonyFrameWork.Editor.Debugger;

using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace SymphonyFrameWork.Tests
{
    /// <summary> 未初期化時のSymphonyMcpToolsが有効なJSONを返すことを検証する。 </summary>
    public sealed class SymphonyMcpToolsTests
    {
        /// <summary> Service Locatorが未初期化でも例外なく有効なJSONを返すことを検証する。 </summary>
        [Test]
        public void GetServiceLocatorJson_WhenUninitialized_ReturnsValidJson()
        {
            AssertUninitializedJson(SymphonyMcpTools.GetServiceLocatorJson);
        }

        /// <summary> Scene Loaderが未初期化でも例外なく有効なJSONを返すことを検証する。 </summary>
        [Test]
        public void GetSceneLoaderJson_WhenUninitialized_ReturnsValidJson()
        {
            AssertUninitializedJson(SymphonyMcpTools.GetSceneLoaderJson);
        }

        /// <summary> Save Data Registryが未初期化でも例外なく有効なJSONを返すことを検証する。 </summary>
        [Test]
        public void GetSaveDataJson_WhenUninitialized_ReturnsValidJson()
        {
            AssertUninitializedJson(SymphonyMcpTools.GetSaveDataJson);
        }

        /// <summary> Pause Managerが未初期化でも例外なく有効なJSONを返すことを検証する。 </summary>
        [Test]
        public void GetPauseJson_WhenUninitialized_ReturnsValidJson()
        {
            AssertUninitializedJson(SymphonyMcpTools.GetPauseJson);
        }

        /// <summary> 呼び出しが例外を投げず、未初期化を示すJSONを返すことを検証する。 </summary>
        /// <param name="getJson"> 検証対象のJSON取得処理。 </param>
        private static void AssertUninitializedJson(Func<string> getJson)
        {
            string json = null;
            Assert.DoesNotThrow(() => json = getJson());

            JObject result = null;
            Assert.DoesNotThrow(() => result = JObject.Parse(json));
            Assert.That(result.Value<bool>("initialized"), Is.False);
        }
    }
}
