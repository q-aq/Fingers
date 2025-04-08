using Google.Protobuf.WellKnownTypes;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fingers
{
    public partial class MainForm : Form
    {
        public zkf finger;
        public Sql db;
        public static bool alive = false;
        public Bitmap CurrentImage = null;//当前处理的图片
        public Bitmap temps = null;
        public float[] DicData;//方向计算结果数据
        public float[] FreData;//频率计算结果数据
        public byte[] MaskData;//掩码计算结果数据
        public byte[] GaborData;//Gabor滤波结果数据
        public byte[] BinData;//二值化结果数据
        public MainForm()
        {
            InitializeComponent();
            this.ButtonLogout.TabStop = false;
            this.ButtonStart.TabIndex = 0;
            this.ButtonStart.TabStop = true;
            //ControlButton(groupBox1);
            ControlButton(groupBox3);
            this.ButtonGather.Enabled = false;
            this.ButtonStop.Enabled = false;
            finger = new zkf();
            db = new Sql();
        }

        public static int DeleteAllFiles(string folderPath, bool recursive = false, bool ignoreErrors = true)
        {
            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine($"目录不存在: {folderPath}");
                return 0;
            }
            int deletedCount = 0;
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            try
            {
                // 获取所有文件路径
                string[] files = Directory.GetFiles(folderPath, "*.*", searchOption);
                foreach (string file in files)
                {
                    try
                    {
                        // 移除只读属性（如果存在）
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                        deletedCount++;
                        Console.WriteLine($"已删除: {file}");
                    }
                    catch (Exception ex) when (ignoreErrors &&
                        (ex is UnauthorizedAccessException ||
                         ex is IOException ||
                         ex is NotSupportedException))
                    {
                        Console.WriteLine($"删除失败 [{ex.GetType().Name}]: {file}\n错误详情: {ex.Message}");
                    }
                }
                Console.WriteLine($"操作完成，共删除 {deletedCount} 个文件");
                return deletedCount;
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine($"目录访问错误: {folderPath}");
                return deletedCount;
            }
        }

        public void SaveMinutiae(Bitmap bmp)//特征入库
        {
            // TODO: 将最后处理得到的特征图存入数据库中，需要重写数据库
        }

        public void BuildNabors()//获取特征点相邻关系
        {

        }

        public double MinuSimilarity()//获取特征匹配相似度
        {
            return 0.0;
        }

        private void RefreshImage()//刷新图片
        {
            while (MainForm.alive)
            {
                Bitmap image = finger.CaptureImage();//获取图片
                if (image != null)
                {
                    this.Invoke(new Action(() =>
                    {
                        if (this.pictureBox1.Image != null)//将图片发送到pictureBox中
                            pictureBox1.Image.Dispose();
                        pictureBox1.Image = (Bitmap)image.Clone();
                        if(CurrentImage != null) CurrentImage.Dispose();
                        CurrentImage = image;
                        List<int> temp = Analysis.GetBitmapInfo(image);
                        textBox2.Text = $"宽度[{temp[0]}],高度[{temp[1]}],深度[{temp[2]}]";
                    }));
                }
                else
                {
                    Thread.Sleep(100);//休眠
                }
            }
        }

        private void ControlButton(Control container,bool temp = false)
        {
            // 遍历GroupBox中的所有控件
            foreach (Control control in container.Controls)
            {
                // 检查控件是否为按钮
                if (control is Button button)
                {
                    button.Enabled = temp; // 设置按钮为不可点击
                }
                else if (control.HasChildren)
                {
                    ControlButton(control, temp);//递归
                }
            }
        }

        private void ButtonLogout_Click(object sender, EventArgs e)//退出按钮
        {
            if (MainForm.alive)
            {
                MainForm.alive = false;
                finger.StopCapture();//停止读取指纹
            }
            this.Close();
        }

        private void ButtonStart_Click(object sender, EventArgs e)//设备启动
        {
            if(finger.StartDevice() == 0)
            {
                //设置按钮为可点击状态
                ControlButton(groupBox1,true);
                ControlButton(groupBox3,true);
                this.ButtonGather.Enabled = true;
                this.ButtonStop.Enabled = true;
                this.ButtonStart.Enabled = false;
                MessageBox.Show($"设备启动完毕");
                //获取图片宽高
                finger.GetImageParameters();
                if(!MainForm.alive)
                {
                    MainForm.alive = true;
                    Thread captureThread = new Thread(RefreshImage);
                    captureThread.Start();
                }
            }
            // TODO: 测试用
            this.ButtonFirst.Enabled = true;
            this.ButtonGather.Enabled=true;
        }

        private void ButtonGather_Click(object sender, EventArgs e)//采集
        {
            if (pictureBox1.Image == null)
            {
                MessageBox.Show("未加载图片");
                return;
            }
            string username = textBox1.Text;
            if(string.IsNullOrEmpty(username))
            {
                MessageBox.Show("姓名不能为空");
                return;
            }
            SaveFileDialog saveFile = new SaveFileDialog();
            saveFile.Title = "保存文件";
            saveFile.InitialDirectory = Application.StartupPath;
            saveFile.Filter = "所有文件 (*.*)|*.*";
            saveFile.DefaultExt = "bmp";
            saveFile.AddExtension = true;
            if(saveFile.ShowDialog() == DialogResult.OK)
            {
                string filePath = saveFile.FileName;
                ImageHelper.SaveBitmapToFile(CurrentImage, filePath,ImageFormat.Bmp);
                //上传指纹库
                string sql = $"insert into fingerprints(username,image_path) values(\"{username}\", \"{filePath.Replace("\\", "\\\\")}\")";  
                object index = db.Exec(sql);
                if(index is int number)
                {
                    Console.WriteLine(number.ToString());
                }
            }
        }
        private void ButtonFirst_Click(object sender, EventArgs e)//载入图像
        {
            DeleteAllFiles("F:\\text.c\\c#\\Fingers\\bin\\cache\\");
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Title = "打开文件";
            openFile.InitialDirectory = Application.StartupPath;
            openFile.Filter = "所有文件 (*.*)|*.*";
            openFile.DefaultExt = "bmp";
            openFile.AddExtension = true;
            if(openFile.ShowDialog() == DialogResult.OK)
            {
                string filePath = openFile.FileName;
                Bitmap bmp = ImageHelper.LoadBitmapFromFile(filePath);
                if (this.pictureBox1.Image != null)//将图片发送到pictureBox中
                    pictureBox1.Image.Dispose();
                pictureBox1.Image = (Bitmap)bmp.Clone();
                if (CurrentImage != null) CurrentImage.Dispose();
                CurrentImage = bmp;
                List<int> temp = Analysis.GetBitmapInfo(bmp);
                string[] l;
                l = filePath.Split('\\');
                textBox2.Text = $"源图[{l[l.Length-1]}],宽度[{temp[0]}],高度[{temp[1]}],深度[{temp[2]}]";
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)//窗口关闭前关闭监听循环
        {
            if (MainForm.alive)
            {
                MainForm.alive = false;
                finger.StopCapture();//停止读取指纹
            }
            e.Cancel = false;
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            DeleteAllFiles("F:\\text.c\\c#\\Fingers\\bin\\cache\\");
        }

        private void ButtonData_Click(object sender, EventArgs e)
        {
            if(db.Login())
            {
                Form da = new FingerData(db);
                da.Show();
            }
        }
        // TODO: 需要解决中间图片输出又变为32位深度的问题

        public void Update(Bitmap res)
        {
            if (pictureBox2.Image != null)
                pictureBox2.Image.Dispose();
            pictureBox2.Image = (Bitmap)CurrentImage.Clone();
            if (pictureBox1.Image != null)
                pictureBox1.Image.Dispose();
            pictureBox1.Image = (Bitmap)res.Clone();
            if (CurrentImage != null) CurrentImage.Dispose();
            CurrentImage = res;
        }
        private void ButtonSecond_Click(object sender, EventArgs e)//中值滤波
        {
            if(pictureBox1.Image == null)
            {
                MessageBox.Show("请载入图像");
                return;
            }
            Bitmap res = Analysis.MidFilter(CurrentImage);
            Update(res);
            ImageHelper.SaveBitmapToFile(res, "F:\\text.c\\c#\\Fingers\\bin\\cache\\step2.bmp",ImageFormat.Bmp);
        }

        private void ButtonThird_Click(object sender, EventArgs e)//均衡化
        {
            if (pictureBox1.Image == null)
            {
                MessageBox.Show("请载入图像");
                return;
            }
            Bitmap res = Analysis.HistoNormalize(CurrentImage);
            Update(res);
            temps = (Bitmap)res.Clone();
            ImageHelper.SaveBitmapToFile(res, "F:\\text.c\\c#\\Fingers\\bin\\cache\\step3.bmp",ImageFormat.Bmp);
        }

        private void ButtonFour_Click(object sender, EventArgs e)//方向计算
        {
            if (pictureBox1.Image == null || temps is null)
            {
                MessageBox.Show("请载入图像");
                return;
            }
            Bitmap res = Analysis.ImgDirection(temps, out DicData);
            Update(res);
            ImageHelper.SaveBitmapToFile(res, "F:\\text.c\\c#\\Fingers\\bin\\cache\\step4.bmp", ImageFormat.Bmp);
        }

        private void ButtonFive_Click(object sender, EventArgs e)//频率计算
        {
            if (pictureBox1.Image == null || temps is null)
            {
                MessageBox.Show("请载入图像");
                return;
            }
            Bitmap res = Analysis.Frequency(temps, DicData, out FreData);
            Update(res);
            ImageHelper.SaveBitmapToFile(res, "F:\\text.c\\c#\\Fingers\\bin\\cache\\step5.bmp", ImageFormat.Bmp);
        }

        private void ButtonSix_Click(object sender, EventArgs e)//掩码计算
        {
            if (pictureBox1.Image == null || temps is null)
            {
                MessageBox.Show("请载入图像");
                return;
            }
            Bitmap res = Analysis.GetMask(temps, DicData, FreData, out MaskData);
            Update(res);
            ImageHelper.SaveBitmapToFile(res, "F:\\text.c\\c#\\Fingers\\bin\\cache\\step6.bmp", ImageFormat.Bmp);
        }

        private void ButtonSeven_Click(object sender, EventArgs e)//Gabor增强
        {
            if (pictureBox1.Image == null || temps is null)
            {
                MessageBox.Show("请载入图像");
                return;
            }
            Bitmap res = Analysis.GaborEnhance(temps, DicData, FreData, MaskData, out GaborData);
            Update(res);
            ImageHelper.SaveBitmapToFile(res, "F:\\text.c\\c#\\Fingers\\bin\\cache\\step7.bmp", ImageFormat.Bmp);
        }

        private void ButtonEight_Click(object sender, EventArgs e)//二值化
        {
            if (pictureBox1.Image == null || temps is null)
            {
                MessageBox.Show("请载入图像");
                return;
            }
            Bitmap res = Analysis.BinaryImg(temps, out BinData);
            Update(res);
            ImageHelper.SaveBitmapToFile(res, "F:\\text.c\\c#\\Fingers\\bin\\cache\\step8.bmp", ImageFormat.Bmp);
        }

        private void ButtonNine_Click(object sender, EventArgs e)//细化
        {

        }

        private void ButtonTen_Click(object sender, EventArgs e)//特征提取
        {

        }

        private void ButtonEleven_Click(object sender, EventArgs e)//特征过滤
        {

        }

        private void ButtonTwelve_Click(object sender, EventArgs e)//特征入库
        {

        }

        private void ButtonThirteen_Click(object sender, EventArgs e)//特征匹配
        {

        }
    }
}
