using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>텍스트 표의 키 하나를 보여 주는 라벨. 언어가 바뀌면 스스로 다시 채운다.</summary>
    [RequireComponent(typeof(UnityEngine.UI.Text))]
    public sealed class LocalizedLabel : MonoBehaviour
    {
        [SerializeField] string textKey;

        UnityEngine.UI.Text label;

        public string TextKey => textKey;

        void OnEnable()
        {
            label = GetComponent<UnityEngine.UI.Text>();
            label.font = UiFont.Get(label.fontSize);
            Text.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            Text.Changed -= Refresh;
        }

        void Refresh()
        {
            label.text = Text.Get(textKey);
        }
    }
}
