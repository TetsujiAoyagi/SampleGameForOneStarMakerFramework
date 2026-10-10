#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.ScriptSystem;
using OneStarMaker.Runtime.UpdateSystem.Api;
using SampleGame.OutGame.HpGauge;
using UnityEngine;
using UnityEngine.UIElements;
namespace SampleGame.Tests.Editor.OutGame
{
    /// <summary>
    /// Visible-flow evidence only. Samples after the real runner in the same scheduler layer;
    /// it never ticks, dispatches, acknowledges, or replaces production participants.
    /// </summary>
    public sealed class HpGaugeVisibleFlowProbe : IUpdateElement, IDisposable
    {
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo RunnerField = typeof(HpGaugeView).GetField("_currentRunner", Hidden)!;
        private static readonly FieldInfo ModelField = typeof(HpGaugeView).GetField("_viewModel", Hidden)!;
        private static readonly FieldInfo MachineField = typeof(ScriptCommandRunner).GetField("_machine", Hidden)!;
        private readonly List<Row> _rows = new();
        private readonly Dictionary<ScriptCommandRunner, int> _runnerIds = new();
        private HpGaugeView? _view;
        private ScriptCommandRunner? _lastRunner;
        private SceneDirector? _director;
        private uint _previousFrame;
        private int _nextRunnerId;
        private bool _hasGap;
        private bool _disposed;
        private HpGaugeVisibleFlowProbe(HpGaugeView view, SceneDirector director)
        {
            _view = view;
            _director = director;
        }
        public static HpGaugeVisibleFlowProbe? Current { get; private set; }
        public bool HasBaseline => _rows.Count > 0 && _rows[0].RunnerId == 0;
        public bool HasGap => _hasGap;
        public int SampleCount => _rows.Count;
        /// <summary>Call before the real Run button. The first scheduled sample must be Idle.</summary>
        public static HpGaugeVisibleFlowProbe Attach(HpGaugeView view, SceneDirector director)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (director == null) throw new ArgumentNullException(nameof(director));
            if (Current != null) throw new InvalidOperationException("A visible-flow probe is already attached.");
            var probe = new HpGaugeVisibleFlowProbe(view, director);
            Current = probe;
            try
            {
                if (!UpdateSystemRuntime.RegisterElement("HpGaugeScriptDemo", probe, 0, 1))
                    throw new InvalidOperationException("Probe registration rejected.");
                return probe;
            }
            catch
            {
                probe.Dispose();
                throw;
            }
        }
        public void OnElementStart() { }
        public void OnElementLateUpdate(in UpdateFrameContext context) { }
        public void OnElementUpdate(in UpdateFrameContext context)
        {
            if (_disposed || _director == null) return;
            if (_previousFrame != 0 && context.FrameIndex != _previousFrame + 1) _hasGap = true;
            _previousFrame = context.FrameIndex;
            var view = _view;
            if (view == null)
            {
                _rows.Add(new Row(context.FrameIndex, context.UnscaledDeltaTime, 0, "ViewGone", -1, 0, 0, -1, _director.IsSceneLoaded("HpGauge"), ""));
                return;
            }
            var runner = RunnerField.GetValue(view) as ScriptCommandRunner;
            if (runner != null) _lastRunner = runner;
            else runner = _lastRunner;
            var model = ModelField.GetValue(view) as HpGaugeViewModel;
            var runnerId = 0;
            var state = "Idle";
            var pc = -1;
            var waitId = 0;
            long waitArgument = 0;
            if (runner != null)
            {
                if (!_runnerIds.TryGetValue(runner, out runnerId))
                {
                    runnerId = ++_nextRunnerId;
                    _runnerIds.Add(runner, runnerId);
                }
                state = runner.State.ToString();
                var machine = (ScriptMachine)MachineField.GetValue(runner)!;
                pc = machine.ProgramCounter;
                var pending = machine.PendingHostRequest;
                if (pending != null)
                {
                    waitId = pending.CommandId;
                    waitArgument = pending.Argument;
                }
            }
            var hp = -1;
            if (model != null)
            {
                var property = typeof(HpGaugeViewModel).GetProperty("Hp")!.GetValue(model)!;
                hp = (int)property.GetType().GetProperty("CurrentValue")!.GetValue(property)!;
            }
            var label = view.Root.Q<Label>("script-status-label")?.text ?? "";
            _rows.Add(new Row(context.FrameIndex, context.UnscaledDeltaTime, runnerId, state,
                pc, waitId, waitArgument, hp, _director.IsSceneLoaded("HpGauge"), label));
        }
        /// <summary>Return scalar rows and release all View/runner references in one call.</summary>
        public static string DetachAndSnapshot()
        {
            var probe = Current ?? throw new InvalidOperationException("No visible-flow probe is attached.");
            try { return probe.Snapshot(); }
            finally { probe.Dispose(); }
        }
        public string Snapshot()
        {
            var text = new StringBuilder();
            text.Append("hasBaseline=").Append(HasBaseline ? "true" : "false")
                .Append(";hasGap=").Append(_hasGap ? "true" : "false")
                .Append(";samples=").Append(_rows.Count).AppendLine();
            text.AppendLine("frame\tdelta\trunner\tstate\tpc\tpendingId\targument\thp\tloaded\tlabel");
            foreach (var row in _rows) text.AppendLine(row.ToTsv());
            return text.ToString();
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { UpdateSystemRuntime.UnregisterElement(this); }
            finally
            {
                _view = null;
                _lastRunner = null;
                _director = null;
                _runnerIds.Clear();
                if (ReferenceEquals(Current, this)) Current = null;
            }
        }
        private readonly struct Row
        {
            public Row(uint frame, float delta, int runnerId, string state, int pc,
                int pendingId, long argument, int hp, bool loaded, string label)
            {
                Frame = frame;
                Delta = delta;
                RunnerId = runnerId;
                State = state;
                Pc = pc;
                PendingId = pendingId;
                Argument = argument;
                Hp = hp;
                Loaded = loaded;
                Label = label;
            }
            public uint Frame { get; }
            public float Delta { get; }
            public int RunnerId { get; }
            public string State { get; }
            public int Pc { get; }
            public int PendingId { get; }
            public long Argument { get; }
            public int Hp { get; }
            public bool Loaded { get; }
            public string Label { get; }
            public string ToTsv() => string.Join("\t", Frame.ToString(CultureInfo.InvariantCulture),
                Delta.ToString("R", CultureInfo.InvariantCulture), RunnerId.ToString(CultureInfo.InvariantCulture),
                State, Pc.ToString(CultureInfo.InvariantCulture), PendingId.ToString(CultureInfo.InvariantCulture),
                Argument.ToString(CultureInfo.InvariantCulture), Hp.ToString(CultureInfo.InvariantCulture),
                Loaded ? "true" : "false", Label);
        }
    }
}
