using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 임시 바닥(끝없는 평면처럼 보이게). 타일 모드 스프라이트가 대상을 무늬 한 칸 단위로 따라가서
    /// 무늬는 제자리에 있는 것처럼 보인다. 실제 맵이 생기면 바꾼다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GroundGrid : MonoBehaviour
    {
        [SerializeField] Transform follow;

        Vector2 period;

        void Awake()
        {
            var sprite = GetComponent<SpriteRenderer>().sprite;
            period = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
        }

        void LateUpdate()
        {
            if (follow == null) return;
            Vector3 p = follow.position;
            float x = Mathf.Round(p.x / period.x) * period.x;
            float y = Mathf.Round(p.y / period.y) * period.y;
            transform.position = new Vector3(x, y, transform.position.z);
        }
    }
}
