#nullable enable

using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Runtime.UISystem;
using SampleGame.OutGame.ConfirmDialog;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SampleGame.Tests.Editor.OutGame
{
    /// <summary>
    /// ConfirmDialog のタブ順と、メッセージ / OK / Cancel のアクセシビリティ名を検証する。
    /// </summary>
    [TestFixture]
    public sealed class ConfirmDialogAccessibilityTests
    {
        private const string UxmlPath = "Assets/SampleGame/OutGame/ConfirmDialog/ConfirmDialog.uxml";

        private SceneSetup[]? _sceneSetup;
        private GameObject? _liveView;
        private string? _previousContentMode;
        private bool _contentModeChanged;
        private const string ContentModeKey = "SAMPLEGAME_CONTENT__RUNTIMEMODE";

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                if (EditorApplication.isPlaying)
                {
                    if (_liveView != null)
                    {
                        UnityEngine.Object.Destroy(_liveView);
                        yield return null;
                        _liveView = null;
                    }

                    yield return new ExitPlayMode();
                }

                if (_sceneSetup != null)
                {
                    if (_sceneSetup.Any(scene => scene.isLoaded && scene.isActive))
                        EditorSceneManager.RestoreSceneManagerSetup(_sceneSetup);
                    else
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    _sceneSetup = null;
                }
            }
            finally
            {
                if (_contentModeChanged)
                {
                    Environment.SetEnvironmentVariable(ContentModeKey, _previousContentMode,
                        EnvironmentVariableTarget.Process);
                    _contentModeChanged = false;
                }
            }
        }

        [Test]
        public void Uxml_SetsTabOrderAndLeavesMessageOutOfTheRing()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            Assert.That(tree, Is.Not.Null);

            var root = tree.CloneTree();
            var message = root.Q<Label>("message-label");
            var ok = root.Q<Button>("ok-button");
            var cancel = root.Q<Button>("cancel-button");

            Assert.That(message, Is.Not.Null);
            Assert.That(ok, Is.Not.Null);
            Assert.That(cancel, Is.Not.Null);
            Assert.That(message!.focusable, Is.False);
            Assert.That(message.tabIndex, Is.EqualTo(-1));
            Assert.That(ok!.focusable, Is.True);
            Assert.That(ok.tabIndex, Is.EqualTo(1));
            Assert.That(cancel!.focusable, Is.True);
            Assert.That(cancel.tabIndex, Is.EqualTo(2));
            Assert.That(
                UIToolkitInitialFocus.ListTabStops(root),
                Is.EqualTo(new VisualElement[] { ok, cancel }));
            Assert.That(UIToolkitInitialFocus.Select(root, "ok-button"), Is.SameAs(ok));
        }

        [UnityTest]
        public IEnumerator View_AssignsAccessibilityTextAndClearsItOnDestroy()
        {
            _sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _previousContentMode = Environment.GetEnvironmentVariable(ContentModeKey, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(ContentModeKey, "addressables", EnvironmentVariableTarget.Process);
            _contentModeChanged = true;
            yield return new EnterPlayMode();

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            Assert.That(tree, Is.Not.Null);
            var gameObject = new GameObject("ConfirmDialogAccessibility");
            _liveView = gameObject;
            var view = gameObject.AddComponent<ConfirmDialogView>();
            view.AssignVisualTreeAssetForEditor(tree);
            view.Initialize();

            var message = view.Root.Q<Label>("message-label")
                ?? throw new System.InvalidOperationException("message-label が見つかりません。");
            var ok = view.Root.Q<Button>("ok-button")
                ?? throw new System.InvalidOperationException("ok-button が見つかりません。");
            var cancel = view.Root.Q<Button>("cancel-button")
                ?? throw new System.InvalidOperationException("cancel-button が見つかりません。");

            Assert.That(UIAccessibilityText.TryGet(message, out var messageName, out var messageHint), Is.True);
            Assert.That(messageName, Is.EqualTo("HPを回復しますか？"));
            Assert.That(messageHint, Is.Null);
            Assert.That(UIAccessibilityText.TryGet(ok, out var okName, out var okHint), Is.True);
            Assert.That(okName, Is.EqualTo("OK"));
            Assert.That(okHint, Is.EqualTo("決定して閉じます"));
            Assert.That(UIAccessibilityText.TryGet(cancel, out var cancelName, out var cancelHint), Is.True);
            Assert.That(cancelName, Is.EqualTo("Cancel"));
            Assert.That(cancelHint, Is.EqualTo("取り消して閉じます"));

            UnityEngine.Object.Destroy(gameObject);
            yield return null;
            Assert.That(gameObject == null, Is.True);
            _liveView = null;

            Assert.That(UIAccessibilityText.TryGet(message, out _, out _), Is.False);
            Assert.That(UIAccessibilityText.TryGet(ok, out _, out _), Is.False);
            Assert.That(UIAccessibilityText.TryGet(cancel, out _, out _), Is.False);
        }
    }
}
