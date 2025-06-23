using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace MidiGenPlay
{
    [CustomEditor(typeof(SoundFontCacheSO))]
    public class SoundFontCacheSOEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SoundFontCacheSO soundFontCache = (SoundFontCacheSO)target;

            if (GUILayout.Button("Refresh SoundFonts"))
            {
                WaitForRefreshAndSetDirty(soundFontCache);
            }
        }

        private async Task WaitForRefreshAndSetDirty(SoundFontCacheSO soundFontCache)
        {
            await soundFontCache.RefreshSoundFontList();
            EditorUtility.SetDirty(soundFontCache);
        }
    }
}
