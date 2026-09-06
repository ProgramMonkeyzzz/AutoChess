using System.Collections.Generic;
using AutoChess.Render;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 主线程血条容器，只为受伤存活棋子在自身下创建 UIHpBar，并同步到 ChessView.HpTransform 的屏幕位置。
    /// </summary>
    public class UIHp : MonoBehaviour
    {
        /// <summary>当前场景中的血条容器；无实例时主线程调用为 no-op。</summary>
        public static UIHp Instance { get; private set; }

        /// <summary>血条预制体，实例化后作为 UIHp 的子节点。</summary>
        [SerializeField] private UIHpBar _hpBarPrefab;

        /// <summary>用于世界到屏幕换算的世界相机；为空时回退 Camera.main。</summary>
        [SerializeField] private Camera _worldCamera;

        /// <summary>key 是棋子 UniqueId。</summary>
        private readonly Dictionary<int, UIHpBar> _hpBars = new Dictionary<int, UIHpBar>();

        /// <summary>本帧仍保留的受伤棋子 ID。</summary>
        private readonly HashSet<int> _retainedIds = new HashSet<int>();

        /// <summary>待移除缓冲，避免遍历时修改字典。</summary>
        private readonly List<int> _pendingRemoveIds = new List<int>();

        /// <summary>用于查找 ChessView.HpTransform 的主线程渲染管理器。</summary>
        private RenderManager _renderManager;

        /// <summary>自身 RectTransform，作为血条 anchoredPosition 的父空间。</summary>
        private RectTransform _rectTransform;

        /// <summary>所在 Canvas，用于 Screen Space Camera 换算。</summary>
        private Canvas _canvas;

        /// <summary>当前血条数量，供测试断言。</summary>
        public int HpBarCount => _hpBars.Count;

        private void Awake()
        {
            Instance = this;
            CacheCanvas();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void LateUpdate()
        {
            SyncAllBarPositions();
        }

        /// <summary>
        /// 按最新渲染帧同步受伤棋子血条；满血、死亡和快照中消失的棋子会销毁血条。
        /// </summary>
        public void ApplyRenderFrame(BattleRenderFrame frame, RenderManager renderManager)
        {
            _renderManager = renderManager;
            if (frame == null)
            {
                ClearBars();
                return;
            }

            _retainedIds.Clear();
            ChessRenderSnapshot[] chessSnapshots = frame.Chess;
            for (int i = 0; i < chessSnapshots.Length; i++)
            {
                ChessRenderSnapshot snapshot = chessSnapshots[i];
                SyncChessHp(snapshot.UniqueId, snapshot.HpProgress);
                if (_hpBars.ContainsKey(snapshot.UniqueId))
                {
                    _retainedIds.Add(snapshot.UniqueId);
                }
            }

            _pendingRemoveIds.Clear();
            foreach (int uniqueId in _hpBars.Keys)
            {
                if (!_retainedIds.Contains(uniqueId))
                {
                    _pendingRemoveIds.Add(uniqueId);
                }
            }

            for (int i = 0; i < _pendingRemoveIds.Count; i++)
            {
                RemoveChessHp(_pendingRemoveIds[i]);
            }

            _pendingRemoveIds.Clear();
        }

        /// <summary>
        /// 为受伤棋子创建或更新血条；满血或非正进度时销毁。
        /// </summary>
        public void SyncChessHp(int chessUniqueId, float hpProgress)
        {
            if (chessUniqueId <= 0)
            {
                return;
            }

            if (hpProgress <= 0f || hpProgress >= 1f || float.IsNaN(hpProgress))
            {
                RemoveChessHp(chessUniqueId);
                return;
            }

            UIHpBar bar = GetOrCreateBar(chessUniqueId);
            if (bar == null)
            {
                return;
            }

            bar.SetProgress(hpProgress);
            SyncBarPosition(chessUniqueId);
        }

        /// <summary>
        /// 将指定棋子血条同步到 ChessView.HpTransform 的屏幕位置；有视图时忽略传入的棋盘坐标。
        /// </summary>
        public void SyncChessPosition(int chessUniqueId, Vector2 position)
        {
            _ = position;
            SyncBarPosition(chessUniqueId);
        }

        /// <summary>
        /// 注入世界相机；为空时回退 Camera.main。
        /// </summary>
        public void SetWorldCamera(Camera worldCamera)
        {
            _worldCamera = worldCamera;
        }

        /// <summary>
        /// 销毁指定棋子的血条。
        /// </summary>
        public void RemoveChessHp(int chessUniqueId)
        {
            if (!_hpBars.TryGetValue(chessUniqueId, out UIHpBar bar))
            {
                return;
            }

            _hpBars.Remove(chessUniqueId);
            DestroyBar(bar);
        }

        /// <summary>
        /// 尝试获取已创建的血条。
        /// </summary>
        public bool TryGetHpBar(int chessUniqueId, out UIHpBar bar)
        {
            return _hpBars.TryGetValue(chessUniqueId, out bar) && bar != null;
        }

        /// <summary>
        /// 按当前 ChessView.HpTransform 刷新全部血条位置；找不到视图的血条会被销毁。
        /// </summary>
        public void SyncAllBarPositions()
        {
            _pendingRemoveIds.Clear();
            foreach (KeyValuePair<int, UIHpBar> pair in _hpBars)
            {
                if (pair.Value == null || !SyncBarPosition(pair.Key))
                {
                    _pendingRemoveIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < _pendingRemoveIds.Count; i++)
            {
                RemoveChessHp(_pendingRemoveIds[i]);
            }

            _pendingRemoveIds.Clear();
        }

        /// <summary>
        /// 用 ChessView.HpTransform 世界坐标更新血条；无视图时返回 false，在相机后保留血条但不改位置。
        /// </summary>
        private bool SyncBarPosition(int chessUniqueId)
        {
            if (!_hpBars.TryGetValue(chessUniqueId, out UIHpBar bar) || bar == null)
            {
                return false;
            }

            if (_renderManager == null
                || !_renderManager.TryGetChessView(chessUniqueId, out ChessView view)
                || view == null)
            {
                return false;
            }

            Transform hpTransform = view.HpTransform;
            if (hpTransform == null)
            {
                return false;
            }

            if (!TryConvertWorldToCanvas(hpTransform.position, out Vector2 canvasLocal))
            {
                return true;
            }

            bar.SetPosition(canvasLocal);
            return true;
        }

        /// <summary>
        /// 按 UniqueId 复用或在 UIHp 下实例化血条。
        /// </summary>
        private UIHpBar GetOrCreateBar(int chessUniqueId)
        {
            if (_hpBars.TryGetValue(chessUniqueId, out UIHpBar existing) && existing != null)
            {
                return existing;
            }

            if (existing != null)
            {
                _hpBars.Remove(chessUniqueId);
            }

            if (_hpBarPrefab == null)
            {
                Debug.LogError("UIHp 未指定 UIHpBar 预制体。");
                return null;
            }

            UIHpBar bar = Instantiate(_hpBarPrefab, transform, false);
            bar.name = "UIHpBar_" + chessUniqueId;
            _hpBars[chessUniqueId] = bar;
            return bar;
        }

        /// <summary>
        /// 将世界坐标转换为 UIHp 的 Canvas 本地坐标。
        /// </summary>
        private bool TryConvertWorldToCanvas(Vector3 worldPosition, out Vector2 canvasLocal)
        {
            canvasLocal = default;
            CacheCanvas();
            Camera worldCamera = _worldCamera != null ? _worldCamera : Camera.main;
            if (worldCamera == null || _rectTransform == null)
            {
                return false;
            }

            Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
            if (screenPoint.z < 0f)
            {
                return false;
            }

            Camera canvasCamera = null;
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                canvasCamera = _canvas.worldCamera;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform,
                screenPoint,
                canvasCamera,
                out canvasLocal);
        }

        /// <summary>
        /// 缓存自身 RectTransform 与父 Canvas，避免 Awake 时尚未入树。
        /// </summary>
        private void CacheCanvas()
        {
            if (_rectTransform == null)
            {
                _rectTransform = transform as RectTransform;
            }

            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }
        }

        /// <summary>
        /// 销毁当前全部血条。
        /// </summary>
        private void ClearBars()
        {
            _pendingRemoveIds.Clear();
            _pendingRemoveIds.AddRange(_hpBars.Keys);
            for (int i = 0; i < _pendingRemoveIds.Count; i++)
            {
                RemoveChessHp(_pendingRemoveIds[i]);
            }

            _pendingRemoveIds.Clear();
        }

        /// <summary>
        /// Play 模式使用 Destroy，EditMode 测试使用 DestroyImmediate。
        /// </summary>
        private static void DestroyBar(UIHpBar bar)
        {
            if (bar == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(bar.gameObject);
            }
            else
            {
                DestroyImmediate(bar.gameObject);
            }
        }
    }
}