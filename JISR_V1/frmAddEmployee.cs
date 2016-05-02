using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JISR_V1
{
    public partial class frmAddEmployee : Form
    {
        SqlConnection cn;
        
        public frmAddEmployee()
        {
            InitializeComponent();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (txtName.Text.Trim() == "")
            {
                MessageBox.Show("Employee Name is Required.");
                return;
            }

            if (txtCode.Text.Trim() == "")
            {
                MessageBox.Show("Employee Code is Required.");
                return;
            }

            try
            {
                if (cn == null) cn = new SqlConnection(@"server=.\mssql;uid=sa;pwd=123;database=eSSLSmartOffice");

                string sql = "insert into Employees(EmployeeName,EmployeeCode,Gender,CompanyId,DepartmentId,CategoryId,EmployeeCodeInDevice,EmployementType,Status) " +
                             "values(@EmployeeName,@EmployeeCode,@Gender,@CompanyId,@DepartmentId,@CategoryId,@EmployeeCodeInDevice,@EmployementType,@Status)";

                SqlCommand cm = new SqlCommand(sql, cn);

                cm.Parameters.AddWithValue("@EmployeeName", txtName.Text);
                cm.Parameters.AddWithValue("@EmployeeCode", txtCode.Text);
                cm.Parameters.AddWithValue("@Gender", "Male");
                cm.Parameters.AddWithValue("@CompanyId", 1);
                cm.Parameters.AddWithValue("@DepartmentId", 1);
                cm.Parameters.AddWithValue("@CategoryId", 1);
                cm.Parameters.AddWithValue("@EmployeeCodeInDevice", txtCode.Text);
                cm.Parameters.AddWithValue("@EmployementType", "Permanent");
                cm.Parameters.AddWithValue("@Status", "Working");

                if (cn.State == ConnectionState.Closed) cn.Open();
                int affectedRows = cm.ExecuteNonQuery();
                cn.Close();

                if (affectedRows > 0)
                {
                    MessageBox.Show("Employee added successfully.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error Occurs during adding employee:" + ex.Message);
            }
        }

        private async void addEmployeesFromAPI()
        {
            // set sql connection object
            if (cn == null) cn = new SqlConnection(@"server=.\mssql;uid=sa;pwd=123;database=eSSLSmartOffice");

            // fetch data from api
            try
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(Configurations.BaseAddress);

                    using (var employeesResponse = await client.GetAsync(String.Format("ping?access_token={0}", Configurations.AccessToken)))
                    {
                        var employeesJsonResult = await employeesResponse.Content.ReadAsStringAsync();
                        EmployeesResponse response = JsonConvert.DeserializeObject<EmployeesResponse>(employeesJsonResult);

                        if (response.success == "true")
                        {
                            foreach (Employee emp in response.employees)
                            {
                                lbxNotifications.Items.Add(
                                    "id: " + emp.name + "  |  " +
                                    "name: " + emp.name + "  |  " +
                                    "emp_id: " + emp.emp_id
                                );
                            }
                        }
                        else
                        {
                            lbxNotifications.Items.Add("Success: false");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error occurs in Get_All_Employees API: \n" + ex.Message);
            }

            // add employees to essl database
            List<SqlCommand> cmList = new List<SqlCommand>();


        }
    }

    public class EmployeesResponse
    {
        public string success { get; set; }
        public Employee[] employees { get; set; }
        public string status_code { get; set; }
    }

    public class Employee
    {
        public string id { get; set; }
        public string name { get; set; }
        public string emp_id { get; set; }
    }
}
