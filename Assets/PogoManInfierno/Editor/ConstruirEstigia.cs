using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using PogoMan.Estigia;

namespace PogoMan.Estigia.Editor
{
    public static class ConstruirEstigia
    {
        const string Root = "Assets/PogoManInfierno";
        const string Generated = Root + "/Generado";
        [Serializable] class Appearance { public string id, file; public float[] color, emission; public bool sky; public float metal, rough, energy; }
        [Serializable] class Node { public string id, label, collision, motion; public float period, phase, amp; public string[] materials; }
        [Serializable] class Lamp { public string name, kind, anchor, aim; public float[] color; public float size, power; }
        [Serializable] class Layout { public Appearance[] materials; public Node[] nodes; public Lamp[] lights; }

        static Color RGB(float[] v) { return new Color(v[0], v[1], v[2], 1f); }
        static void PutTexture(Material m, string[] keys, Texture t)
        {
            foreach (string k in keys) if (m.HasProperty(k)) m.SetTexture(k, t);
        }
        static void PutColor(Material m, string[] keys, Color c)
        {
            foreach (string k in keys) if (m.HasProperty(k)) m.SetColor(k, c);
        }

        static Material BuildMaterial(Appearance a, bool urp, bool hd)
        {
            string shader = a.sky ? (urp ? "Universal Render Pipeline/Unlit" : hd ? "HDRP/Unlit" : "Unlit/Texture")
                : (urp ? "Universal Render Pipeline/Lit" : hd ? "HDRP/Lit" : "Standard");
            var sh = Shader.Find(shader);
            if (!sh) throw new InvalidOperationException("Falta el shader " + shader);
            string path = Generated + "/Materiales/" + a.id + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            Texture tex = string.IsNullOrEmpty(a.file) ? null : AssetDatabase.LoadAssetAtPath<Texture>(Root + "/Textures/" + a.file);
            if (!string.IsNullOrEmpty(a.file) && !tex) throw new FileNotFoundException(a.file);
            PutTexture(m, new[] { "_BaseMap", "_BaseColorMap", "_MainTex", "_UnlitColorMap" }, tex);
            PutColor(m, new[] { "_Color", "_BaseColor", "_UnlitColor" }, RGB(a.color).gamma);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", a.metal);
            foreach (string k in new[] { "_Smoothness", "_Glossiness" })
                if (m.HasProperty(k)) m.SetFloat(k, 1f - a.rough);
            if (a.energy > 0 && !a.sky)
            {
                m.EnableKeyword("_EMISSION");
                PutColor(m, new[] { "_EmissionColor", "_EmissiveColor" }, RGB(a.emission).gamma * a.energy);
                if (tex) PutTexture(m, new[] { "_EmissionMap", "_EmissiveColorMap" }, tex);
                if (tex && hd) m.EnableKeyword("_EMISSIVE_COLOR_MAP");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        [MenuItem("Pogo Man/Crear prefab El Estigia")]
        public static void Create()
        {
            var data = JsonUtility.FromJson<Layout>(File.ReadAllText(Root + "/Mapa.json"));
            if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder(Root, "Generado");
            if (!AssetDatabase.IsValidFolder(Generated + "/Materiales")) AssetDatabase.CreateFolder(Generated, "Materiales");
            string modelPath = Root + "/Meshes/El_Estigia.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            if (importer == null) throw new FileNotFoundException(modelPath);
            // Keep every gameplay marker, including empties lacking a renderer.
            if (!importer.preserveHierarchy || importer.addCollider || importer.meshCompression != ModelImporterMeshCompression.Off)
            {
                importer.preserveHierarchy = true;
                importer.addCollider = false;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.SaveAndReimport();
            }
            string atlas = Root + "/Textures/Tierra_Carmesi.jpg";
            var ti = (TextureImporter)AssetImporter.GetAtPath(atlas);
            if (ti.maxTextureSize < 4096) { ti.maxTextureSize = 4096; ti.SaveAndReimport(); }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var stage = new GameObject("El Estigia");
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.transform.SetParent(stage.transform, false);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var ids = new Dictionary<string, Transform>();
                foreach (var t in instance.GetComponentsInChildren<Transform>(true)) ids[t.name] = t;
                var labels = data.nodes.ToDictionary(n => n.label, n => ids[n.id]);
                float span = Vector3.ProjectOnPlane(labels["Impulso_01"].position - labels["Impulso_02"].position, Vector3.up).magnitude;
                if (span < .001f) throw new InvalidDataException("Escala u orientación inválida del FBX");
                instance.transform.localScale *= new Vector2(105f,17f).magnitude / span;

                string pipeline = GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.GetType().Name : "";
                var materials = data.materials.ToDictionary(a => a.id, a => BuildMaterial(a, pipeline.Contains("Universal"), pipeline.Contains("HDRender")));
                var tracks = new List<EstigiaEffects.Motion>();
                int colliders = 0;
                foreach (var n in data.nodes)
                {
                    var t = ids[n.id];
                    var renderer = t.GetComponent<MeshRenderer>();
                    if (renderer)
                    {
                        var slots = renderer.sharedMaterials;
                        for (int i = 0; i < slots.Length; i++)
                        {
                            var imported = slots[i] ? slots[i].name : "";
                            if (materials.TryGetValue(imported, out var material)) slots[i] = material;
                            else if (i < n.materials.Length) slots[i] = materials[n.materials[i]];
                            else throw new InvalidDataException("Material ausente en " + n.label);
                        }
                        renderer.sharedMaterials = slots;
                        if (n.collision == "collider") renderer.enabled = false;
                        if (n.label.Contains("eclipse") || n.motion != "")
                        {
                            renderer.shadowCastingMode = ShadowCastingMode.Off;
                            renderer.receiveShadows = false;
                        }
                    }
                    if (n.collision != "")
                    {
                        var filter = t.GetComponent<MeshFilter>();
                        if (!filter || !filter.sharedMesh) throw new InvalidDataException("Falta malla de colisión en " + n.label);
                        var col = t.gameObject.AddComponent<MeshCollider>();
                        col.sharedMesh = filter.sharedMesh;
                        colliders++;
                    }
                    if (n.motion != "")
                        tracks.Add(new EstigiaEffects.Motion { target = t, soul = n.motion == "alma",
                            period = n.period, phase = n.phase, amplitude = n.amp });
                }
                stage.AddComponent<EstigiaEffects>().movements = tracks.ToArray();
                var vents = Group(stage.transform, "Impulso de los once géiseres");
                for (int i = 1; i <= 11; i++)
                {
                    string key = i.ToString("00");
                    var vent = Group(vents, "Géiser " + key);
                    vent.position = labels["Impulso_" + key].position;
                    var trigger = vent.gameObject.AddComponent<SphereCollider>();
                    trigger.radius = .95f;
                    trigger.isTrigger = true;
                    vent.gameObject.AddComponent<EstigiaGeyser>().landing = labels["Isla_" + key + "_1"];
                }
                var lights = Group(stage.transform, "Iluminación del Estigia");
                foreach (var source in data.lights)
                {
                    var t = Group(lights, source.name);
                    t.position = labels[source.anchor].position;
                    Vector3 direction = labels[source.aim].position - t.position;
                    t.rotation = Quaternion.LookRotation(direction, Mathf.Abs(direction.normalized.y) > .99f ? Vector3.forward : Vector3.up);
                    var l = t.gameObject.AddComponent<Light>();
                    bool sun = source.kind == "SUN";
                    l.type = sun ? LightType.Directional : LightType.Point;
                    l.color = RGB(source.color);
                    l.intensity = sun ? source.power * .57f : Mathf.Clamp(Mathf.Sqrt(source.power) * .27f, 1f, 13f);
                    l.range = Mathf.Max(3f, Mathf.Sqrt(source.power) * .48f);
                    l.shadows = sun && source.name.StartsWith("Luz") ? LightShadows.Soft : LightShadows.None;
                }
                string path = Generated + "/El_Estigia.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(stage, path, out bool saved);
                if (!saved) throw new IOException("No se pudo guardar " + path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("El Estigia listo: " + colliders + " colisiones estáticas, 11 impulsos, " + tracks.Count + " efectos. " + path);
            }
            finally { UnityEngine.Object.DestroyImmediate(stage); }
        }
    }
}
