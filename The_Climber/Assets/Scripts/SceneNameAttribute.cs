using UnityEngine;

// string 필드에 붙이면 Inspector에서 씬 파일을 끌어다 놓아 고를 수 있다 (값은 씬 이름으로 저장).
// 빌드 설정(File > Build Profiles)에 없는 씬이면 경고를 띄운다. 그리는 쪽은 Editor/SceneNamePropertyDrawer.
public class SceneNameAttribute : PropertyAttribute { }
