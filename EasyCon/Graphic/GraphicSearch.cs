﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Drawing;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Runtime.InteropServices;

namespace EasyCon.Graphic
{
    public class GraphicSearch
    {
        public static List<System.Drawing.Point> FindPic(int left, int top, int width, int height, Bitmap S_bmp, Bitmap P_bmp, ImgLabel.SearchMethod method)
        {
            if (S_bmp.PixelFormat != PixelFormat.Format24bppRgb) { throw new Exception("颜色格式只支持24位bmp"); }
            if (P_bmp.PixelFormat != PixelFormat.Format24bppRgb) { throw new Exception("颜色格式只支持24位bmp"); }
            int S_Width = S_bmp.Width;
            int S_Height = S_bmp.Height;
            int P_Width = P_bmp.Width;
            int P_Height = P_bmp.Height;
            Color BackColor = P_bmp.GetPixel(0, 0); //背景色
            BitmapData S_Data = S_bmp.LockBits(new Rectangle(0, 0, S_Width, S_Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            BitmapData P_Data = P_bmp.LockBits(new Rectangle(0, 0, P_Width, P_Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            List<System.Drawing.Point> List;
            int similar = 10;
            switch (method)
            {
                case ImgLabel.SearchMethod.SqDiff:
                    List = OpenCvFindPic(left, top, width, height, S_bmp, P_bmp, 0);
                    break;
                case ImgLabel.SearchMethod.SqDiffNormed:
                    List = OpenCvFindPic(left, top, width, height, S_bmp, P_bmp, 1);
                    break;
                case ImgLabel.SearchMethod.CCorr:
                    List = OpenCvFindPic(left, top, width, height, S_bmp, P_bmp, 2);
                    break;
                case ImgLabel.SearchMethod.CCorrNormed:
                    List = OpenCvFindPic(left, top, width, height, S_bmp, P_bmp, 3);
                    break;
                case ImgLabel.SearchMethod.CCoeff:
                    List = OpenCvFindPic(left, top, width, height, S_bmp, P_bmp, 4);
                    break;
                case ImgLabel.SearchMethod.CCoeffNormed:
                    List = OpenCvFindPic(left, top, width, height, S_bmp, P_bmp, 5);
                    break;
                case ImgLabel.SearchMethod.StrictMatch:
                    List = StrictMatch(left, top, width, height, S_Data, P_Data);
                    break;
                case ImgLabel.SearchMethod.StrictMatchRND:
                    List = StrictMatchRND(left, top, width, height, S_Data, P_Data);
                    break;
                case ImgLabel.SearchMethod.OpacityDiff:
                    List = OpacityDiff(left, top, width, height, S_Data, P_Data, GetPixelData(P_Data, BackColor), similar);
                    break;
                case ImgLabel.SearchMethod.SimilarMatch:
                    List = SimilarMatch(left, top, width, height, S_Data, P_Data, similar);
                    break;
                default:
                    List = StrictMatchRND(left, top, width, height, S_Data, P_Data);
                    break;
            }

            S_bmp.UnlockBits(S_Data);
            P_bmp.UnlockBits(P_Data);
            return List;
        }

        private static unsafe List<System.Drawing.Point> StrictMatch(int left, int top, int width, int height, BitmapData S_Data, BitmapData P_Data)
        {
            List<System.Drawing.Point> List = new List<System.Drawing.Point>();
            int S_stride = S_Data.Stride;
            int P_stride = P_Data.Stride;
            IntPtr S_Iptr = S_Data.Scan0;
            IntPtr P_Iptr = P_Data.Scan0;
            byte* S_ptr;
            byte* P_ptr;
            bool IsOk = false;
            int _BreakW = width - P_Data.Width + 1;
            int _BreakH = height - P_Data.Height + 1;
            for (int h = top; h < _BreakH; h++)
            {
                for (int w = left; w < _BreakW; w++)
                {
                    P_ptr = (byte*)(P_Iptr);
                    // there could be a random check for quick jump the loop
                    for (int y = 0; y < P_Data.Height; y++)
                    {
                        for (int x = 0; x < P_Data.Width; x++)
                        {
                            S_ptr = (byte*)((int)S_Iptr + S_stride * (h + y) + (w + x) * 3);
                            P_ptr = (byte*)((int)P_Iptr + P_stride * y + x * 3);
                            if (S_ptr[0] == P_ptr[0] && S_ptr[1] == P_ptr[1] && S_ptr[2] == P_ptr[2])
                            {
                                IsOk = true;
                            }
                            else
                            {
                                IsOk = false;
                                break;
                            }
                        }
                        if (!IsOk) { break; }
                    }
                    if (IsOk) { List.Add(new System.Drawing.Point(w, h)); }
                    IsOk = false;
                }
            }
            return List;
        }

        private static unsafe List<System.Drawing.Point> StrictMatchRND(int left, int top, int width, int height, BitmapData S_Data, BitmapData P_Data)
        {
            List<System.Drawing.Point> List = new List<System.Drawing.Point>();
            int S_stride = S_Data.Stride;
            int P_stride = P_Data.Stride;
            IntPtr S_Iptr = S_Data.Scan0;
            IntPtr P_Iptr = P_Data.Scan0;
            byte* S_ptr;
            byte* P_ptr;
            bool IsOk = false;
            int _BreakW = width - P_Data.Width + 1;
            int _BreakH = height - P_Data.Height + 1;

            Random r = new Random();

            // fitst we generate a random num list
            int pix_num = P_Data.Height * P_Data.Width;
            List<int> pix_list = new List<int>();
            for (int i = 0; i < pix_num; i++)
            {
                pix_list.Add(i);
            }

            List<int> random_pix_list = new List<int>();
            for(int i=0;i<pix_num;i++)
            {
                int index = r.Next(pix_num-i);
                //Debug.WriteLine(index);
                random_pix_list.Add(pix_list[index]);
                pix_list.RemoveAt(index);
            }

            //Debug.WriteLine("WH:"+P_Data.Width.ToString() + " " + P_Data.Height.ToString());

            for (int h = top; h < _BreakH; h++)
            {
                for (int w = left; w < _BreakW; w++)
                {
                    P_ptr = (byte*)(P_Iptr);
                    // there could be a random check for quick jump the loop
                    for (int i = 0; i < pix_num; i++)
                    {
                        int y = (int)(random_pix_list[i] / P_Data.Width);
                        int x = random_pix_list[i] % P_Data.Width;
                        //Debug.WriteLine(random_pix_list[i]);
                        //Debug.WriteLine(x.ToString() +" "+ y.ToString());
                        S_ptr = (byte*)((int)S_Iptr + S_stride*(h+y)+(w+x)*3);
                        P_ptr = (byte*)((int)P_Iptr + P_stride * y + x * 3);

                        //Debug.WriteLine(S_ptr[0].ToString() + " " + S_ptr[1].ToString() + " " + S_ptr[2].ToString());
                        //Debug.WriteLine(P_ptr[0].ToString() + " " + P_ptr[1].ToString() + " " + P_ptr[2].ToString());

                        if (S_ptr[0] == P_ptr[0] && S_ptr[1] == P_ptr[1] && S_ptr[2] == P_ptr[2])
                        {
                            IsOk = true;
                        }
                        else
                        {
                            IsOk = false;
                            break;
                        }
                    }

                    if (IsOk) 
                    {
                        //Debug.WriteLine("find");
                        List.Add(new System.Drawing.Point(w, h)); 
                    }
                    IsOk = false;
                }
            }
            return List;
        }

        private static unsafe List<System.Drawing.Point> SimilarMatch(int left, int top, int width, int height, BitmapData S_Data, BitmapData P_Data, int similar)
        {
            List<System.Drawing.Point> List = new List<System.Drawing.Point>();
            int S_stride = S_Data.Stride;
            int P_stride = P_Data.Stride;
            IntPtr S_Iptr = S_Data.Scan0;
            IntPtr P_Iptr = P_Data.Scan0;
            byte* S_ptr;
            byte* P_ptr;
            bool IsOk = false;
            int _BreakW = width - P_Data.Width + 1;
            int _BreakH = height - P_Data.Height + 1;
            for (int h = top; h < _BreakH; h++)
            {
                for (int w = left; w < _BreakW; w++)
                {
                    P_ptr = (byte*)(P_Iptr);
                    for (int y = 0; y < P_Data.Height; y++)
                    {
                        for (int x = 0; x < P_Data.Width; x++)
                        {
                            S_ptr = (byte*)((int)S_Iptr + S_stride * (h + y) + (w + x) * 3);
                            P_ptr = (byte*)((int)P_Iptr + P_stride * y + x * 3);
                            if (ScanColor(S_ptr[0], S_ptr[1], S_ptr[2], P_ptr[0], P_ptr[1], P_ptr[2], similar))  //比较颜色
                            {
                                IsOk = true;
                            }
                            else
                            {
                                IsOk = false; break;
                            }
                        }
                        if (IsOk == false) { break; }
                    }
                    if (IsOk) { List.Add(new System.Drawing.Point(w, h)); }
                    IsOk = false;
                }
            }
            return List;
        }

        private static unsafe List<System.Drawing.Point> OpacityDiff(int left, int top, int width, int height, BitmapData S_Data, BitmapData P_Data, int[,] PixelData, int similar)
        {
            List<System.Drawing.Point> List = new List<System.Drawing.Point>();
            int Len = PixelData.GetLength(0);
            int S_stride = S_Data.Stride;
            int P_stride = P_Data.Stride;
            IntPtr S_Iptr = S_Data.Scan0;
            IntPtr P_Iptr = P_Data.Scan0;
            byte* S_ptr;
            byte* P_ptr;
            bool IsOk = false;
            int _BreakW = width - P_Data.Width + 1;
            int _BreakH = height - P_Data.Height + 1;
            for (int h = top; h < _BreakH; h++)
            {
                for (int w = left; w < _BreakW; w++)
                {
                    for (int i = 0; i < Len; i++)
                    {
                        S_ptr = (byte*)((int)S_Iptr + S_stride * (h + PixelData[i, 1]) + (w + PixelData[i, 0]) * 3);
                        P_ptr = (byte*)((int)P_Iptr + P_stride * PixelData[i, 1] + PixelData[i, 0] * 3);
                        if (ScanColor(S_ptr[0], S_ptr[1], S_ptr[2], P_ptr[0], P_ptr[1], P_ptr[2], similar))  //比较颜色
                        {
                            IsOk = true;
                        }
                        else
                        {
                            IsOk = false; break;
                        }
                    }
                    if (IsOk) { List.Add(new System.Drawing.Point(w, h)); }
                    IsOk = false;
                }
            }
            return List;
        }

        public static List<System.Drawing.Point> OpenCvFindPic(int left, int top, int width, int height, Bitmap S_bmp, Bitmap P_bmp, int method)
        {
            List<System.Drawing.Point> res = new List<System.Drawing.Point>();
            Mat small = BitmapToMat(S_bmp);
            Mat big = BitmapToMat(P_bmp);
            Mat result = new Mat();

            switch (method)
            {
                case 5:
                    Cv2.MatchTemplate(big, small, result, TemplateMatchModes.CCoeffNormed);
                    break;
                case 0:
                    Cv2.MatchTemplate(big, small, result, TemplateMatchModes.SqDiff);
                    break;
                case 1:
                    Cv2.MatchTemplate(big, small, result, TemplateMatchModes.SqDiffNormed);
                    break;
                case 2:
                    // not good for our usage
                    Cv2.MatchTemplate(big, small, result, TemplateMatchModes.CCorr);
                    break;
                case 3:
                    Cv2.MatchTemplate(big, small, result, TemplateMatchModes.CCorrNormed);
                    break;
                case 4:
                    Cv2.MatchTemplate(big, small, result, TemplateMatchModes.CCoeff);
                    break;
                default:
                    Cv2.MatchTemplate(big, small, result, TemplateMatchModes.CCoeffNormed);
                    break;
            }

            OpenCvSharp.Point minLoc = new OpenCvSharp.Point(0, 0);
            OpenCvSharp.Point maxLoc = new OpenCvSharp.Point(0, 0);
            Cv2.MinMaxLoc(result, out minLoc, out maxLoc);

            // the sqD lower is good
            if (method == 3 || method == 2)
            {
                res.Add(new System.Drawing.Point(minLoc.X, minLoc.Y));
            }
            else
            {
                res.Add(new System.Drawing.Point(maxLoc.X, maxLoc.Y));
            }
            return res;
        }

        public static unsafe List<System.Drawing.Point> FindColor(int left, int top, int width, int height, Bitmap S_bmp, Color clr, int similar)
        {
            if (S_bmp.PixelFormat != PixelFormat.Format24bppRgb) { throw new Exception("颜色格式只支持24位bmp"); }
            BitmapData S_Data = S_bmp.LockBits(new Rectangle(0, 0, S_bmp.Width, S_bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            IntPtr _Iptr = S_Data.Scan0;
            byte* _ptr;
            List<System.Drawing.Point> List = new List<System.Drawing.Point>();
            for (int y = top; y < height; y++)
            {
                for (int x = left; x < width; x++)
                {
                    _ptr = (byte*)((int)_Iptr + S_Data.Stride * (y) + (x) * 3);
                    if (ScanColor(_ptr[0], _ptr[1], _ptr[2], clr.B, clr.G, clr.R, similar))
                    {
                        List.Add(new System.Drawing.Point(x, y));
                    }
                }
            }
            S_bmp.UnlockBits(S_Data);
            return List;
        }

        public static bool IsColor(Color clr1, Color clr2, int similar = 0)
        {
            if (ScanColor(clr1.B, clr1.G, clr1.R, clr2.B, clr2.G, clr2.R, similar))
            {
                return true;
            }
            return false;
        }

        public static unsafe Bitmap CopyScreen(int Width, int Height, int x, int y)
        {
            Bitmap bitmap = new Bitmap(Width, Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(Width, Height));
                g.Dispose();
            }
            System.GC.Collect();
            return bitmap;
        }

        private static unsafe int[,] GetPixelData(BitmapData P_Data, Color BackColor)
        {
            byte B = BackColor.B, G = BackColor.G, R = BackColor.R;
            int Width = P_Data.Width, Height = P_Data.Height;
            int P_stride = P_Data.Stride;
            IntPtr P_Iptr = P_Data.Scan0;
            byte* P_ptr;
            int[,] PixelData = new int[Width * Height, 2];
            int i = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    P_ptr = (byte*)((int)P_Iptr + P_stride * y + x * 3);
                    if (B == P_ptr[0] & G == P_ptr[1] & R == P_ptr[2])
                    {

                    }
                    else
                    {
                        PixelData[i, 0] = x;
                        PixelData[i, 1] = y;
                        i++;
                    }
                }
            }
            int[,] PixelData2 = new int[i, 2];
            Array.Copy(PixelData, PixelData2, i * 2);
            return PixelData2;
        }

        private static unsafe bool ScanColor(byte b1, byte g1, byte r1, byte b2, byte g2, byte r2, int similar)
        {
            if ((Math.Abs(b1 - b2)) > similar) { return false; } //B
            if ((Math.Abs(g1 - g2)) > similar) { return false; } //G
            if ((Math.Abs(r1 - r2)) > similar) { return false; } //R
            return true;
        }

        private static Mat BitmapToMat(Bitmap srcbit)
        {
            int iwidth = srcbit.Width;
            int iheight = srcbit.Height;
            int iByte = iwidth * iheight * 3;
            byte[] result = new byte[iByte];
            int step;

            Rectangle rect = new Rectangle(0, 0, iwidth, iheight);
            BitmapData bmpData = srcbit.LockBits(rect, ImageLockMode.ReadWrite, srcbit.PixelFormat);
            IntPtr iPtr = bmpData.Scan0;
            Marshal.Copy(iPtr, result, 0, iByte);
            step = bmpData.Stride;
            srcbit.UnlockBits(bmpData);

            return new Mat(srcbit.Height, srcbit.Width, new MatType(MatType.CV_8UC3), result, step);
        }
    }
}
