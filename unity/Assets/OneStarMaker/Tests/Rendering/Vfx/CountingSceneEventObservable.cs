#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.SceneSystem;
using R3;

namespace OneStarMaker.Tests.Rendering.Vfx
{
    /// <summary>
    /// 購読の開始と、購読 disposable の Dispose を数える scene event 源。
    /// 「Dispose の後にイベントが無害」だけでは購読解除を証明できないので、解除を直接数える。
    /// R3 の Observer は OnCompleted の後に自分で購読を解除するので、完了を流すと解除も1回数える。
    /// </summary>
    internal sealed class CountingSceneEventObservable : Observable<SceneEvent>
    {
        private readonly List<Observer<SceneEvent>> _observers = new();

        public int SubscribeCount { get; private set; }

        public int UnsubscribeCount { get; private set; }

        public int ActiveObserverCount => _observers.Count;

        /// <summary>
        /// 購読の登録の直後（SubscribeCore の中）に呼ばれる。Subscribe の中での再入を注入する。
        /// </summary>
        public Action? OnSubscribe { get; set; }

        /// <summary>
        /// 購読 disposable の Dispose の中で、数えた後に呼ばれる。解除中の再入や、解除の例外を注入する。
        /// </summary>
        public Action? OnUnsubscribe { get; set; }

        public void Emit(SceneEvent sceneEvent)
        {
            foreach (var observer in _observers.ToArray())
            {
                observer.OnNext(sceneEvent);
            }
        }

        public void Complete()
        {
            foreach (var observer in _observers.ToArray())
            {
                observer.OnCompleted();
            }
        }

        protected override IDisposable SubscribeCore(Observer<SceneEvent> observer)
        {
            SubscribeCount++;
            _observers.Add(observer);
            var subscription = new Subscription(this, observer);
            OnSubscribe?.Invoke();
            return subscription;
        }

        private sealed class Subscription : IDisposable
        {
            private readonly CountingSceneEventObservable _owner;
            private readonly Observer<SceneEvent> _observer;
            private bool _disposed;

            public Subscription(CountingSceneEventObservable owner, Observer<SceneEvent> observer)
            {
                _owner = owner;
                _observer = observer;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner.UnsubscribeCount++;
                _owner._observers.Remove(_observer);
                _owner.OnUnsubscribe?.Invoke();
            }
        }
    }
}
