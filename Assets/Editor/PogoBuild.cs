using System.Linq;
using UnityEditor;
using UnityEngine;

// Compila el juego para compartir. Desde Unity: menú "Pogo Man > Compilar para Windows",
// o desde la terminal con ~/bin/pogoman-windows (lo hace todo y deja un .zip).
public static class PogoBuild
{
    [MenuItem("Pogo Man/Compilar para Windows")]
    public static void Windows()
    {
        string dir = System.Environment.GetEnvironmentVariable("POGO_BUILD");
        if (string.IsNullOrEmpty(dir))
            dir = System.IO.Path.GetFullPath("Builds/Windows");

        // SampleScene es la escena de pruebas de Alonso: no va en la copia para jugar.
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled && !s.path.EndsWith("SampleScene.unity")).Select(s => s.path).ToArray();
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = System.IO.Path.Combine(dir, "PogoMan.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("POGO_BUILD_RESULT " + report.summary.result + " " + dir);
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}
