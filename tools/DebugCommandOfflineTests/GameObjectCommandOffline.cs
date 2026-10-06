#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using OneStarMaker.Runtime.DebugCommands;

namespace OneStarMaker.DebugCommandOfflineTests
{
    internal static class GameObjectCommandOffline
    {
        public static void Run(Action<string, Action> run)
        {
            run(nameof(Unregistered_DoesNotResolveGameObjectCommands), Unregistered_DoesNotResolveGameObjectCommands);
            run(nameof(Register_AddsOnlyListAndSelect_AndDoesNotReadTheWorld), Register_AddsOnlyListAndSelect_AndDoesNotReadTheWorld);
            run(nameof(Register_RejectsNullAndDuplicate), Register_RejectsNullAndDuplicate);
            run(nameof(List_DefaultsAndPagesWithoutUsingDisplayPath), List_DefaultsAndPagesWithoutUsingDisplayPath);
            run(nameof(List_RejectsRangeAndMalformedPayloadWithoutReading), List_RejectsRangeAndMalformedPayloadWithoutReading);
            run(nameof(Select_StoresId_AndClearsWhenMissing), Select_StoresId_AndClearsWhenMissing);
            run(nameof(Unavailable_KeepsSelection), Unavailable_KeepsSelection);
            run(nameof(DisplayPath_KeepsThirtyTwoSegments), DisplayPath_KeepsThirtyTwoSegments);
            run(nameof(ThreadGate_AllowsConstructorThreadOnly), ThreadGate_AllowsConstructorThreadOnly);
        }

        private static void Unregistered_DoesNotResolveGameObjectCommands()
        {
            var catalog = new DebugCommandCatalog();
            foreach (var name in new[] { "go.list", "go.select", "go.set-active" })
            {
                True(!catalog.TryExecute(name, "{}", out var missing), name);
                True(!missing.Success, name);
                Equal("Debug command is not registered.", missing.Message, name);
            }
        }

        private static void Register_AddsOnlyListAndSelect_AndDoesNotReadTheWorld()
        {
            var catalog = new DebugCommandCatalog();
            var world = new FakeWorld();
            var selection = new DebugGameObjectSelection();
            var before = catalog.Count;
            DebugGameObjectCommands.RegisterListAndSelect(catalog, world, selection);
            Equal(before + 2, catalog.Count, "count");
            Equal(0, world.DescribeCalls, "describe");
            Equal(0, world.CollectCalls, "collect");
            True(!catalog.TryExecute("go.set-active", "{}", out var inactive), "set-active");
            True(!inactive.Success, "set-active success");
            True(!catalog.TryExecute("GO.LIST", "{}", out var folded), "case");
            True(!folded.Success, "case success");
        }

        private static void Register_RejectsNullAndDuplicate()
        {
            var catalog = new DebugCommandCatalog();
            var world = new FakeWorld();
            var selection = new DebugGameObjectSelection();
            Throws<ArgumentNullException>(() => DebugGameObjectCommands.RegisterListAndSelect(null!, world, selection));
            Throws<ArgumentNullException>(() => DebugGameObjectCommands.RegisterListAndSelect(catalog, null!, selection));
            Throws<ArgumentNullException>(() => DebugGameObjectCommands.RegisterListAndSelect(catalog, world, null!));
            DebugGameObjectCommands.RegisterListAndSelect(catalog, world, selection);
            Throws<InvalidOperationException>(() => DebugGameObjectCommands.RegisterListAndSelect(catalog, world, selection));
        }

        private static void List_DefaultsAndPagesWithoutUsingDisplayPath()
        {
            var world = new FakeWorld();
            world.Rows.Add(Row(7, "A\"b\\c", "/A", childCount: 1));
            world.Rows.Add(Row(8, "Same", "/Same", childCount: 2));
            world.Rows.Add(Row(9, "Same", "/Same", childCount: 4, hasParent: true, parent: 8, activeSelf: false, activeInHierarchy: false));
            var catalog = Catalog(world, out var selection);
            True(catalog.TryExecute("go.list", "   ", out var defaults), "defaults");
            True(defaults.Success, "defaults success");
            Equal("Listed GameObjects.", defaults.Message, "defaults message");
            Equal(
                "{\"offset\":0,\"limit\":64,\"count\":3,\"hasMore\":false,\"selectionCleared\":false,\"selectionInstanceId\":null,\"items\":["
                + "{\"instanceId\":\"7\",\"parentInstanceId\":null,\"name\":\"A\\\"b\\\\c\",\"sceneName\":\"Main\",\"displayPath\":\"/A\",\"activeSelf\":true,\"activeInHierarchy\":true,\"siblingIndex\":0,\"childCount\":1},"
                + "{\"instanceId\":\"8\",\"parentInstanceId\":null,\"name\":\"Same\",\"sceneName\":\"Main\",\"displayPath\":\"/Same\",\"activeSelf\":true,\"activeInHierarchy\":true,\"siblingIndex\":0,\"childCount\":2},"
                + "{\"instanceId\":\"9\",\"parentInstanceId\":\"8\",\"name\":\"Same\",\"sceneName\":\"Main\",\"displayPath\":\"/Same\",\"activeSelf\":false,\"activeInHierarchy\":false,\"siblingIndex\":0,\"childCount\":4}]}",
                defaults.PayloadJson,
                "defaults payload");

            True(catalog.TryExecute("go.list", "{\"offset\":1,\"limit\":1}", out var page), "page");
            True(page.PayloadJson.Contains("\"count\":1") && page.PayloadJson.Contains("\"hasMore\":true") && page.PayloadJson.Contains("\"instanceId\":\"8\""), "page body");
            True(!page.PayloadJson.Contains("\"instanceId\":\"7\"") && !page.PayloadJson.Contains("\"instanceId\":\"9\""), "page neighbors");

            True(catalog.TryExecute("go.list", "{\"offset\":9,\"limit\":1}", out var tail), "tail");
            True(tail.PayloadJson.StartsWith("{\"offset\":9,\"limit\":1,\"count\":0,\"hasMore\":false,", StringComparison.Ordinal), "tail payload");

            var longName = new string('a', 300);
            world.Rows.Clear();
            world.Rows.Add(Row(1, longName, "/long"));
            True(catalog.TryExecute("go.list", "{}", out var capped), "cap");
            True(capped.PayloadJson.Contains(new string('a', 253) + "..."), "capped text");
            True(!capped.PayloadJson.Contains(new string('a', 300)), "full text");
            True(!selection.TryGet(out _), "no selection");
        }

        private static void List_RejectsRangeAndMalformedPayloadWithoutReading()
        {
            var world = new FakeWorld();
            world.Rows.Add(Row(3, "A", "/A"));
            var catalog = Catalog(world, out var selection);
            True(catalog.TryExecute("go.select", "{\"instanceId\":\"3\"}", out var selected), "select");
            True(selected.Success, "select success");
            var describes = world.DescribeCalls;
            var collects = world.CollectCalls;

            foreach (var payload in new[] { "{\"limit\":0}", "{\"limit\":129}", "{\"offset\":-1}", "{\"limit\":\"64\"}", "{\"offset\":1.5}" })
            {
                True(catalog.TryExecute("go.list", payload, out var failed), payload);
                True(!failed.Success, payload);
                Equal("Debug GameObject page is out of range.", failed.Message, payload);
                Equal(string.Empty, failed.PayloadJson, payload);
            }

            foreach (var payload in new[] { "{", "{\"scene\":1}", "{\"offset\":01}", "null", "[]" })
            {
                True(catalog.TryExecute("go.list", payload, out var failed), payload);
                True(!failed.Success, payload);
                Equal("Debug GameObject command payload is invalid.", failed.Message, payload);
            }

            Equal(describes, world.DescribeCalls, "describe calls");
            Equal(collects, world.CollectCalls, "collect calls");
            True(selection.TryGet(out var id), "kept");
            Equal(3UL, id, "kept id");
        }

        private static void Select_StoresId_AndClearsWhenMissing()
        {
            var world = new FakeWorld();
            world.Rows.Add(Row(4, "Kept", "/Kept", childCount: 3));
            world.Rows.Add(Row(5, "Kept", "/Kept", childCount: 6));
            var catalog = Catalog(world, out var selection);
            True(catalog.TryExecute("go.select", null, out var missingPayload), "null payload");
            True(!missingPayload.Success, "null payload success");
            Equal("Debug GameObject command payload is invalid.", missingPayload.Message, "null payload message");
            True(!selection.TryGet(out _), "still empty");

            True(catalog.TryExecute("go.select", "{\"instanceId\":4}", out var numeric), "numeric");
            True(!numeric.Success, "numeric success");
            Equal("Debug GameObject command payload is invalid.", numeric.Message, "numeric message");

            True(catalog.TryExecute("go.select", "{\"instanceId\":\"5\"}", out var chosen), "choose");
            True(chosen.Success, "choose success");
            Equal("Selected GameObject.", chosen.Message, "choose message");
            Equal(
                "{\"instanceId\":\"5\",\"parentInstanceId\":null,\"name\":\"Kept\",\"sceneName\":\"Main\",\"displayPath\":\"/Kept\",\"activeSelf\":true,\"activeInHierarchy\":true,\"siblingIndex\":0,\"childCount\":6}",
                chosen.PayloadJson,
                "choose payload");

            world.Rows.RemoveAt(1);
            True(catalog.TryExecute("go.select", "{\"instanceId\":\"4\"}", out var other), "other");
            True(other.Success, "other success");
            True(other.PayloadJson.Contains("\"childCount\":3"), "other child");
            True(selection.TryGet(out var selectedId) && selectedId == 4UL, "selected 4");

            True(catalog.TryExecute("go.select", "{\"instanceId\":\"5\"}", out var dead), "dead");
            True(!dead.Success, "dead success");
            Equal("GameObject is not alive.", dead.Message, "dead message");
            True(!selection.TryGet(out _), "cleared by missing select");

            world.Rows.Add(Row(4, "Kept", "/Kept", childCount: 3));
            True(catalog.TryExecute("go.select", "{\"instanceId\":\"4\"}", out var again), "again");
            True(again.Success, "again success");
            world.Rows.Clear();
            True(catalog.TryExecute("go.list", "{}", out var listed), "list dead");
            True(listed.Success, "list dead success");
            True(listed.PayloadJson.Contains("\"selectionCleared\":true"), "cleared flag");
            True(listed.PayloadJson.Contains("\"selectionInstanceId\":null"), "cleared id");
            True(!selection.TryGet(out _), "cleared by list");

            True(catalog.TryExecute("go.list", "{}", out var second), "second list");
            True(second.PayloadJson.Contains("\"selectionCleared\":false"), "second flag");
        }

        private static void Unavailable_KeepsSelection()
        {
            var world = new FakeWorld();
            world.Rows.Add(Row(11, "Live", "/Live"));
            var catalog = Catalog(world, out var selection);
            True(catalog.TryExecute("go.select", "{\"instanceId\":\"11\"}", out var selected), "select");
            True(selected.Success, "select success");
            world.Unavailable = true;
            True(catalog.TryExecute("go.list", "{}", out var listed), "list");
            True(!listed.Success, "list success");
            Equal("GameObject inspection is unavailable.", listed.Message, "list message");
            True(catalog.TryExecute("go.select", "{\"instanceId\":\"11\"}", out var denied), "denied");
            True(!denied.Success, "denied success");
            Equal("GameObject inspection is unavailable.", denied.Message, "denied message");
            True(selection.TryGet(out var id) && id == 11UL, "kept");
            world.Unavailable = false;
            True(catalog.TryExecute("go.list", "{}", out var restored), "restored");
            True(restored.Success, "restored success");
            True(restored.PayloadJson.Contains("\"selectionInstanceId\":\"11\""), "still selected");
            True(restored.PayloadJson.Contains("\"selectionCleared\":false"), "not cleared");
        }

        private static void DisplayPath_KeepsThirtyTwoSegments()
        {
            var exact = new List<string>();
            for (var index = 0; index < 32; index++)
            {
                exact.Add(index.ToString("00"));
            }

            var exactPath = DebugGameObjectCommands.FormatDisplayPath(exact);
            True(exactPath.StartsWith("/31/", StringComparison.Ordinal), exactPath);
            True(exactPath.EndsWith("/00", StringComparison.Ordinal), exactPath);
            True(exactPath.IndexOf("/...", StringComparison.Ordinal) < 0, exactPath);

            var longer = new List<string>(exact);
            longer.Add("32");
            var longPath = DebugGameObjectCommands.FormatDisplayPath(longer);
            True(longPath.StartsWith("/.../31/", StringComparison.Ordinal), longPath);
            True(longPath.EndsWith("/00", StringComparison.Ordinal), longPath);
            True(longPath.IndexOf("/32", StringComparison.Ordinal) < 0, longPath);
        }

        private static void ThreadGate_AllowsConstructorThreadOnly()
        {
            var gate = new DebugGameObjectThreadGate();
            True(gate.AllowsCaller, "owner");
            bool? allowed = null;
            Exception? error = null;
            using var done = new ManualResetEventSlim(false);
            var thread = new Thread(() =>
            {
                try
                {
                    allowed = gate.AllowsCaller;
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    done.Set();
                }
            });
            thread.Start();
            True(done.Wait(TimeSpan.FromSeconds(5)), "signaled");
            thread.Join();
            True(error == null, "thread error");
            True(allowed == false, "other thread");
        }

        private static DebugCommandCatalog Catalog(FakeWorld world, out DebugGameObjectSelection selection)
        {
            selection = new DebugGameObjectSelection();
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterListAndSelect(catalog, world, selection);
            return catalog;
        }

        private static DebugGameObjectRow Row(
            ulong id,
            string name,
            string path,
            int childCount = 0,
            bool hasParent = false,
            ulong parent = 0,
            string scene = "Main",
            bool activeSelf = true,
            bool activeInHierarchy = true)
        {
            return new DebugGameObjectRow(id, hasParent, parent, name, scene, path, activeSelf, activeInHierarchy, 0, childCount);
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

        internal sealed class FakeWorld : IDebugGameObjectWorld
        {
            public readonly List<DebugGameObjectRow> Rows = new();
            public bool Unavailable;
            public bool ParentBlocksHierarchy;
            public int DescribeCalls;
            public int CollectCalls;
            public int SetActiveCalls;
            public int GetTransformCalls;
            public int SetTransformCalls;
            public int SetRendererCalls;
            private readonly List<StoredTransform> _transforms = new();
            private readonly List<StoredRenderer> _renderers = new();

            public DebugGameObjectReadStatus TryDescribe(ulong instanceId, out DebugGameObjectRow row, out string failure)
            {
                DescribeCalls++;
                row = default;
                failure = string.Empty;
                if (Unavailable)
                {
                    failure = "unavailable";
                    return DebugGameObjectReadStatus.Unavailable;
                }

                for (var index = 0; index < Rows.Count; index++)
                {
                    if (Rows[index].InstanceId == instanceId)
                    {
                        row = Rows[index];
                        return DebugGameObjectReadStatus.Ok;
                    }
                }

                return DebugGameObjectReadStatus.NotFound;
            }

            public DebugGameObjectReadStatus TryCollectPage(
                int offset,
                int limit,
                List<DebugGameObjectRow> destination,
                out bool hasMore,
                out string failure)
            {
                CollectCalls++;
                destination.Clear();
                hasMore = false;
                failure = string.Empty;
                if (Unavailable)
                {
                    failure = "unavailable";
                    return DebugGameObjectReadStatus.Unavailable;
                }

                var index = offset;
                while (index < Rows.Count && destination.Count < limit)
                {
                    destination.Add(Rows[index]);
                    index++;
                }

                hasMore = index < Rows.Count;
                return DebugGameObjectReadStatus.Ok;
            }

            public DebugGameObjectReadStatus TrySetActive(ulong instanceId, bool active, out DebugGameObjectRow row, out string failure)
            {
                SetActiveCalls++;
                row = default;
                failure = string.Empty;
                if (Unavailable)
                {
                    failure = "unavailable";
                    return DebugGameObjectReadStatus.Unavailable;
                }

                for (var index = 0; index < Rows.Count; index++)
                {
                    if (Rows[index].InstanceId != instanceId)
                    {
                        continue;
                    }

                    var current = Rows[index];
                    row = new DebugGameObjectRow(
                        current.InstanceId,
                        current.HasParent,
                        current.ParentInstanceId,
                        current.Name,
                        current.SceneName,
                        current.DisplayPath,
                        active,
                        active && !ParentBlocksHierarchy,
                        current.SiblingIndex,
                        current.ChildCount);
                    Rows[index] = row;
                    return DebugGameObjectReadStatus.Ok;
                }

                return DebugGameObjectReadStatus.NotFound;
            }

            public DebugGameObjectReadStatus TryGetTransform(ulong instanceId, out DebugGameObjectTransform value, out string failure)
            {
                GetTransformCalls++;
                value = default;
                failure = string.Empty;
                if (Unavailable)
                {
                    failure = "unavailable";
                    return DebugGameObjectReadStatus.Unavailable;
                }

                if (!HasRow(instanceId))
                {
                    return DebugGameObjectReadStatus.NotFound;
                }

                value = ReadTransform(instanceId);
                return DebugGameObjectReadStatus.Ok;
            }

            public DebugGameObjectReadStatus TrySetTransform(
                ulong instanceId,
                DebugGameObjectTransformChange change,
                out DebugGameObjectTransform value,
                out string failure)
            {
                SetTransformCalls++;
                value = default;
                failure = string.Empty;
                if (Unavailable)
                {
                    failure = "unavailable";
                    return DebugGameObjectReadStatus.Unavailable;
                }

                if (!HasRow(instanceId))
                {
                    return DebugGameObjectReadStatus.NotFound;
                }

                var current = ReadTransform(instanceId);
                var position = change.HasPosition ? change.Position : current.LocalPosition;
                var euler = change.HasEuler ? change.Euler : current.LocalEulerAngles;
                var scale = change.HasScale ? change.Scale : current.LocalScale;
                StoreTransform(instanceId, position, euler, scale);
                value = new DebugGameObjectTransform(instanceId, position, euler, scale);
                return DebugGameObjectReadStatus.Ok;
            }

            private bool HasRow(ulong instanceId)
            {
                for (var index = 0; index < Rows.Count; index++)
                {
                    if (Rows[index].InstanceId == instanceId)
                    {
                        return true;
                    }
                }

                return false;
            }

            private DebugGameObjectTransform ReadTransform(ulong instanceId)
            {
                for (var index = 0; index < _transforms.Count; index++)
                {
                    if (_transforms[index].Id == instanceId)
                    {
                        var stored = _transforms[index];
                        return new DebugGameObjectTransform(instanceId, stored.Position, stored.Euler, stored.Scale);
                    }
                }

                return new DebugGameObjectTransform(
                    instanceId,
                    default,
                    default,
                    new DebugGameObjectVector(1d, 1d, 1d));
            }

            private void StoreTransform(
                ulong instanceId,
                DebugGameObjectVector position,
                DebugGameObjectVector euler,
                DebugGameObjectVector scale)
            {
                for (var index = 0; index < _transforms.Count; index++)
                {
                    if (_transforms[index].Id != instanceId)
                    {
                        continue;
                    }

                    _transforms[index] = new StoredTransform(instanceId, position, euler, scale);
                    return;
                }

                _transforms.Add(new StoredTransform(instanceId, position, euler, scale));
            }

            private readonly struct StoredTransform
            {
                public StoredTransform(
                    ulong id,
                    DebugGameObjectVector position,
                    DebugGameObjectVector euler,
                    DebugGameObjectVector scale)
                {
                    Id = id;
                    Position = position;
                    Euler = euler;
                    Scale = scale;
                }

                public ulong Id { get; }

                public DebugGameObjectVector Position { get; }

                public DebugGameObjectVector Euler { get; }

                public DebugGameObjectVector Scale { get; }
            }

            public void AddRenderer(ulong instanceId, string typeName, bool enabled)
            {
                _renderers.Add(new StoredRenderer(instanceId, typeName, enabled));
            }

            public bool RendererEnabled(ulong instanceId, int index)
            {
                if (!TryRendererAt(instanceId, index, out var stored) || stored == null)
                {
                    throw new InvalidOperationException("missing renderer");
                }

                return stored.Enabled;
            }

            public DebugGameObjectReadStatus TrySetRendererEnabled(
                ulong instanceId,
                int rendererIndex,
                bool enabled,
                out DebugGameObjectRenderer value,
                out string failure)
            {
                SetRendererCalls++;
                value = default;
                failure = string.Empty;
                if (Unavailable)
                {
                    failure = "unavailable";
                    return DebugGameObjectReadStatus.Unavailable;
                }

                if (!HasRow(instanceId))
                {
                    return DebugGameObjectReadStatus.NotFound;
                }

                var count = CountRenderers(instanceId);
                if (rendererIndex < 0 || rendererIndex >= count || !TryRendererAt(instanceId, rendererIndex, out var stored) || stored == null)
                {
                    return DebugGameObjectReadStatus.MissingRenderer;
                }

                stored.Enabled = enabled;
                value = new DebugGameObjectRenderer(instanceId, rendererIndex, count, enabled, stored.TypeName);
                return DebugGameObjectReadStatus.Ok;
            }

            private int CountRenderers(ulong instanceId)
            {
                var count = 0;
                for (var index = 0; index < _renderers.Count; index++)
                {
                    if (_renderers[index].Id == instanceId)
                    {
                        count++;
                    }
                }

                return count;
            }

            private bool TryRendererAt(ulong instanceId, int rendererIndex, out StoredRenderer? stored)
            {
                var seen = 0;
                for (var index = 0; index < _renderers.Count; index++)
                {
                    if (_renderers[index].Id != instanceId)
                    {
                        continue;
                    }

                    if (seen == rendererIndex)
                    {
                        stored = _renderers[index];
                        return true;
                    }

                    seen++;
                }

                stored = null;
                return false;
            }

            private sealed class StoredRenderer
            {
                public StoredRenderer(ulong id, string typeName, bool enabled)
                {
                    Id = id;
                    TypeName = typeName;
                    Enabled = enabled;
                }

                public ulong Id { get; }

                public string TypeName { get; }

                public bool Enabled { get; set; }
            }
        }
    }
}
