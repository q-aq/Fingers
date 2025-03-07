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
        }
        // TODO:可以读取图片，但是会卡死，不显示图片
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
            MainForm.alive = false;
            finger.StopCapture();//停止读取指纹
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
        }
    }
}
