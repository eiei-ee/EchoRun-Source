using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class RuntimeSmokeTests
{
    [UnityTest]
    public IEnumerator RunnerPortraitUsesActualFaceAndRestoresGameplayRendering()
    {
        SavePreferenceSnapshot saved = CaptureSavePreferences();
        GameManager game = null;
        string output = Path.GetFullPath("TestResults/DetailPolish-20260925/Preview");
        Directory.CreateDirectory(output);
        try
        {
            InstallIsolatedSave(new EchoRunSaveData());
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return WaitForFreshRun(null, false);
            yield return new WaitForSeconds(.75f);
            game = GameManager.Instance;
            UIManager ui = Object.FindObjectOfType<UIManager>();
            Transform model = GameObject.Find("player").transform.Find("CharacterModel");
            Vector3 position = model.position;
            Quaternion rotation = model.rotation;
            var layers = new Dictionary<GameObject, int>();
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
                layers.Add(child.gameObject, child.gameObject.layer);
            Camera main = Camera.main;
            int originalMask = main.cullingMask;
            typeof(UIManager).GetMethod("ShowCharacter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
            yield return null; yield return null;
            RunnerColorPreview preview = Object.FindObjectOfType<RunnerColorPreview>();
            Assert.IsNotNull(preview);
            Assert.IsTrue(preview.GetComponent<RawImage>().raycastTarget);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, preview.transform.position)
            }, hits);
            Assert.IsNotEmpty(hits);
            Assert.AreEqual(preview.gameObject, hits[0].gameObject,
                "The portrait must receive drag input ahead of the parent scroll view.");
            Camera camera = GameObject.Find("RunnerPreviewCamera").GetComponent<Camera>();
            float fullDistance = Vector3.Distance(camera.transform.position, model.position);
            SavePreviewTexture(preview, output + "/full-body.png");
            preview.SetPortraitMode(true);
            yield return null; yield return null;
            Assert.IsTrue(preview.PortraitMode);
            Transform head = model.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
            Vector3 viewport = camera.WorldToViewportPoint(head.position);
            Assert.That(viewport.x, Is.InRange(.35f, .65f));
            Assert.That(viewport.y, Is.InRange(.30f, .70f));
            Assert.Less(Vector3.Distance(camera.transform.position, head.position), fullDistance * .6f);
            SavePreviewTexture(preview, output + "/face-front.png");
            Quaternion viewBefore = camera.transform.rotation;
            var pointer = new PointerEventData(EventSystem.current) { delta = new Vector2(100f, 0f) };
            ExecuteEvents.Execute(preview.gameObject, pointer, ExecuteEvents.dragHandler);
            yield return null;
            Assert.Greater(Quaternion.Angle(viewBefore, camera.transform.rotation), 5f);
            Assert.Less(Vector3.Distance(position, model.position), .001f);
            Assert.Less(Quaternion.Angle(rotation, model.rotation), .001f);
            SavePreviewTexture(preview, output + "/face-three-quarter.png");
            typeof(UIManager).GetMethod("HideCharacter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
            yield return null; yield return null;
            Assert.AreEqual(originalMask, main.cullingMask);
            foreach (var pair in layers) Assert.AreEqual(pair.Value, pair.Key.layer);
            Assert.IsNull(GameObject.Find("RunnerPreviewCamera"));
            Assert.IsNull(GameObject.Find("RunnerPreviewKey"));
            Assert.IsNull(GameObject.Find("RunnerPreviewFill"));
        }
        finally
        {
            if (game != null) game.ReturnToMenu();
            RestoreSavePreferences(saved);
        }
    }

    private static void SavePreviewTexture(RunnerColorPreview preview, string path)
    {
        RenderTexture before = RenderTexture.active;
        var target = (RenderTexture)preview.GetComponent<RawImage>().texture;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally { RenderTexture.active = before; Object.Destroy(pixels); }
    }
}
