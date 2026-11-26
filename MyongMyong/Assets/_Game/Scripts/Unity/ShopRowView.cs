using UnityEngine;

namespace Myong.Unity
{
    /// <summary>강화 화면(ShopView)의 한 줄: 이름 · 설명 · 단계 · 다음 가격 · [사기]. ShopView가 본(template)을 표 줄 수만큼 복제해 쓴다. 그리기만 한다.</summary>
    public sealed class ShopRowView : MonoBehaviour
    {
        public UnityEngine.UI.Text title;
        public UnityEngine.UI.Text description;
        public UnityEngine.UI.Text stage;
        public UnityEngine.UI.Text price;
        public UnityEngine.UI.Button buyButton;
        public UnityEngine.UI.Text buyLabel;
    }
}
