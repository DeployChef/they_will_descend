using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Banks that live and die together: <c>Core</c> for the whole process, <c>GameWorld</c> while Game is open.
    /// </summary>
    [CreateAssetMenu(menuName = "TheyWillDescend/Audio/Audio Bank Set")]
    public sealed class AudioBankSet : ScriptableObject
    {
        [BankRef]
        [SerializeField] string[] banks = Array.Empty<string>();

        public IReadOnlyList<string> Banks => banks;
    }
}
