using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 도트 표시 기준(한 곳). 텍스처 임포트 규칙 · 픽셀 퍼펙트 카메라 · 위치 맞춤이 모두 이 값을 쓴다.
    /// 바꾸면 메뉴 Myong/Setup/Run All을 다시 돌린다.
    /// </summary>
    public static class PixelArt
    {
        /// <summary>1유닛 = 도트 몇 칸.</summary>
        public const int PixelsPerUnit = 32;

        /// <summary>기준 해상도(도트 칸 수). 실제 화면은 이 크기의 정수배로 확대된다.</summary>
        public const int ReferenceWidth = 960;
        public const int ReferenceHeight = 540;

        /// <summary>도트 격자에 맞춘 값.</summary>
        public static float Snap(float units) => Mathf.Round(units * PixelsPerUnit) / PixelsPerUnit;

        public static Vector3 Snap(float x, float y, float z) => new Vector3(Snap(x), Snap(y), z);
    }
}
