using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

using Ceremony.Models;
using Ceremony.Service;
using System.IO;

namespace Ceremony
{
    public static class Library
    {
        public static int GetSignupNumber(int Year, Guid CeremonyCategoryID, int SignupType)
        {
            SignupsService signupsService = new SignupsService();

            int Number = 1;
            IQueryable<Signups> signups = signupsService.Get().Where(a => a.Year == Year && a.CeremonyCategoryID == CeremonyCategoryID && a.SignupType == SignupType).OrderByDescending(o => o.Number);
            if(signups.Any())
            {
                Number = (int)signups.FirstOrDefault().Number + 1;
            }

            return Number;
        }

        public static byte[] DrawText(string text)
        {
            string address = string.Empty;

            Font font = new Font("標楷體", 25, GraphicsUnit.Pixel);
            
            //create a new image of the right size
            Image img = new Bitmap(25, 605);
            Graphics drawing = Graphics.FromImage(img);

            SolidBrush blackBrush = new SolidBrush(Color.Black);
            
            //paint the background
            drawing.Clear(Color.Transparent);

            float y = 0;
            char[] arr = text.ToCharArray();
            foreach(char c in arr)
            {
                string l = c.ToString();

                Image textimg = new Bitmap(1, 1);
                Graphics textdrawing = Graphics.FromImage(textimg);
                SizeF textsize = textdrawing.MeasureString(l, font);

                textimg.Dispose();
                textdrawing.Dispose();

                if (Regex.IsMatch(l, @"^[a-zA-Z0-9\-\(\)]$"))
                {
                    textimg = new Bitmap((int)textsize.Height, (int)textsize.Width);
                    textdrawing = Graphics.FromImage(textimg);
                    textdrawing.Clear(Color.Transparent);

                    textdrawing.TranslateTransform(textsize.Width / 2, textsize.Height / 2);
                    textdrawing.RotateTransform(90);

                    textdrawing.SmoothingMode = SmoothingMode.AntiAlias;
                    textdrawing.TextRenderingHint = TextRenderingHint.AntiAlias;
                    textdrawing.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    textdrawing.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    textdrawing.CompositingQuality = CompositingQuality.HighQuality;
                    textdrawing.CompositingMode = CompositingMode.SourceOver;
                    textdrawing.DrawString(l, font, blackBrush, -(textsize.Height / 2), -(textsize.Width / 2) - 4);

                    textdrawing.Save();
                    textdrawing.Dispose();

                    drawing.DrawImage(textimg, 0, y);
                    y = y + (textsize.Height - 10);
                }
                else
                {
                    textimg = new Bitmap((int)textsize.Width, (int)textsize.Height);
                    textdrawing = Graphics.FromImage(textimg);
                    textdrawing.Clear(Color.Transparent);

                    textdrawing.SmoothingMode = SmoothingMode.AntiAlias;
                    textdrawing.TextRenderingHint = TextRenderingHint.AntiAlias;
                    textdrawing.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    textdrawing.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    textdrawing.CompositingQuality = CompositingQuality.HighQuality;
                    textdrawing.CompositingMode = CompositingMode.SourceOver;
                    textdrawing.DrawString(l, font, blackBrush, -4, 0);

                    textdrawing.Save();
                    textdrawing.Dispose();

                    drawing.DrawImage(textimg, 0, y);
                    y = y + (textsize.Height - 9);
                }
            }

            drawing.SmoothingMode = SmoothingMode.AntiAlias;
            drawing.TextRenderingHint = TextRenderingHint.AntiAlias;
            drawing.InterpolationMode = InterpolationMode.HighQualityBicubic;
            drawing.PixelOffsetMode = PixelOffsetMode.HighQuality;
            drawing.CompositingQuality = CompositingQuality.HighQuality;
            drawing.CompositingMode = CompositingMode.SourceOver;

            drawing.Save();
            blackBrush.Dispose();
            drawing.Dispose();

            using (MemoryStream ms = new MemoryStream())
            {
                //img.Save("D:\\abc.png", ImageFormat.Png);
                img.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }
}
