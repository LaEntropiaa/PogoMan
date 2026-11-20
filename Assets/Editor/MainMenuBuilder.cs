using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Herramienta del editor: genera la escena MainMenu con su UI.
// Se usa desde el menú de arriba: PogoMan > Crear escena de menú principal.
// Después de generarla se puede editar a mano como cualquier escena.
public static class MainMenuBuilder
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";

    private static readonly Color Background = new Color(0.10f, 0.08f, 0.18f);
    private static readonly Color ButtonColor = new Color(0.69f, 0.00f, 1.00f);
    private static readonly Color TitleColor = new Color(1.00f, 0.85f, 0.20f);

    [MenuItem("PogoMan/Crear escena de menú principal")]
    public static void Build()
    {
        if (!Application.isBatchMode)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (System.IO.File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog(
                    "Menú principal",
                    "Ya existe " + ScenePath + ". ¿Reemplazarla?",
                    "Reemplazar",
                    "Cancelar"))
                return;
        }

        var scene = EditorSceneManager.NewScene(
            NewSceneSetup.DefaultGameObjects,
            NewSceneMode.Single
        );

        // Cámara con fondo de color liso (sin cielo).
        Camera cam = Camera.main;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Background;

        // El EventSystem es lo que hace que los botones reciban clics.
        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule)
        );

        // Canvas: el "lienzo" donde vive toda la UI.
        var canvasGO = new GameObject(
            "Canvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var menu = new GameObject("MainMenu").AddComponent<MainMenu>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Transform root = canvasGO.transform;

        CreateText(root, "Title", "POGOMAN", font, 160, TitleColor,
            new Vector2(0, 260), new Vector2(1400, 220));

        Button play = CreateButton(root, "PlayButton", "Jugar", font, new Vector2(0, 0));
        UnityEventTools.AddPersistentListener(play.onClick, menu.Play);

        Button quit = CreateButton(root, "QuitButton", "Salir", font, new Vector2(0, -150));
        UnityEventTools.AddPersistentListener(quit.onClick, menu.Quit);

        EditorSceneManager.SaveScene(scene, ScenePath);
        PutFirstInBuild();

        Debug.Log("Menú principal creado en " + ScenePath);
    }

    private static Text CreateText(Transform parent, string name, string content,
        Font font, int size, Color color, Vector2 position, Vector2 boxSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = boxSize;

        var text = go.GetComponent<Text>();
        text.text = content;
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label,
        Font font, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(420, 110);

        var image = go.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = ButtonColor;

        var button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
        colors.pressedColor = new Color(0.6f, 0.6f, 0.6f);
        button.colors = colors;

        CreateText(go.transform, "Label", label, font, 56, Color.white,
            Vector2.zero, rect.sizeDelta);

        return button;
    }

    // La escena en la posición 0 de la build es la que abre el juego.
    private static void PutFirstInBuild()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.path != ScenePath)
            .ToList();

        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
