#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor.TestTools.TestRunner.Api;

namespace OneStarMaker.Editor.TestObservation
{
    internal sealed class TestObservationCallbacks : ICallbacks
    {
        private readonly TestObservationState state;
        private readonly Action persist;
        private readonly bool injectMissingStarted;
        private const string FaultCase = "OneStarMaker.Tests.Editor.TestObservation.ObservationFaultFixture.IntentionalMissingCallback";

        internal TestObservationCallbacks(TestObservationState state, Action persist, string filter)
        {
            this.state = state;
            this.persist = persist;
            // 故障は明示した単独 filter の invocation だけに注入する。全件回帰へエラーログを漏らさない。
            injectMissingStarted = string.Equals(filter, FaultCase, StringComparison.Ordinal);
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            var leaves = new List<TestLeaf>();
            WalkSelection(testsToRun, leaves);
            state.RunStarted(leaves, DateTime.UtcNow.ToString("o"), Stopwatch.GetTimestamp());
            TestObservationBootstrap.CaptureAssemblies();
            persist();
        }

        public void TestStarted(ITestAdaptor test)
        {
            if (test.IsSuite) return;
            if (injectMissingStarted && test.FullName == FaultCase) return;
            state.Event("started", test.Id, "", "", DateTime.UtcNow.ToString("o"), Stopwatch.GetTimestamp());
            persist();
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.Test.IsSuite) return;
            SplitResult(result, out var value, out var label);
            state.Event("finished", result.Test.Id, value, label, DateTime.UtcNow.ToString("o"), Stopwatch.GetTimestamp());
            persist();
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            SplitResult(result, out var rootResult, out _);
            if (rootResult != "Passed") TestObservationBootstrap.Fail("RunFinished root result が Passed ではありません: " + rootResult);
            var leaves = new List<TestResultLeaf>();
            WalkResults(result, leaves);
            state.RunFinished(leaves, DateTime.UtcNow.ToString("o"), Stopwatch.GetTimestamp());
            TestObservationBootstrap.CaptureAssemblies();
            // 完了は終了時 seal が決める。ここで terminal を作ると遅い callback を取り逃がす。
            persist();
        }

        private static void WalkSelection(ITestAdaptor node, List<TestLeaf> leaves)
        {
            if (!node.IsSuite)
            {
                var assembly = node.Method?.TypeInfo?.Assembly ?? node.TypeInfo?.Assembly;
                leaves.Add(new TestLeaf { id = node.Id, name = node.Name, fullname = node.FullName,
                    uniqueName = node.UniqueName, assemblyName = assembly?.GetName().Name ?? "",
                    runState = node.RunState.ToString() });
                return;
            }
            foreach (var child in node.Children) WalkSelection(child, leaves);
        }

        private static void WalkResults(ITestResultAdaptor node, List<TestResultLeaf> leaves)
        {
            if (!node.Test.IsSuite)
            {
                SplitResult(node, out var value, out var label);
                leaves.Add(new TestResultLeaf { id = node.Test.Id, name = node.Name,
                    fullname = node.FullName, result = value, label = label });
                return;
            }
            foreach (var child in node.Children) WalkResults(child, leaves);
        }

        private static void SplitResult(ITestResultAdaptor result, out string value, out string label)
        {
            value = result.TestStatus.ToString();
            var raw = result.ResultState ?? "";
            var colon = raw.IndexOf(':');
            var baseValue = colon < 0 ? raw : raw.Substring(0, colon);
            label = colon < 0 ? "" : raw.Substring(colon + 1);
            if (baseValue != value) TestObservationBootstrap.Fail("ResultState と TestStatus が矛盾します: " + raw);
        }
    }
}
