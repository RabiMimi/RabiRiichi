using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RabiRiichi.Riichi;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RabiRiichi.Util
{
    public class HaisImage : IDisposable
    {
        const int HaiWidth = 80;
        const int HaiHeight = 129;

        private readonly Dictionary<Hai, Image> haiImages = new Dictionary<Hai, Image>();
        private bool disposedValue;

        public HaisImage()
        {
            foreach (var hai in new Hais("123456789m123456789p123456789s1234567z"))
            {
                haiImages.Add(hai, Image.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @$"\HaisImage\{hai}.png")));
            }
        }

        public Image Generate(Hais hais)
        {
            Image img = new Image<Rgb24>(HaiWidth * hais.Count, HaiHeight);
            for (int i = 0; i < hais.Count; i++)
            {
                img.Mutate(x => x.DrawImage(haiImages[hais[i]], new Point(40 + i * HaiWidth), 1));
            }
            return img;
        }

        public Image Generate(Hais hais, int countPerLine, int yPadding)
        {
            int line = hais.Count / countPerLine;
            int height = line * HaiHeight + (line - 1) * yPadding;

            throw new NotImplementedException();
        }

        public Image Generate(Hais hais, List<HaiLayout> haiLayouts)
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
                    foreach (var img in haiImages.Values)
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
        // ~HaisImage()
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

    public class HaiLayout
    {

    }
}
