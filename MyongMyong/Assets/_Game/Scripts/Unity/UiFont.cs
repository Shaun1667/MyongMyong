using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// UI 글꼴 한 곳. 프로젝트에 한글 글꼴 파일이 없어서 기기에 깔린 글꼴로 동적 글꼴을 만든다.
    /// 후보를 앞에서부터 찾아 처음 있는 것을 쓴다(Windows · Android · iOS/macOS · Linux 순).
    /// 글꼴 파일을 프로젝트에 넣게 되면 이 클래스만 바꾼다.
    /// </summary>
    public static class UiFont
    {
        static readonly string[] Candidates =
        {
            "Malgun Gothic",
            "Noto Sans CJK KR",
            "Noto Sans KR",
            "NotoSansCJK",
            "Apple SD Gothic Neo",
            "AppleGothic",
            "NanumGothic",
            "Droid Sans Fallback",
            "Arial",
        };

        static Font cached;

        public static Font Get(int size)
        {
            if (cached == null) cached = Font.CreateDynamicFontFromOSFont(Candidates, size);
            return cached;
        }
    }
}
