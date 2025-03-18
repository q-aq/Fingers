using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fingers
{
    public partial class FingerData : Form
    {
        Sql db = null;
        public FingerData(Sql db)
        {
            InitializeComponent();
            this.db = db;
            UpdateInforamtion();
        }
        public void UpdateInforamtion()
        {
            string sql = "select * from fingerprints";
            object data =  db.Exec(sql);
            if(data is DataTable dataTable)
            {
                dataGridView1.DataSource = dataTable;
                dataGridView1.Columns[0].HeaderText = "序号";
                dataGridView1.Columns[1].HeaderText = "姓名";
                dataGridView1.Columns[2].HeaderText = "文件路径";
                dataGridView1.Columns[3].HeaderText = "备注";

                dataGridView1.Columns[0].Width = 100;
                dataGridView1.Columns[1].Width = 150;
                dataGridView1.Columns[2].Width = 250;
                dataGridView1.Columns[3].Width = 100;
            }
            else
            {
                MessageBox.Show("错误");
            }
        }

        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            // 获取当前双击的行索引
            int rowIndex = e.RowIndex;
            DataGridViewRow row = dataGridView1.Rows[rowIndex];
            string filepath = row.Cells[2].Value.ToString();
            Bitmap bmp = ImageHelper.LoadBitmapFromFile(filepath);
            if (bmp != null)
            {
                if (this.pictureBox1.Image != null)//将图片发送到pictureBox中
                    pictureBox1.Image.Dispose();
                pictureBox1.Image = bmp;
            }
            label1.Text = "序号：" + row.Cells[0].Value.ToString();
            label2.Text = "姓名：" + row.Cells[1].Value.ToString();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
