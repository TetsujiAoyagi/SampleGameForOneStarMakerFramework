#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.DebugCommands;

namespace OneStarMaker.DebugCommandOfflineTests
{
    internal static class RendererCommandOffline
    {
        public static void Run(Action<string, Action> run)
        {
            run(nameof(SetRenderer_UsesSelectionAndDoesNotRetarget), SetRenderer_UsesSelectionAndDoesNotRetarget);
            run(nameof(SetRenderer_ChangesOnlyTheAddressedRenderer), SetRenderer_ChangesOnlyTheAddressedRenderer);
            run(nameof(SetRenderer_RejectsMalformedWithoutCalling), SetRenderer_RejectsMalformedWithoutCalling);
            run(nameof(SetRenderer_MissingRendererKeepsSelectionAndState), SetRenderer_MissingRendererKeepsSelectionAndState);
            run(nameof(List_StillRejectsRendererIndex), List_StillRejectsRendererIndex);
        }

        private static void SetRenderer_UsesSelectionAndDoesNotRetarget()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            world.Rows.Add(Row(4, "A"));
            world.Rows.Add(Row(5, "B"));
            world.AddRenderer(4, "MeshRenderer", true);
            world.AddRenderer(5, "MeshRenderer", true);
            var selection = new DebugGameObjectSelection();
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterListAndSelect(catalog, world, selection);
            DebugGameObjectCommands.RegisterSetActive(catalog, world, selection);
            DebugGameObjectCommands.RegisterTransform(catalog, world, selection);
            True(!catalog.TryExecute("go.set-renderer-enabled", "{\"enabled\":false}", out var missing), "unregistered");
            Equal("Debug command is not registered.", missing.Message, "unregistered message");

            var before = catalog.Count;
            DebugGameObjectCommands.RegisterRenderer(catalog, world, selection);
            Equal(before + 1, catalog.Count, "count");
            Equal(0, world.SetRendererCalls, "register calls");

            True(catalog.TryExecute("go.set-renderer-enabled", "{\"enabled\":false}", out var none), "none");
            True(!none.Success, "none success");
            Equal("GameObject is not selected.", none.Message, "none message");
            Equal(0, world.SetRendererCalls, "none calls");

            True(catalog.TryExecute("go.select", "{\"instanceId\":\"4\"}", out var selected), "select");
            True(selected.Success, "select success");
            True(catalog.TryExecute("go.set-renderer-enabled", "{\"enabled\":false}", out var fromSelection), "from selection");
            True(fromSelection.Success, "from selection success");
            Equal("Set Renderer enabled.", fromSelection.Message, "from selection message");
            Equal(Payload("4", 0, 1, false, "MeshRenderer"), fromSelection.PayloadJson, "from selection payload");
            True(selection.TryGet(out var kept) && kept == 4UL, "selection stays");
            True(!world.RendererEnabled(4, 0), "selected disabled");
            True(world.RendererEnabled(5, 0), "other object stays");

            True(catalog.TryExecute("go.set-renderer-enabled", "{\"instanceId\":\"5\",\"enabled\":false}", out var other), "other");
            True(other.Success, "other success");
            Equal(Payload("5", 0, 1, false, "MeshRenderer"), other.PayloadJson, "other payload");
            True(selection.TryGet(out kept) && kept == 4UL, "selection not retargeted");
            True(!catalog.TryExecute("Go.set-renderer-enabled", "{\"enabled\":true}", out _), "case");
        }

        private static void SetRenderer_ChangesOnlyTheAddressedRenderer()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            world.Rows.Add(Row(4, "A"));
            world.AddRenderer(4, "MeshRenderer", true);
            world.AddRenderer(4, "SkinnedMeshRenderer", true);
            var selection = new DebugGameObjectSelection();
            selection.Replace(4);
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterRenderer(catalog, world, selection);

            True(catalog.TryExecute("go.set-renderer-enabled", "{\"rendererIndex\":1,\"enabled\":false}", out var second), "second");
            True(second.Success, "second success");
            Equal(Payload("4", 1, 2, false, "SkinnedMeshRenderer"), second.PayloadJson, "second payload");
            True(world.RendererEnabled(4, 0), "first stays");
            True(!world.RendererEnabled(4, 1), "second disabled");
            True(selection.TryGet(out var kept) && kept == 4UL, "selection stays");

            True(catalog.TryExecute("go.set-renderer-enabled", "{\"enabled\":false}", out var omitted), "omitted");
            True(omitted.Success, "omitted success");
            Equal(Payload("4", 0, 2, false, "MeshRenderer"), omitted.PayloadJson, "omitted payload");
            True(!world.RendererEnabled(4, 0), "first disabled");
            True(!world.RendererEnabled(4, 1), "second still disabled");
        }

        private static void SetRenderer_RejectsMalformedWithoutCalling()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            world.Rows.Add(Row(4, "A"));
            world.AddRenderer(4, "MeshRenderer", true);
            var selection = new DebugGameObjectSelection();
            selection.Replace(4);
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterRenderer(catalog, world, selection);

            foreach (var payload in new[]
            {
                "",
                "{}",
                "{\"enabled\":\"true\"}",
                "{\"instanceId\":4,\"enabled\":true}",
                "{\"rendererIndex\":-1,\"enabled\":true}",
                "{\"rendererIndex\":1.5,\"enabled\":true}",
                "{\"rendererIndex\":\"0\",\"enabled\":true}",
                "{\"rendererIndex\":01,\"enabled\":true}",
                "{\"enabled\":true,\"enabled\":false}",
                "{\"rendererIndex\":0,\"enabled\":true,\"extra\":1}",
                "{\"enabled\":true,}",
            })
            {
                True(catalog.TryExecute("go.set-renderer-enabled", payload, out var failed), payload);
                True(!failed.Success, payload + " success");
                Equal("Debug GameObject command payload is invalid.", failed.Message, payload);
            }

            Equal(0, world.SetRendererCalls, "calls");
            True(world.RendererEnabled(4, 0), "unchanged");
            True(selection.TryGet(out var kept) && kept == 4UL, "kept");
        }

        private static void SetRenderer_MissingRendererKeepsSelectionAndState()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            world.Rows.Add(Row(4, "A"));
            world.Rows.Add(Row(8, "Empty"));
            world.AddRenderer(4, "MeshRenderer", true);
            world.AddRenderer(4, "SkinnedMeshRenderer", true);
            var selection = new DebugGameObjectSelection();
            selection.Replace(4);
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterRenderer(catalog, world, selection);

            True(catalog.TryExecute("go.set-renderer-enabled", "{\"rendererIndex\":2,\"enabled\":false}", out var outOfRange), "out of range");
            True(!outOfRange.Success, "out of range success");
            Equal("Renderer was not found.", outOfRange.Message, "out of range message");
            True(world.RendererEnabled(4, 0) && world.RendererEnabled(4, 1), "both stay");
            True(selection.TryGet(out var kept) && kept == 4UL, "kept range");

            True(catalog.TryExecute("go.set-renderer-enabled", "{\"instanceId\":\"8\",\"enabled\":false}", out var empty), "empty");
            True(!empty.Success, "empty success");
            Equal("Renderer was not found.", empty.Message, "empty message");
            True(selection.TryGet(out kept) && kept == 4UL, "kept empty");
            True(world.RendererEnabled(4, 0) && world.RendererEnabled(4, 1), "empty did not change");

            True(catalog.TryExecute("go.set-renderer-enabled", "{\"instanceId\":\"9\",\"enabled\":false}", out var otherDead), "other dead");
            True(!otherDead.Success, "other dead success");
            Equal("GameObject is not alive.", otherDead.Message, "other dead message");
            True(selection.TryGet(out kept) && kept == 4UL, "kept other");

            world.Unavailable = true;
            True(catalog.TryExecute("go.set-renderer-enabled", "{\"enabled\":false}", out var denied), "denied");
            True(!denied.Success, "denied success");
            Equal("GameObject inspection is unavailable.", denied.Message, "denied message");
            True(selection.TryGet(out kept) && kept == 4UL, "kept unavailable");
            world.Unavailable = false;
            True(world.RendererEnabled(4, 0), "not written while unavailable");

            world.Rows.Clear();
            True(catalog.TryExecute("go.set-renderer-enabled", "{\"enabled\":false}", out var dead), "dead");
            True(!dead.Success, "dead success");
            Equal("GameObject is not alive.", dead.Message, "dead message");
            True(!selection.TryGet(out _), "cleared");
        }

        private static void List_StillRejectsRendererIndex()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            world.Rows.Add(Row(4, "A"));
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterListAndSelect(catalog, world, new DebugGameObjectSelection());
            True(catalog.TryExecute("go.list", "{\"rendererIndex\":0}", out var listed), "list");
            True(!listed.Success, "list success");
            Equal("Debug GameObject command payload is invalid.", listed.Message, "list message");
            Equal(0, world.CollectCalls, "not collected");
        }

        private static string Payload(string instanceId, int index, int count, bool enabled, string typeName)
        {
            return "{\"instanceId\":\"" + instanceId
                + "\",\"rendererIndex\":" + index.ToString()
                + ",\"rendererCount\":" + count.ToString()
                + ",\"enabled\":" + (enabled ? "true" : "false")
                + ",\"typeName\":\"" + typeName + "\"}";
        }

        private static DebugGameObjectRow Row(ulong id, string name)
        {
            return new DebugGameObjectRow(id, false, 0, name, "Main", "/" + name, true, true, 0, 0);
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
    }
}
