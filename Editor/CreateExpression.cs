using System;
using System.IO;
using UniGLTF;
using UnityEngine;
using UniVRM10;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VST1
{
    public class VST1_Expression_CreateExpression : EditorWindow
    {
        private GameObject vrmPrefab                    = null;
        private UnityEngine.Object expressionFolder     = null;
        private SkinnedMeshRenderer skinnedMeshRenderer = null;
        private int selectedSourceIndex                 = 0;
        private int selectedExistExpressionOptionIndex  = 0;

        [MenuItem("VRM1/VST1/Expression/Create Expression")]
        static void Init()
        {
            var window = GetWindowWithRect<VST1_Expression_CreateExpression>(new Rect(0, 0, 400, 560));
            window.Show();
        }

        void OnGUI()
        {
            GUIStyle styleRadio = new GUIStyle(EditorStyles.radioButton);

            /* --- source selector --- */
            GUILayout.BeginHorizontal();
            GUILayout.Label("Blend Shape Source:");
            string[] sourceOptions = {"Prefab", "Mesh"};
            selectedSourceIndex = GUILayout.SelectionGrid(selectedSourceIndex, sourceOptions, 1, styleRadio);
            GUILayout.EndHorizontal();
            GUILayout.Space(10); // 5px

            /* --- exist clip option --- */
            GUILayout.BeginHorizontal();
            GUILayout.Label("Exist Expression File:");
            string[] existExpressionFileOptions = {"Set Weight to 1 (from 0 or empty)", "Skip"};
            selectedExistExpressionOptionIndex = GUILayout.SelectionGrid(selectedExistExpressionOptionIndex, existExpressionFileOptions, 1, styleRadio);
            GUILayout.EndHorizontal();
            GUILayout.Space(10); // 10px

            /* --- object field --- */
            vrmPrefab = (GameObject)EditorGUILayout.ObjectField("VRM Prefab", vrmPrefab, typeof(GameObject), false);

            if (sourceOptions[selectedSourceIndex] == "Mesh")
            {
                skinnedMeshRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Source Mesh", skinnedMeshRenderer,
                                                                                       typeof(SkinnedMeshRenderer), true);
            }
            expressionFolder = EditorGUILayout.ObjectField("Export Folder", expressionFolder, typeof(UnityEngine.Object), true);
            GUILayout.Space(5); // 5px

            /* --- create expression files --- */
            if (GUILayout.Button("Create Expressions"))
            {
                // check if the parameter is null
                if (vrmPrefab == null || expressionFolder == null) {
                    Debug.LogError("[VST1:Create Expression] VRM Prefab or Export Folder is null.");
                    return;
                }

                if (sourceOptions[selectedSourceIndex] == "Prefab") {
                    SkinnedMeshRenderer[] renderers = vrmPrefab.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (var renderer in renderers) {
                        CreateExpressionsFromSMR(renderer);
                        AddExpression();
                    }
                } else if (sourceOptions[selectedSourceIndex] == "Mesh") CreateExpressionsFromSMR(skinnedMeshRenderer);

                Debug.Log("[VST1:Create Expression] finish.");
            }
        }

        private void CreateExpressionsFromSMR(SkinnedMeshRenderer renderer)
        {
            var mesh = renderer.sharedMesh;

            Debug.Log("[VST1:Create Expression] --- Mesh: " + renderer.name + " ---");

            for (int i = 0; i < mesh.blendShapeCount; ++i)
            {
                VRM10Expression clip  = null;
                string blendShapeName = mesh.GetBlendShapeName(i);
                string savePath = AssetDatabase.GetAssetPath(expressionFolder);
                string dataPath = savePath + "/" + blendShapeName + ".asset";

                // get mesh relative path
                GameObject meshParent   = FindMeshParentObject(vrmPrefab.transform, renderer.name);
                string meshRelativePath = GetRelativePath(meshParent);

                // load or create expression file
                if (File.Exists(dataPath)) {
                    if (selectedExistExpressionOptionIndex == 1) continue;  // "selectedExistExpressionOptionIndex == 1" means "Skip"
                    clip = AssetDatabase.LoadAssetAtPath<VRM10Expression>(dataPath);
                    if (clip.Prefab == null) clip.Prefab = vrmPrefab;
                } else {
                    clip = ScriptableObject.CreateInstance<VRM10Expression>();
                    clip.Prefab = vrmPrefab;
                    AssetDatabase.CreateAsset(clip, dataPath);
                }

                if (clip.MorphTargetBindings.Length == 0) {
                    MorphTargetBinding binding = CreateMorphTargetBinding(meshRelativePath, i);
                    clip.MorphTargetBindings = new MorphTargetBinding[] { binding };
                } else {
                    if (Array.FindIndex(clip.MorphTargetBindings, x => x.RelativePath == meshRelativePath) == -1) {
                        Array.Resize(ref clip.MorphTargetBindings, clip.MorphTargetBindings.Length + 1);
                        clip.MorphTargetBindings[clip.MorphTargetBindings.Length - 1] = CreateMorphTargetBinding(meshRelativePath, i);
                    } else {
                        if (clip.MorphTargetBindings[Array.FindIndex(clip.MorphTargetBindings, x => x.RelativePath == meshRelativePath)].Weight == 0) {
                            clip.MorphTargetBindings[Array.FindIndex(clip.MorphTargetBindings, x => x.RelativePath == meshRelativePath)].Weight = 1.0f;
                        }
                    }
                }

                EditorUtility.SetDirty(clip);
            }

            AssetDatabase.SaveAssets();
        }

        private MorphTargetBinding CreateMorphTargetBinding(string meshRelativePath, int blendShapeIndex)
        {
            MorphTargetBinding binding = new MorphTargetBinding
            {
                RelativePath = meshRelativePath,
                Index        = blendShapeIndex,
                Weight       = 1.0f,
            };

            return binding;
        }

        private GameObject FindMeshParentObject(Transform transform, string meshName)
        {
            GameObject meshParentObject = null;
            for (int i = 0; i < transform.childCount && meshParentObject is null; i++)
            {
                Transform childTransform = transform.GetChild(i);
                if (childTransform.name == meshName) return childTransform.gameObject;
                else meshParentObject = FindMeshParentObject(childTransform, meshName);
            }

            return meshParentObject;
        }

        static private string GetRelativePath(GameObject gameObject) {
            string path = gameObject.name;
            Transform transform = (gameObject.GetComponent(typeof(Transform)) as Transform).parent;

            while (true) {
                if (transform != null && transform.gameObject.GetComponent(typeof(Animator)) == null) {
                    path = transform.gameObject.name + "/" + path;
                    transform = transform.parent;
                } else break;
            }

            return path;
        }

        private void AddExpression()
        {
            string expressionFilePath = AssetDatabase.GetAssetPath(expressionFolder);
            DirectoryInfo expressionDir = new DirectoryInfo(expressionFilePath);
            FileInfo[] expressionFiles  = expressionDir.GetFiles("*.asset");
            Vrm10Instance vrm = vrmPrefab.GetComponent<Vrm10Instance>();

            foreach (var expressionFile in expressionFiles)
            {
                VRM10Expression clip = AssetDatabase.LoadAssetAtPath<VRM10Expression>(expressionFilePath + "/" + expressionFile.Name);

                // guard
                if (clip?.GetType() != typeof(VRM10Expression)) continue;

                switch (clip.name) {
                    case "happy": if (vrm.Vrm.Expression.Happy is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.happy, clip); break;
                    case "angry": if (vrm.Vrm.Expression.Angry is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.angry, clip); break;
                    case "sad": if (vrm.Vrm.Expression.Sad is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.sad, clip); break;
                    case "relaxed": if (vrm.Vrm.Expression.Relaxed is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.relaxed, clip); break;
                    case "surprised": if (vrm.Vrm.Expression.Surprised is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.surprised, clip); break;
                    case "aa": if (vrm.Vrm.Expression.Aa is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.aa, clip); break;
                    case "ih": if (vrm.Vrm.Expression.Ih is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.ih, clip); break;
                    case "ou": if (vrm.Vrm.Expression.Ou is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.ou, clip); break;
                    case "ee": if (vrm.Vrm.Expression.Ee is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.ee, clip); break;
                    case "oh": if (vrm.Vrm.Expression.Oh is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.oh, clip); break;
                    case "blink": if (vrm.Vrm.Expression.Blink is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.blink, clip); break;
                    case "blinkLeft": if (vrm.Vrm.Expression.BlinkLeft is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.blinkLeft, clip); break;
                    case "blinkRight": if (vrm.Vrm.Expression.BlinkRight is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.blinkRight, clip); break;
                    case "lookUp": if (vrm.Vrm.Expression.LookUp is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.lookUp, clip); break;
                    case "lookDown": if (vrm.Vrm.Expression.LookDown is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.lookDown, clip); break;
                    case "lookLeft": if (vrm.Vrm.Expression.LookLeft is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.lookLeft, clip); break;
                    case "lookRight": if (vrm.Vrm.Expression.LookRight is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.lookRight, clip); break;
                    case "neutral": if (vrm.Vrm.Expression.Neutral is null) vrm.Vrm.Expression.AddClip(ExpressionPreset.neutral, clip); break;
                    default: if (vrm.Vrm.Expression.CustomClips.IndexOf(clip) < 0) vrm.Vrm.Expression.CustomClips.Add(clip); break;
                }
            }

            EditorUtility.SetDirty(vrm.Vrm);
            AssetDatabase.SaveAssets();
        }
    }
}
