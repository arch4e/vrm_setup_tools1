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
                        // todo: SetExpressionToVRM();
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
    }
}
