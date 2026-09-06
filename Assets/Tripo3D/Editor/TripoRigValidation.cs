using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.Animations;
using UnityEngine.Playables;
[InitializeOnLoad] public static class TripoRigValidation
{
 static string Slug,Dir,Work;
 const string RequestPath="Temp/tripo-rig-validation.json";
 [Serializable] class Request {public string slug,assetDirectory,workDirectory;}
 static double nextCheck;
 static bool running;
 static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
 [Serializable] class Result {public bool ok,avatarValid,avatarHuman;public string error,fbxSha256;public string[] clips;public float[] durations;public int mappedBones;public float jumpRise;}
 [Serializable] class Topology {public int[] triangles;public Vector3[] vertices;}
 static TripoRigValidation(){EditorApplication.update+=Tick;}
 static void Tick(){if(EditorApplication.timeSinceStartup<nextCheck)return;nextCheck=EditorApplication.timeSinceStartup+2;if(File.Exists(RequestPath))Run();}
 [MenuItem("Tripo 3D/Validate Requested Rig")]
 public static void Run(){
  if(running||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(RequestPath))return;
  var request=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
  if(request==null||string.IsNullOrEmpty(request.slug)||string.IsNullOrEmpty(request.assetDirectory)||string.IsNullOrEmpty(request.workDirectory))return;
  Slug=request.slug;Dir=request.assetDirectory;Work=request.workDirectory;
  if(!Dir.StartsWith("Assets/",StringComparison.Ordinal)||Slug!=Path.GetFileName(Slug))return;
  var fbxPath=Dir+"/"+Slug+".fbx";if(!File.Exists(fbxPath))return;
  Directory.CreateDirectory(Work);string hash=Hash(fbxPath);
  if(File.Exists(Work+"/unity-result.json")){var prior=JsonUtility.FromJson<Result>(File.ReadAllText(Work+"/unity-result.json"));if(prior!=null&&prior.fbxSha256==hash)return;}
  running=true;
  foreach(var job in Tripo3D.Editor.TripoSession.instance.jobs.Where(j=>j.kind=="BlenderRig" && j.assetPath==fbxPath))
  {job.status="Running";job.progress=95;job.message="Checking Unity Humanoid playback and mesh clearance...";}
  Tripo3D.Editor.TripoSession.instance.Persist();
  Tripo3D.Editor.TripoJobRunner.ReconcileOpenJobs();
  var result=new Result{fbxSha256=hash};
  try{
   string path=Dir+"/"+Slug+".fbx";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
   var sourceModel=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(sourceModel==null||sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length!=1||sourceModel.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().Count()!=1)throw new Exception("This validator currently supports one skinned mesh and one albedo material; use a matching validator for other profiles.");var imp=(ModelImporter)AssetImporter.GetAtPath(path);imp.isReadable=true;imp.animationType=ModelImporterAnimationType.Human;imp.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;imp.importAnimation=true;imp.animationCompression=ModelImporterAnimationCompression.Off;imp.importNormals=ModelImporterNormals.Import;imp.importTangents=ModelImporterTangents.Import;imp.meshCompression=ModelImporterMeshCompression.Off;
   var map=new Dictionary<HumanBodyBones,string>{{HumanBodyBones.Hips,"Hips"},{HumanBodyBones.Spine,"Spine"},{HumanBodyBones.Chest,"Chest"},{HumanBodyBones.Neck,"Neck"},{HumanBodyBones.Head,"Head"}};
   foreach(var side in new[]{"Left","Right"}){string suffix=side=="Left"?".L":".R";string[] h={"Shoulder","UpperArm","LowerArm","Hand","UpperLeg","LowerLeg","Foot","Toes"};string[] b={"Clavicle","UpperArm","Forearm","Hand","Thigh","Shin","Foot","Toes"};for(int i=0;i<h.Length;i++)map[(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+h[i])]=b[i]+suffix;foreach(var f in new[]{"Thumb","Index","Middle","Ring","Little"}){var joints=new[]{"Proximal","Intermediate","Distal"};for(int j=0;j<3;j++)map[(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+f+joints[j])]=f+(j+1)+suffix;}}
   var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var hd=imp.humanDescription;hd.human=map.Select(k=>new HumanBone{humanName=HumanTrait.BoneName[(int)k.Key],boneName=k.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray();hd.skeleton=model.GetComponentsInChildren<Transform>(true).Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();hd.armStretch=0;hd.legStretch=0;imp.humanDescription=hd;
   var clips=imp.defaultClipAnimations;foreach(var c in clips){c.name=c.name.Split('|').Last();c.loopTime=c.name=="Idle"||c.name=="Run";c.loopPose=c.loopTime;c.lockRootRotation=true;c.lockRootPositionXZ=true;c.lockRootHeightY=true;c.keepOriginalOrientation=true;c.keepOriginalPositionXZ=true;c.keepOriginalPositionY=true;}imp.clipAnimations=clips;imp.SaveAndReimport();
   string texture=Dir+"/"+Slug+"_texture.png";AssetDatabase.ImportAsset(texture,ImportAssetOptions.ForceSynchronousImport);var ti=(TextureImporter)AssetImporter.GetAtPath(texture);ti.sRGBTexture=true;ti.maxTextureSize=4096;ti.SaveAndReimport();var mat=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Character.mat");if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,Dir+"/Character.mat");}mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texture));mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",.25f);EditorUtility.SetDirty(mat);
   model=AssetDatabase.LoadAssetAtPath<GameObject>(path);foreach(var sm in model.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct())imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),sm.name),mat);imp.SaveAndReimport();
   model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();result.avatarValid=avatar!=null&&avatar.isValid;result.avatarHuman=avatar!=null&&avatar.isHuman;if(!result.avatarValid||!result.avatarHuman)throw new Exception("Invalid Humanoid avatar");var anims=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();result.clips=anims.Select(c=>c.name).ToArray();result.durations=anims.Select(c=>c.length).ToArray();if(anims.Length!=4||!anims.Select(c=>c.name).OrderBy(n=>n).SequenceEqual(new[]{"Idle","Jump","Run","SwordSlash"})||anims.Any(c=>!c.humanMotion))throw new Exception("Expected four Humanoid clips");
   var preview=EditorSceneManager.NewPreviewScene();var go=(GameObject)PrefabUtility.InstantiatePrefab(model,preview);var animator=go.GetComponent<Animator>();animator.avatar=avatar;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();result.mappedBones=map.Keys.Count(k=>animator.GetBoneTransform(k)!=null);if(result.mappedBones!=51)throw new Exception("Expected all 51 Humanoid bone mappings");var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();skin.quality=SkinQuality.Bone4;var baked=new Mesh();File.WriteAllText(Work+"/unity-topology.json",JsonUtility.ToJson(new Topology{triangles=skin.sharedMesh.triangles,vertices=skin.sharedMesh.vertices}));
   foreach(var clip in anims){var graph=PlayableGraph.Create("Validate"+clip.name);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var output=AnimationPlayableOutput.Create(graph,"Animation",animator);var playable=AnimationClipPlayable.Create(graph,clip);output.SetSourcePlayable(playable);graph.Play();float min=100,max=-100;int count=Mathf.RoundToInt(clip.length*120)+1;
    using(var writer=new BinaryWriter(File.Create(Work+"/unity-"+clip.name+".bin"))){writer.Write(count);writer.Write(skin.sharedMesh.vertexCount);for(int i=0;i<count;i++){playable.SetTime(i/120.0);graph.Evaluate(0);float y=animator.GetBoneTransform(HumanBodyBones.Hips).position.y;min=Mathf.Min(min,y);max=Mathf.Max(max,y);skin.BakeMesh(baked);foreach(var v in baked.vertices){var w=skin.transform.TransformPoint(v);writer.Write(w.x);writer.Write(w.y);writer.Write(w.z);}}}if(clip.name=="Jump")result.jumpRise=max-min;graph.Destroy();}
   UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(go);EditorSceneManager.ClosePreviewScene(preview);if(result.jumpRise<.2f)throw new Exception("Jump height did not animate");
   string cp=Dir+"/Character.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(cp);if(controller==null){controller=AnimatorController.CreateAnimatorControllerAtPath(cp);var sm=controller.layers[0].stateMachine;foreach(var clip in anims){var state=sm.AddState(clip.name);state.motion=clip;if(clip.name=="Idle")sm.defaultState=state;}}
   var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);try{instance.GetComponent<Animator>().runtimeAnimatorController=controller;instance.GetComponent<Animator>().applyRootMotion=false;instance.GetComponentInChildren<SkinnedMeshRenderer>().quality=SkinQuality.Bone4;PrefabUtility.SaveAsPrefabAsset(instance,Dir+"/Character.prefab");}finally{UnityEngine.Object.DestroyImmediate(instance);}AssetDatabase.SaveAssets();Selection.activeObject=model;EditorGUIUtility.PingObject(model);result.ok=true;
  }catch(Exception ex){result.error=ex.ToString();Debug.LogException(ex);}File.WriteAllText(Work+"/unity-result.json",JsonUtility.ToJson(result,true));running=false;
 }
}
