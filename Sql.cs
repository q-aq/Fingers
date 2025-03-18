using MySql.Data.MySqlClient;
using Org.BouncyCastle.Asn1.Crmf;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fingers
{
    public class Sql//数据库类
    {
        public MySqlConnection connection;
        public string Loginsql =
                "server=localhost;" +
                "user=root;" +
                "database=Fingers;" +
                "port=3306;" +
                "password=2212080074;";
        public bool state = false;
        public Sql()//构造函数
        {
        }
        public bool Login()
        {
            using (connection = new MySqlConnection(Loginsql))
            {
                try
                {
                    connection.Open();
                    this.state = true;
                    Console.WriteLine("连接到数据库成功！");
                    return true;
                }
                catch (MySqlException ex)
                {
                    this.state = false;
                    MessageBox.Show("无法连接到数据库:" + ex.Message);
                    return false;
                }
            }
        }
        private string GetSqlCommandType(string sql)
        {
            // 去除注释和多余空格的正则表达式
            string cleanedSql = Regex.Replace(sql, @"(--.*)|((/\*)+?[\w\W]+?(\*/)+)", "")
                                    .TrimStart()
                                    .ToLower();
            if (cleanedSql.StartsWith("select")) return "SELECT";
            if (cleanedSql.StartsWith("insert")) return "INSERT";
            if (cleanedSql.StartsWith("update")) return "UPDATE";
            if (cleanedSql.StartsWith("delete")) return "DELETE";
            return null;
        }
        public object Exec(string sql, MySqlParameterCollection parameters = null)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                Console.WriteLine("SQL 语句不能为空");
                return null;
            }
            string sqlType = GetSqlCommandType(sql);
            if (sqlType == null)
            {
                Console.WriteLine("不支持的 SQL 类型");
                return null;
            }
            using(connection = new MySqlConnection(Loginsql))
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                try
                {
                    connection.Open();
                    // 添加参数
                    if (parameters != null)
                    {
                       //command.Parameters.AddRange();
                    }
                    // 确保连接已打开
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }
                    switch (sqlType.ToLower())
                    {
                        case "select":
                            using (MySqlDataReader reader = command.ExecuteReader())
                            {
                                DataTable resultTable = new DataTable();
                                resultTable.Load(reader);
                                return resultTable; // 返回查询结果
                            }
                        case "insert":
                        case "update":
                        case "delete":
                            int affectedRows = command.ExecuteNonQuery();
                            return affectedRows; // 返回受影响行数
                        default:
                            return null;
                    }
                }
                catch (MySqlException ex)
                {
                    Console.WriteLine($"数据库错误: {ex.Number} - {ex.Message}");
                    return null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"系统错误: {ex.Message}");
                    return null;
                }
            }
        }
    }
}
