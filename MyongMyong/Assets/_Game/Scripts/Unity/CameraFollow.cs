using UnityEngine;

namespace Myong.Unity
{
    /// <summary>카메라가 대상을 따라간다. 대상이 이미 도트 격자에 맞춰져 있어 카메라도 격자 위에 있다.</summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 p = target.position;
            transform.position = PixelArt.Snap(p.x, p.y, transform.position.z);
        }
    }
}
