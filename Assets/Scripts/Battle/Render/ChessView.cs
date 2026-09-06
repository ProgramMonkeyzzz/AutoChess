using UnityEngine;

namespace AutoChess.Render
{
    /// <summary>
    /// 棋子在主线程上的表现视图，负责同步棋盘 XZ 位置和朝向。
    /// </summary>
    public class ChessView : MonoBehaviour
    {
        [SerializeField] private Transform _chessTransform;
        [SerializeField] private Transform _hpTransform;
        [SerializeField] private float _height;

        /// <summary>用于阵营着色的材质实例，避免修改预制体共享材质。</summary>
        private Material _materialInstance;

        /// <summary>棋子网格渲染器。</summary>
        private Renderer _renderer;

        /// <summary>未序列化时回退到自身 transform。</summary>
        private Transform TargetTransform
        {
            get
            {
                if (_chessTransform == null)
                {
                    _chessTransform = transform;
                }

                return _chessTransform;
            }
        }

        /// <summary>血条跟随的世界坐标锚点；未序列化时回退到棋子 transform。</summary>
        public Transform HpTransform
        {
            get
            {
                return _hpTransform != null ? _hpTransform : TargetTransform;
            }
        }

        private void Awake()
        {
            if (_chessTransform == null)
            {
                _chessTransform = transform;
            }

            _renderer = GetComponentInChildren<Renderer>();
        }

        /// <summary>
        /// 将棋盘 XZ 坐标映射到世界坐标 (x, height, z)。
        /// </summary>
        public void SetPosition(Vector2 boardPosition)
        {
            TargetTransform.position = new Vector3(boardPosition.x, _height, boardPosition.y);
        }

        /// <summary>
        /// 在 XZ 平面设置朝向；零向量时保持当前旋转。
        /// </summary>
        public void SetForward(Vector2 boardForward)
        {
            if (boardForward.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector3 worldForward = new Vector3(boardForward.x, 0f, boardForward.y);
            TargetTransform.rotation = Quaternion.LookRotation(worldForward, Vector3.up);
        }

        /// <summary>
        /// 使用独立材质实例设置阵营颜色。
        /// </summary>
        public void SetCampColor(Color color)
        {
            if (_renderer == null)
            {
                _renderer = GetComponentInChildren<Renderer>();
            }

            if (_renderer == null)
            {
                return;
            }

            if (_materialInstance == null)
            {
                _materialInstance = _renderer.material;
            }

            _materialInstance.color = color;
        }

        private void OnDestroy()
        {
            if (_materialInstance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_materialInstance);
            }
            else
            {
                DestroyImmediate(_materialInstance);
            }

            _materialInstance = null;
        }
    }
}