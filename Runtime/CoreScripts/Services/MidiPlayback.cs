using Melanchall.DryWetMidi.Core;
using MidiGenPlay.Interfaces;
using System.IO;
using static UnityEditor.Experimental.GraphView.GraphView;

namespace MidiGenPlay.Services
{
    public class MidiPlayback : IMidiPlayback
    {
        private readonly IPlayMidi player;

        public MidiPlayback(IPlayMidi player)
        {
            this.player = player;
        }

        public void Play(MidiFile song)
        {
            if (song == null || player == null) return;

            // Convert MidiFile → byte[]
            byte[] data;
            using (var ms = new MemoryStream())
            {
                song.Write(ms);
                data = ms.ToArray();
            }

            player.Stop(); // stop any existing playback
            player.Play(data);
        }

        public void Stop()
        {
            player?.Stop();
        }
    }
}