using System.IO;
using UnityEditor;

// Lets the asset tools ask a running Editor to rebuild the monsters: when Tools/rebuild-monsters.request
// exists after a script reload (or on leaving play mode), the request is removed and Roguelike/Build Monsters
// runs once, as if picked from the menu.
[InitializeOnLoad]
static class MonsterRebuildRequest
{
    const string RequestPath = "Tools/rebuild-monsters.request";

    static MonsterRebuildRequest()
    {
        EditorApplication.delayCall += Check;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Check;
        };
    }

    static void Check()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;
        File.Delete(RequestPath);
        MonsterSetup.BuildAll();
    }
}
