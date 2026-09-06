using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 单个棋子的血条视图，只负责进度填充和 Canvas 本地坐标。
    /// </summary>
    public class UIHpBar : MonoBehaviour
    {
        [SerializeField] private Image _progressBar;

        /// <summary>
        /// 设置血量进度，结果限制在 [0, 1]。
        /// </summary>
        public void SetProgress(float progress)
        {
            if (_progressBar == null)
            {
                return;
            }

            _progressBar.fillAmount = Mathf.Clamp01(progress);
        }

        /// <summary>
        /// 设置血条在 UIHp 父节点下的 anchoredPosition。
        /// </summary>
        public void SetPosition(Vector2 position)
        {
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchoredPosition = position;
        }
    }
}