using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoChess.Render
{
    /// <summary>
    /// 主线程渲染管理器，根据战斗快照创建、更新和清除棋子/子弹视图。
    /// </summary>
    public sealed class RenderManager
    {
        /// <summary>Resources 下的棋子预制体路径。</summary>
        private const string ChessPrefabName = "Prefabs/ChessView";

        /// <summary>Resources 下的子弹预制体路径。</summary>
        private const string BulletPrefabName = "Prefabs/BulletView";

        /// <summary>key 是棋子唯一 InstanceId。</summary>
        private readonly Dictionary<int, ChessView> _chessViews = new Dictionary<int, ChessView>();

        /// <summary>key 是子弹唯一 BulletId。</summary>
        private readonly Dictionary<int, BulletView> _bulletViews = new Dictionary<int, BulletView>();

        /// <summary>本帧存活棋子 ID，供差量清除使用。</summary>
        private readonly HashSet<int> _liveChessIds = new HashSet<int>();

        /// <summary>本帧活动子弹 ID，供差量清除使用。</summary>
        private readonly HashSet<int> _liveBulletIds = new HashSet<int>();

        /// <summary>待移除的视图 ID 缓冲，避免遍历时修改字典。</summary>
        private readonly List<int> _pendingRemoveIds = new List<int>();

        private readonly GameObject _chessPrefab;
        private readonly GameObject _bulletPrefab;
        private readonly Transform _chessRoot;
        private readonly Transform _bulletRoot;
        private readonly GameObject _renderRoot;

        /// <summary>当前棋子视图数量。</summary>
        public int ChessViewCount => _chessViews.Count;

        /// <summary>当前子弹视图数量。</summary>
        public int BulletViewCount => _bulletViews.Count;

        /// <summary>从 Resources 加载预制体并挂到指定根节点下。</summary>
        public RenderManager(Transform root = null)
            : this(
                Resources.Load<GameObject>(ChessPrefabName),
                Resources.Load<GameObject>(BulletPrefabName),
                root,
                true)
        {
        }

        /// <summary>使用外部注入的预制体创建渲染管理器，供 EditMode 测试使用。</summary>
        public RenderManager(GameObject chessPrefab, GameObject bulletPrefab, Transform root)
            : this(chessPrefab, bulletPrefab, root, false)
        {
        }

        /// <summary>创建渲染根节点并保存预制体引用。</summary>
        private RenderManager(GameObject chessPrefab, GameObject bulletPrefab, Transform root, bool logMissingPrefabs)
        {
            _chessPrefab = chessPrefab;
            _bulletPrefab = bulletPrefab;
            if (logMissingPrefabs)
            {
                if (_chessPrefab == null)
                {
                    Debug.LogError($"RenderManager 未能加载棋子预制体 Resources/{ChessPrefabName}。");
                }

                if (_bulletPrefab == null)
                {
                    Debug.LogError($"RenderManager 未能加载子弹预制体 Resources/{BulletPrefabName}。");
                }
            }

            _renderRoot = new GameObject("RenderRoot");
            if (root != null)
            {
                _renderRoot.transform.SetParent(root, false);
            }

            GameObject chessRootObject = new GameObject("ChessViews");
            chessRootObject.transform.SetParent(_renderRoot.transform, false);
            _chessRoot = chessRootObject.transform;

            GameObject bulletRootObject = new GameObject("BulletViews");
            bulletRootObject.transform.SetParent(_renderRoot.transform, false);
            _bulletRoot = bulletRootObject.transform;
        }

        /// <summary>按最新战斗渲染帧同步视图；快照中不存在的棋子或子弹会被立即清除。</summary>
        public void ApplyFrame(BattleRenderFrame frame)
        {
            if (frame == null)
            {
                return;
            }

            _liveChessIds.Clear();
            ChessRenderSnapshot[] chessSnapshots = frame.Chess;
            for (int i = 0; i < chessSnapshots.Length; i++)
            {
                ChessRenderSnapshot snapshot = chessSnapshots[i];
                if (snapshot.InstanceId <= 0)
                {
                    continue;
                }

                ChessView view = CreateChessView(snapshot.InstanceId, snapshot.CampId);
                if (view == null)
                {
                    continue;
                }

                view.SetPosition(snapshot.Position);
                view.SetForward(snapshot.Forward);
                _liveChessIds.Add(snapshot.InstanceId);
            }

            RemoveMissingViews(_chessViews, _liveChessIds, RemoveChessView);

            _liveBulletIds.Clear();
            BulletRenderSnapshot[] bulletSnapshots = frame.Bullets;
            for (int i = 0; i < bulletSnapshots.Length; i++)
            {
                BulletRenderSnapshot snapshot = bulletSnapshots[i];
                if (snapshot.BulletId <= 0)
                {
                    continue;
                }

                BulletView view = CreateBulletView(snapshot.BulletId);
                if (view == null)
                {
                    continue;
                }

                view.SetPosition(snapshot.Position);
                view.SetForward(snapshot.Forward);
                _liveBulletIds.Add(snapshot.BulletId);
            }

            RemoveMissingViews(_bulletViews, _liveBulletIds, RemoveBulletView);
        }

        /// <summary>幂等创建棋子视图；同一 InstanceId 不会重复实例化。</summary>
        public ChessView CreateChessView(int instanceId)
        {
            return CreateChessView(instanceId, 0);
        }

        /// <summary>幂等创建指定阵营的棋子视图。</summary>
        public ChessView CreateChessView(int instanceId, int campId)
        {
            if (instanceId <= 0)
            {
                return null;
            }

            if (_chessViews.TryGetValue(instanceId, out ChessView existing) && existing != null)
            {
                existing.SetCampColor(GetCampColor(campId));
                return existing;
            }

            if (existing != null)
            {
                _chessViews.Remove(instanceId);
            }

            if (_chessPrefab == null)
            {
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(_chessPrefab, _chessRoot);
            instance.name = $"ChessView_{instanceId}";
            ChessView view = instance.GetComponent<ChessView>();
            if (view == null)
            {
                view = instance.AddComponent<ChessView>();
            }

            view.SetCampColor(GetCampColor(campId));
            _chessViews[instanceId] = view;
            return view;
        }

        /// <summary>幂等创建子弹视图；同一 BulletId 不会重复实例化。</summary>
        public BulletView CreateBulletView(int bulletId)
        {
            if (bulletId <= 0)
            {
                return null;
            }

            if (_bulletViews.TryGetValue(bulletId, out BulletView existing) && existing != null)
            {
                return existing;
            }

            if (existing != null)
            {
                _bulletViews.Remove(bulletId);
            }

            if (_bulletPrefab == null)
            {
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(_bulletPrefab, _bulletRoot);
            instance.name = $"BulletView_{bulletId}";
            BulletView view = instance.GetComponent<BulletView>();
            if (view == null)
            {
                view = instance.AddComponent<BulletView>();
            }

            _bulletViews[bulletId] = view;
            return view;
        }

        /// <summary>销毁并移除指定棋子视图。</summary>
        public void RemoveChessView(int instanceId)
        {
            if (!_chessViews.TryGetValue(instanceId, out ChessView view))
            {
                return;
            }

            _chessViews.Remove(instanceId);
            DestroyView(view);
        }

        /// <summary>销毁并移除指定子弹视图。</summary>
        public void RemoveBulletView(int bulletId)
        {
            if (!_bulletViews.TryGetValue(bulletId, out BulletView view))
            {
                return;
            }

            _bulletViews.Remove(bulletId);
            DestroyView(view);
        }

        /// <summary>尝试获取已创建的棋子视图。</summary>
        public bool TryGetChessView(int instanceId, out ChessView view)
        {
            return _chessViews.TryGetValue(instanceId, out view) && view != null;
        }

        /// <summary>尝试获取已创建的子弹视图。</summary>
        public bool TryGetBulletView(int bulletId, out BulletView view)
        {
            return _bulletViews.TryGetValue(bulletId, out view) && view != null;
        }

        /// <summary>销毁全部棋子和子弹视图，保留渲染根节点。</summary>
        public void Clear()
        {
            _pendingRemoveIds.Clear();
            _pendingRemoveIds.AddRange(_chessViews.Keys);
            for (int i = 0; i < _pendingRemoveIds.Count; i++)
            {
                RemoveChessView(_pendingRemoveIds[i]);
            }

            _pendingRemoveIds.Clear();
            _pendingRemoveIds.AddRange(_bulletViews.Keys);
            for (int i = 0; i < _pendingRemoveIds.Count; i++)
            {
                RemoveBulletView(_pendingRemoveIds[i]);
            }

            _pendingRemoveIds.Clear();
            _chessViews.Clear();
            _bulletViews.Clear();
        }

        /// <summary>清除全部视图并销毁渲染根节点。</summary>
        public void Dispose()
        {
            Clear();
            DestroyObject(_renderRoot);
        }

        /// <summary>按阵营返回着色：1 蓝、2 红、其他灰。</summary>
        private static Color GetCampColor(int campId)
        {
            switch (campId)
            {
                case 1:
                    return Color.blue;
                case 2:
                    return Color.red;
                default:
                    return Color.gray;
            }
        }

        /// <summary>删除快照中不再出现的视图。</summary>
        private void RemoveMissingViews<TView>(
            Dictionary<int, TView> views,
            HashSet<int> liveIds,
            Action<int> remove)
            where TView : Component
        {
            _pendingRemoveIds.Clear();
            foreach (KeyValuePair<int, TView> pair in views)
            {
                if (!liveIds.Contains(pair.Key) || pair.Value == null)
                {
                    _pendingRemoveIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < _pendingRemoveIds.Count; i++)
            {
                remove(_pendingRemoveIds[i]);
            }

            _pendingRemoveIds.Clear();
        }

        /// <summary>销毁视图组件对应的 GameObject。</summary>
        private static void DestroyView(Component view)
        {
            if (view == null)
            {
                return;
            }

            DestroyObject(view.gameObject);
        }

        /// <summary>Play 模式使用 Destroy，EditMode 测试使用 DestroyImmediate。</summary>
        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}