using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace PogoMan.Rebote.Editor
{
    public static class PrepararRebote
    {
        const string Base = "Assets/PogoMan/Rebote";
        [MenuItem("Pogo Man/Preparar protagonista y ojo")]
        public static void Preparar()
        {
            if (!AssetDatabase.IsValidFolder(Base + "/Generados")) AssetDatabase.CreateFolder(Base, "Generados");
            foreach (var nombre in new[]{"PogoMan","Ojo_Alado"})
            {
                bool ojo = nombre=="Ojo_Alado";
                var src = Base+"/Modelos/"+nombre+".fbx";
                var imp = AssetImporter.GetAtPath(src) as ModelImporter;
                if (!imp) throw new InvalidOperationException("Falta "+src);
                var clips=imp.clipAnimations;
                if(clips.Length==0) clips=imp.defaultClipAnimations;
                if(clips.Length!=(ojo?6:9)) throw new InvalidOperationException("Revisa los clips de "+src);
                bool cambio=imp.animationType!=ModelImporterAnimationType.Generic || imp.animationCompression!=ModelImporterAnimationCompression.Off;
                foreach(var clip in clips)
                {
                    string n=clip.name.Substring(clip.name.LastIndexOf('|')+1);
                    bool loop=n=="PM_Reposo"||n=="PM_Aire"||n=="Ojo_Vigilar"||n=="Ojo_Perseguir";
                    cambio |= clip.name!=n || clip.loopTime!=loop;
                    clip.name=n; clip.loopTime=loop;
                }
                if(cambio)
                {
                    imp.animationType=ModelImporterAnimationType.Generic;
                    imp.animationCompression=ModelImporterAnimationCompression.Off;
                    imp.clipAnimations=clips;imp.SaveAndReimport();
                }
                string dst=Base+"/Generados/"+nombre;
                var ctl=AssetDatabase.LoadAssetAtPath<AnimatorController>(dst+".controller");
                if(!ctl)ctl=AnimatorController.CreateAnimatorControllerAtPath(dst+".controller");
                var machine=ctl.layers[0].stateMachine;
                var animations=AssetDatabase.LoadAllAssetsAtPath(src).OfType<AnimationClip>().Where(c=>c.name.StartsWith(ojo?"Ojo_":"PM_")).ToArray();
                if(animations.Length!=clips.Length)throw new InvalidOperationException("No se resolvieron los clips en "+src);
                foreach(var clip in animations)
                {
                    var state=machine.states.Select(x=>x.state).FirstOrDefault(x=>x.name==clip.name)??machine.AddState(clip.name);
                    state.motion=clip;
                    if(clip.name==(ojo?"Ojo_Vigilar":"PM_Reposo")) machine.defaultState=state;
                }
                if(AssetDatabase.LoadAssetAtPath<GameObject>(dst+".prefab")) continue;
                var root=new GameObject(nombre);
                try
                {
                    var vis=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(src));
                    vis.transform.SetParent(root.transform,false);
                    var anim=vis.GetComponent<Animator>();if(!anim)anim=vis.AddComponent<Animator>();anim.applyRootMotion=false;anim.runtimeAnimatorController=ctl;
                    if(ojo)
                    {
                        // Blender faces -Y. Unity's imported forward convention can vary; derive it from the gaze bone.
                        var gaze=vis.GetComponentsInChildren<Transform>().FirstOrDefault(x=>x.name=="Gaze");
                        var nerve=vis.GetComponentsInChildren<Transform>().FirstOrDefault(x=>x.name=="Nervio0");
                        if(gaze && nerve)
                        {
                            var front=Vector3.ProjectOnPlane(gaze.position-nerve.position,Vector3.up);
                            if(front.sqrMagnitude>.0001f)vis.transform.rotation=Quaternion.FromToRotation(front.normalized,Vector3.forward)*vis.transform.rotation;
                        }
                        var rb=root.AddComponent<Rigidbody>();rb.useGravity=false;rb.isKinematic=true;rb.interpolation=RigidbodyInterpolation.Interpolate;
                        var sphere=root.AddComponent<SphereCollider>();sphere.radius=.35f;sphere.center=Vector3.up*.68f;sphere.isTrigger=true;
                        var ai=root.AddComponent<OjoAlado>();ai.animador=anim;ai.obstaculos=LayerMask.GetMask("Default","Terrain","Ground","Environment");
                    }
                    else root.AddComponent<PogoVisual>().animador = anim;
                    PrefabUtility.SaveAsPrefabAsset(root,dst+".prefab",out bool ok);
                    if(!ok)throw new InvalidOperationException(dst);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
