using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Numerics;
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

        public static Bitmap HistoNormalize(Bitmap input, int m0 = 100, double sigma0Squared = 128.0)//直方图均衡化算法
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            // 严格验证输入图像为 8 位深度
            List<int> bitmapInfo = GetBitmapInfo(input);
            if (bitmapInfo[2] != 8)
            {
                return null; // 非 8 位直接退出
            }
            // 确保输入是 8bpp 索引格式
            if (input.PixelFormat != PixelFormat.Format8bppIndexed)
            {
                return null;
            }
            // 创建输出图像并继承调色板（关键步骤）
            Bitmap output = new Bitmap(input.Width, input.Height, PixelFormat.Format8bppIndexed);
            output.Palette = input.Palette; // 直接复制调色板
            // 锁定位图数据
            Rectangle rect = new Rectangle(0, 0, input.Width, input.Height);
            BitmapData inputData = input.LockBits(rect, ImageLockMode.ReadOnly, input.PixelFormat);
            BitmapData outputData = output.LockBits(rect, ImageLockMode.WriteOnly, output.PixelFormat);
            try
            {
                int bytesPerPixel = 1; // 8bpp 固定为 1 字节/像素
                int stride = inputData.Stride;
                int bufferLength = stride * input.Height;
                byte[] pixelBuffer = new byte[bufferLength];
                Marshal.Copy(inputData.Scan0, pixelBuffer, 0, bufferLength);
                // 计算全局统计量（直接操作 8 位数据）
                long sum = 0;
                long sumSquares = 0;
                int totalPixels = input.Width * input.Height;
                for (int i = 0; i < pixelBuffer.Length; i += bytesPerPixel)
                {
                    byte gray = pixelBuffer[i];
                    sum += gray;
                    sumSquares += gray * gray;
                }
                double m = (double)sum / totalPixels;
                double sigmaSquared = (double)sumSquares / totalPixels - m * m;
                sigmaSquared = Math.Max(sigmaSquared, 0);
                double sigma = Math.Sqrt(sigmaSquared);
                double sigma0 = Math.Sqrt(sigma0Squared);
                bool sigmaIsZero = sigma < double.Epsilon;
                // 应用非线性映射（优化为单通道处理）
                for (int i = 0; i < pixelBuffer.Length; i += bytesPerPixel)
                {
                    byte gray = pixelBuffer[i];
                    double newValue;

                    if (sigmaIsZero)
                    {
                        newValue = m0;
                    }
                    else
                    {
                        double diff = gray - m;
                        newValue = (gray > m) ?
                            m0 + Math.Sqrt((sigma0 * sigma0 / sigmaSquared) * diff * diff) :
                            m0 - Math.Sqrt((sigma0 * sigma0 / sigmaSquared) * diff * diff);
                    }
                    // 钳制到 [0,255] 并写入缓冲区
                    byte newGray = (byte)Math.Max(0, Math.Min(255, Math.Round(newValue)));
                    pixelBuffer[i] = newGray;
                }
                // 复制处理后的数据到输出
                Marshal.Copy(pixelBuffer, 0, outputData.Scan0, bufferLength);
            }
            finally
            {
                input.UnlockBits(inputData);
                output.UnlockBits(outputData);
            }
            return output;
        }

        public static Bitmap ImgDirection(Bitmap input)//方向计算
        {
            // 验证输入为 8 位灰度图像
            List<int> bitmapInfo = GetBitmapInfo(input);
            if (bitmapInfo[2] != 8 || input.PixelFormat != PixelFormat.Format8bppIndexed)
                return null;

            int width = input.Width;
            int height = input.Height;

            // 将位图转换为字节数组
            byte[] bytes = BmpToBytes(input);

            // 计算原始方向场
            float[] fDirc = CalculateDirectionField(bytes, width, height);

            // 低通滤波平滑方向场
            float[] fFitDirc = DirectionLowPassFilter(fDirc, width, height);

            // 生成可视化灰度位图（方向值映射到0-255）
            byte[] resultBytes = new byte[width * height];
            for (int i = 0; i < fFitDirc.Length; i++)
            {
                resultBytes[i] = (byte)((fFitDirc[i] + Math.PI / 2) * 127 / Math.PI); // 方向映射到[0,255]
            }

            return BuildGrayBitmap(resultBytes, width, height);
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

    public static Bitmap Frequency(Bitmap input)//频率计算
        {
            Bitmap output = null;

            return output;
        }

        public static Bitmap GetMask(Bitmap input)//掩码计算
        {
            Bitmap output = null;

            return output;
        }

        public static Bitmap GaborEnhance(Bitmap input)//Gabor增强
        {
            Bitmap output = null;

            return output;
        }

        public static Bitmap BinaryImg(Bitmap input)//二值化
        {
            Bitmap output = null;

            return output;
        }

        public static Bitmap Thinning(Bitmap input)//细化
        {
            Bitmap output = null;

            return output;
        }

        public static Bitmap Extract(Bitmap input)//特征提取
        {
            Bitmap output = null;

            return output;
        }

        public static Bitmap MinuFilter(Bitmap input)//特征过滤
        {
            Bitmap output = null;

            return output;
        }

        public static Bitmap CutEdge(Bitmap input)//边缘特征点去除
        {
            Bitmap output = null;

            return output;
        }
    }
}
