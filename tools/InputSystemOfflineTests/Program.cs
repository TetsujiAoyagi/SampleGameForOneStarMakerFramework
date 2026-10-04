#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OneStarMaker.Runtime.InputSystem;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UpdateSystem.Api;

namespace OneStarMaker.InputSystemOfflineTests
{
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static int Main()
        {
            Run(nameof(SceneState_OnlyStableAccepts_AndTheFourteenValuesStayInOrder), SceneState_OnlyStableAccepts_AndTheFourteenValuesStayInOrder);
            Run(nameof(LayerOrder_IsBeforeGameplayDefaultAndCamera), LayerOrder_IsBeforeGameplayDefaultAndCamera);
            Run(nameof(KindMap_PublishesButtonAndVector2Only), KindMap_PublishesButtonAndVector2Only);
            Run(nameof(Profile_AcceptsOnlyTheDefaultId), Profile_AcceptsOnlyTheDefaultId);
            Run(nameof(Map_RejectsUndefinedValues), Map_RejectsUndefinedValues);
            Run(nameof(Publication_KeepsActiveMapAndClearsStaleLevels), Publication_KeepsActiveMapAndClearsStaleLevels);
            Run(nameof(ImmediateBoundaries_OverwriteRetainedSpan), ImmediateBoundaries_OverwriteRetainedSpan);
            Run(nameof(Copy_LengthMismatchDoesNotPublishAPartialBuffer), Copy_LengthMismatchDoesNotPublishAPartialBuffer);
            Run(nameof(Catalog_RejectsDuplicateNamesAndBrokenIndexes), Catalog_RejectsDuplicateNamesAndBrokenIndexes);
            Run(nameof(Lookup_IsByActionNameAndMap), Lookup_IsByActionNameAndMap);
            Run(nameof(Sample_DoesNotAllocate), Sample_DoesNotAllocate);
            Run(nameof(ExistingAsset_HasPlayerAndUiAndDefersPoseActions), ExistingAsset_HasPlayerAndUiAndDefersPoseActions);
            Run(nameof(RuntimeInputSources_DoNotNameTheGameOrTheEditor), RuntimeInputSources_DoNotNameTheGameOrTheEditor);

            Console.WriteLine($"InputSystem offline: {_passed} passed, {_failed} failed, {_passed + _failed} executed");
            return _failed == 0 && _passed > 0 ? 0 : 1;
        }

        private static void Run(string name, Action body)
        {
            try
            {
                body();
                _passed++;
                Console.WriteLine("PASS " + name);
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("FAIL " + name);
                Console.WriteLine(ex);
            }
        }

        private static void SceneState_OnlyStableAccepts_AndTheFourteenValuesStayInOrder()
        {
            var expected = new[]
            {
                (SceneState.None, 0),
                (SceneState.PreLoading, 1),
                (SceneState.PreLoaded, 2),
                (SceneState.Loading, 3),
                (SceneState.Loaded, 4),
                (SceneState.WaitLoadChildScene, 5),
                (SceneState.Initializing, 6),
                (SceneState.LoadCanceled, 7),
                (SceneState.Stable, 8),
                (SceneState.PreUnloading, 9),
                (SceneState.PreUnloaded, 10),
                (SceneState.Unloading, 11),
                (SceneState.Unloaded, 12),
                (SceneState.AfterUnloading, 13),
            };
            var actual = (SceneState[])Enum.GetValues(typeof(SceneState));
            Equal(expected.Length, actual.Length, "SceneState count");
            for (var i = 0; i < expected.Length; i++)
            {
                Equal(expected[i].Item1, actual[i], "SceneState order " + i);
                Equal(expected[i].Item2, (int)actual[i], "SceneState value " + i);
                Equal(expected[i].Item1 == SceneState.Stable, InputAcceptance.IsAccepting(actual[i]), "accept " + actual[i]);
            }

            True(!InputAcceptance.IsAccepting((SceneState)99), "undefined state");
            True(!InputAcceptance.IsAccepting((SceneState)(-1)), "negative state");
        }

        private static void LayerOrder_IsBeforeGameplayDefaultAndCamera()
        {
            Equal("Input", UpdateLayerIds.Input, "layer id");
            Equal(-100, UpdateLayerIds.InputLayerOrder, "input order");
            True(UpdateLayerIds.InputLayerOrder < 0, "before adapter default 0");
            True(UpdateLayerIds.InputLayerOrder < UpdateLayerIds.CameraLayerOrder, "before camera");
            True(UpdateLayerIds.InputLayerOrder < UpdateLayerIds.StreamingLayerOrder, "before streaming");
            Equal(50, UpdateLayerIds.CameraLayerOrder, "camera order unchanged");
            Equal(60, UpdateLayerIds.StreamingLayerOrder, "streaming order unchanged");
        }

        private static void KindMap_PublishesButtonAndVector2Only()
        {
            True(InputActionKindMap.TryGetKind(InputActionKindMap.ButtonControl, out var button), "button");
            Equal(InputActionValueKind.Button, button, "button kind");
            True(InputActionKindMap.TryGetKind(InputActionKindMap.Axis2Control, out var axis), "axis");
            Equal(InputActionValueKind.Axis2, axis, "axis kind");
            True(!InputActionKindMap.TryGetKind("Vector3", out _), "vector3");
            True(!InputActionKindMap.TryGetKind("Quaternion", out _), "quaternion");
            True(!InputActionKindMap.TryGetKind("button", out _), "case");
            True(!InputActionKindMap.TryGetKind(null, out _), "null");
            True(!InputActionKindMap.TryGetKind(string.Empty, out _), "empty");
        }

        private static void Profile_AcceptsOnlyTheDefaultId()
        {
            var frame = NewFrame();
            True(frame.TrySelectProfile(InputProfileId.Default), "static default");
            True(frame.TrySelectProfile(new InputProfileId(InputProfileId.DefaultValue)), "same text");
            True(!frame.TrySelectProfile(new InputProfileId("one-handed")), "one-handed");
            True(!frame.TrySelectProfile(new InputProfileId(null)), "null");
            True(!frame.TrySelectProfile(new InputProfileId(string.Empty)), "empty");
            True(frame.ActiveProfile.Equals(InputProfileId.Default), "stays default");
        }

        private static void Map_RejectsUndefinedValues()
        {
            var frame = NewFrame();
            Equal(InputControlMap.Player, frame.ActiveMap, "initial");
            True(frame.TrySetMap(InputControlMap.UI, out var changed), "to ui");
            True(changed, "ui changed");
            True(frame.TrySetMap(InputControlMap.UI, out changed), "ui again");
            True(!changed, "same map");
            True(!frame.TrySetMap((InputControlMap)9, out changed), "undefined");
            True(!changed, "undefined changed flag");
            Equal(InputControlMap.UI, frame.ActiveMap, "stays ui");
        }

        private static void Publication_KeepsActiveMapAndClearsStaleLevels()
        {
            var reader = new FillReader();
            var frame = NewFrame();
            frame.SetInteractionState(SceneState.Stable);
            frame.Sample(reader);
            Equal(1, reader.Calls, "read once");
            True(frame.TryRead(0, out var move), "move");
            Equal(4f, move.X, "move x");
            Equal(0, move.Index, "move index");
            Equal(InputControlMap.Player, move.Map, "move map");
            Equal(InputActionValueKind.Axis2, move.Kind, "move kind");
            True(frame.TryRead(2, out var navigate), "navigate");
            Equal(0f, navigate.X, "inactive x");
            Equal(0f, navigate.Y, "inactive y");
            Equal(InputControlMap.UI, navigate.Map, "navigate map");

            frame.SetInteractionState(SceneState.Loading);
            frame.Sample(reader);
            Equal(1, reader.Calls, "no read while loading");
            True(frame.TryRead(0, out move), "move after");
            Equal(0f, move.X, "cleared x");
            Equal(0f, move.Y, "cleared y");
            Equal(0, move.Index, "index kept");
            Equal(InputActionValueKind.Axis2, move.Kind, "kind kept");

            reader.X = 8f;
            frame.SetInteractionState(SceneState.Stable);
            frame.Sample(reader);
            True(frame.TryRead(0, out move), "fresh");
            Equal(8f, move.X, "fresh x");
        }

        private static void ImmediateBoundaries_OverwriteRetainedSpan()
        {
            var frame = NewFrame();
            var reader = new FillReader();
            frame.SetInteractionState(SceneState.Stable);
            frame.Sample(reader);
            var span = frame.Published;
            frame.SetInteractionState(SceneState.Loading);
            Equal(0f, span[0].X, "immediate stop");
            Equal(0, span[0].Index, "identity retained");
            frame.SetInteractionState(SceneState.Stable);
            Equal(0f, span[0].X, "stable alone remains neutral");
            frame.Sample(reader);
            True(!frame.TrySetMap((InputControlMap)99, out _), "invalid map");
            Equal(4f, span[0].X, "invalid preserves values");
            frame.TrySetMap(InputControlMap.UI, out _);
            Equal(0f, span[0].X, "immediate map change");
            frame.Sample(reader);
            Equal(4f, span[2].X, "new selected values");
            frame.Neutralize();
            Equal(0f, span[2].X, "owner disposal boundary");
            Equal(2, span[2].Index, "ui identity retained");
        }

        private static void Copy_LengthMismatchDoesNotPublishAPartialBuffer()
        {
            var destination = new InputActionValue[1];
            destination[0] = new InputActionValue(3, InputControlMap.UI, InputActionValueKind.Button, 1f, 1f);
            var source = new InputActionValue[2];
            True(!InputPublication.TryCopy(source, destination), "mismatch");
            Equal(1f, destination[0].X, "destination unchanged");
            Equal(3, destination[0].Index, "identity unchanged");
        }

        private static void Catalog_RejectsDuplicateNamesAndBrokenIndexes()
        {
            Throws<ArgumentException>(() => new InputFrame(new[]
            {
                new InputActionSlot(0, InputControlMap.Player, InputActionValueKind.Button, "A"),
                new InputActionSlot(1, InputControlMap.Player, InputActionValueKind.Button, "A"),
            }));
            Throws<ArgumentException>(() => new InputFrame(new[]
            {
                new InputActionSlot(1, InputControlMap.Player, InputActionValueKind.Button, "A"),
            }));
            Throws<ArgumentException>(() => new InputActionSlot(0, InputControlMap.Player, InputActionValueKind.Button, ""));
        }

        private static void Lookup_IsByActionNameAndMap()
        {
            var frame = NewFrame();
            True(frame.TryGetActionIndex("Move", InputControlMap.Player, out var move), "move");
            Equal(0, move, "move index");
            True(!frame.TryGetActionIndex("Move", InputControlMap.UI, out _), "move is not ui");
            True(frame.TryGetActionIndex("Submit", InputControlMap.UI, out var submit), "submit");
            Equal(3, submit, "submit index");
            True(!frame.TryGetActionIndex(null, InputControlMap.Player, out _), "null name");
            True(!frame.TryRead(-1, out _), "negative");
            True(!frame.TryRead(4, out _), "past end");
        }

        private static void Sample_DoesNotAllocate()
        {
            var reader = new FillReader();
            var frame = NewFrame();
            Exercise(frame, reader);
            Exercise(frame, reader);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2000; i++)
            {
                Exercise(frame, reader);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Equal(0L, allocated, "allocated bytes");
        }

        private static void Exercise(InputFrame frame, FillReader reader)
        {
            frame.SetInteractionState(SceneState.Stable);
            frame.TrySetMap(InputControlMap.UI, out _);
            frame.Sample(reader);
            frame.TryGetActionIndex("Navigate", InputControlMap.UI, out _);
            frame.SetInteractionState(SceneState.Loading);
            frame.Sample(reader);
            frame.TrySetMap(InputControlMap.Player, out _);
            frame.TrySelectProfile(InputProfileId.Default);
        }

        private static void ExistingAsset_HasPlayerAndUiAndDefersPoseActions()
        {
            var path = Path.Combine(FindRepoRoot(), "unity", "Assets", "InputSystem_Actions.inputactions");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var maps = document.RootElement.GetProperty("maps");
            var names = new HashSet<string>(StringComparer.Ordinal);
            var deferred = new HashSet<string>(StringComparer.Ordinal)
            {
                "TrackedDevicePosition",
                "TrackedDeviceOrientation",
            };
            var deferredSeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var map in maps.EnumerateArray())
            {
                var mapName = map.GetProperty("name").GetString();
                True(mapName == InputActionMapNames.Player || mapName == InputActionMapNames.UI, "map " + mapName);
                names.Add(mapName ?? string.Empty);
                foreach (var action in map.GetProperty("actions").EnumerateArray())
                {
                    var actionName = action.GetProperty("name").GetString();
                    var control = action.GetProperty("expectedControlType").GetString();
                    var supported = InputActionKindMap.TryGetKind(control, out _);
                    if (deferred.Contains(actionName ?? string.Empty))
                    {
                        True(!supported, actionName + " must stay unpublished");
                        deferredSeen.Add(actionName ?? string.Empty);
                    }
                    else
                    {
                        True(supported, actionName + " control " + control);
                    }
                }
            }

            True(names.SetEquals(new[] { InputActionMapNames.Player, InputActionMapNames.UI }), "map set");
            True(deferredSeen.SetEquals(deferred), "pose set");
        }

        private static void RuntimeInputSources_DoNotNameTheGameOrTheEditor()
        {
            var directory = Path.Combine(
                FindRepoRoot(),
                "unity",
                "Assets",
                "OneStarMaker",
                "Scripts",
                "Runtime",
                "InputSystem");
            var forbidden = new[] { "SampleGame", "FlyController", "NewStg", "UnityEditor" };
            foreach (var file in Directory.GetFiles(directory, "*.cs"))
            {
                var text = File.ReadAllText(file);
                foreach (var word in forbidden)
                {
                    True(text.IndexOf(word, StringComparison.Ordinal) < 0, Path.GetFileName(file) + " contains " + word);
                }
            }
        }

        private static InputFrame NewFrame()
        {
            return new InputFrame(new[]
            {
                new InputActionSlot(0, InputControlMap.Player, InputActionValueKind.Axis2, "Move"),
                new InputActionSlot(1, InputControlMap.Player, InputActionValueKind.Button, "Attack"),
                new InputActionSlot(2, InputControlMap.UI, InputActionValueKind.Axis2, "Navigate"),
                new InputActionSlot(3, InputControlMap.UI, InputActionValueKind.Button, "Submit"),
            });
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "unity", "Assets", "InputSystem_Actions.inputactions");
                if (File.Exists(candidate))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("リポジトリルートを見つけられません: " + AppContext.BaseDirectory);
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + " expected " + expected + " actual " + actual);
            }
        }

        private static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void Throws<T>(Action body)
            where T : Exception
        {
            try
            {
                body();
            }
            catch (T)
            {
                return;
            }

            throw new InvalidOperationException("expected " + typeof(T).Name);
        }

        private sealed class FillReader : IInputDeviceReader
        {
            public int Calls;
            public float X = 4f;

            public void Read(Span<InputActionValue> destination)
            {
                Calls++;
                for (var i = 0; i < destination.Length; i++)
                {
                    destination[i] = new InputActionValue(99, InputControlMap.UI, InputActionValueKind.Button, X, 7f);
                }
            }
        }
    }
}
