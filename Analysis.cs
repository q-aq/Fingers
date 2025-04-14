using Org.BouncyCastle.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fingers
{
    public class Analysis//指纹分析类
    {

        [Serializable]
        public struct NEIGHBOUR
        {
            public int x;
            public int y;//横纵坐标
            public int type;//特征点类型（1：端点；3：分叉点）
            public float Theta;//两点连线角度（弧度）
            public float Theta2Ridge;//两点脊线方向夹角（弧度）
            public float ThetaThisNibor;//相邻特征点的脊线方向（弧度）
            public int distance;//两点距离（像素数量）

        };

        [Serializable]
        public struct MINUTIAE
        {
            public int x;
            public int y;//横纵坐标
            public int type;//特征点类型（1：端点；3：分叉点）
            public float theta;//该点处脊线方向（弧度）
            public NEIGHBOUR[] neibors;//相邻点特征序列
        };

        public Analysis()
        {

        }

        /// <summary>
        /// 将8bpp位图转换为字节数组
        /// </summary>
        private static byte[] BmpToBytes(Bitmap bmp)
        {
            BitmapData data = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format8bppIndexed
            );

            byte[] bytes = new byte[data.Stride * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            bmp.UnlockBits(data);

            return bytes;
        }

        /// <summary>
        /// 将byte数组转化为INT数组方便计算
        /// </summary>
        public static int[] byteToInt(byte[] bytes)
        {
            int[] Intarr = new int[bytes.Length];

            for (int i = 0; i < bytes.Length; i++)
            {
                Intarr[i] = (int)bytes[i];
            }
            return Intarr;
        }
        /// <summary>
        /// 将INT数组转化为byte类型，方便后续转化为BMP对象
        /// </summary>
        public static byte[] IntTobyte(int[] Intarr)
        {
            byte[] bytes = new byte[Intarr.Length];
            for (int i = 0; i < Intarr.Length; i++)
            {
                bytes[i] = (byte)Intarr[i];
            }
            return bytes;
        }
        /// <summary>
        /// 获取图片宽高，深度等信息，返回三元组。
        /// </summary>
        public static List<int> GetBitmapInfo(Bitmap bitmap)
        {
            List<int> result = new List<int>();
            int Width = bitmap.Width;
            int Height = bitmap.Height;
            int Depth = Image.GetPixelFormatSize(bitmap.PixelFormat);
            result.Add(Width);
            result.Add(Height);
            result.Add(Depth);
            return result;
        }

        /// <summary>
        /// 从字节数组构建8位灰度位图
        /// </summary>
        private static Bitmap BuildGrayBitmap(byte[] bytes, int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format8bppIndexed);

            // 设置灰度调色板
            ColorPalette palette = bmp.Palette;
            for (int i = 0; i < 256; i++)
                palette.Entries[i] = Color.FromArgb(i, i, i);
            bmp.Palette = palette;

            // 复制像素数据
            BitmapData data = bmp.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format8bppIndexed
            );

            Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            bmp.UnlockBits(data);

            return bmp;
        }

        /// <summary>
        /// 中值滤波
        /// </summary>
        public static Bitmap MidFilter(Bitmap input, int kernelSize = 3)
        {
            // 使用 GetBitmapInfo 验证图像位深度是否为 8
            List<int> bitmapInfo = GetBitmapInfo(input);
            int depth = bitmapInfo[2];
            if (depth != 8)
            {
                return null; // 非 8 位深度直接退出
            }
            // 验证内核大小合法性
            if (kernelSize % 2 == 0 || kernelSize < 3)
                throw new ArgumentException("Kernel size must be odd and at least 3.");
            // 创建输出位图并复制调色板（确保灰度信息保留）
            Bitmap output = new Bitmap(input.Width, input.Height, input.PixelFormat);
            output.Palette = input.Palette; // 关键：继承输入图像的调色板
            // 锁定位图数据
            Rectangle rect = new Rectangle(0, 0, input.Width, input.Height);
            BitmapData inputData = input.LockBits(rect, ImageLockMode.ReadOnly, input.PixelFormat);
            BitmapData outputData = output.LockBits(rect, ImageLockMode.WriteOnly, output.PixelFormat);
            try
            {
                int bytesPerPixel = 1;
                int radius = kernelSize / 2;
                int byteCount = inputData.Stride * input.Height;
                byte[] pixels = new byte[byteCount];
                Marshal.Copy(inputData.Scan0, pixels, 0, byteCount);
                // 遍历每个像素进行中值滤波
                for (int y = 0; y < input.Height; y++)
                {
                    for (int x = 0; x < input.Width; x++)
                    {
                        List<byte> neighborhood = new List<byte>();
                        // 收集邻域像素值
                        for (int ky = -radius; ky <= radius; ky++)
                        {
                            int currentY = y + ky;
                            if (currentY < 0 || currentY >= input.Height) continue;
                            for (int kx = -radius; kx <= radius; kx++)
                            {
                                int currentX = x + kx;
                                if (currentX < 0 || currentX >= input.Width) continue;

                                // 计算偏移量（注意 Stride 可能包含填充字节）
                                int offset = currentY * inputData.Stride + currentX * bytesPerPixel;
                                byte pixelValue = pixels[offset]; // 直接读取单字节像素值
                                neighborhood.Add(pixelValue);
                            }
                        }
                        // 计算中值并写入输出
                        neighborhood.Sort();
                        byte median = neighborhood[neighborhood.Count / 2];
                        int outputOffset = y * outputData.Stride + x * bytesPerPixel;
                        Marshal.WriteByte(outputData.Scan0, outputOffset, median); // 仅写入 1 字节
                    }
                }
            }
            finally
            {
                input.UnlockBits(inputData);
                output.UnlockBits(outputData);
            }
            return output;
        }

        /// <summary>
        /// 直方图均衡化
        /// </summary>
        public static Bitmap HistoNormalize(Bitmap input)
        {
            byte[] bytes = BmpToBytes(input);
            int w = input.Width;
            int h = input.Height;
            //bytes 转为 Intarr，便于求值
            int[] Intarr = byteToInt(bytes);
            int sum = Intarr.Sum();
            //求平均值
            double dMean = sum / Intarr.Length * 1.0;
            //求方差
            double dSigma = 0;
            for (int i = 0; i < Intarr.Length; i++)
            {
                dSigma += (Intarr[i] - dMean) * (Intarr[i] - dMean);
            }
            dSigma = dSigma / Intarr.Length * 1.0;
            dSigma = Math.Sqrt(dSigma);
            //均衡化
            double dMean0 = 128, dSigma0 = 128;//预设灰度均值和方差
            double dCoeff = dSigma0 / dSigma;//预设转换系数
            for (int i = 0; i < Intarr.Length; i++)
            {
                double dValue = (double)Intarr[i];
                dValue = dMean0 + dCoeff * (dValue - dMean0);
                if (dValue < 0)
                {
                    dValue = 0;
                }
                else if (dValue > 255)
                {
                    dValue = 255;
                }
                Intarr[i] = (int)dValue;
            }
            //Intarr转为bytes
            bytes = IntTobyte(Intarr);
            //由bytes生成bitmap
            Bitmap output = BuildGrayBitmap(bytes, input.Width, input.Height);
            return output;
        }

        /// <summary>
        /// 方向计算
        /// </summary>
        public static Bitmap ImgDirection(Bitmap input, out float[] fFitDirc)
        {
            byte[] bytes = BmpToBytes(input);
            int w = input.Width;
            int h = input.Height;
            float[] fDirc = new float[w * h];
            fFitDirc = new float[w * h];
            fDirc = imgdirection(bytes, w, h);
            fFitDirc = DircLowPass(fDirc, w, h);
            for (int i = 0; i < fFitDirc.Length; i++)
            {
                bytes[i] = (byte)(fFitDirc[i] * 100);
            }
            Bitmap output = BuildGrayBitmap(bytes, w, h);
            return output;
        }

        /// <summary>
        /// 方向场计算核心算法
        /// </summary>
        public static float[] imgdirection(byte[] bytes, int w, int h)
        {

            float[] fDirc = new float[w * h];
            int[] Intarr = byteToInt(bytes);
            const int WindowR = 7;//窗口半径
            int[,] dx = new int[WindowR * 2 + 1, WindowR * 2 + 1];
            int[,] dy = new int[WindowR * 2 + 1, WindowR * 2 + 1];
            float fx, fy;
            //计算每一像素的脊线方向
            for (int y = WindowR + 1; y < h - WindowR - 1; y++)//逐行，除了边缘
            {
                for (int x = WindowR + 1; x < w - WindowR - 1; x++)//逐列，除了边缘
                {
                    for (int j = 0; j < WindowR * 2 + 1; j++)
                    {
                        for (int i = 0; i < WindowR * 2 + 1; i++)
                        {
                            int index1 = (y + j - WindowR) * w + x + i - WindowR;
                            int index2 = (y + j - WindowR) * w + x + i - WindowR - 1;
                            int index3 = (y + j - WindowR - 1) * w + x + i - WindowR;
                            dx[i, j] = Intarr[index1] - Intarr[index2];
                            dy[i, j] = Intarr[index1] - Intarr[index3];
                        }
                    }
                    //计算当前像素脊线方向值
                    fx = 0.0f; fy = 0.0f;
                    for (int j = 0; j < WindowR * 2 + 1; j++)
                    {
                        for (int i = 0; i < WindowR * 2 + 1; i++)
                        {
                            fx += (float)(2 * dx[i, j] * dy[i, j] * 1.0);
                            fy += (float)((dx[i, j] * dx[i, j] - dy[i, j] * dy[i, j]) * 1.0);

                        }
                    }
                    fDirc[y * w + x] = (float)Math.Atan2(fx, fy);//此处转换可能存在精度问题
                }
            }
            return fDirc;
        }

        /// <summary>
        /// 方向场低通滤波
        /// </summary>
        public static float[] DircLowPass(float[] fDirc, int w, int h)
        {
            int arrsize = w * h;
            float[] fFitDirc = new float[arrsize];
            const int fisize = 2;
            int blocksize = 2 * fisize + 1;
            float[] filter = new float[blocksize * blocksize];
            float[] phix = new float[arrsize];
            float[] phiy = new float[arrsize];
            float[] phi2x = new float[arrsize];
            float[] phi2y = new float[arrsize];
            float sum = 0.0f;
            for (int y = 0; y < blocksize; y++)
            {
                for (int x = 0; x < blocksize; x++)
                {
                    filter[y * blocksize + x] = (float)(blocksize - Math.Abs(fisize - x) + Math.Abs(fisize - y));
                    sum += filter[y * blocksize + x];
                }
            }
            for (int y = 0; y < blocksize; y++)
            {
                for (int x = 0; x < blocksize; x++)
                {
                    filter[y * blocksize + x] /= sum;
                }
            }
            //计算各像素点的方向正弦值，余弦值
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    phix[y * w + x] = (float)Math.Cos(fDirc[y * w + x]);
                    phiy[y * w + x] = (float)Math.Sin(fDirc[y * w + x]);
                }
            }
            //对所有像素进行低通滤波
            float nx, ny;
            int val;
            for (int y = 0; y < h - blocksize; y++)
            {
                for (int x = 0; x < w - blocksize; x++)
                {
                    nx = 0.0f; ny = 0.0f;
                    for (int j = 0; j < blocksize; j++)
                    {
                        for (int i = 0; i < blocksize; i++)
                        {
                            val = (x + i) + (j + y) * w;
                            nx += filter[j * blocksize + i] * phix[val];
                            ny += filter[j * blocksize + i] * phiy[val];
                        }
                    }
                    val = x + y * w;
                    phi2x[val] = nx;
                    phi2y[val] = ny;
                }
            }
            //根据加权累加累加结果，计算各像素的方向滤波结果值
            for (int y = 0; y < h - blocksize; y++)
            {
                for (int x = 0; x < w - blocksize; x++)
                {
                    val = x + y * w;
                    fFitDirc[val] = (float)(Math.Atan2(phi2y[val], phi2x[val]) * 0.5);
                }
            }

            return fFitDirc;
        }

        /// <summary>
        /// 频率计算
        /// </summary>
        public static Bitmap Frequency(Bitmap input, float[] fDir, out float[] fFreqGet)
        {
            byte[] bytes = BmpToBytes(input);
            int w = input.Width;
            int h = input.Height;
            fFreqGet = new float[w*h];
            frequency(bytes, fDir, fFreqGet, w, h);
            for(int i = 0;i<bytes.Length;i++)
            {
                bytes[i] = (byte)(fFreqGet[i] * 1000);
            }
            Bitmap output = BuildGrayBitmap(bytes, w, h);
            return output;
        }

        /// <summary>
        /// 频率计算核心算法
        /// </summary>
        public static void frequency(byte[] bytes, float[] fDire, float[] fFreq, int w, int h)
        {
            //窗口大小
            const int l1 = 32, w1 = 16, l2 = 16, w2 = 8;
            //正弦波峰值点
            int[] peak_pos = new int[l1];
            int peak_cnt;
            float peak_freq;
            float[] xsig = new float[l1];
            //方向
            float dir = 0.0f, cosdir = 0.0f, sindir = 0.0f, maxpeak, minpeak;
            //结果初始化
            float[] freq1 = new float[w * h];
            int x, y, d, k, u, v;
            for (y = l2; y < h - l2; y++)//逐行遍历，除边缘
            {
                for (x = l2; x < w - l2; x++)//逐列遍历，除边缘
                {
                    //当前脊线方向
                    dir = fDire[(y + w2) * w + (x + w2)];
                    cosdir = (float)(-Math.Sin(dir));
                    sindir = (float)Math.Cos(dir);//可能存在精度问题
                    //计算以当前像素为中心l1*w1邻域窗口的幅值序列X[0]-X[l1-1]
                    for (k = 0; k < l1; k++)
                    {
                        xsig[k] = 0.0f;
                        for (d = 0; d < w1; d++)
                        {
                            u = (int)(x + (d - w2) * cosdir + (k - l2) * sindir);
                            v = (int)(y + (d - w2) * sindir - (k - l2) * cosdir);
                            //边界点处理
                            if (u < 0) { u = 0; }
                            else if (u > w - 1) { u = w - 1; }
                            if (v < 0) { v = 0; }
                            else if (v > h - 1) { v = h - 1; }
                            xsig[k] += (float)bytes[u + v * w];
                        }
                        xsig[k] /= (float)(w1 * 1.0);
                    }
                    //确定幅值序列变化范围
                    maxpeak = minpeak = xsig[0];
                    for (k = 0; k < l1; k++)
                    {
                        if (minpeak > xsig[k]) { minpeak = xsig[k]; }
                        if (maxpeak < xsig[k]) { maxpeak = xsig[k]; }
                    }
                    //确定峰值点位置
                    peak_cnt = 0;
                    if ((maxpeak - minpeak) > 64)
                    {
                        for (k = 1; k < l1 - 1; k++)
                        {
                            if (xsig[k - 1] < xsig[k] && xsig[k] >= xsig[k + 1])
                            {
                                peak_pos[peak_cnt++] = k;
                            }
                        }
                    }
                    //计算峰值点间平均距离
                    peak_freq = 0.0f;
                    if (peak_cnt >= 2)
                    {
                        for (k = 0; k < peak_cnt - 1; k++)
                        {
                            peak_freq += (peak_pos[k + 1] - peak_pos[k]);
                        }
                        peak_freq /= (float)((peak_cnt - 1) * 1.0);
                    }
                    //计算当前像素的频率
                    if (peak_freq < 3.0f || peak_freq > 25.0f)
                    {
                        freq1[x + y * w] = 0.0f;
                    }
                    else
                    {
                        freq1[x + y * w] = (float)(1.0 / peak_freq);
                    }
                }
            }
            //对频率进行均值滤波
            for (y = l2; y < h - l2; y++)//逐行遍历，除边缘
            {
                for (x = l2; x < w - l2; x++)//逐列遍历，除边缘
                {
                    k = x + y * w;//当前像素位置
                    peak_freq = 0.0f;
                    //使用以当前像素为中心的5*5邻域窗口进行均值滤波
                    for (v = -2; v <= 2; v++)
                    {
                        for (u = -2; u < 2; u++)
                        {
                            peak_freq += freq1[(x + u) + (y + v) * w];//求频率累加和
                        }
                    }
                    fFreq[k] = (float)(peak_freq / 25 * 1.0);
                }
            }
        }

        /// <summary>
        /// 掩码计算
        /// </summary>
        public static Bitmap GetMask(Bitmap input, float[] fDir, float[] fFreqGet, out byte[] ucMask)
        {
            byte[] bytes = BmpToBytes(input);
            int w = input.Width;
            int h = input.Height;
            ucMask = new byte[w * h];
            getmask(bytes, w, h, fDir, fFreqGet, out ucMask);
            Bitmap output = BuildGrayBitmap(ucMask,input.Width, input.Height);
            return output;
        }

        /// <summary>
        /// 掩码计算核心算法
        /// </summary>
        public static void getmask(byte[] bytes, int w, int h, float[] fDir, float[] fFreq, out byte[] ucMask)
        {
            ucMask = new byte[bytes.Length];
            //阈值分割
            float freqMin = (float)(1.0 / 25.0);
            float freqMax = (float)(1.0 / 3.0);
            int x, y, k;
            int pos, posout;
            for (int i = 0; i < ucMask.Length; i++)
            {
                ucMask[i] = 0;
            }
            for (y = 0; y < h; y++)
            {
                for (x = 0; x < w; x++)
                {
                    pos = x + y * w;
                    posout = x + y * w;
                    ucMask[posout] = 0;
                    if (fFreq[pos] >= freqMin && fFreq[pos] <= freqMax)
                    {
                        ucMask[posout] = 255;
                    }
                }
            }
            //第二步：填充孔洞
            for (k = 0; k < 4; k++)//重复膨胀多次，可自定
            {
                //标记前景点
                for (y = 1; y < h - 1; y++)
                {
                    for (x = 1; x < w - 1; x++)
                    {
                        //前景点的上下左右四个相邻点都标记为前景点
                        if (ucMask[x + y * w] == 0xFF)//前景点
                        {
                            ucMask[x - 1 + y * w] |= 0x80;
                            ucMask[x + 1 + y * w] |= 0x80;
                            ucMask[x + (y - 1) * w] |= 0x80;
                            ucMask[x + (y + 1) * w] |= 0x80;
                        }
                    }
                }
                //判断和设置前景点
                for (y = 1; y < h - 1; y++)
                {
                    for (x = 1; x < w - 1; x++)
                    {
                        //将标记前景点的的像素都设为前景点
                        if (ucMask[x + y * w] != 0x0)//前景点
                        {
                            ucMask[x + y * w] = 0xFF;//设置为前景点
                        }
                    }
                }
            }
            //第三步：去除边缘点和孤立点
            for (k = 0; k < 12; k++)//重复腐蚀多次，可自定
            {
                //标记背景点
                for (y = 1; y < h - 1; y++)
                {
                    for (x = 1; x < w - 1; x++)
                    {
                        //前景点的上下左右四个相邻点都标记为潜在背景点
                        if (ucMask[x + y * w] == 0x0)//前景点
                        {
                            ucMask[x - 1 + y * w] &= 0x80;
                            ucMask[x + 1 + y * w] &= 0x80;
                            ucMask[x + (y - 1) * w] &= 0x80;
                            ucMask[x + (y + 1) * w] &= 0x80;
                        }
                    }
                }
                //判断和设置背景点
                for (y = 1; y < h - 1; y++)
                {
                    for (x = 1; x < w - 1; x++)
                    {
                        //前景点的上下左右四个相邻点都标记为前景点
                        if (ucMask[x + y * w] != 0xFF)//前景点
                        {
                            ucMask[x + y * w] |= 0x0;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gabor增强
        /// </summary>
        public static Bitmap GaborEnhance(Bitmap input, float[] fDir, float[] fFreqGet, byte[] ucMask, out byte[] ucImgEnhance)
        {
            byte[] bytes = BmpToBytes(input);
            int w = input.Width;
            int h = input.Height;
            ucImgEnhance = new byte[w * h];
            gaborenhance(bytes, fDir, fFreqGet, ucMask, w, h, out ucImgEnhance);
            Bitmap output = BuildGrayBitmap(bytes, w, h);
            return output;
        }

        /// <summary>
        /// Gabor增强核心算法
        /// </summary>
        public static void gaborenhance(byte[] bytes, float[] fDir, float[] fFreq, byte[] ucMask, int w, int h, out byte[] ucImgEnhanced)
        {
            const float PI = 3.141592654f;
            ucImgEnhanced = new byte[w * h];
            int i, j, u, v;
            int wg2 = 5;//11*11的Gabor滤波器，半边长5
            float sum, f, g;
            float x2, y2;
            float dx2 = (float)(1.0 / (4.0 * 4.0));
            float dy2 = (float)(1.0 / (4.0 * 4.0));
            //Gabor滤波
            for (j = wg2; j < h - wg2; j++)//逐行遍历，除边缘
            {
                for (i = wg2; i < w - wg2; i++)//逐列遍历，除边缘
                {
                    int index = i + j * w;
                    //跳过背景点
                    if (ucMask[index] == 0)//掩码为0表示背景点
                    {
                        continue;
                    }
                    //获取当前像素的方向hepinlv
                    g = fDir[index];
                    f = fFreq[index];
                    g += PI / 2;
                    //对当前像素进行Gabor滤波
                    sum = 0.0f;
                    for (v = -wg2; v <= wg2; v++)
                    {
                        for (u = -wg2; u <= wg2; u++)
                        {
                            x2 = (float)(-u * Math.Sin(g) + v * Math.Cos(g));
                            y2 = (float)(u * Math.Cos(g) + v * Math.Sin(g));
                            sum += (float)(Math.Exp(-0.5 * (x2 * x2 * dx2 + y2 * y2 * dy2)) * Math.Cos(2 * PI * x2 * f) * bytes[(i - u) + (j - v) * w]);
                        }
                    }
                    //边界值处理
                    if (sum > 255.0f)
                    {
                        sum = 255.0f;
                    }
                    if (sum <= 0.0f)
                    {
                        sum = 0.0f;
                    }
                    //得到当前像素的滤波结果
                    ucImgEnhanced[i + j * w] = (byte)sum;
                }
            }
        }

        /// <summary>
        /// 二值化
        /// </summary>
        public static Bitmap BinaryImg(Bitmap input, out byte[] ucBinImage)
        {
            byte Threshold = 128;
            byte[] bytes = BmpToBytes(input);
            int w = input.Width;
            int h = input.Height;
            ucBinImage = new byte[w * h];
            for(int i = 0;i<bytes.Length;i++)
            {
                if (bytes[i] <= Threshold)
                {
                    ucBinImage[i] = 0;
                }
                else
                {
                    ucBinImage[i] = 1;
                }
            }
            byte[] bytesCopy = new byte[w * h];
            for(int i = 0;i<bytesCopy.Length;i++)
            {
                if (ucBinImage[i] == 0)
                {
                    bytesCopy[i] = 0;
                }
                else
                {
                    bytesCopy[i] = 255;
                }
            }
            Bitmap output = BuildGrayBitmap(bytesCopy, w, h);
            return output;
        }

        /// <summary>
        /// 细化
        /// </summary>
        public static Bitmap Thinning(Bitmap input, byte[] ucBinImage, out byte[] ucThinnedImage, int count)
        {
            int w = input.Width;
            int h = input.Height;
            thining(w, h, count, ucBinImage, out  ucThinnedImage);
            byte[] bytesCopy = new byte[w * h];
            for(int i = 0; i<bytesCopy.Length;i++)
            {
                if (ucThinnedImage[i] == 0)
                {
                    bytesCopy[i] = 0;
                }
                else
                {
                    bytesCopy[i] = 255;
                }
            }
            Bitmap output = BuildGrayBitmap(bytesCopy, w, h);
            return output;
        }

        /// <summary>
        /// 细化核心代码
        /// </summary>
        private static void thining(int w, int h, int count, byte[] ucBinImage, out byte[] ucThinnedImage)
        {
            byte x1, x2, x3, x4, x5, x6, x7, x8, xp;
            byte g1, g2, g3, g4;
            byte b1 = 0, b2 = 0, b3 = 0, b4 = 0;
            byte np1, np2, npm;
            int pUp, pDown, pImg;
            int iDlePoints = 0;
            ucThinnedImage = new byte[w * h];
            Array.Copy(ucBinImage, ucThinnedImage, w * h);
            for (int it = 0; it < count; it++)
            {
                iDlePoints = 0;//初始化本次迭代删除点数
                //本次迭代的第一次遍历
                for (int i = 1; i < h - 1; i++)//逐行遍历
                {
                    pUp = (i - 1) * w;
                    pImg = i * w;
                    pDown = (i + 1) * w;
                    for (int j = 1; j < w - 1; j++)//=逐列遍历
                    {
                        pUp++;
                        pImg++;
                        pDown++;
                        if (ucThinnedImage[pImg] == 0)
                        {
                            continue;
                        }
                        //获取3*3邻域窗口内9个像素的灰度值
                        x6 = ucThinnedImage[pUp - 1];
                        x5 = ucThinnedImage[pImg - 1];
                        x4 = ucThinnedImage[pDown - 1];
                        x7 = ucThinnedImage[pUp];
                        xp = ucThinnedImage[pImg];
                        x3 = ucThinnedImage[pDown];
                        x8 = ucThinnedImage[pUp + 1];
                        x1 = ucThinnedImage[pImg + 1];
                        x2 = ucThinnedImage[pDown + 1];
                        //判断条件G1
                        if (x1 == 0 && (x2 == 1 || x3 == 1)) b1 = 1;
                        else b1 = 0;
                        if (x3 == 0 && (x4 == 1 || x5 == 1)) b2 = 1;
                        else b2 = 0;
                        if (x5 == 0 && (x6 == 1 || x7 == 1)) b3 = 1;
                        else b3 = 0;
                        if (x7 == 0 && (x8 == 1 || x1 == 1)) b4 = 1;
                        else b4 = 0;
                        if (b1 + b2 + b3 + b4 != 0) g1 = 1;
                        else g1 = 0;
                        //判断条件g2
                        if (x1 + x2 != 0) np1 = 1;
                        else np1 = 0;
                        if (x3 + x4 != 0) np1++;
                        if (x5 + x6 != 0) np1++;
                        if (x7 + x8 != 0) np1++;
                        if (x2 + x3 != 0) np2 = 1;
                        else np2 = 0;
                        if (x4 + x5 != 0) np2++;
                        if (x7 + x6 != 0) np2++;
                        if (x1 + x8 != 0) np2++;
                        npm = np1 > np2 ? np2 : np1;
                        if (npm >= 2 && npm <= 3) g2 = 1;
                        else g2 = 0;
                        //判断g3，g4
                        int temp;
                        if (x1 != 0 && (x2 != 0 || x3 != 0 || x8 != 1)) temp = 1;
                        else temp = 0;
                        if (temp == 0) g3 = 1;
                        else g3 = 0;
                        int temp1;
                        if (x5 != 0 && (x6 != 0 || x7 != 0 || x4 != 1)) temp1 = 1;
                        else temp1 = 0;
                        if (temp1 == 0) g4 = 1;
                        else g4 = 0;
                        //组合判断
                        if (g1 != 0 && g2 != 0 && g3 != 0)
                        {
                            ucThinnedImage[w * i + j] = 0;
                            ++iDlePoints;
                        }
                    }
                }
                //结果同步
                Array.Copy(ucThinnedImage, ucBinImage, w * h);
                //迭代第二次遍历
                for (int i = 1; i < h - 1; i++)
                {
                    pUp = (i - 1) * w;
                    pImg = i * w;
                    pDown = (i + 1) * w;
                    for (int j = 1; j < w - 1; j++)
                    {
                        pUp++;
                        pImg++;
                        pDown++;
                        if (ucThinnedImage[pImg] == 0)
                        {
                            continue;
                        }
                        //获取3*3邻域窗口内9个像素的灰度值
                        x6 = ucThinnedImage[pUp - 1];
                        x5 = ucThinnedImage[pImg - 1];
                        x4 = ucThinnedImage[pDown - 1];
                        x7 = ucThinnedImage[pUp];
                        xp = ucThinnedImage[pImg];
                        x3 = ucThinnedImage[pDown];
                        x8 = ucThinnedImage[pUp + 1];
                        x1 = ucThinnedImage[pImg + 1];
                        x2 = ucThinnedImage[pDown + 1];
                        //判断条件G1
                        if (x1 == 0 && (x2 == 1 || x3 == 1)) b1 = 1;
                        else b1 = 0;
                        if (x3 == 0 && (x4 == 1 || x5 == 1)) b2 = 1;
                        else b2 = 0;
                        if (x5 == 0 && (x6 == 1 || x7 == 1)) b3 = 1;
                        else b3 = 0;
                        if (x7 == 0 && (x8 == 1 || x1 == 1)) b4 = 1;
                        else b4 = 0;
                        if (b1 + b2 + b3 + b4 != 0) g1 = 1;
                        else g1 = 0;
                        //判断条件g2
                        if (x1 + x2 != 0) np1 = 1;
                        else np1 = 0;
                        if (x3 + x4 != 0) np1++;
                        if (x5 + x6 != 0) np1++;
                        if (x7 + x8 != 0) np1++;
                        if (x2 + x3 != 0) np2 = 1;
                        else np2 = 0;
                        if (x4 + x5 != 0) np2++;
                        if (x7 + x6 != 0) np2++;
                        if (x1 + x8 != 0) np2++;
                        npm = np1 > np2 ? np2 : np1;
                        if (npm >= 2 && npm <= 3) g2 = 1;
                        else g2 = 0;
                        //判断g3，g4
                        int temp;
                        if (x1 != 0 && (x2 != 0 || x3 != 0 || x8 != 1)) temp = 1;
                        else temp = 0;
                        if (temp == 0) g3 = 1;
                        else g3 = 0;
                        int temp1;
                        if (x5 != 0 && (x6 != 0 || x7 != 0 || x4 != 1)) temp1 = 1;
                        else temp1 = 0;
                        if (temp1 == 0) g4 = 1;
                        else g4 = 0;
                        //组合判断
                        if (g1 != 0 && g2 != 0 && g4 != 0)
                        {
                            ucThinnedImage[w * i + j] = 0;
                            ++iDlePoints;
                        }
                    }
                }
                //结果同步
                Array.Copy(ucThinnedImage, ucBinImage, w * h);
                //若本次迭代无点可删除，停止迭代
                if (iDlePoints == 0)//
                {
                    break;
                }
            }
            //清除边缘区段
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < w; j++)
                {
                    if (i < 16)//上边缘
                        ucThinnedImage[i * w + j] = 0;
                    else if (i >= h - 16)//下边缘
                        ucThinnedImage[i * w + j] = 0;
                    else if (j < 16)//左边缘
                        ucThinnedImage[i * w + j] = 0;
                    else if (j >= w - 16)//右边缘
                        ucThinnedImage[i * w + j] = 0;
                }
            }
        }

        /// <summary>
        /// 特征提取
        /// </summary>
        public static Bitmap Extract(Bitmap input, byte[] ucThinImg, out byte[] ucMinuImg, out int count)
        {
            int w = input.Width;
            int h = input.Height;
            extract(w, h, ucThinImg, out ucMinuImg, out count);
            byte[] bytesCopy = new byte[w * h];
            for (int i = 0; i < bytesCopy.Length; i++)
            {
                if (ucMinuImg[i] == 0)
                {
                    bytesCopy[i] = 0;
                }
                else
                {
                    bytesCopy[i] = 255;
                }
            }
            Bitmap output = BuildGrayBitmap(bytesCopy, w, h);
            return output;
        }

        /// <summary>
        /// 特征提取核心代码
        /// </summary>
        private static void extract(int w, int h, byte[] ucThinImg, out byte[] ucMinuImg, out int count)
        {
            int pUp, pDown, pImg;
            byte x1, x2, x3, x4, x5, x6, x7, x8;
            int nc;//八邻点中黑点数量
            ucMinuImg = new byte[w * h];
            count = 0;
            //遍历源图提取特征
            for (int i = 1; i < h - 1; i++)//逐行遍历
            {
                pUp = (i - 1) * w;
                pImg = i * w;
                pDown = (i + 1) * w;
                for (int j = 1; j < w - 1; j++)//逐列遍历
                {
                    pUp++;
                    pImg++;
                    pDown++;
                    if (ucThinImg[pImg] == 0)
                    {
                        continue;
                    }
                    //获取3*3邻域窗口内9个像素的灰度值
                    x6 = ucThinImg[pUp - 1];
                    x5 = ucThinImg[pImg - 1];
                    x4 = ucThinImg[pDown - 1];
                    x7 = ucThinImg[pUp];
                    x3 = ucThinImg[pDown];
                    x8 = ucThinImg[pUp + 1];
                    x1 = ucThinImg[pImg + 1];
                    x2 = ucThinImg[pDown + 1];
                    //统计八邻点黑点数
                    nc = (byte)(x1 + x2 + x3 + x4 + x5 + x6 + x7 + x8);
                    //特征点判断
                    if (nc == 1)//端点
                    {
                        ucMinuImg[i * w + j] = 1;
                        ++count;//特征点数量+1
                    }
                    else if (nc == 3)
                    {
                        ucMinuImg[i * w + j] = 3;
                        ++count;//特征点数量+1
                    }
                }
            }
        }

        /// <summary>
        /// 特征过滤
        /// </summary>
        public static Bitmap MinuFilter(Bitmap input, byte[] thinData, byte[] minuData, out MINUTIAE[] minutiaes, ref int minuCount)
        {
            int w = input.Width;
            int h = input.Height;
            MinuFilter(w, h, thinData, minuData, out minutiaes, ref minuCount);
            byte[] bytesCopy = new byte[w * h];
            for (int i = 0; i < bytesCopy.Length; i++)
            {
                bytesCopy[i] = 0;
            }
            for (int i = 0; i < minuCount; i++)
            {
                bytesCopy[(minutiaes[i].y - 1) * w + (minutiaes[i].x - 1)] = 255;
            }
            Bitmap output = BuildGrayBitmap(bytesCopy, w, h);
            return output;
        }

        /// <summary>
        /// 特征过滤核心算法
        /// </summary>
        public static void MinuFilter(int w, int h, byte[] thinData, byte[] minuData, out MINUTIAE[] minutiaes, ref int minuCount)
        {
            //1.计算细化图中各点方向
            float[] dir = new float[w * h];
            minutiaes = new MINUTIAE[w * h];
            dir = imgdirection(thinData, w, h);
            //2.从特征图中提取特征点数据
            int pImg;
            byte val;
            int temp = 0;
            for (int i = 1; i < h - 1; i++)
            {
                pImg = i * w;
                for (int j = 1; j < w - 1; j++)
                {
                    //获取特征图数据
                    pImg++;
                    val = minuData[pImg];
                    //提取特征点数据
                    if (val > 0)
                    {
                        minutiaes[temp].x = j + 1;
                        minutiaes[temp].y = i + 1;
                        minutiaes[temp].theta = dir[i * w + j];//脊线方向
                        minutiaes[temp].type = (int)val;
                        temp++;
                    }

                }
            }
            Array.Copy(minutiaes, minutiaes, temp);
            //第三步：去除边缘特征点
            minuCount = CutEdge(minutiaes, minuCount, thinData, w, h);
            //第四步：去除毛刺、小孔，间断等伪特征点
            int[] pFlag = new int[minuCount];//0:保留；1：删除；
            //遍历所有特征点
            int x1, x2, y1, y2, type1, type2;
            for (int i = 0; i < minuCount; i++)//特征点1遍历
            {
                //获取特征点1的数据
                x1 = minutiaes[i].x;
                y1 = minutiaes[i].y;
                type1 = minutiaes[i].type;//特征点类型
                for (int j = i + 1; j < minuCount; j++)//遍历特征点2
                {
                    //跳过已删特征点
                    if (pFlag[j] == 1)
                    {
                        continue;
                    }
                    //获取特征点2的数据
                    x2 = minutiaes[j].x;
                    y2 = minutiaes[j].y;
                    type2 = minutiaes[j].type;//特征点类型
                    //计算两点间间距
                    int r = (int)(Math.Sqrt((float)((y1 - y2) * (y1 - y2) + (x1 - x2) * (x1 - x2))));
                    //删除间距过小的特征点
                    if (r <= 4)//距离小于5删除
                    {
                        if (type1 == type2)//二者类型相同
                        {
                            if (type1 == 1)
                            {
                                pFlag[i] = pFlag[j] = 1;//同时删掉两点
                            }
                            else//两点均为分叉点，则认定为小孔
                            {
                                pFlag[j] = 1;//只删除点2
                            }
                        }
                        else if (type1 == 1)//1为端点，2为分叉点，则1为毛刺
                        {
                            pFlag[i] = 1;
                        }
                        else//2为毛刺
                        {
                            pFlag[j] = 1;
                        }
                    }
                }

            }
            //重组特征点结构数组
            int newCount = 0;//有效特征点数量
            for (int i = 0; i < minuCount; i++)
            {
                if (pFlag[i] == 0)
                {
                    minutiaes[newCount] = minutiaes[i];
                    newCount++;
                }
            }
            minuCount = newCount;//保存有效特征点数量
            Array.Copy(minutiaes, minutiaes, minuCount);
        }

        /// <summary>
        /// 去除边缘特征点
        /// </summary>
        private static int CutEdge(MINUTIAE[] minutiaes, int count, byte[] thinData, int w, int h)
        {
            //定义变量
            int minuCount = count;
            int x, y, type;
            bool del;
            //初始化标记数组
            int[] pFlag = new int[minuCount];
            //遍历所有特征点
            for (int i = 0; i < minuCount; i++)
            {
                //获取当前特征点信息
                y = minutiaes[i].y - 1;
                x = minutiaes[i].x - 1;
                type = minutiaes[i].type;
                //将当前特征点删除标记初始化为true
                del = true;
                //根据当前特征点位置判断是否为边远特征点
                if (x < w / 2)//位于左半图
                {
                    if (Math.Abs(w / 2 - x) > Math.Abs(h / 2 - 2))//位于左半图左侧
                    {
                        //在特征图中查找当前特征点同一行左侧是否还有其它特征点
                        while (--x >= 0)//逐一左移查找
                        {
                            //如果在左侧存在其他特征点，则说明当前特征点不是边缘特征点，无须删除
                            if (thinData[x + y * w] > 0)
                            {
                                del = false;
                                break;
                            }
                        }
                    }
                    else//左半图右侧
                    {
                        if (y > h / 2)//右下侧
                        {
                            while (++y < h)
                            {
                                if (thinData[x + y * w] > 0)
                                {
                                    del = false;
                                    break;
                                }
                            }
                        }
                        else//位于右上侧
                        {
                            while (--y >= 0)
                            {
                                if (thinData[x + y * w] > 0)
                                {
                                    del = false;
                                    break;
                                }
                            }
                        }
                    }
                }
                //如果位于图像右半图
                else
                {
                    if (Math.Abs(w / 2 - x) > Math.Abs(h / 2 - y))//右侧
                    {
                        while (++x < w)
                        {
                            if (thinData[x + y * w] > 0)
                            {
                                del = false;
                                break;
                            }
                        }
                    }
                    else//左侧
                    {
                        if (y > h / 2)//左下侧
                        {
                            while (++y < h)
                            {
                                if (thinData[x + y * w] > 0)
                                {
                                    del = false;
                                    break;
                                }
                            }
                        }
                        else//左上侧
                        {
                            while (--y >= 0)
                            {
                                if (thinData[x + y * w] > 0)
                                {
                                    del = false;
                                    break;
                                }
                            }
                        }
                    }
                }
                //如果当前特征点是边缘特征点，则予以删除
                if (del)
                {
                    pFlag[i] = 1;
                    continue;
                }
            }
            //重组特征点结构数组
            int newCount = 0;
            for (int i = 0; i < minuCount; i++)
            {
                if (pFlag[i] == 0)
                {
                    minutiaes[newCount] = minutiaes[i];
                    newCount++;
                }
            }
            Array.Copy(minutiaes, minutiaes, newCount);//此处可能存在问题
            pFlag = null;
            return newCount;
        }

        /// <summary>
        /// 特征匹配算法
        /// </summary>
        // TODO: 该算法存在问题
        public static bool CompareMinutiaeArrays(MINUTIAE[] minutiae1, MINUTIAE[] minutiae2, double similarityThreshold = 0.7)
        {
            // 如果两个数组为空或长度差异过大，直接返回 false
            if (minutiae1 == null || minutiae2 == null)
            {
                return false;
            }
            if (Math.Abs(minutiae1.Length - minutiae2.Length) > minutiae1.Length * 0.3) // 允许一定数量差异
            {
                return false;
            }
            // 提取特征点的哈希表，方便快速查找
            Dictionary<(int x, int y), MINUTIAE> minutiaeMap1 = CreateMinutiaeMap(minutiae1);
            Dictionary<(int x, int y), MINUTIAE> minutiaeMap2 = CreateMinutiaeMap(minutiae2);
            int matchingMinutiae = 0;
            // 遍历第一个数组中的每个特征点
            foreach (MINUTIAE m1 in minutiae1)
            {
                // 在第二个数组中查找匹配的特征点
                if (minutiaeMap2.TryGetValue((m1.x, m1.y), out MINUTIAE m2))
                {
                    // 比较特征点的类型和脊线方向
                    if (m1.type == m2.type && Math.Abs(m1.theta - m2.theta) < 0.2) // 方向差异小于 0.2 弧度
                    {
                        // 比较相邻点
                        if (CompareNeighbours(m1.neibors, m2.neibors))
                        {
                            matchingMinutiae++;
                        }
                    }
                }
            }
            // 计算相似度
            double similarity = (double)matchingMinutiae / Math.Max(minutiae1.Length, minutiae2.Length);
            return similarity >= similarityThreshold;
        }

        private static Dictionary<(int x, int y), MINUTIAE> CreateMinutiaeMap(MINUTIAE[] minutiae)
        {
            Dictionary<(int x, int y), MINUTIAE> map = new Dictionary<(int x, int y), MINUTIAE>();
            foreach (MINUTIAE m in minutiae)
            {
                map[(m.x, m.y)] = m;
            }
            return map;
        }

        private static bool CompareNeighbours(NEIGHBOUR[] neibors1, NEIGHBOUR[] neibors2)
        {
            if (neibors1 == null || neibors2 == null)
            {
                return false;
            }
            if (neibors1.Length != neibors2.Length)
            {
                return false;
            }
            // 比较相邻点的特征
            for (int i = 0; i < neibors1.Length; i++)
            {
                NEIGHBOUR n1 = neibors1[i];
                NEIGHBOUR n2 = neibors2[i];

                if (n1.type != n2.type || Math.Abs(n1.Theta - n2.Theta) > 0.2 ||
                    Math.Abs(n1.Theta2Ridge - n2.Theta2Ridge) > 0.2 ||
                    Math.Abs(n1.ThetaThisNibor - n2.ThetaThisNibor) > 0.2 ||
                    Math.Abs(n1.distance - n2.distance) > 5)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
