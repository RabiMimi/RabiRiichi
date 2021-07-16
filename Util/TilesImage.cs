using System;
using System.Collections.Generic;
using System.IO;
using RabiRiichi.Riichi;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using HUtil = HoshinoSharp.Runtime.Util;

namespace RabiRiichi.Util
{
    public class TilesImage : IDisposable
    {
        private static TilesImage mInstance;
        public static TilesImage V {
            get {
                if (mInstance == null)
                    mInstance = new TilesImage();
                return mInstance;
            }
        }

        const int TileWidth = 40;
        const int TileHeight = 65;

        private readonly Dictionary<Tile, Image> tileImages = new Dictionary<Tile, Image>();
        private bool disposedValue;

        public TilesImage()
        {
            foreach (var tile in new Tiles("12345r56789m12345r56789p12345r56789s1234567z"))
            {
                tileImages.Add(tile, Image.Load(Path.Combine(Constants.BASE_DIR, $"TilesImage/{tile}.png")));
            }
        }

        public Image Generate(Tiles tiles)
        {
            Image img = new Image<Rgb24>(TileWidth * tiles.Count, TileHeight);
            for (int i = 0; i < tiles.Count; i++)
            {
                img.Mutate(x => x.DrawImage(tileImages[tiles[i]], new Point(i * TileWidth), 1));
            }
            return img;
        }

        public Image Generate(Tiles tiles, int countPerLine, int yPadding)
        {
            int line = tiles.Count / countPerLine;
            int height = line * TileHeight + (line - 1) * yPadding;

            throw new NotImplementedException();
        }

        public Image Generate(Tiles tiles, List<TileLayout> tileLayouts)
        {
            throw new NotImplementedException();
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)
                    foreach (var img in tileImages.Values)
                    {
                        img.Dispose();
                    }
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~TilesImage()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class TileLayout
    {

    }
}
