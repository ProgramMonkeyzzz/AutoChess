using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace BehaviorTree
{
    /// <summary>按配置名索引可信本地行为树定义的运行时仓库。</summary>
    public sealed class BehaviorTreeRepository
    {
        /// <summary>默认 Resources 路径。</summary>
        public const string DefaultResourcesPath = "Configs/BehaviorTrees";

        /// <summary>按配置名索引的行为树定义。</summary>
        private readonly ConcurrentDictionary<string, BehaviorTreeDefinition> _byName =
            new ConcurrentDictionary<string, BehaviorTreeDefinition>();

        /// <summary>注册一份行为树定义；重复名称会抛出异常。</summary>
        public void Register(string name, BehaviorTreeDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("行为树名称不能为空。", nameof(name));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            string key = name.Trim();
            if (!_byName.TryAdd(key, definition))
            {
                throw new InvalidOperationException("行为树 '" + key + "' 已注册。");
            }
        }

        /// <summary>尝试按配置名获取仓库中的行为树定义，供主线程和战斗线程只读查询。</summary>
        public bool TryGet(string name, out BehaviorTreeDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return _byName.TryGetValue(name.Trim(), out definition) && definition != null;
        }

        /// <summary>从 Resources 加载 .bytes 行为树并按资源名索引；仅允许主线程调用。</summary>
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
                    BehaviorTreeDefinition definition = BehaviorTreeBinarySerializer.Deserialize(asset.bytes);
                    Register(asset.name, definition);
                    loadedCount++;
                }
                catch (Exception exception)
                {
                    Debug.LogError("加载行为树失败 " + asset.name + ": " + exception.Message);
                }
            }

            return loadedCount;
        }
    }
}
