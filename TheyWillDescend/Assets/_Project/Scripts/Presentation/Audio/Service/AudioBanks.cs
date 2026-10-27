using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FMODUnity;
using TheyWillDescend.Presentation.Infrastructure;

namespace TheyWillDescend.Presentation.Audio
{
    /// <summary>
    /// Loads bank sets on top of the reference count in <see cref="RuntimeManager"/>.
    /// A set is held by the handle it returns; disposing the handle releases it.
    /// </summary>
    public sealed class AudioBanks
    {
        /// <summary>
        /// Bank data is loaded before the first await, so scene objects can create instances in Start.
        /// The task completes when sample data has finished loading.
        /// </summary>
        public async UniTask<IDisposable> Acquire(AudioBankSet set, CancellationToken ct)
        {
            var handle = Load(set);
            try
            {
                await UniTask.WaitWhile(RuntimeManager.AnySampleDataLoading, cancellationToken: ct);
            }
            catch
            {
                handle.Dispose();
                throw;
            }

            return handle;
        }

        public bool IsLoaded(AudioBankSet set)
        {
            if (set == null)
                return false;

            var any = false;
            for (var i = 0; i < set.Banks.Count; i++)
            {
                var bank = set.Banks[i];
                if (string.IsNullOrEmpty(bank))
                    continue;
                any = true;
                if (!RuntimeManager.HasBankLoaded(bank))
                    return false;
            }

            return any;
        }

        /// <summary>Loads bank data now and starts sample loading without waiting for it.</summary>
        public IDisposable Load(AudioBankSet set)
        {
            if (set == null)
            {
                GameLog.Error("AudioBanks: bank set is not assigned in AudioCatalog.");
                return new Handle(Array.Empty<string>());
            }

            var loaded = new List<string>(set.Banks.Count);
            for (var i = 0; i < set.Banks.Count; i++)
            {
                var bank = set.Banks[i];
                if (string.IsNullOrEmpty(bank))
                    continue;

                try
                {
                    RuntimeManager.LoadBank(bank, loadSamples: true);
                    loaded.Add(bank);
                }
                catch (BankLoadException e)
                {
                    GameLog.Error($"AudioBanks: bank '{bank}' of set '{set.name}' failed to load. {e.Message}");
                }
            }

            GameLog.Info($"AudioBanks: set '{set.name}' acquired ({string.Join(", ", loaded)}).");
            return new Handle(loaded.ToArray(), set.name);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Picks up banks rebuilt in FMOD Studio during Play. Only correct while the set is the sole holder
        /// of these banks: the reload drops one reference and takes it back.
        /// </summary>
        public void ReloadInEditor(AudioBankSet set)
        {
            if (set == null)
                return;
            for (var i = 0; i < set.Banks.Count; i++)
            {
                var bank = set.Banks[i];
                if (string.IsNullOrEmpty(bank))
                    continue;
                RuntimeManager.UnloadBank(bank);
                RuntimeManager.LoadBank(bank, loadSamples: true);
            }

            GameLog.Info($"AudioBanks: set '{set.name}' reloaded.");
        }
#endif

        sealed class Handle : IDisposable
        {
            string[] _banks;
            readonly string _setName;

            public Handle(string[] banks, string setName = null)
            {
                _banks = banks;
                _setName = setName;
            }

            public void Dispose()
            {
                if (_banks == null)
                    return;
                var banks = _banks;
                _banks = null;
                UnloadNextFrame(banks, _setName).Forget();
            }

            // Scene objects release their instances in OnDestroy, after the scope that held the set.
            static async UniTaskVoid UnloadNextFrame(string[] banks, string setName)
            {
                await UniTask.Yield();
                if (!RuntimeManager.IsInitialized)
                    return;
                for (var i = 0; i < banks.Length; i++)
                    RuntimeManager.UnloadBank(banks[i]);
                if (banks.Length > 0)
                    GameLog.Info($"AudioBanks: set '{setName}' released.");
            }
        }
    }
}
