using FMODUnity;
using UnityEngine;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Every sound the code names, in one asset the sound designer owns. References survive renames in FMOD Studio.
    /// </summary>
    [CreateAssetMenu(menuName = "TheyWillDescend/Audio/Audio Catalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        [Header("Banks")]
        [Tooltip("Loaded at process start and held until quit.")]
        [SerializeField] AudioBankSet core;
        [Tooltip("Loaded while Game is open, as a loading task.")]
        [SerializeField] AudioBankSet gameWorld;

        [Header("Music")]
        [SerializeField] MusicCue menuMusic;
        [SerializeField] MusicCue gameplayMusic;

        [Header("Snapshots")]
        [SerializeField] EventReference pauseSnapshot;

        [Header("UI")]
        [SerializeField] EventReference uiEvent;
        [Tooltip("Labeled parameter on the UI event whose values are UiState.")]
        [SerializeField] string uiStateParameter = "UI_States";

        [Header("World")]
        [SerializeField] EventReference wind;
        [Tooltip("Studio bus path for global ambience. FMOD has no bus reference type, so this one stays a string.")]
        [SerializeField] string ambienceBus;
        [Tooltip("Construction sound for building views that have no event of their own.")]
        [SerializeField] EventReference construction;

        public AudioBankSet Core => core;
        public AudioBankSet GameWorld => gameWorld;
        public EventReference PauseSnapshot => pauseSnapshot;
        public EventReference UiEvent => uiEvent;
        public string UiStateParameter => uiStateParameter;
        public EventReference Wind => wind;
        public string AmbienceBus => ambienceBus;
        public EventReference Construction => construction;

        public MusicCue Music(MusicTheme theme) => theme == MusicTheme.Menu ? menuMusic : gameplayMusic;
    }
}
