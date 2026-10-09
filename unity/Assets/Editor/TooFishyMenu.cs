using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TooFishy.Editor
{
    public static class TooFishyMenu
    {
        [MenuItem("Too Fishy/Open Main Scene")]
        static void OpenMain()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        }

        [MenuItem("Too Fishy/Play Main Scene")]
        static void PlayMain()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
                EditorApplication.isPlaying = true;
            }
        }
    }
}
