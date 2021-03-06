﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace OpenVIII.AV
{
    public static class Music
    {
        public static void Init()
        {
            Memory.Log.WriteLine($"{nameof(Music)} :: {nameof(Init)}");
            // PC 2000 version has an CD audio track for eyes on me. I don't think we can play that.
            const MusicId unkPrefix = (MusicId)999;
            const MusicId altLoserPrefix = (MusicId)512;
            const MusicId loserPrefix = (MusicId)0;
            const MusicId eyesOnMePrefix = (MusicId)513;
            const MusicId altEyesOnMePrefix = (MusicId)22;
            string[] ext = { ".ogg", ".sgt", ".wav", ".mp3" };
            //Roses and Wine V07 moves most of the sgt files to dmusic_backup
            //it leaves a few files behind. I think because RaW doesn't replace everything.
            //ogg files stored in:
            var RaW_ogg_pt = Extended.GetUnixFullPath(Path.Combine(Memory.FF8Dir, "RaW", "GLOBAL", "Music"));
            // From what I gather the OGG files and the sgt files have the same numerical prefix. I
            // might try to add the functionality to the debug screen monday.

            var dmusic_pt = Extended.GetUnixFullPath(Path.Combine(Memory.FF8DirData, "Music", "dmusic_backup"));
            var music_pt = Extended.GetUnixFullPath(Path.Combine(Memory.FF8DirData, "Music", "dmusic"));
            var music_wav_pt = Extended.GetUnixFullPath(Path.Combine(Memory.FF8DirData, "Music"));

            // goal of dicmusic is to be able to select a track by prefix. it adds an list of files
            // with the same prefix. so you can later on switch out which one you want.
            AddMusicPath(RaW_ogg_pt);
            AddMusicPath(music_wav_pt);
            AddMusicPath(dmusic_pt);
            AddMusicPath(music_pt);
            if (!Memory.DicMusic.ContainsKey(eyesOnMePrefix) && Memory.DicMusic.ContainsKey(altEyesOnMePrefix))
            {
                Memory.DicMusic.Add(eyesOnMePrefix, Memory.DicMusic[altEyesOnMePrefix]);
            }
            var a = ArchiveZzz.Load(Memory.Archives.ZZZ_OTHER);
            var list = a?.GetListOfFiles();
            if (list != null && list.Length > 0)
            {
                ZZZ = true;
                foreach (var m in list.Where(x => ext.Any(y => x.EndsWith(y, StringComparison.OrdinalIgnoreCase))))
                {
                    AddMusic(m);
                }
            }
            void AddMusicPath(string p)
            {
                if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
                {
                    foreach (var m in Directory.GetFiles(p).Where(x => ext.Any(y => x.EndsWith(y, StringComparison.OrdinalIgnoreCase))))
                    {
                        AddMusic(m);
                    }
                }
            }
            void AddMusic(string m)
            {
                if (ushort.TryParse(Path.GetFileName(m).Substring(0, 3), out var key))
                {
                    //mismatched prefix's go here
                    if ((MusicId)key == altLoserPrefix)
                    {
                        key = (ushort)loserPrefix; //loser.ogg and sgt don't match.
                    }
                }
                else if (m.IndexOf("eyes_on_me", StringComparison.OrdinalIgnoreCase) >= 0)
                    key = (ushort)eyesOnMePrefix;
                else
                    key = (ushort)unkPrefix;

                if (!Memory.DicMusic.ContainsKey((MusicId)key))
                {
                    Memory.DicMusic.Add((MusicId)key, new List<string> { m });
                }
                else
                {
                    Memory.DicMusic[(MusicId)key].Add(m);
                }
            }
        }

        public static bool Playing => musicplaying;

        /// <summary>
        /// contains files from other.zzz
        /// </summary>
        public static bool ZZZ { get; set; } = false;

        public static object MusicTask { get;  }

        /// <summary>
        /// <para>checks to see if music buffer is running low and getframe triggers a refill.</para>
        /// <para>if played in task we don't need to do this.</para>
        /// </summary>
        public static void Update()
        {
            if (!Memory.Threaded)
                ffccMusic?.NextLoop();
        }

        //public static byte[] ReadFullyByte(Stream stream)
        //{
        //    // following formula goal is to calculate the number of bytes to make buffer. might be wrong.
        //    long size = stream.Length; // stream.Length should be in bytes. will error later if short.
        //    int start = 0;
        //    byte[] buffer = new byte[size];
        //    int read = 0;
        //    //do
        //    //{
        //    read = stream.Read(buffer, start, buffer.Length);
        //    start++;
        //    //}
        //    //while (read == 0 && start < size);
        //    if (read == 0)
        //    {
        //        return null;
        //    }

        //    if (read < size)
        //    {
        //        Array.Resize<byte>(ref buffer, read);
        //    }

        //    return buffer;
        //}

        //public static byte[] GetSamplesWaveData(float[] samples, int samplesCount)
        //{ // converts 32 bit float samples to 16 bit pcm. I think :P
        //    // https://stackoverflow.com/questions/31957211/how-to-convert-an-array-of-int16-sound-samples-to-a-byte-array-to-use-in-monogam/42151979#42151979
        //    byte[] pcm = new byte[samplesCount * 2];
        //    int sampleIndex = 0,
        //        pcmIndex = 0;

        //    while (sampleIndex < samplesCount)
        //    {
        //        short outsample = (short)(samples[sampleIndex] * short.MaxValue);
        //        pcm[pcmIndex] = (byte)(outsample & 0xff);
        //        pcm[pcmIndex + 1] = (byte)((outsample >> 8) & 0xff);

        //        sampleIndex++;
        //        pcmIndex += 2;
        //    }

        //    return pcm;
        //}
        private static bool musicplaying = false;

        private static int lastplayed = -1;

        public static void PlayStop(ushort? index = null, float volume = 0.5f, float pitch = 0.0f, float pan = 0.0f)
        {
            if (!musicplaying || lastplayed != Memory.MusicIndex)
            {
                Play(index: index, volume: volume, pitch: pitch, pan: pan);
            }
            else
            {
                Stop();
            }
        }

        private static AV.Audio ffccMusic = null; // testing using class to play music instead of Naudio / Nvorbis

        public static void Play(ushort? index = null, float volume = 0.5f, float pitch = 0.0f, float pan = 0.0f, bool loop = true)
        {
            Memory.MusicIndex = index ?? Memory.MusicIndex;

            if (musicplaying && lastplayed == Memory.MusicIndex) return;
            var ext = "";

            if (Memory.DicMusic.Count > 0 && Memory.DicMusic[(MusicId)Memory.MusicIndex].Count > 0)
            {
                ext = Path.GetExtension(Memory.DicMusic[(MusicId)Memory.MusicIndex][0]).ToLower();
            }
            else
                return;

            var filename = Memory.DicMusic[(MusicId)Memory.MusicIndex][0];

            Stop();

            switch (ext)
            {
                case ".ogg":
                case ".wav":
                case ".mp3":
                default:
                    //ffccMusic = new Ffcc(@"c:\eyes_on_me.wav", AVMediaType.AVMEDIA_TYPE_AUDIO, Ffcc.FfccMode.STATE_MACH);
                    if (ffccMusic != null)
                    {
                        ffccMusic?.Dispose();
                        ffccMusic = null;
                    }
                    if (!ZZZ)
                    {
                        ffccMusic = AV.Audio.Load(filename, loop ? 0 : -1);
                        if (!loop)
                            ffccMusic.LoopStart = -1;
                        if (Memory.Threaded)
                            ffccMusic.PlayInTask(volume, pitch, pan);
                        else
                            ffccMusic.Play(volume, pitch, pan);
                    }
                    else
                    {
                        return;
                        //cancelTokenSource = new CancellationTokenSource();
                        //cancelToken = cancelTokenSource.Token;
                        //MusicTask = Task.Run(()=>PlayInTask(ref ffccMusic,volume, pitch, pan, loop, filename,cancelToken), cancelToken);
                    }
                    break;

                case ".sgt":
#if _X64 || !_WINDOWS
                    if (fluid_Midi == null)
                        fluid_Midi = new AV.Midi.Fluid();
                    fluid_Midi.ReadSegmentFileManually(filename);
                    fluid_Midi.Play();
#else
                    
                        if (dm_Midi == null)
                            dm_Midi = new AV.Midi.DirectMedia();
                        dm_Midi.Play(filename,loop);
                    
#endif

                    break;
            }

            musicplaying = true;
            lastplayed = Memory.MusicIndex;
        }

        private static unsafe void PlayInTask(ref Audio ffAudio, float volume, float pitch, float pan, bool loop, string filename, CancellationToken cancelToken)
        {
            return;
            //ArchiveZzz a = (ArchiveZzz)ArchiveZzz.Load(Memory.Archives.ZZZ_OTHER);
            //var fd = a.ArchiveMap.GetFileData(filename);
            //BufferData buffer_Data = new AV.BufferData
            //{
            //    DataSeekLoc = fd.Value.Offset,
            //    DataSize = fd.Value.UncompressedSize,
            //    HeaderSize = 0,
            //    Target = BufferData.TargetFile.other_zzz
            //};
            //GCHandle gch = GCHandle.Alloc(buffer_Data, GCHandleType.Pinned);
            //ffAudio = AV.Audio.Load(
            //    &buffer_Data,
            //    null, loop ? 0 : -1, Ffcc.FfccMode.STATE_MACH);
            //ffAudio.PlayInTask(volume, pitch, pan);
            //while (!cancelToken.IsCancellationRequested)
            //    Thread.Sleep(1000);
            //gch.Free();
        }

        public static void KillAudio()
        {
            dm_Midi?.Dispose();
            fluid_Midi?.Dispose();
        }

        public static void Stop()
        {
            musicplaying = false;
            if (ffccMusic != null)
            {
                ffccMusic.Dispose();
                ffccMusic = null;
            }
            cancelTokenSource?.Cancel();
#if !_X64
            if (dm_Midi != null)
                dm_Midi.Stop();
#else
            fluid_Midi?.Stop();
#endif
        }

        private static AV.Midi.DirectMedia dm_Midi;
        private static AV.Midi.Fluid fluid_Midi;
        private static CancellationTokenSource cancelTokenSource;
        private static CancellationToken cancelToken;
    }
}