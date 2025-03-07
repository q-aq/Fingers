using System;
using System.Drawing.Imaging;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using libzkfpcsharp;
using System.Threading;
using System.IO;
using static System.Net.Mime.MediaTypeNames;

namespace Fingers
{
    public class zkf : zkfp2//设备控制类
    {
        public IntPtr device;
        private int imageWidth;           // 图像宽度
        private int imageHeight;          // 图像高度

        public zkf()//构造函数
        {
        }

        public int StartDevice()//启动设备
        {
            int index = Init();//初始化
            if (index != 0)
            {
                MessageBox.Show($"初始化错误，错误代码[{index}]", "ERROR");
                Console.WriteLine("初始化失败");
                return -1;
            }
            else
            {
                int num = GetDeviceCount();//获取已连接设备数量
                if (num <= 0)
                {
                    MessageBox.Show($"未检测到设备连接{num}");
                    return -2;
                }
                else
                {
                    device = OpenDevice(0);//返回设备句柄
                    return 0;
                }
            }
        }

        public void GetImageParameters()//获取图片宽高
        {
            byte[] widthBytes = new byte[4];
            int size = 4;
            if (zkfp2.GetParameters(device, 1, widthBytes, ref size) != 0)
            {
                MessageBox.Show("获取图像宽度失败！");
                return;
            }
            imageWidth = BitConverter.ToInt32(widthBytes, 0);

            byte[] heightBytes = new byte[4];
            if (zkfp2.GetParameters(device, 2, heightBytes, ref size) != 0)
            {
                MessageBox.Show("获取图像高度失败！");
                return;
            }
            imageHeight = BitConverter.ToInt32(heightBytes, 0);
        }

        public Bitmap CaptureImage()//获取图片
        {
            if (device == IntPtr.Zero)
                return null;

            byte[] imgBuffer = new byte[imageWidth * imageHeight];
            int result = zkfp2.AcquireFingerprintImage(device, imgBuffer);//接收图片内容到imgbuffer

            if (result != 0)
            {
                return null;
            }
            //将imgbuffer中的内容保存在bitmap中
            Bitmap bitmap = new Bitmap(imageWidth, imageHeight, PixelFormat.Format8bppIndexed);
            BitmapData bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, imageWidth, imageHeight),
                    ImageLockMode.WriteOnly,
                    bitmap.PixelFormat
            );
            Marshal.Copy(imgBuffer, 0, bitmapData.Scan0, imgBuffer.Length);
            bitmap.UnlockBits(bitmapData);
            return bitmap;
        }
        public void StopCapture()//关闭设备
        {
            if (device != IntPtr.Zero)
            {
                zkfp2.CloseDevice(device);
            }
            zkfp2.Terminate();
        }
    }

    public static class ImageHelper
    {
        private static Random random = new Random();//随机数产生器
        public static string CreateFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("文件名不能为空", nameof(fileName));
            if (!File.Exists(fileName))//如果该文件不存在则直接返回文件名
                return fileName;
            string directory = Path.GetDirectoryName(fileName);//文件夹名
            string OldName = Path.GetFileNameWithoutExtension(fileName);//源文件名
            string extension = Path.GetExtension(fileName);//文件拓展名
            int randomSuffix = random.Next(1000, 9999); // 生成一个4位随机数用作文件名的随机后缀
            string NewName = $"{OldName}_{randomSuffix}{extension}";//创建新的文件名
            string NewPath = Path.Combine(directory, NewName);//生成文件路径
            return CreateFileName(NewPath);//递归检查该文件路径是否存在
        }
        public static string SaveBitmapToFile(Bitmap bitmap, string filePath , ImageFormat format)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));//确保目录存在
                filePath = CreateFileName(filePath);//检查文件名
                // 保存图像
                bitmap.Save(filePath, format);//保存文件
                Console.WriteLine($"图像已保存：{filePath}");
            }
            catch (IOException ex)
            {
                throw new IOException("保存图像失败", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException("没有写入权限", ex);
            }
            finally
            {
                // 释放资源（虽然 Bitmap 会被垃圾回收，但显式释放更安全）
                bitmap.Dispose();
            }
            return filePath;
        }
    }
}
