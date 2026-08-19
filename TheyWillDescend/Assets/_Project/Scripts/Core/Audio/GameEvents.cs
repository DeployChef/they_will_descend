using System;
using UnityEngine;
using UnityEngine.Events;

namespace Futboloid.Core.Audio
{
    /// <summary>
    /// Базовое игровое событие.
    /// </summary>
    public abstract class GameEvent
    {
        public abstract int Type { get; }
    }

    /// <summary>
    /// Собыие навигации (смена контекста).
    /// </summary>
    [Serializable]
    public class NavigationChangedEvent : GameEvent
    {
        public override int Type => 1;
        public NavigationContext NewContext { get; }
        public NavigationContext OldContext { get; }

        public NavigationChangedEvent(NavigationContext newContext, NavigationContext oldContext)
        {
            NewContext = newContext;
            OldContext = oldContext;
        }
    }

    /// <summary>
    /// Собыие начала матча.
    /// </summary>
    [Serializable]
    public class MatchStartedEvent : GameEvent
    {
        public override int Type => 2;
    }

    /// <summary>
    /// Собыие окончания матча.
    /// </summary>
    [Serializable]
    public class MatchEndedEvent : GameEvent
    {
        public override int Type => 3;
    }

    /// <summary>
    /// Собыие перезапроса поля (рестарт турнира).
    /// </summary>
    [Serializable]
    public class PitchResetRequestedEvent : GameEvent
    {
        public override int Type => 4;
        public bool IsOnField { get; }

        public PitchResetRequestedEvent(bool isOnField)
        {
            IsOnField = isOnField;
        }
    }

    /// <summary>
    /// Простая шина событий для аудио.
    /// </summary>
    public class AudioEventBus
    {
        private readonly UnityEvent<GameEvent> _events = new();

        public void Publish(GameEvent e)
        {
            _events?.Invoke(e);
        }

        public void Subscribe(Action<GameEvent> handler)
        {
            _events?.AddListener(handler);
        }

        public void Unsubscribe(Action<GameEvent> handler)
        {
            _events?.RemoveListener(handler);
        }
    }
}
