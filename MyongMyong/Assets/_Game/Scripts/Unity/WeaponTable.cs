using System;
using System.Collections.Generic;
using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>무기 표. 새 무기 = 줄 하나(종류별 분기 코드 없음). 비주얼은 visualId로만 가리킨다 — 그림이 없으면 단색 도트.</summary>
    [CreateAssetMenu(menuName = "Myong/Data/Weapon Table", fileName = "WeaponTable")]
    public sealed class WeaponTable : ScriptableObject
    {
        [Serializable]
        public sealed class Row
        {
            [Tooltip("Key used by Starting Weapons (and later by level-up choices).")]
            public string id;
            [Tooltip("Seconds between volleys. Rounded to whole ticks (at least 1).")]
            [Min(0f)] public float fireInterval;
            [Tooltip("Bullet speed in units per second.")]
            [Min(0f)] public float bulletSpeed;
            [Tooltip("Damage one bullet deals to one enemy.")]
            [Min(0f)] public float damage;
            [Tooltip("Range in units: the bullet vanishes after flying this far (lifetime = range / speed). Enemies farther than this are not aimed at.")]
            [Min(0f)] public float range;
            [Tooltip("Bullets per volley. n bullets go to the n nearest enemies, one each.")]
            [Min(1)] public int shotsPerVolley = 1;
            [Tooltip("Enemies a bullet passes through after its first hit (0 = gone on the first hit).")]
            [Min(0)] public int pierce;
            [Tooltip("Bullet radius in units (hit test and the placeholder dot size).")]
            [Min(0f)] public float bulletRadius;
            [Tooltip("Bullet visual key. With no sprite for it, a solid placeholder dot is drawn.")]
            public string visualId;
        }

        [SerializeField] List<Row> rows = new List<Row>();

        [Tooltip("Weapon ids the player starts a run with.")]
        [SerializeField] List<string> startingWeapons = new List<string>();

        [Tooltip("Most bullets in flight at once (the pool size). When full, new shots are skipped.")]
        [SerializeField, Min(0)] int maxBullets;

        [Tooltip("Where bullets leave from: the player's position plus this many units up.")]
        [SerializeField] float muzzleHeight;

        public IReadOnlyList<Row> Rows => rows;

        public IReadOnlyList<string> StartingWeapons => startingWeapons;

        public int MaxBullets => maxBullets;

        public float MuzzleHeight => muzzleHeight;

        /// <summary>id의 줄 번호. 없으면 -1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].id == id) return i;
            }
            return -1;
        }

        /// <summary>표를 Core 규칙 수치로 옮긴다. 없는 무기 id를 가리키는 시작 무기는 오류를 남기고 뺀다.</summary>
        public WeaponRulesData ToRules()
        {
            var kinds = new WeaponKindData[rows.Count];
            for (int i = 0; i < kinds.Length; i++)
            {
                Row r = rows[i];
                kinds[i] = new WeaponKindData(r.fireInterval, r.bulletSpeed, r.damage, r.range, r.shotsPerVolley, r.pierce, r.bulletRadius);
            }

            var starting = new List<int>(startingWeapons.Count);
            foreach (string id in startingWeapons)
            {
                int index = IndexOf(id);
                if (index < 0)
                {
                    Debug.LogError($"[Myong] WeaponTable starting weapon '{id}' is not a row id", this);
                    continue;
                }
                starting.Add(index);
            }
            return new WeaponRulesData(kinds, starting.ToArray(), maxBullets, new Vec2(0f, muzzleHeight));
        }
    }
}
