using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PogoMan.Guardianes.Editor
{
    public static class PrepararGuardianes
    {
        const string Base = "Assets/PogoMan/Guardianes";
        static readonly string[] Bosses = { "Fundidor", "Viuda", "Recaudador", "Barquero" };

        [MenuItem("Pogo Man/Preparar guardianes de los pilares")]
        public static void Crear()
        {
            string dir = Base + "/Generados";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Base, "Generados");
            foreach (string name in Bosses)
            {
                string path = Base + "/Modelos/" + name + ".fbx";
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) throw new InvalidOperationException("No se encontró " + path);
                var clips = imp.clipAnimations;
                if (clips.Length == 0) clips = imp.defaultClipAnimations;
                if (clips.Length != 6) throw new InvalidOperationException(name + " debe tener seis clips");
                bool dirty = imp.animationType != ModelImporterAnimationType.Generic;
                foreach (var clip in clips)
                {
                    string label = clip.name.Substring(clip.name.LastIndexOf('|') + 1);
                    bool loop = label.EndsWith("_Reposo") || label.EndsWith("_Avance");
                    dirty |= clip.name != label || clip.loopTime != loop;
                    clip.name = label;
                    clip.loopTime = loop;
                }
                if (dirty)
                {
                    imp.animationType = ModelImporterAnimationType.Generic;
                    imp.clipAnimations = clips;
                    imp.SaveAndReimport();
                }
                string ctlPath = dir + "/" + name + ".controller";
                var ctl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctlPath);
                if (!ctl) ctl = AnimatorController.CreateAnimatorControllerAtPath(ctlPath);
                var machine = ctl.layers[0].stateMachine;
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(c => c.name.StartsWith(name + "_")))
                {
                    var state = machine.states.Select(x => x.state).FirstOrDefault(x => x.name == clip.name);
                    if (!state)
                    {
                        state = machine.AddState(clip.name);
                        state.motion = clip;
                    }
                    if (!state.motion) state.motion = clip;
                    if (clip.name == name + "_Reposo") machine.defaultState = state;
                }
                if (machine.states.Count(x => x.state.name.StartsWith(name + "_")) != 6)
                    throw new InvalidOperationException("Revisa los estados de " + name);
                string prefabPath = dir + "/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var root = new GameObject(name);
                try
                {
                    var body = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    body.transform.SetParent(root.transform, false);
                    var animator = body.GetComponent<Animator>();
                    if (!animator) animator = body.AddComponent<Animator>();
                    animator.applyRootMotion = false;
                    animator.runtimeAnimatorController = ctl;
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool ok);
                    if (!ok) throw new InvalidOperationException(prefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Cuatro guardianes preparados en " + dir);
        }
    }
}
