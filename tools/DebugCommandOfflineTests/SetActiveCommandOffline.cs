#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.DebugCommands;

namespace OneStarMaker.DebugCommandOfflineTests
{
    internal static class SetActiveCommandOffline
    {
        public static void Run(Action<string, Action> run)
        {
            run(nameof(SetActive_UsesSelectionWithoutRetargeting), SetActive_UsesSelectionWithoutRetargeting);
            run(nameof(SetActive_ClearsOnlyTheDeadCurrentSelection), SetActive_ClearsOnlyTheDeadCurrentSelection);
            run(nameof(SetActive_ReportsInactiveParentWithoutCallingOnBadPayload), SetActive_ReportsInactiveParentWithoutCallingOnBadPayload);
            run(nameof(List_StillRejectsActiveAsUnknown), List_StillRejectsActiveAsUnknown);
        }

        private static void SetActive_UsesSelectionWithoutRetargeting()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            world.Rows.Add(Row(4, "A"));
            world.Rows.Add(Row(5, "B"));
            var selection = new DebugGameObjectSelection();
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterListAndSelect(catalog, world, selection);
            True(!catalog.TryExecute("go.set-active", "{\"active\":true}", out var missing), "unregistered");
            True(!missing.Success, "unregistered success");
            Equal("Debug command is not registered.", missing.Message, "unregistered message");

            var before = catalog.Count;
            DebugGameObjectCommands.RegisterSetActive(catalog, world, selection);
            Equal(before + 1, catalog.Count, "count");
            True(catalog.TryExecute("go.set-active", "{\"active\":true}", out var none), "none");
            True(!none.Success, "none success");
            Equal("GameObject is not selected.", none.Message, "none message");
            Equal(0, world.SetActiveCalls, "none calls");

            True(catalog.TryExecute("go.select", "{\"instanceId\":\"4\"}", out var selected), "select");
            True(selected.Success, "select success");
            True(catalog.TryExecute("go.set-active", "{\"active\":false}", out var fromSelection), "from selection");
            True(fromSelection.Success, "from selection success");
            Equal("Set GameObject active.", fromSelection.Message, "from selection message");
            True(fromSelection.PayloadJson.Contains("\"instanceId\":\"4\"") && fromSelection.PayloadJson.Contains("\"activeSelf\":false"), "from selection payload");
            True(selection.TryGet(out var kept) && kept == 4UL, "selection stays");

            True(catalog.TryExecute("go.set-active", "{\"instanceId\":\"5\",\"active\":true}", out var other), "other");
            True(other.Success, "other success");
            True(other.PayloadJson.Contains("\"instanceId\":\"5\"") && other.PayloadJson.Contains("\"activeSelf\":true"), "other payload");
            True(selection.TryGet(out kept) && kept == 4UL, "selection not retargeted");
        }

        private static void SetActive_ClearsOnlyTheDeadCurrentSelection()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            world.Rows.Add(Row(4, "A"));
            var selection = new DebugGameObjectSelection();
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterListAndSelect(catalog, world, selection);
            DebugGameObjectCommands.RegisterSetActive(catalog, world, selection);
            True(catalog.TryExecute("go.select", "{\"instanceId\":\"4\"}", out _), "select");
            var calls = world.SetActiveCalls;
            True(catalog.TryExecute("go.set-active", "{\"instanceId\":\"9\",\"active\":true}", out var otherDead), "other dead");
            True(!otherDead.Success, "other dead success");
            Equal("GameObject is not alive.", otherDead.Message, "other dead message");
            True(selection.TryGet(out var kept) && kept == 4UL, "kept other");
            True(world.SetActiveCalls == calls + 1, "consulted");

            world.Rows.Clear();
            True(catalog.TryExecute("go.set-active", "{\"instanceId\":\"4\",\"active\":true}", out var deadSelection), "dead selection");
            True(!deadSelection.Success, "dead selection success");
            Equal("GameObject is not alive.", deadSelection.Message, "dead selection message");
            True(!selection.TryGet(out _), "cleared");
        }

        private static void SetActive_ReportsInactiveParentWithoutCallingOnBadPayload()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            world.ParentBlocksHierarchy = true;
            world.Rows.Add(Row(2, "Child"));
            var selection = new DebugGameObjectSelection();
            selection.Replace(2);
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterSetActive(catalog, world, selection);

            foreach (var payload in new[] { "{}", "{\"active\":\"true\"}", "{\"instanceId\":2,\"active\":true}", "{\"active\":true,\"extra\":1}" })
            {
                True(catalog.TryExecute("go.set-active", payload, out var failed), payload);
                True(!failed.Success, payload);
                Equal("Debug GameObject command payload is invalid.", failed.Message, payload);
            }

            Equal(0, world.SetActiveCalls, "bad payload");
            True(selection.TryGet(out var kept) && kept == 2UL, "kept after bad");

            True(catalog.TryExecute("go.set-active", "{\"active\":true}", out var blocked), "blocked");
            True(blocked.Success, "blocked success");
            Equal("Set activeSelf. An inactive parent still keeps activeInHierarchy false.", blocked.Message, "blocked message");
            True(blocked.PayloadJson.Contains("\"activeSelf\":true") && blocked.PayloadJson.Contains("\"activeInHierarchy\":false"), "blocked flags");

            world.ParentBlocksHierarchy = false;
            True(catalog.TryExecute("go.set-active", "{\"active\":false}", out var off), "off");
            Equal("Set GameObject active.", off.Message, "off message");
            True(off.PayloadJson.Contains("\"activeSelf\":false"), "off flag");

            world.Unavailable = true;
            True(catalog.TryExecute("go.set-active", "{\"active\":true}", out var denied), "denied");
            True(!denied.Success, "denied success");
            Equal("GameObject inspection is unavailable.", denied.Message, "denied message");
            True(selection.TryGet(out kept) && kept == 2UL, "kept unavailable");
        }

        private static void List_StillRejectsActiveAsUnknown()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterListAndSelect(catalog, world, new DebugGameObjectSelection());
            True(catalog.TryExecute("go.list", "{\"active\":true}", out var listed), "list");
            True(!listed.Success, "list success");
            Equal("Debug GameObject command payload is invalid.", listed.Message, "list message");
            Equal(0, world.CollectCalls, "not collected");
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
