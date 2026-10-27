using System.Collections.Generic;
using System;
using FMOD.Studio;
using FMODUnity;
using STOP_MODE = FMOD.Studio.STOP_MODE;
using TheyWillDescend.Presentation.Infrastructure;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Values of the UI event's labeled state parameter (NONE=0, HOVER=1, YES=2, NO=3, WARNING=4, CLICK=5).
    /// </summary>
    public enum UiState
    {
        None = 0,
        Hover = 1,
        Yes = 2,
        No = 3,
        Warning = 4,
        Click = 5,
    }

    /// <summary>
    /// One persistent UI event. Instruments sit on a parameter sheet and fire when the state parameter
    /// enters their value, so the event starts once and every sound is a state flash.
    /// Hover and click on any interactable Selectable are picked up by a raycast, without per-button components.
    /// </summary>
    public sealed class UiAudio : IDisposable
    {
        // FMOD reads parameters in ~20 ms mixer chunks; a one-frame flash between chunks is lost.
        const double FlashSeconds = 0.1;

        readonly string _parameter;
        readonly List<RaycastResult> _hits = new();
        EventInstance _event;
        PARAMETER_ID _parameterId;
        bool _ready;
        UiState _current = UiState.None;
        double _returnAt;
        PointerEventData _pointer;
        GameObject _hovered;
        bool _wasPressed;

        public UiAudio(EventReference uiEvent, string parameter)
        {
            _parameter = parameter;
            Create(uiEvent);
        }

        /// <summary>Plays one UI sound. The same state twice in a row inside the flash is ignored.</summary>
        public void Play(UiState state)
        {
            if (!_ready || state == _current)
                return;

            _current = state;
            _returnAt = state != UiState.None ? Time.unscaledTimeAsDouble + FlashSeconds : 0.0;
            _event.setParameterByID(_parameterId, (float)state, ignoreseekspeed: true);
        }

        public void Tick()
        {
            if (!_ready)
                return;

            TrackPointer();

            if (_returnAt > 0.0 && Time.unscaledTimeAsDouble >= _returnAt)
            {
                _returnAt = 0.0;
                Play(UiState.None);
            }
        }

        public void Dispose()
        {
            _ready = false;
            if (!_event.isValid())
                return;
            _event.stop(STOP_MODE.ALLOWFADEOUT);
            _event.release();
            _event.clearHandle();
        }

        void Create(EventReference uiEvent)
        {
            if (uiEvent.IsNull)
            {
                GameLog.Warning("UiAudio: UI event is not assigned in AudioCatalog.");
                return;
            }

            try
            {
                _event = RuntimeManager.CreateInstance(uiEvent);
            }
            catch (EventNotFoundException)
            {
                GameLog.Error($"UiAudio: UI event {uiEvent} is not in the loaded banks (Core set).");
                return;
            }

            if (!ResolveParameter())
                return;

            // Without this the event opens on the parameter's preset value and plays one sound at start.
            _event.setParameterByID(_parameterId, (float)UiState.None, ignoreseekspeed: true);
            _event.start();
            _ready = true;
        }

        bool ResolveParameter()
        {
            if (_event.getDescription(out var description) != FMOD.RESULT.OK
                || description.getParameterDescriptionByName(_parameter, out var parameter) != FMOD.RESULT.OK)
            {
                GameLog.Warning($"UiAudio: parameter '{_parameter}' not found on the UI event.");
                return false;
            }

            if ((parameter.flags & PARAMETER_FLAGS.READONLY) != 0)
            {
                GameLog.Warning($"UiAudio: parameter '{_parameter}' is read-only. Make it a labeled game parameter in FMOD Studio.");
                return false;
            }

            _parameterId = parameter.id;
            return true;
        }

        void TrackPointer()
        {
            var eventSystem = EventSystem.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (eventSystem == null || mouse == null)
                return;

            _pointer ??= new PointerEventData(eventSystem);
            _pointer.position = mouse.position.ReadValue();
            _hits.Clear();
            eventSystem.RaycastAll(_pointer, _hits);

            GameObject found = null;
            for (var i = 0; i < _hits.Count; i++)
            {
                var selectable = _hits[i].gameObject.GetComponentInParent<Selectable>(true);
                if (selectable == null || !selectable.IsInteractable())
                    continue;
                found = selectable.gameObject;
                break;
            }

            if (found != _hovered)
            {
                _hovered = found;
                Play(found != null ? UiState.Hover : UiState.None);
            }

            var pressed = mouse.leftButton.isPressed;
            if (pressed == _wasPressed)
                return;
            _wasPressed = pressed;
            if (pressed && _hovered != null)
                Play(UiState.Click);
        }
    }
}
