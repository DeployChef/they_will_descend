using System;
using System.Collections.Generic;
using _Project.Scripts.Infrastructure.Logging;

namespace _Project.Scripts.Shell
{
    /// <summary>
    /// Tiny app flow router. States own Enter/Exit side effects (UI, SimGate).
    /// </summary>
    public sealed class AppStateMachine
    {
        readonly Dictionary<AppStateId, IAppState> _states = new();
        IAppState _current;

        public AppStateId? CurrentId => _current?.Id;

        public void Register(IAppState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            _states[state.Id] = state;
        }

        public void Start(AppStateId id)
        {
            if (!_states.TryGetValue(id, out var next))
                throw new InvalidOperationException($"App state '{id}' is not registered.");

            _current = next;
            GameLog.Info($"AppFlow Start → {id}");
            _current.Enter();
        }

        public void TransitionTo(AppStateId id)
        {
            if (_current != null && _current.Id == id)
                return;

            if (!_states.TryGetValue(id, out var next))
                throw new InvalidOperationException($"App state '{id}' is not registered.");

            GameLog.Info($"AppFlow {_current?.Id} → {id}");
            _current?.Exit();
            _current = next;
            _current.Enter();
        }

        public void Tick()
        {
            _current?.Tick();
        }
    }
}
