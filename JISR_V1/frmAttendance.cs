using Newtonsoft.Json;
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
using System.Xml.Linq;

namespace JISR_V1
{
    public partial class frmAttendance : Form
    {
        #region Private data members
        private bool isLoggedIn;
        private bool isFirstCalled;
        private DateTime? lastSavedLogDate;

        private SqlConnection connection = null;
        private SqlDataAdapter adapter = null;
        private DataSet attendanceDataset = null;
        private const string tableName = "attendance";
        #endregion

        #region Constructor and form_load
        public frmAttendance()
        {
            InitializeComponent();

            // Initialize Members
            this.isLoggedIn = false;
            this.isFirstCalled = true;
            this.lastSavedLogDate = null;
        }

        private void frmAttendance_Load(object sender, EventArgs e)
        {
            // Load Configurations from xml file
            Configurations.Load();
            this.lastSavedLogDate = Configurations.LastSavedDate;
            this.isFirstCalled = this.lastSavedLogDate == null ? true : false;


            // Check if saved AccessToken is valid
            PingAPI();

            // Initialize Connection and DataSet
            if (connection == null) connection = GetSqlConnection();
            if (attendanceDataset == null) attendanceDataset = new DataSet();

            // Initialize Timer Interval
            timer.Interval = Configurations.TimerInterval;
        }
        #endregion

        #region SQL Connection and Query
        private SqlConnection GetSqlConnection()
        {
            if (connection == null)
            {
                return new SqlConnection(Configurations.ConnectionString);
            }
            else
            {
                return connection;
            }
        }

        private string GetSQL()
        {
            DateTime date = DateTime.Now;
            string tbl = GetTableName(date, "DeviceLogs");
            string sql = String.Format("select DeviceLogId,EmployeeCode,LogDate,Direction from {0},Employees where {0}.UserId=Employees.EmployeeId and {1} order by DeviceLogId", tbl, GetWhere(date));
            return sql;
        }

        private string GetTableName(DateTime date, string tbl)
        {
            date = Convert.ToDateTime(date.ToString(new CultureInfo("en-US")));
            int month = date.Month;
            int year = date.Year;
            return String.Format("{0}_{1}_{2}", tbl, month, year);
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
                //onlyIfFirstTime = String.Format("and  logDate between '{0}' and '{1}'", today.AddMinutes(-1), today);
                if (this.lastSavedLogDate != null)
                {
                    onlyIfFirstTime = String.Format("and  logDate > '{0}'", this.lastSavedLogDate.Value);
                }
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
                    client.BaseAddress = new Uri(Configurations.BaseAddress);

                    using (var pingResponse = await client.GetAsync(String.Format("ping?access_token={0}", Configurations.AccessToken)))
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
                    client.BaseAddress = new Uri(Configurations.BaseAddress);

                    var serializedLogin = JsonConvert.SerializeObject(
                        new Login
                        {
                            login = Configurations.Login,
                            password = Configurations.Password
                        }
                    );

                    var content = new StringContent(serializedLogin, Encoding.UTF8, "application/json");
                    var result = await client.PostAsync("sessions", content);
                    var loginJsonResult = await result.Content.ReadAsStringAsync();
                    var loginDic = JsonConvert.DeserializeObject<Dictionary<string, string>>(loginJsonResult);

                    if (loginDic["success"] == "true")
                    {
                        Configurations.AccessToken = loginDic["access_token"];
                        Configurations.Save("AccessToken", loginDic["access_token"]);

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
                    client.BaseAddress = new Uri(Configurations.BaseAddress);

                    var result = await client.DeleteAsync(String.Format("sessions?access_token={0}", Configurations.AccessToken));
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

        #region Timer_tick for sending logData to api
        private void timer_Tick(object sender, EventArgs e)
        {
            try
            {
                // setup sql_data_adapter and get data
                string sqlStatement = GetSQL();

                if (adapter == null)
                {
                    adapter = new SqlDataAdapter(sqlStatement, GetSqlConnection());
                }
                else
                {
                    adapter.SelectCommand.CommandText = sqlStatement;
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
                    SendAttendanceLogsToAPI(attendanceDataset.Tables[tableName]).ContinueWith( r => {

                        if (r.Exception == null)
                        {
                            // save lastSavedLogDate
                            int rowsCount = attendanceDataset.Tables[tableName].Rows.Count;
                            this.lastSavedLogDate = Convert.ToDateTime(attendanceDataset.Tables[tableName].Rows[rowsCount - 1]["LogDate"]);
                            Configurations.Save("LastSavedDate", this.lastSavedLogDate.ToString()); 
                        }
                        else
                        {
                            //MessageBox.Show("Error occurs in SendAttendanceLogsToAPI method: " + r.Exception.Message);
                        }
                        
                    });
                }
                else
                {
                    lbxNotifications.Items.Add("no new attendance logs. no data sent to api on " + DateTime.Now);
                }
            }
            catch (Exception ex)
            {
                lbxNotifications.Items.Add("Error occurs in Timer_Tick: " + ex.Message);
            }
        }

        private async Task SendAttendanceLogsToAPI(DataTable AttendanceLogs)
        {
            // map attendance logs as needed in api params
            List<Record> logsList = MapLogsToApiParams(AttendanceLogs);
            dynamic logsListWarper = new { record = logsList };

            // serialize logs to json
            var logsSerialized = await JsonConvert.SerializeObjectAsync(logsListWarper);

            // send logs to api
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(Configurations.BaseAddress);
                var content = new StringContent(logsSerialized, Encoding.UTF8, "application/json");
                var result = await client.PostAsync("device_attendances?access_token=" + Configurations.AccessToken, content);

                var attendanceJsonResult = await result.Content.ReadAsStringAsync();
                var attendanceDic = JsonConvert.DeserializeObject<Dictionary<string, string>>(attendanceJsonResult);

                if (attendanceDic["success"] == "true")
                {
                    lbxNotifications.Items.Add(String.Format("Data sent successfully on {0}. records_updated: {1}", DateTime.Now, attendanceDic["records_updated"]));
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
                    new Record
                    {
                        id = Convert.ToString(row["EmployeeCode"]),
                        day = Convert.ToDateTime(row["LogDate"]).ToString("dd/MM/yyyy"),
                        time = Convert.ToDateTime(row["LogDate"]).ToString("HH:mm"),
                        direction = row["Direction"].ToString()
                    }
                );
            }

            return list;
        }
        #endregion

        #region Load Configuration Button Click
        private void btnLoadConfigurations_Click(object sender, EventArgs e)
        {
            Configurations.Load();
        } 
        #endregion
    }


    #region model classes
    public class Login
    {
        public string login { get; set; }
        public string password { get; set; }
    }

    public class Record
    {
        public string id { get; set; }
        public string day { get; set; }
        public string time { get; set; }
        public string direction { get; set; }
    }

    public static class Configurations
    {
        public static string AccessToken { get; set; }
        public static string ConnectionString { get; set; }
        public static string BaseAddress { get; set; }
        public static string Login { get; set; }
        public static string Password { get; set; }
        public static int TimerInterval { get; set; }
        public static DateTime? LastSavedDate { get; set; }
        private static XElement configurations { get; set; }
        public static void Load()
        {
            try
            {
                configurations = XElement.Load("configurations.xml");
                XElement attendance = configurations.Elements().First();

                AccessToken = attendance.Element("AccessToken").Value;
                BaseAddress = attendance.Element("BaseAddress").Value;
                ConnectionString = attendance.Element("ConnectionString").Value;
                Login = attendance.Element("Login").Value;
                Password = attendance.Element("Password").Value;
                TimerInterval = Convert.ToInt32(attendance.Element("TimerInterval").Value);
                
                string strDate = attendance.Element("LastSavedDate").Value;
                DateTime tempDate;
                LastSavedDate = DateTime.TryParse(strDate, out tempDate)? tempDate : (DateTime?)null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        public static void Save(string Node, string Value)
        {
            try
            {
                XElement attendance = configurations.Elements().First();
                attendance.SetElementValue(Node, Value);
                configurations.Save("configurations.xml");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
    #endregion
}
