﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JISR_V1
{
    public partial class frmAttendance : Form
    {
        private string accessToken;
        private string baseAddress;
        private bool isLoggedIn;
        private bool isFirstCalled;

        private SqlConnection connection = null;
        private SqlDataAdapter adapter = null;
        private DataSet attendanceDataset = null;
        private const string tableName = "atendance";

        public frmAttendance()
        {
            InitializeComponent();

            // Initialize Members
            this.accessToken = Properties.Settings.Default.AccessToken;
            this.baseAddress = Properties.Settings.Default.BaseAddress;
            this.isLoggedIn = false;
            this.isFirstCalled = true;
        }

        private void frmAttendance_Load(object sender, EventArgs e)
        {
            // Check if saved AccessToken is valid
            PingAPI();

            // Initialize Connection and DataSet
            if (connection == null) connection = GetSqlConnection();
            if (attendanceDataset == null) attendanceDataset = new DataSet();
        }

        #region SQL Connection and Query
        private SqlConnection GetSqlConnection()
        {
            if (connection == null)
            {
                return new SqlConnection(Properties.Settings.Default.ConnectionString);
            }
            else
            {
                return connection;
            }
        }

        private string GetTableName(DateTime date, string tbl)
        {
            date = Convert.ToDateTime(date.ToString(new CultureInfo("en-US")));
            int month = date.Month;
            int year = date.Year;
            return String.Format("{0}_{1}_{2}", tbl, month, year);
        }

        private string GetSQL()
        {
            string tbl = GetTableName(DateTime.Now, "DeviceLogs");
            string sql = String.Format("select DeviceLogId,EmployeeCode,LogDate,Direction from {0},Employees where {0}.UserId=Employees.EmployeeId and {1} order by DeviceLogId", tbl, GetWhere(DateTime.Now));
            return sql;
        }

        private string GetWhere(DateTime date)
        {
            DateTime today = Convert.ToDateTime(date.ToString(new CultureInfo("en-US")));
            string onlyIfFirstTime = "";

            if (this.isFirstCalled)
            {
                this.isFirstCalled = false;
            }
            else
            {
                onlyIfFirstTime = String.Format("and  logDate between '{0}' and '{1}'", today.AddMinutes(-1), today);
            }

            return String.Format("cast(logDate as date) = '{0}' {1}", today.ToShortDateString(), onlyIfFirstTime);
        }
        #endregion

        #region Timer Buttons Methods
        private void btnStart_Click(object sender, EventArgs e)
        {
            if (this.isLoggedIn)
            {
                if (!timer.Enabled)
                {
                    lbxNotifications.Items.Add("Timer started.");
                    timer.Enabled = true;
                }
                else
                {
                    MessageBox.Show("Timer is already Started.");
                }
            }
            else
            {
                MessageBox.Show("Please login to start timer.");
            }
        }
        private void btnStop_Click(object sender, EventArgs e)
        {
            if (timer.Enabled)
            {
                lbxNotifications.Items.Add("Timer Stoped");
                timer.Enabled = false;
            }
            else
            {
                MessageBox.Show("Timer is already stopped.");
            }
        }

        #endregion

        #region Authentication
        private void btnLogin_Click(object sender, EventArgs e)
        {
            try
            {
                Login();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error occurs in Login API: \n" + ex.Message);
            }
        }

        private void btnLogout_Click(object sender, EventArgs e)
        {
            try
            {
                Logout();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error occurs in Logout API: \n" + ex.Message);
            }
        }

        private async void PingAPI()
        {
            try
            {
                // ping api
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(this.baseAddress);

                    using (var pingResponse = await client.GetAsync(String.Format("ping?access_token={0}", this.accessToken)))
                    {
                        var pingJsonResult = await pingResponse.Content.ReadAsStringAsync();
                        Dictionary<string, string> pingDic = JsonConvert.DeserializeObject<Dictionary<string, string>>(pingJsonResult);

                        if (pingDic["success"] == "true")
                        {
                            lbxNotifications.Items.Add("Status: You are logged in.");
                            this.isLoggedIn = true;
                        }
                        else
                        {
                            lbxNotifications.Items.Add("Status: You are logged out.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error occurs in Ping API: \n" + ex.Message);
            }
        }

        private async void Login()
        {

            if (!this.isLoggedIn)
            {
                // login
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(this.baseAddress);

                    var serializedLogin = JsonConvert.SerializeObject(
                        new Login
                        {
                            login = Properties.Settings.Default.Login,
                            password = Properties.Settings.Default.Password
                        }
                    );

                    var content = new StringContent(serializedLogin, Encoding.UTF8, "application/json");
                    var result = await client.PostAsync("sessions", content);
                    var loginJsonResult = await result.Content.ReadAsStringAsync();
                    var loginDic = JsonConvert.DeserializeObject<Dictionary<string, string>>(loginJsonResult);

                    if (loginDic["success"] == "true")
                    {
                        this.accessToken = loginDic["access_token"];
                        Properties.Settings.Default.AccessToken = loginDic["access_token"];
                        Properties.Settings.Default.Save();

                        lbxNotifications.Items.Add(loginDic["message"]);
                        this.isLoggedIn = true;
                    }
                    else
                    {
                        lbxNotifications.Items.Add(loginDic["error"]);
                    }
                }
            }
            else
            {
                MessageBox.Show("You already logged in.");
            }

        }

        private async void Logout()
        {
            if (this.isLoggedIn)
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(this.baseAddress);

                    var result = await client.DeleteAsync(String.Format("sessions?access_token={0}", this.accessToken));
                    var logoutJsonResult = await result.Content.ReadAsStringAsync();
                    Dictionary<string, string> logoutDic = JsonConvert.DeserializeObject<Dictionary<string, string>>(logoutJsonResult);

                    if (logoutDic["success"] == "true")
                    {
                        lbxNotifications.Items.Add(logoutDic["message"]);
                        this.isLoggedIn = false;
                        timer.Enabled = false;
                    }

                }
            }
            else
            {
                MessageBox.Show("You already logged out.");
            }
        }

        #endregion

        private void frmAttendance_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            MessageBox.Show(GetSQL());
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            // setup sql_data_adapter and get data
            if (adapter == null)
            {
                adapter = new SqlDataAdapter(GetSQL(), GetSqlConnection());
            }
            else
            {
                adapter.SelectCommand.CommandText = GetSQL();
            }

            // fill data in dataset
            attendanceDataset.Reset();
            adapter.Fill(attendanceDataset, tableName);

            // check if there are data need to be sent to api
            if (attendanceDataset.Tables[tableName].Rows.Count > 0)
            {
                // add notification to lbxNotifications
                lbxNotifications.Items.Add("attendance logs to be sent to api: " + attendanceDataset.Tables[tableName].Rows.Count);

                // send data to api
                SendAttendanceLogsToAPI(attendanceDataset.Tables[tableName]);
            }
            else
            {
                lbxNotifications.Items.Add("no new attendance logs. no data sent to api on " + DateTime.Now);
            }
        }

        private async void SendAttendanceLogsToAPI(DataTable AttendanceLogs)
        {
            // map attendance logs as needed in api params
            List<Record> logsList = MapLogsToApiParams(AttendanceLogs);
            dynamic logsListWarper = new { record = logsList };

            // serialize logs to json
            var logsSerialized = await JsonConvert.SerializeObjectAsync(logsListWarper);

            // send logs to api
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(this.baseAddress);
                var content = new StringContent(logsSerialized, Encoding.UTF8, "application/json");
                var result = await client.PostAsync("device_attendances?access_token=" + this.accessToken, content);

                var attendanceJsonResult = await result.Content.ReadAsStringAsync();
                var attendanceDic = JsonConvert.DeserializeObject<Dictionary<string, string>>(attendanceJsonResult);

                if (attendanceDic["success"] == "true")
                {
                    lbxNotifications.Items.Add("Data sent successfully. records_updated: " + attendanceDic["records_updated"]);
                    this.isLoggedIn = true;
                }
                else
                {
                    lbxNotifications.Items.Add(attendanceDic["error"]);
                }
            }
        }

        private List<Record> MapLogsToApiParams(DataTable AttendanceLogs)
        {
            List<Record> list = new List<Record>();

            foreach (DataRow row in AttendanceLogs.Rows)
            {
                list.Add(
                    new Record {
                        id = Convert.ToInt32(row["EmployeeCode"]),
                        day = Convert.ToDateTime(row["LogDate"]).ToString("MM/dd/yyyy"),
                        time = Convert.ToDateTime(row["LogDate"]).ToString("HH:mm"),
                        direction = row["Direction"].ToString()
                    }
                );
            }

            return list;
        }
    }

    public class Login
    {
        public string login { get; set; }
        public string password { get; set; }
    }

    public class Record
    {
        public int id { get; set; }
        public string day { get; set; }
        public string time { get; set; }
        public string direction { get; set; }
    }
}
