using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>화면 위쪽 경험치 바와 레벨 숫자. 그리기만 한다(값은 Core의 World).</summary>
    public sealed class ExperienceHud : MonoBehaviour
    {
        [Tooltip("Bar fill. Its right anchor is set to the experience ratio (0..1).")]
        [SerializeField] RectTransform fill;
        [SerializeField] UnityEngine.UI.Text levelLabel;

        int shownLevel = -1;
        float shownRatio = -1f;

        void OnEnable()
        {
            if (levelLabel != null) levelLabel.font = UiFont.Get(levelLabel.fontSize);
            Text.Changed += OnTextChanged;
        }

        void OnDisable()
        {
            Text.Changed -= OnTextChanged;
        }

        void OnTextChanged()
        {
            shownLevel = -1;
        }

        public void Render(int level, int experience, int experienceToNext)
        {
            float ratio = experienceToNext <= 0 || experienceToNext == int.MaxValue ? 0f : Mathf.Clamp01((float)experience / experienceToNext);
            if (fill != null && !Mathf.Approximately(ratio, shownRatio))
            {
                Vector2 max = fill.anchorMax;
                max.x = ratio;
                fill.anchorMax = max;
                shownRatio = ratio;
            }
            if (levelLabel != null && level != shownLevel)
            {
                levelLabel.text = Text.Get("ui.hud.level", level);
                shownLevel = level;
            }
        }
    }
}
