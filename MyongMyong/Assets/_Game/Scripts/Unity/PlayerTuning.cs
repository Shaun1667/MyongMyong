using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>플레이어 수치 표. 코드에 숫자를 쓰지 않고 이 에셋에서 읽는다.</summary>
    [CreateAssetMenu(menuName = "Myong/Data/Player Tuning", fileName = "PlayerTuning")]
    public sealed class PlayerTuning : ScriptableObject
    {
        [Tooltip("Move speed in units per second (1 unit = PixelArt.PixelsPerUnit dots).")]
        [SerializeField, Min(0f)] float moveSpeed;

        [Tooltip("Pickup radius in units: experience gems inside it fly to the player.")]
        [SerializeField, Min(0f)] float pickupRadius;

        [Tooltip("Health at the start of a run. The run ends when it reaches 0.")]
        [SerializeField, Min(0f)] float maxHealth;

        [Tooltip("Body radius in units. An enemy closer than this plus its own radius touches the player.")]
        [SerializeField, Min(0f)] float bodyRadius;

        [Tooltip("Seconds after a hit during which enemy contact does no damage (rounded to ticks).")]
        [SerializeField, Min(0f)] float hurtInvulnerableSeconds;

        public float MoveSpeed => moveSpeed;

        public float PickupRadius => pickupRadius;

        public float MaxHealth => maxHealth;

        public float BodyRadius => bodyRadius;

        public float HurtInvulnerableSeconds => hurtInvulnerableSeconds;

        public PlayerTuningData ToData() => new PlayerTuningData(moveSpeed, pickupRadius, maxHealth, bodyRadius, hurtInvulnerableSeconds);
    }
}
