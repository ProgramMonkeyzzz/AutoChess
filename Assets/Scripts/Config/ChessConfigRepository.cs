using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace Config
{
    /// <summary>按 ConfigId 索引可信本地棋子配置的运行时仓库。</summary>
    public sealed class ChessConfigRepository
    {
        /// <summary>默认 Resources 路径。</summary>
        public const string DefaultResourcesPath = "Configs/ChessBaseData";

        /// <summary>按 ConfigId 索引的配置副本。</summary>
        private readonly ConcurrentDictionary<int, ChessBaseData> _byConfigId =
            new ConcurrentDictionary<int, ChessBaseData>();

        /// <summary>注册一份配置副本；重复 ConfigId 会抛出异常。 ConfigId=0 合法。</summary>
        public void Register(ChessBaseData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            ChessBaseData copy = data.Clone();
            if (!_byConfigId.TryAdd(copy.ConfigId, copy))
            {
                throw new InvalidOperationException(
                    "ConfigId " + copy.ConfigId + " 已注册。");
            }
        }

        /// <summary>尝试按 ConfigId 获取仓库中的配置，供主线程和战斗线程只读查询。</summary>
        public bool TryGet(int configId, out ChessBaseData data)
        {
            return _byConfigId.TryGetValue(configId, out data) && data != null;
        }

        /// <summary>从 Resources 加载 .bytes 配置并按 ConfigId 索引；仅允许主线程调用。</summary>
        public int LoadFromResources(string resourcesPath = DefaultResourcesPath)
        {
            if (string.IsNullOrEmpty(resourcesPath))
            {
                throw new ArgumentException("资源路径不能为空。", nameof(resourcesPath));
            }

            TextAsset[] assets = Resources.LoadAll<TextAsset>(resourcesPath);
            int loadedCount = 0;
            if (assets == null)
            {
                return 0;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                TextAsset asset = assets[i];
                if (asset == null || asset.bytes == null || asset.bytes.Length == 0)
                {
                    continue;
                }

                try
                {
                    ChessBaseData data = Deserialize(asset.bytes);
                    if (data == null)
                    {
                        continue;
                    }

                    Register(data);
                    loadedCount++;
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "加载棋子配置失败 " + asset.name + ": " + exception.Message);
                }
            }

            return loadedCount;
        }

        /// <summary>反序列化一份可信本地棋子配置。</summary>
        internal static ChessBaseData Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

#pragma warning disable SYSLIB0011
            var formatter = new BinaryFormatter();
            using (var stream = new MemoryStream(bytes))
            {
                return formatter.Deserialize(stream) as ChessBaseData;
            }
#pragma warning restore SYSLIB0011
        }
    }
}
