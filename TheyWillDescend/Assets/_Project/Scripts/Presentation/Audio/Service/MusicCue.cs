using System;
using FMODUnity;
using UnityEngine;

namespace TheyWillDescend.Presentation.Audio
{
    public enum MusicTheme
    {
        Menu = 0,
        Gameplay = 1
    }

    /// <summary>
    /// What should be playing: an event, and optionally a labeled parameter state on it.
    /// Same event with another label changes the state; another event crossfades.
    /// </summary>
    [Serializable]
    public struct MusicCue
    {
        public EventReference Event;

        [Tooltip("Labeled parameter on the event (or a global one). Empty = the event plays as is.")]
        public string Parameter;

        [Tooltip("Label of that parameter for this cue.")]
        public string Label;

        public bool HasState => !string.IsNullOrEmpty(Parameter) && !string.IsNullOrEmpty(Label);
    }
}
