#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using OneStarMaker.Runtime.DebugCommands;

namespace OneStarMaker.DebugCommandOfflineTests
{
    internal static class TransformCommandOffline
    {
        public static void Run(Action<string, Action> run)
        {
            run(nameof(Get_ReadsSelectionAndDoesNotRetarget), Get_ReadsSelectionAndDoesNotRetarget);
            run(nameof(Set_WritesOnlyProvidedComponents), Set_WritesOnlyProvidedComponents);
            run(nameof(Set_RejectsNonFiniteAndMalformedWithoutWriting), Set_RejectsNonFiniteAndMalformedWithoutWriting);
            run(nameof(Transform_ClearsOnlyTheDeadCurrentSelection), Transform_ClearsOnlyTheDeadCurrentSelection);
            run(nameof(List_StillRejectsLocalPosition), List_StillRejectsLocalPosition);
        }

        private static void Get_ReadsSelectionAndDoesNotRetarget()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            world.Rows.Add(Row(4, "A"));
            world.Rows.Add(Row(5, "B"));
            var selection = new DebugGameObjectSelection();
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterListAndSelect(catalog, world, selection);
            DebugGameObjectCommands.RegisterSetActive(catalog, world, selection);
            True(!catalog.TryExecute("go.get-transform", "{}", out var missing), "unregistered get");
            True(!catalog.TryExecute("go.set-transform", "{}", out var missingSet), "unregistered set");
            Equal("Debug command is not registered.", missing.Message, "unregistered get message");
            Equal("Debug command is not registered.", missingSet.Message, "unregistered set message");

            var before = catalog.Count;
            DebugGameObjectCommands.RegisterTransform(catalog, world, selection);
            Equal(before + 2, catalog.Count, "count");
            Equal(0, world.GetTransformCalls, "register get");
            Equal(0, world.SetTransformCalls, "register set");

            True(catalog.TryExecute("go.get-transform", "", out var none), "none");
            True(!none.Success, "none success");
            Equal("GameObject is not selected.", none.Message, "none message");
            Equal(0, world.GetTransformCalls, "none calls");

            True(catalog.TryExecute("go.select", "{\"instanceId\":\"4\"}", out var selected), "select");
            True(selected.Success, "select success");
            True(catalog.TryExecute("go.get-transform", "{}", out var fromSelection), "from selection");
            True(fromSelection.Success, "from selection success");
            Equal("Read local transform.", fromSelection.Message, "from selection message");
            Equal(DefaultJson("4"), fromSelection.PayloadJson, "default json");
            True(selection.TryGet(out var kept) && kept == 4UL, "selection stays");

            True(catalog.TryExecute("go.get-transform", "{\"instanceId\":\"5\"}", out var other), "other");
            True(other.Success, "other success");
            Equal(DefaultJson("5"), other.PayloadJson, "other json");
            True(selection.TryGet(out kept) && kept == 4UL, "selection not retargeted");
            True(!catalog.TryExecute("Go.get-transform", "{}", out _), "case");
        }

        private static void Set_WritesOnlyProvidedComponents()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            world.Rows.Add(Row(4, "A"));
            var selection = new DebugGameObjectSelection();
            selection.Replace(4);
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterTransform(catalog, world, selection);

            True(catalog.TryExecute("go.set-transform", "{\"localPosition\":{\"x\":1.5,\"y\":-2,\"z\":0}}", out var position), "position");
            True(position.Success, "position success");
            Equal("Set local transform.", position.Message, "position message");
            Equal(Expected("4", 1.5d, -2d, 0d, 0d, 0d, 0d, 1d, 1d, 1d), position.PayloadJson, "position payload");
            True(selection.TryGet(out var kept) && kept == 4UL, "selection stays");

            True(catalog.TryExecute("go.set-transform", "{\"localScale\":{\"z\":4,\"y\":3,\"x\":2}}", out var scale), "scale");
            True(scale.Success, "scale success");
            Equal(Expected("4", 1.5d, -2d, 0d, 0d, 0d, 0d, 2d, 3d, 4d), scale.PayloadJson, "scale payload");

            True(catalog.TryExecute("go.get-transform", "", out var read), "read");
            Equal(Expected("4", 1.5d, -2d, 0d, 0d, 0d, 0d, 2d, 3d, 4d), read.PayloadJson, "read payload");
        }

        private static void Set_RejectsNonFiniteAndMalformedWithoutWriting()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            world.Rows.Add(Row(4, "A"));
            var selection = new DebugGameObjectSelection();
            selection.Replace(4);
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterTransform(catalog, world, selection);
            True(catalog.TryExecute("go.set-transform", "{\"localPosition\":{\"x\":4,\"y\":5,\"z\":6}}", out var seeded), "seed");
            True(seeded.Success, "seed success");
            var gets = world.GetTransformCalls;
            var sets = world.SetTransformCalls;

            foreach (var payload in new[]
            {
                "",
                "{}",
                "{\"instanceId\":\"4\"}",
                "{\"instanceId\":4,\"localPosition\":{\"x\":1,\"y\":2,\"z\":3}}",
                "{\"localPosition\":{\"x\":1,\"y\":2}}",
                "{\"localPosition\":{\"x\":1,\"y\":2,\"z\":3,\"w\":0}}",
                "{\"localPosition\":{\"x\":\"1\",\"y\":2,\"z\":3}}",
                "{\"localPosition\":{\"x\":1,\"y\":2,\"z\":3},\"localPosition\":{\"x\":0,\"y\":0,\"z\":0}}",
                "{\"localPosition\":{\"x\":1,\"y\":2,\"z\":3,\"x\":4}}",
                "{\"localPosition\":{\"x\":9,\"y\":9,\"z\":9},\"localEulerAngles\":{\"x\":1e309,\"y\":0,\"z\":0}}",
                "{\"localScale\":{\"x\":1e39,\"y\":1,\"z\":1}}",
                "{\"localScale\":{\"x\":-1e39,\"y\":1,\"z\":1}}",
                "{\"localPosition\":{\"x\":1,\"y\":2,\"z\":3},\"active\":true}",
                "{\"localEulerAngles\":{\"x\":01,\"y\":0,\"z\":0}}",
                "{\"localPosition\":{\"x\":1,\"y\":2,\"z\":3},}",
            })
            {
                True(catalog.TryExecute("go.set-transform", payload, out var failed), payload);
                True(!failed.Success, payload + " success");
                Equal("Debug GameObject command payload is invalid.", failed.Message, payload);
            }

            True(catalog.TryExecute("go.get-transform", "{\"localPosition\":{\"x\":1,\"y\":0,\"z\":0}}", out var badGet), "bad get");
            True(!badGet.Success, "bad get success");
            Equal("Debug GameObject command payload is invalid.", badGet.Message, "bad get message");

            Equal(gets, world.GetTransformCalls, "get calls");
            Equal(sets, world.SetTransformCalls, "set calls");
            True(selection.TryGet(out var kept) && kept == 4UL, "kept");
            True(catalog.TryExecute("go.get-transform", "{}", out var read), "read");
            Equal(Expected("4", 4d, 5d, 6d, 0d, 0d, 0d, 1d, 1d, 1d), read.PayloadJson, "unchanged");
        }

        private static void Transform_ClearsOnlyTheDeadCurrentSelection()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            world.Rows.Add(Row(4, "A"));
            var selection = new DebugGameObjectSelection();
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterTransform(catalog, world, selection);
            selection.Replace(4);

            True(catalog.TryExecute("go.set-transform", "{\"instanceId\":\"9\",\"localPosition\":{\"x\":1,\"y\":0,\"z\":0}}", out var otherDead), "other dead");
            True(!otherDead.Success, "other dead success");
            Equal("GameObject is not alive.", otherDead.Message, "other dead message");
            True(selection.TryGet(out var kept) && kept == 4UL, "kept other");

            True(catalog.TryExecute("go.get-transform", "{\"instanceId\":\"9\"}", out var otherGet), "other get");
            True(!otherGet.Success, "other get success");
            Equal("GameObject is not alive.", otherGet.Message, "other get message");
            True(selection.TryGet(out kept) && kept == 4UL, "kept other get");

            world.Rows.Clear();
            True(catalog.TryExecute("go.get-transform", "{}", out var deadGet), "dead get");
            True(!deadGet.Success, "dead get success");
            Equal("GameObject is not alive.", deadGet.Message, "dead get message");
            True(!selection.TryGet(out _), "cleared by get");

            selection.Replace(4);
            True(catalog.TryExecute("go.set-transform", "{\"localScale\":{\"x\":2,\"y\":2,\"z\":2}}", out var deadSet), "dead set");
            True(!deadSet.Success, "dead set success");
            Equal("GameObject is not alive.", deadSet.Message, "dead set message");
            True(!selection.TryGet(out _), "cleared by set");

            world.Rows.Add(Row(4, "A"));
            selection.Replace(4);
            world.Unavailable = true;
            True(catalog.TryExecute("go.set-transform", "{\"localPosition\":{\"x\":1,\"y\":1,\"z\":1}}", out var denied), "denied");
            True(!denied.Success, "denied success");
            Equal("GameObject inspection is unavailable.", denied.Message, "denied message");
            True(selection.TryGet(out kept) && kept == 4UL, "kept unavailable");
            world.Unavailable = false;
            True(catalog.TryExecute("go.get-transform", "{}", out var after), "after");
            Equal(DefaultJson("4"), after.PayloadJson, "not written while unavailable");
        }

        private static void List_StillRejectsLocalPosition()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            world.Rows.Add(Row(4, "A"));
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterListAndSelect(catalog, world, new DebugGameObjectSelection());
            True(catalog.TryExecute("go.list", "{\"localPosition\":{\"x\":1,\"y\":2,\"z\":3}}", out var listed), "list");
            True(!listed.Success, "list success");
            Equal("Debug GameObject command payload is invalid.", listed.Message, "list message");
            Equal(0, world.CollectCalls, "not collected");
        }

        private static string DefaultJson(string instanceId)
        {
            return Expected(instanceId, 0d, 0d, 0d, 0d, 0d, 0d, 1d, 1d, 1d);
        }

        private static string Expected(
            string instanceId,
            double px,
            double py,
            double pz,
            double ex,
            double ey,
            double ez,
            double sx,
            double sy,
            double sz)
        {
            return "{\"instanceId\":\"" + instanceId
                + "\",\"localPosition\":{\"x\":" + px.ToString("R", CultureInfo.InvariantCulture)
                + ",\"y\":" + py.ToString("R", CultureInfo.InvariantCulture)
                + ",\"z\":" + pz.ToString("R", CultureInfo.InvariantCulture)
                + "},\"localEulerAngles\":{\"x\":" + ex.ToString("R", CultureInfo.InvariantCulture)
                + ",\"y\":" + ey.ToString("R", CultureInfo.InvariantCulture)
                + ",\"z\":" + ez.ToString("R", CultureInfo.InvariantCulture)
                + "},\"localScale\":{\"x\":" + sx.ToString("R", CultureInfo.InvariantCulture)
                + ",\"y\":" + sy.ToString("R", CultureInfo.InvariantCulture)
                + ",\"z\":" + sz.ToString("R", CultureInfo.InvariantCulture) + "}}";
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
