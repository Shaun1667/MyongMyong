using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 플레이어 그리기. Core의 상태를 받아 위치(보간 + 도트 격자 맞춤) · 좌우 뒤집기 · 애니메이션 파라미터 ·
    /// 맞은 직후 붉은 번쩍임 · 무적 중 깜빡임(스프라이트 색만)을 바꾼다. 리그 · 클립은 건드리지 않는다.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        static readonly int SpeedParam = Animator.StringToHash("Speed");
        static readonly int HitParam = Animator.StringToHash("Hit");

        const int LookNormal = 0;
        const int LookFlash = 1;
        const int LookBlinkOff = 2;

        [Tooltip("Rig node that is mirrored on X when facing left.")]
        [SerializeField] Transform flipRoot;
        [SerializeField] Animator animator;

        [Header("Hurt (presentation only)")]
        [Tooltip("Ticks the sprites stay tinted after a hit.")]
        [SerializeField, Min(0)] int hurtFlashTicks = 6;
        [Tooltip("Tint multiplied into every part sprite right after a hit.")]
        [SerializeField] Color hurtFlashColor = new Color(1f, 0.25f, 0.25f, 1f);
        [Tooltip("While invulnerable after a hit, the sprites blink every this many ticks (0 = no blink).")]
        [SerializeField, Min(0)] int blinkTicks = 4;
        [Tooltip("Alpha of the dim half of the blink.")]
        [SerializeField, Range(0f, 1f)] float blinkAlpha = 0.35f;

        SpriteRenderer[] parts = new SpriteRenderer[0];
        Color[] baseColors = new Color[0];
        int shownLook = -1;
        long seenHurtTick = PlayerState.NeverHurt;

        /// <summary>지금 붉게 번쩍이는 중인지(확인용).</summary>
        public bool IsFlashing => shownLook == LookFlash;

        /// <summary>깜빡임의 어두운 쪽인지(확인용).</summary>
        public bool IsBlinkDim => shownLook == LookBlinkOff;

        void Awake()
        {
            parts = GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[parts.Length];
            for (int i = 0; i < parts.Length; i++) baseColors[i] = parts[i].color;
        }

        /// <summary>판을 새로 시작할 때: 번쩍임 · 깜빡임을 끈다.</summary>
        public void ResetView()
        {
            seenHurtTick = PlayerState.NeverHurt;
            SetLook(LookNormal);
        }

        /// <param name="alpha">직전 틱(0)과 지금 틱(1) 사이 어디를 그릴지.</param>
        /// <param name="maxSpeed">최대 이동 속도. 애니메이터의 Speed는 0~1 비율로 넘긴다.</param>
        /// <param name="tick">World.Tick(맞은 뒤 몇 틱이 지났는지 보는 데 쓴다).</param>
        public void Render(in PlayerState state, float alpha, float maxSpeed, long tick)
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

            // 새로 맞았으면 피격 애니메이션(기존 Hit 트리거).
            if (state.LastHurtTick != seenHurtTick)
            {
                seenHurtTick = state.LastHurtTick;
                if (state.LastHurtTick != PlayerState.NeverHurt) PlayHit();
            }

            int look = LookNormal;
            if (state.LastHurtTick != PlayerState.NeverHurt && tick - state.LastHurtTick <= hurtFlashTicks) look = LookFlash;
            else if (blinkTicks > 0 && tick < state.InvulnerableUntilTick && ((tick - state.LastHurtTick) / blinkTicks) % 2 == 1) look = LookBlinkOff;
            SetLook(look);
        }

        void SetLook(int look)
        {
            if (look == shownLook) return;
            shownLook = look;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                Color c = baseColors[i];
                if (look == LookFlash) c *= hurtFlashColor;
                else if (look == LookBlinkOff) c.a *= blinkAlpha;
                parts[i].color = c;
            }
        }

        public void PlayHit()
        {
            if (animator != null) animator.SetTrigger(HitParam);
        }
    }
}
