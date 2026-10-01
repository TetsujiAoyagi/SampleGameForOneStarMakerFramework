#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using OneStarMaker.Editor.TestObservation;
using UnityEditor.TestTools.TestRunner.Api;
using RunState = UnityEditor.TestTools.TestRunner.Api.RunState;
using TestStatus = UnityEditor.TestTools.TestRunner.Api.TestStatus;

namespace OneStarMaker.Tests.Editor.TestObservation
{
    public sealed class ObservationCallbackTests
    {
        [Test]
        public void FailedRoot_PreservesExecutedAndNonexecutedLeafClassification()
        {
            foreach (var executed in new[] { true, false })
            {
                var state = TestObservationState.Create(Guid.NewGuid().ToString(), "C:/project", 123, Stopwatch.Frequency);
                state.Bootstrap("domain-a", DateTime.UtcNow.ToString("o"), Stopwatch.GetTimestamp(), Stopwatch.Frequency);
                state.RunStarted(new[] { new TestLeaf { id = "leaf", name = "Case", fullname = "Fixture.Case",
                    uniqueName = "Tests/Fixture/Case", assemblyName = "OneStarMaker.Tests.TestObservation.Editor", runState = "Runnable" } },
                    DateTime.UtcNow.ToString("o"), Stopwatch.GetTimestamp());
                Capture(state, state.clock.runStartedUtc, state.clock.runStartedTicks);
                // global Bootstrap へ触れず、実 callback adapter の結果だけを local state に刻む。
                var callbacks = new TestObservationCallbacks(state, () => { }, "", (utc, ticks) => Capture(state, utc, ticks));
                var leaf = new FakeTest(false, "leaf", "Case", "Fixture.Case");
                var leafResult = new FakeResult(leaf, "Failed:Error", TestStatus.Failed);
                var root = new FakeTest(true, "root", "Fixture", "Fixture");
                var rootResult = new FakeResult(root, "Failed:Child", TestStatus.Failed, leafResult);
                if (executed)
                {
                    callbacks.TestStarted(leaf);
                    callbacks.TestFinished(leafResult);
                }
                callbacks.RunFinished(rootResult);
                state.Seal();
                Assert.That(state.results[0].result, Is.EqualTo("Failed"));
                Assert.That(state.results[0].label, Is.EqualTo("Error"));
                Assert.That(state.status, Is.EqualTo(executed ? "complete" : "incomplete"));
            }
        }

        private static void Capture(TestObservationState state, string utc, long ticks)
        {
            state.AddAssemblies(new[] { new AssemblyObservation { domainOrdinal = 0, observedAtUtc = utc,
                ticks = ticks, fullName = "OneStarMaker.Editor.TestObservation, Version=1.0.0.0",
                location = "C:/observer.dll", loadedModuleVersionId = "mvid", diskSha256 = new string('A', 64), status = "observed" } });
        }

        private sealed class FakeTest : ITestAdaptor
        {
            internal FakeTest(bool suite, string id, string name, string fullname)
            { IsSuite = suite; Id = id; Name = name; FullName = fullname; }
            public string Id { get; }
            public string Name { get; }
            public string FullName { get; }
            public int TestCaseCount => IsSuite ? 1 : 0;
            public bool HasChildren => IsSuite;
            public bool IsSuite { get; }
            public IEnumerable<ITestAdaptor> Children => Array.Empty<ITestAdaptor>();
            public ITestAdaptor Parent => null!;
            public int TestCaseTimeout => 0;
            public ITypeInfo TypeInfo => null!;
            public IMethodInfo Method => null!;
            public object[] Arguments => Array.Empty<object>();
            public string[] Categories => Array.Empty<string>();
            public bool IsTestAssembly => false;
            public RunState RunState => UnityEditor.TestTools.TestRunner.Api.RunState.Runnable;
            public string Description => "";
            public string SkipReason => "";
            public string ParentId => "";
            public string ParentFullName => "";
            public string UniqueName => FullName;
            public string ParentUniqueName => "";
            public int ChildIndex => 0;
            public TestMode TestMode => UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode;
        }

        private sealed class FakeResult : ITestResultAdaptor
        {
            private readonly ITestResultAdaptor[] children;
            internal FakeResult(ITestAdaptor test, string resultState, TestStatus testStatus, params ITestResultAdaptor[] children)
            { Test = test; ResultState = resultState; TestStatus = testStatus; this.children = children; }
            public ITestAdaptor Test { get; }
            public string Name => Test.Name;
            public string FullName => Test.FullName;
            public string ResultState { get; }
            public TestStatus TestStatus { get; }
            public double Duration => 0;
            public DateTime StartTime => DateTime.MinValue;
            public DateTime EndTime => DateTime.MinValue;
            public string Message => "";
            public string StackTrace => "";
            public int AssertCount => 0;
            public int FailCount => 0;
            public int PassCount => 0;
            public int SkipCount => 0;
            public int InconclusiveCount => 0;
            public bool HasChildren => children.Length > 0;
            public IEnumerable<ITestResultAdaptor> Children => children;
            public string Output => "";
            public TNode ToXml() => null!;
        }
    }
}
