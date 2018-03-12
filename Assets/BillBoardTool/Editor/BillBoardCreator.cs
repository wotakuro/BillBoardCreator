using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UTJ
{

    /// <summary>
    /// キャラクターのビルボードを事前に作成するためのスクリプトです
    /// </summary>
    public class BillBoardCreator : EditorWindow
    {
        private const int CameraNum = 8;
        private const int RenderWidth = 128;
        private const int RenderHeight = 128;
        private const string ResultDir = "BillBoardCapture";

        private Camera[] cameras = new Camera[CameraNum];

        private float oldTime = 0.0f;
        private int snapNum = 8;
        private float endNormalizeTime = 1.0f;
        private bool isRecording = false;
        private bool updateNowFlag = false;
        private System.Text.StringBuilder sb = new System.Text.StringBuilder(256);


        private Animator targetAnimator;
        private string[] stateNames;
        private int currentStateIdx;


        private string targetAnimationStateName
        {
            get
            {
                if (stateNames == null || currentStateIdx < 0 || currentStateIdx >= stateNames.Length)
                {
                    return "";
                }
                return stateNames[currentStateIdx];
            }
        }

        [MenuItem("Tools/CreateWindow")]
        public static void Create()
        {
            EditorWindow.GetWindow<BillBoardCreator>();
        }
        void OnEnable()
        {
            var animators = Resources.FindObjectsOfTypeAll<Animator>();
            if (animators != null && animators.Length > 0)
            {
                targetAnimator = animators[0];
            }
            OnChangetargetAnimator();
        }

        private void OnChangetargetAnimator()
        {
            if (targetAnimator == null) { return; }
            var controller = targetAnimator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            var stateMachine = controller.layers[0].stateMachine;
            var stats = stateMachine.states;
            stateNames = new string[stats.Length];
            for (int i = 0; i < stats.Length; ++i)
            {
                stateNames[i] = stats[i].state.name;
            }
        }

        void Update()
        {
            Repaint();
            if (!targetAnimator)
            {
                return;
            }
            if (!Application.isPlaying)
            {
                updateNowFlag = false;
                isRecording = false;
                currentStateIdx = 0;
                return;
            }

            if (updateNowFlag)
            {
                targetAnimator.Play(targetAnimationStateName);
                targetAnimator.Update(0.0f);
                this.SnapShot(targetAnimationStateName, 0);
                updateNowFlag = false;
            }
            // 撮影処理
            Application.targetFrameRate = 60;
            Time.captureFramerate = 60;
            float nowTime = targetAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            for (int i = 1; i < snapNum; ++i)
            {
                float snapTime = ((endNormalizeTime / (float)snapNum) * (float)i);
                if (!string.IsNullOrEmpty(targetAnimationStateName) && this.oldTime < snapTime && snapTime <= nowTime)
                {
                    this.SnapShot(targetAnimationStateName, i);
                }
            }
            //撮影中フラグ解除
            if (this.oldTime < 1.5f && 1.5f <= nowTime)
            {
                isRecording = false;
            }
            this.oldTime = nowTime;
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("ビルボード生成君");
            EditorGUILayout.LabelField("");
            var oldTargetAnimator = targetAnimator;
            targetAnimator = EditorGUILayout.ObjectField("操作対象", targetAnimator, typeof(Animator), true) as Animator;
            if (oldTargetAnimator != targetAnimator)
            {
                OnChangetargetAnimator();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("アプリケーションを実行中じゃないと出来ないEditor拡張です");
                if (GUILayout.Button("実行する"))
                {
                    EditorApplication.isPlaying = true;
                }
                isRecording = false;
                return;
            }
            if (!isRecording)
            {
                if (stateNames != null)
                {
                    currentStateIdx = EditorGUILayout.Popup("ステート指定", currentStateIdx, stateNames);
                }
                snapNum = EditorGUILayout.IntField("取るコマ数", snapNum);
                endNormalizeTime = EditorGUILayout.Slider("終了タイミング(0.0～1.0)", endNormalizeTime, 0.0f, 1.0f);
                if (GUILayout.Button("再生のみ"))
                {
                    updateNowFlag = true;
                }
                if (GUILayout.Button("Animatorを反映"))
                {
                    updateNowFlag = true;
                    isRecording = true;
                }
            }
            else
            {
                EditorGUILayout.LabelField("現在、撮影中につき変更できません");
            }
        }

        private void SnapShot(string name, int idx)
        {
            this.CreateCameras();
            this.SaveRenderResult(name, idx);
            this.DestoryCameras();
        }

        private void CreateCameras()
        {
            Camera mainCam = Camera.main;
            Vector3 pos = mainCam.transform.position;
            float y = pos.y;
            pos.y = 0.0f;
            float distance = pos.magnitude;
            float fov = mainCam.fieldOfView;

            for (int i = 0; i < CameraNum; ++i)
            {
                CreateCameraObject(i, distance, y, fov);
            }
        }


        private void CreateCameraObject(int idx, float distance, float yPos, float fov)
        {
            sb.Length = 0;
            sb.Append("Camera-").Append(idx);
            var gmo = new GameObject(sb.ToString());
            float angle = idx / (float)CameraNum * Mathf.PI * 2.0f;
            gmo.transform.position = new Vector3(Mathf.Sin(angle) * distance, yPos, Mathf.Cos(angle) * distance);
            gmo.transform.LookAt(new Vector3(0.0f, yPos, 0.0f));
            this.cameras[idx] = gmo.AddComponent<Camera>();
            this.cameras[idx].fieldOfView = fov;
            this.cameras[idx].targetTexture = new RenderTexture(RenderWidth, RenderHeight, 24, RenderTextureFormat.ARGB32); ;
            this.cameras[idx].clearFlags = CameraClearFlags.SolidColor;
            this.cameras[idx].backgroundColor = new Color(1.0f, 1.0f, 1.0f, 0);
        }

        private void DestoryCameras()
        {
            for (int i = 0; i < cameras.Length; ++i)
            {
                if (this.cameras[i] == null) { continue; }
                if (this.cameras[i].targetTexture)
                {
                    cameras[i].targetTexture.Release();
                }
                Object.DestroyImmediate(this.cameras[i].gameObject);
                this.cameras[i] = null;
            }
        }

        private void SaveRenderResult(string filehead, int idx)
        {
            if (!System.IO.Directory.Exists(ResultDir))
            {
                System.IO.Directory.CreateDirectory(ResultDir);
            }
            for (int i = 0; i < cameras.Length; ++i)
            {
                if (!cameras[i])
                {
                    Debug.LogError("Camera " + i + " is null");
                    continue;
                }
                cameras[i].Render();
                sb.Length = 0;
                sb.Append(ResultDir).Append("/").Append(targetAnimator.gameObject.name).Append("-").Append(targetAnimationStateName).Append("-pat-").Append( string.Format("{0:D2}",i) ).Append("-").Append(string.Format("{0:D2}",idx ) ).Append(".png");
                SaveRenderTexture(sb.ToString(), cameras[i].targetTexture);
            }
        }

        private void SaveRenderTexture(string file, RenderTexture targetTexture)
        {
            RenderTexture.active = targetTexture;
            var tex = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.RGBA32, false, false);

            tex.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), 0, 0);
            RenderTexture.active = null;

            System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
            Texture2D.DestroyImmediate(tex);
        }
    }
}