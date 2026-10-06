#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using OneStarMaker.Runtime.DebugCommands;

namespace OneStarMaker.DebugCommandOfflineTests
{
    internal static class AppOptInCommandOffline
    {
        private static readonly string[] CommandNames =
        {
            DebugGameObjectCommands.ListName,
            DebugGameObjectCommands.SelectName,
            DebugGameObjectCommands.SetActiveName,
            DebugGameObjectCommands.GetTransformName,
            DebugGameObjectCommands.SetTransformName,
            DebugGameObjectCommands.SetRendererEnabledName,
        };

        public static void Run(Action<string, Action> run)
        {
            run(nameof(Register_AddsSixCommandsWithoutReadingTheWorld), Register_AddsSixCommandsWithoutReadingTheWorld);
            run(nameof(Registration_SharesSelectionAcrossCommands), Registration_SharesSelectionAcrossCommands);
            run(nameof(UnregisteredCatalog_DoesNotResolveGameObjectCommands), UnregisteredCatalog_DoesNotResolveGameObjectCommands);
            run(nameof(StartupSources_KeepTheNullDispatcher), StartupSources_KeepTheNullDispatcher);
        }

        private static void Register_AddsSixCommandsWithoutReadingTheWorld()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            world.Rows.Add(Row(4, "A"));
            var selection = new DebugGameObjectSelection();
            var catalog = new DebugCommandCatalog();
            DebugGameObjectCommands.RegisterGameObjectCommands(catalog, world, selection);
            Equal(6, catalog.Count, "count");
            Equal(0, world.DescribeCalls, "describe");
            Equal(0, world.CollectCalls, "collect");
            Equal(0, world.SetActiveCalls, "set active");
            Equal(0, world.GetTransformCalls, "get transform");
            Equal(0, world.SetTransformCalls, "set transform");
            Equal(0, world.SetRendererCalls, "renderer");

            foreach (var name in CommandNames)
            {
                True(catalog.TryExecute(name, "{", out var failed), name);
                True(!failed.Success, name + " success");
                Equal("Debug GameObject command payload is invalid.", failed.Message, name);
            }

            Throws<InvalidOperationException>(() =>
                DebugGameObjectCommands.RegisterGameObjectCommands(catalog, world, selection));
            Equal(6, catalog.Count, "count after duplicate");
            Throws<ArgumentNullException>(() => DebugGameObjectCommands.CreateRegistration(null!));
            Throws<ArgumentNullException>(() =>
                DebugGameObjectCommands.RegisterGameObjectCommands(null!, world, selection));
        }

        private static void Registration_SharesSelectionAcrossCommands()
        {
            var world = new GameObjectCommandOffline.FakeWorld();
            world.Rows.Add(Row(4, "A"));
            var registration = DebugGameObjectCommands.CreateRegistration(world);
            Equal(6, registration.Catalog.Count, "count");
            True(ReferenceEquals(world, registration.World), "world");
            Equal(0, world.CollectCalls, "register collect");

            True(registration.Catalog.TryExecute("go.select", "{\"instanceId\":\"4\"}", out var selected), "select");
            True(selected.Success, "select success");
            True(registration.Selection.TryGet(out var chosen) && chosen == 4UL, "stored");

            True(registration.Catalog.TryExecute("go.set-active", "{\"active\":false}", out var active), "active");
            True(active.Success, "active success");
            Equal("Set GameObject active.", active.Message, "active message");
            True(active.PayloadJson.Contains("\"instanceId\":\"4\"") && active.PayloadJson.Contains("\"activeSelf\":false"), "active payload");
            True(registration.Selection.TryGet(out chosen) && chosen == 4UL, "selection stays");

            True(registration.Catalog.TryExecute("go.list", "", out var listed), "list");
            True(listed.Success, "list success");
            True(listed.PayloadJson.Contains("\"instanceId\":\"4\""), "listed");
        }

        private static void UnregisteredCatalog_DoesNotResolveGameObjectCommands()
        {
            var catalog = new DebugCommandCatalog();
            foreach (var name in CommandNames)
            {
                True(!catalog.TryExecute(name, "{}", out var missing), name);
                True(!missing.Success, name + " success");
                Equal("Debug command is not registered.", missing.Message, name);
            }
        }

        private static void StartupSources_KeepTheNullDispatcher()
        {
            var root = FindRepoRoot();
            var initializer = File.ReadAllText(Path.Combine(
                root,
                "unity",
                "Assets",
                "OneStarMaker",
                "Scripts",
                "Runtime",
                "Bootstrap",
                "AbstractApplicationInitializer.cs"));
            True(initializer.Contains("=> NullDebugCommandDispatcher.Instance;"), "null return");
            True(initializer.IndexOf("DebugGameObject", StringComparison.Ordinal) < 0, "initializer names commands");
            True(initializer.IndexOf("RegisterGameObjectCommands", StringComparison.Ordinal) < 0, "initializer registers");

            var app = File.ReadAllText(Path.Combine(
                root,
                "unity",
                "Assets",
                "SampleGame",
                "DependOnAll",
                "AppInitializer.cs"));
            True(app.IndexOf("DebugGameObject", StringComparison.Ordinal) < 0, "app names commands");
            True(app.IndexOf("CatalogDebugCommandDispatcher", StringComparison.Ordinal) < 0, "app dispatcher");
            True(app.IndexOf("CreateDebugCommandDispatcher", StringComparison.Ordinal) < 0, "app override");

            var socket = File.ReadAllText(Path.Combine(
                root,
                "unity",
                "Assets",
                "OneStarMaker",
                "Scripts",
                "Runtime",
                "DebugSocketServices",
                "DebugSocketService.cs"));
            True(socket.IndexOf("DebugGameObject", StringComparison.Ordinal) < 0, "socket names commands");
            True(socket.IndexOf("RegisterGameObjectCommands", StringComparison.Ordinal) < 0, "socket registers");
        }

        private static DebugGameObjectRow Row(ulong id, string name)
        {
            return new DebugGameObjectRow(id, false, 0, name, "Main", "/" + name, true, true, 0, 0);
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(
                    dir.FullName,
                    "unity",
                    "Assets",
                    "OneStarMaker",
                    "Scripts",
                    "Runtime",
                    "DebugCommands",
                    "DebugCommandCatalog.cs");
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
    }
}
