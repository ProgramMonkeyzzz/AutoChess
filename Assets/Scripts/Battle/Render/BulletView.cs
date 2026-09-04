using UnityEngine;

namespace AutoChess.Render
{
    /// <summary>
    /// 子弹在主线程上的表现视图，负责同步棋盘 XZ 位置和朝向。
    /// </summary>
    public class BulletView : MonoBehaviour
    {
        [SerializeField] private Transform _bulletTransform;
        [SerializeField] private float _height = 0.3f;

        /// <summary>未序列化时回退到自身 transform。</summary>
        private Transform TargetTransform
        {
            get
            {
                if (_bulletTransform == null)
                {
                    _bulletTransform = transform;
                }

                return _bulletTransform;
            }
        }

        private void Awake()
        {
            if (_bulletTransform == null)
            {
                _bulletTransform = transform;
            }
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
    }
}