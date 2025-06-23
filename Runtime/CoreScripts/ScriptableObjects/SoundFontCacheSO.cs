using System.Collections.Generic;
using UnityEngine;
using MidiPlayerTK;
using System.Collections;
using System;
using System.Threading.Tasks;

namespace MidiGenPlay
{
    [System.Serializable]
    public class SoundFontData
    {
        public string name;
        public List<BankData> banks;
    }

    [System.Serializable]
    public class BankData
    {
        public int bankNumber;
        public List<PatchData> patches;
    }

    [System.Serializable]
    public class PatchData
    {
        public int patchNumber;
        public string patchName;
    }

    [CreateAssetMenu(fileName = "SoundFont Cache", menuName = "MidiGenPlay/SoundFont Cache")]
    public class SoundFontCacheSO : ScriptableObject
    {
        public List<SoundFontData> availableSoundFonts = new List<SoundFontData>();

        public async Task RefreshSoundFontList()
        {
            availableSoundFonts.Clear();

            if (MidiPlayerGlobal.Instance != null && MidiPlayerGlobal.MPTK_ListSoundFont != null)
            {
                Debug.Log("MPTK is initialized and SoundFonts are available.");

                foreach (var soundFont in MidiPlayerGlobal.MPTK_ListSoundFont)
                {
                    Debug.Log("Loading SoundFont: " + soundFont);

                    // Wait for the SoundFont to be loaded asynchronously
                    bool loadSuccess = await WaitUntilSoundFontLoaded(soundFont);

                    if (loadSuccess)
                    {
                        Debug.Log("SoundFont loaded: " + soundFont);

                        // Collect banks and patches for this SoundFont
                        SoundFontData soundFontData = new SoundFontData
                        {
                            name = soundFont,
                            banks = new List<BankData>()
                        };

                        List<MPTKListItem> banks = MidiPlayerGlobal.MPTK_ListBank;
                        foreach (var bank in banks)
                        {
                            if (bank != null)
                            {
                                Debug.Log(bank.Index);

                                BankData bankData = new BankData
                                {
                                    bankNumber = bank.Index,
                                    patches = new List<PatchData>()
                                };

                                MidiPlayerGlobal.MPTK_SelectBankInstrument(bank.Index);

                                List<MPTKListItem> patches = MidiPlayerGlobal.MPTK_ListPreset;

                                foreach (var patch in patches)
                                {
                                    PatchData patchData = new PatchData
                                    {
                                        patchNumber = patch.Index,
                                        patchName = patch.Label
                                    };

                                    Debug.Log(patch.Label);
                                    bankData.patches.Add(patchData);
                                }

                                soundFontData.banks.Add(bankData);
                            }
                        }

                        availableSoundFonts.Add(soundFontData);
                    }
                    else
                    {
                        Debug.LogWarning("Failed to load SoundFont: " + soundFont);
                    }

                    await Task.Yield();
                }
            }
            else
            {
                Debug.LogWarning("MPTK is not initialized or no SoundFonts are available.");
            }
        }

        private async Task<bool> WaitUntilSoundFontLoaded(string soundFont)
        {
            // Start loading the SoundFont
            MidiPlayerGlobal.MPTK_SelectSoundFont(soundFont);

            // Use TaskCompletionSource to await the SoundFont loading process
            var tcs = new TaskCompletionSource<bool>();

            // Continuously check the status in a loop
            while (!MidiPlayerGlobal.MPTK_SoundFontLoaded)
            {
                await Task.Yield(); // Avoid blocking the main thread
            }

            tcs.SetResult(true);
            Debug.Log("SoundFont loading completed successfully.");

            return await tcs.Task;
        }
    }
}
