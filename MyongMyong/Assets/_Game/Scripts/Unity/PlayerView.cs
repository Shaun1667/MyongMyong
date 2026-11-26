using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 플레이어 그리기. Core의 상태를 받아 위치(보간 + 도트 격자 맞춤) · 좌우 뒤집기 · 애니메이션 파라미터 ·
    /// 맞은 직후 흰색 번쩍임(재질 바꾸기) · 무적 중 깜빡임(스프라이트 색의 알파) · 사망 애니메이션을 바꾼다.
    /// 리그 · 클립은 건드리지 않는다. 수치는 FeedbackTuning(GameRoot가 Configure로 넘긴다), 계산은 FeedbackRules.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        static readonly int SpeedParam = Animator.StringToHash("Speed");
        static readonly int HitParam = Animator.StringToHash("Hit");
        static readonly int DeadParam = Animator.StringToHash("Dead");
        static readonly int DeathState = Animator.StringToHash("Death");

        [Tooltip("Rig node that is mirrored on X when facing left.")]
        [SerializeField] Transform flipRoot;
        [SerializeField] Animator animator;
        [Tooltip("Material that paints the sprites one solid colour (white hit flash). Empty: no flash.")]
        [SerializeField] Material flashMaterial;

        SpriteRenderer[] parts = new SpriteRenderer[0];
        Color[] baseColors = new Color[0];
        Material[] baseMaterials = new Material[0];
        int shownLook = -1;
        long seenHurtTick = PlayerState.NeverHurt;

        int flashTicks;
        int blinkTicks;
        float blinkAlpha = 1f;
        float flashSeconds;
        float gameOverDelaySeconds;
        float deathMaxWaitSeconds;

        bool dead;
        float deathSeconds;
        float deathAnimationEndedAt = -1f;

        /// <summary>지금 흰색으로 번쩍이는 중인지(확인용).</summary>
        public bool IsFlashing => shownLook == (int)PlayerLook.Flash;

        /// <summary>깜빡임의 흐린 쪽인지(확인용).</summary>
        public bool IsBlinkDim => shownLook == (int)PlayerLook.BlinkDim;

        /// <summary>사망 연출 중인지 · 시작한 뒤 몇 초인지 · 사망 애니메이션이 끝까지 갔는지(확인용).</summary>
        public bool IsDead => dead;

        public float DeathSeconds => deathSeconds;

        public bool DeathAnimationEnded => deathAnimationEndedAt >= 0f;

        /// <summary>사망 연출이 끝났다(애니메이션 끝 + 기다림, 또는 최대 기다림을 넘김) — 이때 판 끝 창을 띄운다.</summary>
        public bool DeathPresentationDone =>
            dead && ((deathAnimationEndedAt >= 0f && deathSeconds - deathAnimationEndedAt >= gameOverDelaySeconds) || deathSeconds >= deathMaxWaitSeconds);

        void Awake()
        {
            parts = GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[parts.Length];
            baseMaterials = new Material[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                baseColors[i] = parts[i].color;
                baseMaterials[i] = parts[i].sharedMaterial;
            }
        }

        /// <summary>연출 수치(FeedbackTuning)를 받는다. 판을 시작할 때마다.</summary>
        public void Configure(FeedbackTuning tuning, int ticksPerSecond)
        {
            if (tuning == null)
            {
                flashTicks = 0;
                blinkTicks = 0;
                blinkAlpha = 1f;
                flashSeconds = 0f;
                gameOverDelaySeconds = 0f;
                deathMaxWaitSeconds = 0f;
                return;
            }
            flashTicks = FeedbackRules.TicksOf(tuning.PlayerFlashSeconds, ticksPerSecond);
            blinkTicks = FeedbackRules.TicksOf(tuning.PlayerBlinkSeconds, ticksPerSecond);
            blinkAlpha = tuning.PlayerBlinkAlpha;
            flashSeconds = tuning.PlayerFlashSeconds;
            gameOverDelaySeconds = tuning.GameOverDelaySeconds;
            deathMaxWaitSeconds = tuning.DeathMaxWaitSeconds;
        }

        /// <summary>판을 새로 시작할 때: 번쩍임 · 깜빡임 · 사망 자세를 끈다.</summary>
        public void ResetView()
        {
            seenHurtTick = PlayerState.NeverHurt;
            SetDead(false);
            SetLook(PlayerLook.Normal);
        }

        /// <param name="alpha">직전 틱(0)과 지금 틱(1) 사이 어디를 그릴지.</param>
        /// <param name="maxSpeed">최대 이동 속도. 애니메이터의 Speed는 0~1 비율로 넘긴다.</param>
        /// <param name="tick">World.Tick(맞은 뒤 몇 틱이 지났는지 보는 데 쓴다).</param>
        /// <param name="isDead">체력이 0이 되어 판이 끝났는지(World.Outcome == Defeat).</param>
        public void Render(in PlayerState state, float alpha, float maxSpeed, long tick, bool isDead)
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

            SetDead(isDead);

            // 새로 맞았으면 피격 애니메이션(Hit 트리거). 죽는 틱의 피격은 사망 애니메이션이 대신한다.
            if (state.LastHurtTick != seenHurtTick)
            {
                seenHurtTick = state.LastHurtTick;
                if (state.LastHurtTick != PlayerState.NeverHurt) PlayHit();
            }

            if (dead)
            {
                // 판이 끝나면 틱이 멈추므로 사망 연출은 시간으로 센다: 처음에 한 번 흰색, 그 뒤 보통.
                deathSeconds += Time.deltaTime;
                if (deathAnimationEndedAt < 0f && DeathClipEnded()) deathAnimationEndedAt = deathSeconds;
                SetLook(flashSeconds > 0f && deathSeconds <= flashSeconds ? PlayerLook.Flash : PlayerLook.Normal);
                return;
            }

            SetLook(FeedbackRules.PlayerLookAt(tick, state.LastHurtTick, state.InvulnerableUntilTick, flashTicks, blinkTicks));
        }

        void SetDead(bool value)
        {
            if (value == dead) return;
            dead = value;
            deathSeconds = 0f;
            deathAnimationEndedAt = -1f;
            if (animator == null) return;
            animator.SetBool(DeadParam, value);
            if (value) animator.ResetTrigger(HitParam);
        }

        bool DeathClipEnded()
        {
            if (animator == null) return true;
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            return info.shortNameHash == DeathState && info.normalizedTime >= 1f;
        }

        void SetLook(PlayerLook look)
        {
            if ((int)look == shownLook) return;
            shownLook = (int)look;
            bool flash = look == PlayerLook.Flash && flashMaterial != null;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                Color c = baseColors[i];
                if (look == PlayerLook.BlinkDim) c.a *= blinkAlpha;
                parts[i].color = c;
                parts[i].sharedMaterial = flash ? flashMaterial : baseMaterials[i];
            }
        }

        /// <summary>피격 애니메이션만 재생(사망 중에는 하지 않는다).</summary>
        public void PlayHit()
        {
            if (animator != null && !dead) animator.SetTrigger(HitParam);
        }
    }
}
