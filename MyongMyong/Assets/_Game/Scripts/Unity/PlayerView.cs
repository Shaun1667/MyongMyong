using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>플레이어 그리기. Core의 상태를 받아 위치(보간 + 도트 격자 맞춤) · 좌우 뒤집기 · 애니메이션 파라미터만 바꾼다.</summary>
    public sealed class PlayerView : MonoBehaviour
    {
        static readonly int SpeedParam = Animator.StringToHash("Speed");
        static readonly int HitParam = Animator.StringToHash("Hit");

        [Tooltip("Rig node that is mirrored on X when facing left.")]
        [SerializeField] Transform flipRoot;
        [SerializeField] Animator animator;

        /// <param name="alpha">직전 틱(0)과 지금 틱(1) 사이 어디를 그릴지.</param>
        /// <param name="maxSpeed">최대 이동 속도. 애니메이터의 Speed는 0~1 비율로 넘긴다.</param>
        public void Render(in PlayerState state, float alpha, float maxSpeed)
        {
            Vec2 p = Vec2.Lerp(state.PreviousPosition, state.Position, alpha);
            transform.position = PixelArt.Snap(p.X, p.Y, transform.position.z);

            if (flipRoot != null)
            {
                Vector3 scale = Vector3.one;
                scale.x = state.FacingX;
                flipRoot.localScale = scale;
            }

            if (animator != null)
            {
                float ratio = maxSpeed > 0f ? state.Velocity.Magnitude / maxSpeed : 0f;
                animator.SetFloat(SpeedParam, ratio);
            }
        }

        public void PlayHit()
        {
            if (animator != null) animator.SetTrigger(HitParam);
        }
    }
}
