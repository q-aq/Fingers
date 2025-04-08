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
            // 验证输入为 8 位灰度图像
            List<int> bitmapInfo = GetBitmapInfo(input);
            int width = input.Width;
            int height = input.Height;
            fFitDirc = new float[width * height];
            if (bitmapInfo[2] != 8 || input.PixelFormat != PixelFormat.Format8bppIndexed)
                return null;
            // 将位图转换为字节数组
            byte[] bytes = BmpToBytes(input);
            // 计算原始方向场
            float[] fDirc = CalculateDirectionField(bytes, width, height);
            // 低通滤波平滑方向场
            fFitDirc = DirectionLowPassFilter(fDirc, width, height);
            // 生成可视化灰度位图（方向值映射到0-255）
            byte[] resultBytes = new byte[width * height];
            for (int i = 0; i < fFitDirc.Length; i++)
            {
                resultBytes[i] = (byte)((fFitDirc[i] + Math.PI / 2) * 127 / Math.PI); // 方向映射到[0,255]
            }

            return BuildGrayBitmap(resultBytes, width, height);
        }

        /// <summary>
        /// 方向场计算核心算法
        /// </summary>
        private static float[] CalculateDirectionField(byte[] bytes, int w, int h)
        {
            float[] fDirc = new float[w * h];
            const int WindowR = 7; // 窗口半径
            int windowSize = 2 * WindowR + 1;
            for (int y = WindowR; y < h - WindowR; y++)
            {
                for (int x = WindowR; x < w - WindowR; x++)
                {
                    double fx = 0, fy = 0;
                    // 遍历窗口计算梯度统计量
                    for (int j = -WindowR; j <= WindowR; j++)
                    {
                        for (int i = -WindowR; i <= WindowR; i++)
                        {
                            int currentY = y + j;
                            int currentX = x + i;
                            // 确保当前坐标在图像范围内
                            if (currentY < 0 || currentY >= h || currentX < 0 || currentX >= w)
                            {
                                continue; // 跳过超出范围的坐标
                            }
                            int index = currentY * w + currentX;
                            int indexLeft = currentY * w + Math.Max(0, currentX - 1); // 确保不超出左边界
                            int indexTop = Math.Max(0, currentY - 1) * w + currentX; // 确保不超出上边界
                            // 计算梯度 (dx, dy)
                            int dx = bytes[index] - bytes[indexLeft];
                            int dy = bytes[index] - bytes[indexTop];
                            fx += 2 * dx * dy;  // 2 * Σ(dx*dy)
                            fy += dx * dx - dy * dy; // Σ(dx² - dy²)
                        }
                    }
                    // 计算方向角度 (0.5 * arctan(fx/fy))
                    fDirc[y * w + x] = (float)(0.5 * Math.Atan2(fx, fy));
                }
            }
            return fDirc;
        }

        /// <summary>
        /// 方向场低通滤波
        /// </summary>
        private static float[] DirectionLowPassFilter(float[] fDirc, int w, int h)
        {
            float[] fFitDirc = new float[w * h];
            const int filterRadius = 2;
            int filterSize = 2 * filterRadius + 1;
            float[,] filter = CreateGaussianFilter(filterSize, 1.0f);
            // 计算正弦和余弦分量
            float[] sinTheta = new float[w * h];
            float[] cosTheta = new float[w * h];
            for (int i = 0; i < fDirc.Length; i++)
            {
                sinTheta[i] = (float)Math.Sin(2 * fDirc[i]); // 使用双倍角度处理方向周期性
                cosTheta[i] = (float)Math.Cos(2 * fDirc[i]);
            }
            // 应用高斯滤波
            for (int y = filterRadius; y < h - filterRadius; y++)
            {
                for (int x = filterRadius; x < w - filterRadius; x++)
                {
                    float sumSin = 0, sumCos = 0;
                    for (int j = -filterRadius; j <= filterRadius; j++)
                    {
                        for (int i = -filterRadius; i <= filterRadius; i++)
                        {
                            int index = (y + j) * w + (x + i);
                            float weight = filter[j + filterRadius, i + filterRadius];
                            sumSin += weight * sinTheta[index];
                            sumCos += weight * cosTheta[index];
                        }
                    }
                    // 计算平均角度
                    fFitDirc[y * w + x] = (float)(0.5 * Math.Atan2(sumSin, sumCos));
                }
            }
            return fFitDirc;
        }

        /// <summary>
        /// 生成高斯滤波核
        /// </summary>
        private static float[,] CreateGaussianFilter(int size, float sigma)
        {
            float[,] filter = new float[size, size];
            float sum = 0;
            int radius = size / 2;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    float value = (float)Math.Exp(-(x * x + y * y) / (2 * sigma * sigma));
                    filter[y + radius, x + radius] = value;
                    sum += value;
                }
            }
            // 归一化
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    filter[y, x] /= sum;
            return filter;
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
        public static Bitmap Thinning(Bitmap input)
        {
            Bitmap output = null;

            return output;
        }

        /// <summary>
        /// 特征提取
        /// </summary>
        public static Bitmap Extract(Bitmap input)
        {
            Bitmap output = null;

            return output;
        }

        /// <summary>
        /// 特征过滤
        /// </summary>
        public static Bitmap MinuFilter(Bitmap input)
        {
            Bitmap output = null;

            return output;
        }

        /// <summary>
        /// 边缘特征点去除
        /// </summary>
        public static Bitmap CutEdge(Bitmap input)
        {
            Bitmap output = null;

            return output;
        }
    }
}
