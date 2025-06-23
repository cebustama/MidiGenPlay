using MidiPlayerTK;
using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay
{
    public static class SoundFontUtility
    {
        private static SoundFontCacheSO soundFontCache;

        private static void LoadCache()
        {
            if (soundFontCache == null)
            {
                soundFontCache = Resources.Load<SoundFontCacheSO>("ScriptableObjects/Midi Instruments/_SoundFont Cache");
                if (soundFontCache == null)
                {
                    Debug.LogError("SoundFontCacheSO asset not found in Resources/MIDI/");
                }
            }
        }

        public static List<string> GetSoundFontNames()
        {
            LoadCache();
            List<string> soundFontNames = new List<string>();
            if (soundFontCache != null)
            {
                foreach (var soundFont in soundFontCache.availableSoundFonts)
                {
                    soundFontNames.Add(soundFont.name);
                }
            }
            return soundFontNames;
        }

        public static List<string> GetBanksForSoundFont(string soundFontName)
        {
            LoadCache();
            List<string> bankIndices = new List<string>();
            if (soundFontCache != null)
            {
                SoundFontData soundFont = soundFontCache.availableSoundFonts.Find(sf => sf.name == soundFontName);
                if (soundFont != null)
                {
                    // Convert bank numbers to strings
                    foreach (var bank in soundFont.banks)
                    {
                        // Format bank number as three-digit string (e.g., "001", "002")
                        bankIndices.Add(bank.bankNumber.ToString("D3"));
                    }
                }
                else
                {
                    Debug.LogWarning($"SoundFont '{soundFontName}' not found in SoundFontCache.");
                }
            }
            return bankIndices;
        }

        public static List<PatchData> GetPatchesDataForBank(string soundFontName, int bankIndex)
        {
            LoadCache();
            List<PatchData> patchesData = new List<PatchData>();
            if (soundFontCache != null)
            {
                // Find the specified SoundFont
                SoundFontData soundFont = soundFontCache.availableSoundFonts.Find(sf => sf.name == soundFontName);
                if (soundFont != null)
                {
                    // Find the specified bank within the SoundFont
                    BankData bank = soundFont.banks.Find(b => b.bankNumber == bankIndex);
                    if (bank != null)
                    {
                        // Extract and add each patch name in the bank to the list
                        foreach (var patch in bank.patches)
                        {
                            patchesData.Add(patch); // Add only the patch name
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Bank '{bankIndex}' not found in SoundFont '{soundFontName}'.");
                    }
                }
                else
                {
                    Debug.LogWarning($"SoundFont '{soundFontName}' not found in SoundFontCache.");
                }
            }
            return patchesData;
        }
    }
}
