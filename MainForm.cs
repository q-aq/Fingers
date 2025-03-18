using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
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
        public MainForm()
        {
            InitializeComponent();
            this.ButtonLogout.TabStop = false;
            this.ButtonStart.TabIndex = 0;
            this.ButtonStart.TabStop = true;
            ControlButton(groupBox1);
            ControlButton(groupBox3);
            this.ButtonGather.Enabled = false;
            this.ButtonStop.Enabled = false;
            finger = new zkf();
            db = new Sql();
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
                        pictureBox1.Image = image;
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
                Bitmap bitmap = new Bitmap(pictureBox1.Image);
                ImageHelper.SaveBitmapToFile(bitmap, filePath,ImageFormat.Bmp);
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
                pictureBox1.Image = bmp;
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

        private void ButtonData_Click(object sender, EventArgs e)
        {
            if(db.Login())
            {
                Form da = new FingerData(db);
                da.Show();
            }
        }
    }
}
