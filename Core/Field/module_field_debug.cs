﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenVIII.Encoding.Tags;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OpenVIII
{
    //issues found.
    //634 //mismatch tiles
    //726 //mismatch tiles
    //809 //mismatch tiles
    //880 //mismatch tiles
    //267 //text showing with white background.
    public static class Module_field_debug
    {
        public enum BlendMode : byte
        {
            halfadd,
            add,
            subtract,
            quarteradd,
            none,
        }

        private static Field_mods mod = 0;

        //private static Texture2D tex;
        //private static Texture2D texOverlap;
        private static ConcurrentDictionary<ushort, ConcurrentDictionary<byte, ConcurrentDictionary<BlendMode, Texture2D>>> Textures;

        private static EventEngine eventEngine;
        private static IServices services;
        private static List<Tile> tiles;

        private class Tile
        {
            /// <summary>
            /// Distance from left side
            /// </summary>
            public short X;

            /// <summary>
            /// Distance from top side
            /// </summary>
            public short Y;

            /// <summary>
            /// Larger number is farther away. So OrderByDecending for draw order.
            /// </summary>
            public ushort Z;

            public byte TextureID; // 4 bits
            public byte PaletteID; //[6-10]

            /// <summary>
            /// Source X from Texture Data
            /// </summary>
            public byte SourceX;

            /// <summary>
            /// Source Y from Texture Data
            /// </summary>
            public byte SourceY;

            /// <summary>
            /// Layer ID, Used to control which parts draw.
            /// </summary>
            public byte LayerID;

            public BlendMode BlendMode;
            public byte AnimationState;
            public byte AnimationID;
            public byte blend1;
            public byte blend2;
            public float Zfloat => Z / 4096f;
            public int TileID;

            public static implicit operator Vector3(Tile @in) => new Vector3(@in.X, @in.Y, @in.Zfloat);
        }

        private static int Width, Height;

        private enum Field_mods
        {
            INIT,
            DEBUGRENDER,
            DISABLED
        };

        public static void Draw()
        {
            switch (mod)
            {
                case Field_mods.INIT:
                    break; //null
                case Field_mods.DEBUGRENDER:
                    DrawDebug();
                    break;
            }
        }

        public static void ResetField() => mod = Field_mods.INIT;

        private static List<KeyValuePair<BlendMode, Texture2D>> drawtextures() => Textures.OrderByDescending(x => x.Key).SelectMany(x => x.Value.OrderBy(y => y.Key).SelectMany(y => y.Value)).ToList();

        private static void DrawDebug()
        {
            Memory.graphics.GraphicsDevice.Clear(Color.Black);

            List<KeyValuePair<BlendMode, Texture2D>> _drawtextures = drawtextures();
            bool open = false;
            BlendMode lastbm = BlendMode.none;
            float alpha = 1f;
            foreach (KeyValuePair<BlendMode, Texture2D> kvp in _drawtextures)
            {
                if (!open || lastbm != kvp.Key)
                {
                    if (open)
                        Memory.SpriteBatchEnd();
                    open = true;
                    alpha = 1f;
                    switch (kvp.Key)
                    {
                        default:
                            Memory.SpriteBatchStartAlpha();
                            break;

                        case BlendMode.halfadd:
                            //alpha = .5f; // not right but may work okay lets see.
                            Memory.SpriteBatchStart(bs: Memory.blendState_Add);
                            break;

                        case BlendMode.quarteradd:
                            //alpha = .25f; //not right but may work okay lets see.
                            Memory.SpriteBatchStart(bs: Memory.blendState_Add);
                            break;

                        case BlendMode.add:
                            Memory.SpriteBatchStart(bs: Memory.blendState_Add);
                            break;

                        case BlendMode.subtract:
                            //open = false;
                            //continue; // isn't working. unsure why.
                            alpha = .9f;
                            Memory.SpriteBatchStart(bs: Memory.blendState_Subtract);
                            break;
                    }
                    lastbm = kvp.Key;
                }
                Texture2D tex = kvp.Value;
                Rectangle src = new Rectangle(0, 0, tex.Width, tex.Height);
                Rectangle dst = src;
                dst.Size = (dst.Size.ToVector2() * Memory.Scale(tex.Width, tex.Height, Memory.ScaleMode.FitBoth)).ToPoint();
                //In game I think we'd keep the field from leaving the screen edge but would center on the Squall and the party when it can.
                //I setup scaling after noticing the field didn't size with the screen. I set it to center on screen.
                dst.Offset(Memory.Center.X - dst.Center.X, Memory.Center.Y - dst.Center.Y);
                Memory.spriteBatch.Draw(tex, dst, src, Color.White * alpha);
                //new Microsoft.Xna.Framework.Rectangle(0, 0, 1280 + (width - 320), 720 + (height - 224)),
                //new Microsoft.Xna.Framework.Rectangle(0, 0, tex.Width, tex.Height)
            }
            if (open)
                Memory.SpriteBatchEnd();
            Memory.SpriteBatchStartAlpha();
            Memory.font.RenderBasicText($"FieldID: {Memory.FieldHolder.FieldID}", new Point(20, 20), new Vector2(3f));
            Memory.SpriteBatchEnd();
        }

        public static void Update()
        {
#if DEBUG
            // lets you move through all the feilds just holding left or right. it will just loop when it runs out.
            if (Input2.DelayedButton(FF8TextTagKey.Left))
            {
                init_debugger_Audio.PlaySound(0);
                if (Memory.FieldHolder.FieldID > 0)
                    Memory.FieldHolder.FieldID--;
                else
                    Memory.FieldHolder.FieldID = checked((ushort)(Memory.FieldHolder.fields.Length - 1));
                ResetField();
            }
            if (Input2.DelayedButton(FF8TextTagKey.Right))
            {
                init_debugger_Audio.PlaySound(0);
                if (Memory.FieldHolder.FieldID < checked((ushort)(Memory.FieldHolder.fields.Length - 1)))
                    Memory.FieldHolder.FieldID++;
                else
                    Memory.FieldHolder.FieldID = 0;
                ResetField();
            }
#endif
            switch (mod)
            {
                case Field_mods.INIT:
                    Init();
                    break;

                case Field_mods.DEBUGRENDER:
                    UpdateScript();
                    break; //await events here
                case Field_mods.DISABLED:
                    break;
            }
        }

        private static void UpdateScript()
        {
            //We do not know every instruction and it's not possible for now to play field with unknown instruction
            //eventEngine.Update(services);
        }

        private static void Init()
        {
            ArchiveWorker aw = new ArchiveWorker(Memory.Archives.A_FIELD);
            string[] test = aw.GetListOfFiles();

            //TODO fix endless look on FieldID 50.
            if (Memory.FieldHolder.FieldID >= Memory.FieldHolder.fields.Length ||
                Memory.FieldHolder.FieldID < 0)
                return;
            string fieldArchiveName = test.FirstOrDefault(x => x.IndexOf(Memory.FieldHolder.fields[Memory.FieldHolder.FieldID], StringComparison.OrdinalIgnoreCase) >= 0);
            if (string.IsNullOrWhiteSpace(fieldArchiveName)) return;
            ArchiveWorker fieldArchive = aw.GetArchive(fieldArchiveName);
            string[] filelist = fieldArchive.GetListOfFiles();
            string findstr(string s) =>
                filelist.FirstOrDefault(x => x.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);

            byte[] getfile(string s)
            {
                s = findstr(s);
                if (!string.IsNullOrWhiteSpace(s))
                    return fieldArchive.GetBinaryFile(s);
                else
                    return null;
            }
            string s_jsm = findstr(".jsm");
            string s_sy = findstr(".sy");
            if (!ParseBackground(getfile(".mim"), getfile(".map")))
                return;
            //let's start with scripts
            List<Jsm.GameObject> jsmObjects;

            if (!string.IsNullOrWhiteSpace(s_jsm))
            {
                try
                {
                    jsmObjects = Jsm.File.Read(fieldArchive.GetBinaryFile(s_jsm));
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                    mod = Field_mods.DISABLED;
                    return;
                }

                Sym.GameObjects symObjects;
                if (!string.IsNullOrWhiteSpace(s_sy))
                {
                    symObjects = Sym.Reader.FromBytes(fieldArchive.GetBinaryFile(s_sy));
                }
                else
                    return;
                services = FieldInitializer.GetServices();
                eventEngine = ServiceId.Field[services].Engine;
                eventEngine.Reset();
                for (int objIndex = 0; objIndex < jsmObjects.Count; objIndex++)
                {
                    Jsm.GameObject obj = jsmObjects[objIndex];
                    FieldObject fieldObject = new FieldObject(obj.Id, symObjects.GetObjectOrDefault(objIndex).Name);

                    foreach (Jsm.GameScript script in obj.Scripts)
                        fieldObject.Scripts.Add(script.ScriptId, script.Segment.GetExecuter());

                    eventEngine.RegisterObject(fieldObject);
                }
            }
            else
            {
                mod = Field_mods.DISABLED;
                return;
            }

            //byte[] mchb = getfile(".mch");//Field character models
            //byte[] oneb = getfile(".one");//Field character models container
            //byte[] msdb = getfile(".msd");//dialogs
            //byte[] infb = getfile(".inf");//gateways
            //byte[] idb = getfile(".id");//walkmesh
            //byte[] cab = getfile(".ca");//camera
            //byte[] tdwb = getfile(".tdw");//extra font
            //byte[] mskb = getfile(".msk");//movie cam
            //byte[] ratb = getfile(".rat");//battle on field
            //byte[] pmdb = getfile(".pmd");//particle info
            //byte[] sfxb = getfile(".sfx");//sound effects

            mod++;
            return;
        }

        public class TextureBuffer
        {
            private Color[] colors;

            public TextureBuffer(int width, int height)
            {
                Height = height;
                Width = width;
                colors = new Color[height * width];
            }

            public int Height { get; private set; }
            public int Width { get; private set; }
            public Color[] Colors { get => colors; private set => colors = value; }
            public int Length => colors?.Length ?? 0;

            public Color this[int i] { get => colors[i]; set => colors[i] = value; }
            public Color this[int x, int y] { get => colors[x + y * Width]; set => colors[x + y * Width] = value; }

            public static implicit operator Color[] (TextureBuffer @in) => @in.colors;

            public static explicit operator Texture2D(TextureBuffer @in)
            {
                Texture2D tex = new Texture2D(Memory.graphics.GraphicsDevice, @in.Width, @in.Height);
                @in.SetData(tex);
                return tex;
            }

            public Color[] this[Rectangle rectangle]
            {
                get => this[rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height];
                set => this[rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height] = value;
            }

            public void SetData(Texture2D tex) => tex.SetData(colors);

            public Color[] this[int x, int y, int width, int height]
            {
                get
                {
                    int pos = (x + y * Width);
                    List<Color> r = new List<Color>(width * height);
                    int row = 0;
                    while (r.Count < width * height)
                    {
                        r.AddRange(colors.Skip(pos + row * Width).Take(width));
                        row++;
                    }
                    return r.ToArray();
                }
                set
                {
                    for (int _y = y; (_y - y) < height; _y++)
                        for (int _x = x; (_x - x) < width; _x++)
                        {
                            int pos = (_x + _y * Width);
                            colors[pos] = value[(_x - x) + (_y - y) * width];
                        }
                }
            }
        }

        private static bool ParseBackground(byte[] mimb, byte[] mapb)
        {
            if (mimb == null || mapb == null)
                return false;

            const int type1Width = 1664;

            tiles = new List<Tile>();
            int palettes = 24;
            //128x256
            //using (BinaryReader pbsmim = new BinaryReader(new MemoryStream(mimb))
            const int tilesize = 16;
            using (BinaryReader pbsmap = new BinaryReader(new MemoryStream(mapb)))
                while (pbsmap.BaseStream.Position + 16 < pbsmap.BaseStream.Length)
                {
                    Tile tile = new Tile { X = pbsmap.ReadInt16() };
                    if (tile.X == 0x7FFF)
                        break;
                    tile.Y = pbsmap.ReadInt16();
                    tile.Z = pbsmap.ReadUInt16();// (ushort)(4096 - pbsmap.ReadUShort());
                    byte texIdBuffer = pbsmap.ReadByte();
                    tile.TextureID = (byte)(texIdBuffer & 0xF);
                    pbsmap.BaseStream.Seek(1, SeekOrigin.Current);
                    //short testz = pbsmap.ReadShort();
                    //testz = (short)(testz >> 6);
                    //testz &= 0xF;
                    tile.PaletteID = (byte)((pbsmap.ReadInt16() >> 6) & 0xF);
                    tile.SourceX = pbsmap.ReadByte();
                    tile.SourceY = pbsmap.ReadByte();
                    tile.LayerID = (byte)(pbsmap.ReadByte() & 0x7F);
                    tile.BlendMode = (BlendMode)pbsmap.ReadByte();
                    tile.AnimationID = pbsmap.ReadByte();
                    tile.AnimationState = pbsmap.ReadByte();
                    tile.blend1 = (byte)((texIdBuffer >> 4) & 0x1);
                    tile.blend2 = (byte)(texIdBuffer >> 5);
                    tile.TileID = tiles.Count;
                    tiles.Add(tile);
                    if ((Memory.EnableDumpingData) && tile.blend2 < 4)
                    {
                        string path = Path.Combine(Path.GetTempPath(), "Fields", Memory.FieldHolder.FieldID.ToString());
                        Directory.CreateDirectory(path);
                        path = Path.Combine(path, $"SkipTile.{Memory.FieldHolder.FieldID}.{tiles.Count - 1}.{tile.blend2}.bin");
                        if (!File.Exists(path))
                            using (BinaryWriter bw = new BinaryWriter(new FileStream(
                                path,
                                FileMode.Create, FileAccess.Write, FileShare.ReadWrite)))
                            {
                                pbsmap.BaseStream.Seek(-16, SeekOrigin.Current);
                                bw.Write(pbsmap.ReadBytes(16));
                            }
                    }
                    //srcY = srcX == texID * 128 + srcX;
                }

            int lowestY = tiles.Min(x => x.Y);
            int maximumY = tiles.Max(x => x.Y);
            int lowestX = tiles.Min(x => x.X); //-160;
            int maximumX = tiles.Max(x => x.X);

            Height = Math.Abs(lowestY) + maximumY + tilesize; //224
            Width = Math.Abs(lowestX) + maximumX + tilesize; //320
                                                             //Color[] finalImage = new Color[height * width]; //ARGB;
                                                             //Color[] finalOverlapImage = new Color[height * width];
                                                             //tex = new Texture2D(Memory.graphics.GraphicsDevice, width, height);
                                                             //texOverlap = new Texture2D(Memory.graphics.GraphicsDevice, width, height);
            IOrderedEnumerable<byte> layers = tiles.Select(x => x.LayerID).Distinct().OrderBy(x => x);
            byte MaximumLayer = layers.Max();
            byte MinimumLayer = layers.Min();
            IOrderedEnumerable<ushort> BufferDepth = tiles.Select(x => x.Z).Distinct().OrderByDescending(x => x); // larger number is farther away.
            var sortedtiles = tiles.OrderByDescending(x => x.Z).ThenBy(x => x.LayerID).ThenBy(x => x.BlendMode).ThenBy(x => x.Y).ThenBy(x => x.X).ToList();

            if (Textures != null)
            {
                foreach (Texture2D tex in drawtextures().Select(x => x.Value))
                    tex.Dispose();
            }
            Textures = new ConcurrentDictionary<ushort, ConcurrentDictionary<byte, ConcurrentDictionary<BlendMode, Texture2D>>>();
            ushort z = 0;
            byte layerID = 0;
            BlendMode blendmode = BlendMode.none;
            TextureBuffer texturebuffer = null;
            bool hasColor = false;
            void convertColorToTexture2d()
            {
                if (!hasColor || texturebuffer == null) return;
                hasColor = false;
                ConcurrentDictionary<byte, ConcurrentDictionary<BlendMode, Texture2D>> dictlayers = Textures.GetOrAdd(z, new ConcurrentDictionary<byte, ConcurrentDictionary<BlendMode, Texture2D>>());
                ConcurrentDictionary<BlendMode, Texture2D> dictblend = dictlayers.GetOrAdd(layerID, new ConcurrentDictionary<BlendMode, Texture2D>());
                Texture2D tex = dictblend.GetOrAdd(blendmode, new Texture2D(Memory.graphics.GraphicsDevice, Width, Height));
                texturebuffer.SetData(tex);
            }
            for (int i = 0; i < sortedtiles.Count; i++)
            {
                Tile previousTile = (i > 0)? previousTile = sortedtiles[i - 1]:null;
                Tile tile = sortedtiles[i];
                if (texturebuffer == null || previousTile == null ||
                    (previousTile.Z != tile.Z ||
                    previousTile.LayerID != tile.LayerID ||
                    previousTile.BlendMode != tile.BlendMode))
                {
                    convertColorToTexture2d();
                    texturebuffer = new TextureBuffer(Width, Height);
                    z = tile.Z;
                    layerID = tile.LayerID;
                    blendmode = tile.BlendMode;
                }

                const int bytesPerPalette = 2 * 256; //16 bit color (2 bytes) * 256 colors
                int palettePointer = 4096 + ((tile.PaletteID) * bytesPerPalette);
                int sourceImagePointer = bytesPerPalette * palettes;

                int realX = Math.Abs(lowestX) + tile.X; //baseX
                int realY = Math.Abs(lowestY) + tile.Y; //*width
                int realDestinationPixel = ((realY * Width) + realX);

                if (tile.blend2 >= 4)
                {
                    Rectangle dst = new Rectangle(realX, realY, tilesize, tilesize);
                    const int texturewidth = 128;
                    int startPixel = sourceImagePointer + tile.SourceX + texturewidth * tile.TextureID + (type1Width * tile.SourceY);
                    for (int y = 0; y < tilesize; y++)
                        for (int x = 0; x < tilesize; x++)
                        {
                            byte pixel = mimb[startPixel + x + (y * type1Width)];
                            ushort color16bit = BitConverter.ToUInt16(mimb, 2 * pixel + palettePointer);
                            if (color16bit == 0) // 0 is Color.TransparentBlack So we skip it.
                                continue;
                            Color color = Texture_Base.ABGR1555toRGBA32bit(color16bit);

                            int pos = realDestinationPixel + (x) + (y * Width);
                            Color bufferedcolor = texturebuffer[pos];
                            if (blendmode < BlendMode.none)
                            {
                                if (color == Color.Black)
                                    continue;

                                if (blendmode == BlendMode.subtract)
                                {
                                    if (bufferedcolor != Color.TransparentBlack)
                                        color = blend2(bufferedcolor, color);
                                }
                                else
                                {
                                    if (blendmode == BlendMode.quarteradd)
                                        color = Color.Multiply(color, .25f);
                                    else if (blendmode == BlendMode.halfadd)
                                        color = Color.Multiply(color, .5f);
                                    if (bufferedcolor != Color.TransparentBlack)
                                        color = blend1(bufferedcolor, color);
                                }
                            }
                            else if (bufferedcolor != Color.TransparentBlack)
                            {
                                var issues = sortedtiles.Where(k => (k.X >= tile.X && k.X < tile.X+16) && (k.Y >= tile.Y && k.Y < tile.Y + 16) && k.Z == tile.Z && k.LayerID == tile.LayerID).ToList();
                                throw new Exception("Color is already set something may be wrong.");
                            }
                            color.A = 0xFF;
                            texturebuffer[pos] = color;
                            hasColor = true;
                        }
                }
                else
                {
                    //So there are tiles being skipped unsure why yet.
                    //if(tile.blend2 !=2 && tile.blend2 != 1&& tile.blend2 != 0&& tile.blend2 !=3)
                    //throw new Exception("this one is different!");
                    //    int startPixel = sourceImagePointer + tile.srcx / 2 + 128 * tile.texID + (type1Width * tile.srcy);
                    //    for (int y = 0; y < 16; y++)
                    //        for (int x = 0; x < 16; x++)
                    //        {
                    //            byte index = mimb[startPixel + x + (y * 1664)];
                    //            ushort pixels = BitConverter.ToUInt16(mimb, 2 * (index & 0xF) + palettePointer);
                    //            byte red = (byte)((pixels) & 0x1F);
                    //            byte green = (byte)((pixels >> 5) & 0x1F);
                    //            byte blue = (byte)((pixels >> 10) & 0x1F);
                    //            red = (byte)MathHelper.Clamp((red * 8), 0, 255);
                    //            green = (byte)MathHelper.Clamp((green * 8), 0, 255);
                    //            blue = (byte)MathHelper.Clamp((blue * 8), 0, 255);
                    //            if (pixels != 0)
                    //            {
                    //                if (tile.blendType < 4)
                    //                {
                    //                    byte baseColorR = finalImage[realDestinationPixel + (x * 4) + (y * width * 4)];
                    //                    byte baseColorG = finalImage[realDestinationPixel + (x * 4) + (y * width * 4) + 1];
                    //                    byte baseColorB = finalImage[realDestinationPixel + (x * 4) + (y * width * 4) + 2];
                    //                    Blend(baseColorR, baseColorG, baseColorB, red, green, blue, tile, finalImage, realDestinationPixel, x, y);
                    //                }
                    //            }
                    //            pixels = BitConverter.ToUInt16(mimb, 2 * (index >> 4) + palettePointer);
                    //            red = (byte)((pixels) & 0x1F);
                    //            green = (byte)((pixels >> 5) & 0x1F);
                    //            blue = (byte)((pixels >> 10) & 0x1F);
                    //            red = (byte)MathHelper.Clamp((red * 8), 0, 255);
                    //            green = (byte)MathHelper.Clamp((green * 8), 0, 255);
                    //            blue = (byte)MathHelper.Clamp((blue * 8), 0, 255);
                    //            if (pixels != 0)
                    //            {
                    //                if (tile.blendType < 4)
                    //                {
                    //                    byte baseColorR = finalImage[realDestinationPixel + (x * 4) + (y * width * 4)];
                    //                    byte baseColorG = finalImage[realDestinationPixel + (x * 4) + (y * width * 4) + 1];
                    //                    byte baseColorB = finalImage[realDestinationPixel + (x * 4) + (y * width * 4) + 2];
                    //                    Blend(baseColorR, baseColorG, baseColorB, red, green, blue, tile, finalImage, realDestinationPixel, x, y);
                    //                }
                    //            }
                    //            else
                    //            {
                    //                finalImage[realDestinationPixel + (x * 4) + (y * width * 4)] = red;
                    //                finalImage[realDestinationPixel + (x * 4) + (y * width * 4) + 1] = green;
                    //                finalImage[realDestinationPixel + (x * 4) + (y * width * 4) + 2] = blue;
                    //                finalImage[realDestinationPixel + (x * 4) + (y * width * 4) + 3] = 0xFF;
                    //            }
                    //        }
                }
            }
            convertColorToTexture2d(); // gets leftover colors from last batch and makes a texture.
            SaveTextures();
            return true;
        }

        private static void SaveTextures()
        {
            if (Memory.EnableDumpingData || true)
            {
                //List<KeyValuePair<BlendModes, Texture2D>> _drawtextures = drawtextures();
                foreach (KeyValuePair<ushort, ConcurrentDictionary<byte, ConcurrentDictionary<BlendMode, Texture2D>>> zv in Textures)
                    foreach (KeyValuePair<byte, ConcurrentDictionary<BlendMode, Texture2D>> layer in zv.Value)
                        foreach (KeyValuePair<BlendMode, Texture2D> kvp in layer.Value)
                        {
                            string path = Path.Combine(Path.GetTempPath(), "Fields", Memory.FieldHolder.FieldID.ToString());
                            Directory.CreateDirectory(path);
                            kvp.Value.SaveAsPng(
                                new FileStream(Path.Combine(path,
                                $"Field.{Memory.FieldHolder.FieldID}.{zv.Key.ToString("D4")}.{layer.Key.ToString("D2")}.{(int)kvp.Key}.png"),
                                FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
                                kvp.Value.Width, kvp.Value.Height);
                        }
            }
        }

        private static void DrawEntities() => throw new NotImplementedException();

        ///// <summary>
        ///// Blend the colors depending on tile.blendmode
        ///// </summary>
        ///// <param name="finalImageColor"></param>
        ///// <param name="color"></param>
        ///// <param name="tile"></param>
        ///// <returns>Color</returns>
        ///// <see cref="http://www.raphnet.net/electronique/psx_adaptor/Playstation.txt"/>
        ///// <seealso cref="http://www.psxdev.net/forum/viewtopic.php?t=953"/>
        ///// <seealso cref="//http://wiki.ffrtt.ru/index.php?title=FF8/FileFormat_MAP"/>
        //private static Color BlendColors(ref Color finalImageColor, Color color, Tile tile)
        //{
        //    //• Semi Transparency
        //    //When semi transparency is set for a pixel, the GPU first reads the pixel it wants to write to, and then calculates
        //    //the color it will write from the 2 pixels according to the semi - transparency mode selected.Processing speed is lower
        //    //in this mode because additional reading and calculating are necessary.There are 4 semi - transparency modes in the
        //    //GPU.
        //    //B = the pixel read from the image in the frame buffer, F = the half transparent pixel
        //    //• 1.0 x B + 0.5 x F
        //    //• 1.0 x B + 1.0 x F
        //    //• 1.0 x B - 1.0 x F
        //    //• 1.0 x B + 0.25 x F
        //    //color must not be black
        //    Color baseColor = finalImageColor;
        //    //BlendState blendmode1 = new BlendState
        //    //{
        //    //    ColorSourceBlend = Blend.SourceColor,
        //    //    ColorDestinationBlend = Blend.DestinationColor,
        //    //    ColorBlendFunction = BlendFunction.Add
        //    //};
        //    //BlendState blendmode2 = new BlendState
        //    //{
        //    //    ColorSourceBlend = Blend.SourceColor,
        //    //    ColorDestinationBlend = Blend.DestinationColor,
        //    //    ColorBlendFunction = BlendFunction.Subtract
        //    //};
        //    //BlendState blendmode3 = new BlendState
        //    //{
        //    //    BlendFactor =
        //    //    ColorSourceBlend = Blend.SourceColor,
        //    //    ColorDestinationBlend = Blend.DestinationColor,
        //    //    ColorBlendFunction = BlendFunction.Subtract
        //    //};

        //    switch (tile.BlendMode)
        //    {
        //        case BlendMode.halfadd:
        //            return finalImageColor = blend0(baseColor, color);

        //        case BlendMode.add:
        //            return finalImageColor = blend1(baseColor, color);

        //        case BlendMode.subtract:
        //            return finalImageColor = blend2(baseColor, color);

        //        case BlendMode.quarteradd:
        //            //break;
        //            return finalImageColor = blend3(baseColor, color);
        //    }
        //    throw new Exception($"Blendtype is {tile.BlendMode}: There are only 4 blend modes, 0-3, 4+ are drawn directly.");
        //}

        private static Color blend0(Color baseColor, Color color)
        {
            Color r;
            r.R = (byte)MathHelper.Clamp(baseColor.R + color.R / 2, 0, 255);
            r.G = (byte)MathHelper.Clamp(baseColor.R + color.G / 2, 0, 255);
            r.B = (byte)MathHelper.Clamp(baseColor.B + color.B / 2, 0, 255);
            r.A = 0xFF;
            return r;
        }

        private static Color blend1(Color baseColor, Color color)
        {
            Color r;
            r.R = (byte)MathHelper.Clamp(baseColor.R + color.R, 0, 255);
            r.G = (byte)MathHelper.Clamp(baseColor.G + color.G, 0, 255);
            r.B = (byte)MathHelper.Clamp(baseColor.B + color.B, 0, 255);
            r.A = 0xFF;
            return r;
        }

        private static Color blend2(Color baseColor, Color color)
        {
            Color r;
            r.R = (byte)MathHelper.Clamp(baseColor.R - color.R, 0, 255);
            r.G = (byte)MathHelper.Clamp(baseColor.G - color.G, 0, 255);
            r.B = (byte)MathHelper.Clamp(baseColor.B - color.B, 0, 255);
            r.A = 0xFF;
            return r;
        }

        private static Color blend3(Color baseColor, Color color)
        {
            Color r;
            r.R = (byte)MathHelper.Clamp((byte)(baseColor.R + (color.R / 4)), 0, 255);
            r.G = (byte)MathHelper.Clamp((byte)(baseColor.G + (color.G / 4)), 0, 255);
            r.B = (byte)MathHelper.Clamp((byte)(baseColor.B + (color.B / 4)), 0, 255);
            r.A = 0xFF;
            return r;
        }
    }
}