using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using MySql.Data;
using MySql.Data.MySqlClient;
using System.Data;

namespace ProjectTemplate
{
	[WebService(Namespace = "http://tempuri.org/")]
	[WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
	[System.ComponentModel.ToolboxItem(false)]
	[System.Web.Script.Services.ScriptService]

	public class ProjectServices : System.Web.Services.WebService
	{
		////////////////////////////////////////////////////////////////////////
		///replace the values of these variables with your database credentials
		////////////////////////////////////////////////////////////////////////
		private string dbID = "cis440summer2025team5";
		private string dbPass = "cis440summer2025team5";
		private string dbName = "cis440summer2025team5";
		////////////////////////////////////////////////////////////////////////
		
		////////////////////////////////////////////////////////////////////////
		///call this method anywhere that you need the connection string!
		////////////////////////////////////////////////////////////////////////
		private string getConString() {
			return "SERVER=107.180.1.16; PORT=3306; DATABASE=" + dbName+"; UID=" + dbID + "; PASSWORD=" + dbPass;
		}
		////////////////////////////////////////////////////////////////////////



		/////////////////////////////////////////////////////////////////////////
		//don't forget to include this decoration above each method that you want
		//to be exposed as a web service!
		[WebMethod(EnableSession = true)]
		/////////////////////////////////////////////////////////////////////////
		public string TestConnection()
		{
			try
			{
				string testQuery = "select * from users";

				////////////////////////////////////////////////////////////////////////
				///here's an example of using the getConString method!
				////////////////////////////////////////////////////////////////////////
				MySqlConnection con = new MySqlConnection(getConString());
				////////////////////////////////////////////////////////////////////////

				MySqlCommand cmd = new MySqlCommand(testQuery, con);
				MySqlDataAdapter adapter = new MySqlDataAdapter(cmd);
				DataTable table = new DataTable();
				adapter.Fill(table);
				return "Success!";
			}
			catch (Exception e)
			{
				return "Something went wrong, please check your credentials and db name and try again.  Error: "+e.Message;
			}
		}

        [WebMethod(EnableSession = true)]
		public int[] TemplateTest()
		{
			int[] test = new int[4];
			test[0] = 42;
			test[1] = 10;
			test[2] = 33;
			test[3] = 12;
			return test;
        }

        [WebMethod(EnableSession = true)]
        public bool LogOn(string uid, string pass)
        {
            //LOGIC: pass the parameters into the database to see if an account
            //with these credentials exist.  If it does, then return true.  If
            //it doesn't, then return false

            //we return this flag to tell them if they logged in or not
            bool success = false;

            //our connection string comes from our web.config file like we talked about earlier
            string sqlConnectString = getConString();
            //here's our query.  A basic select with nothing fancy.  Note the parameters that begin with @
            string sqlSelect = "SELECT empid, is_admin FROM users WHERE username=@idValue and pass=@passValue";

            //set up our connection object to be ready to use our connection string
            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            //set up our command object to use our connection, and our query
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);

            //tell our command to replace the @parameters with real values
            //we decode them because they came to us via the web so they were encoded
            //for transmission (funky characters escaped, mostly)
            sqlCommand.Parameters.AddWithValue("@idValue", HttpUtility.UrlDecode(uid));
            sqlCommand.Parameters.AddWithValue("@passValue", HttpUtility.UrlDecode(pass));

            //a data adapter acts like a bridge between our command object and 
            //the data we are trying to get back and put in a table object
            MySqlDataAdapter sqlDa = new MySqlDataAdapter(sqlCommand);
            //here's the table we want to fill with the results from our query
            DataTable sqlDt = new DataTable();
            //here we go filling it!
            sqlDa.Fill(sqlDt);
            //check to see if any rows were returned.  If they were, it means it's 
            //a legit account
            if (sqlDt.Rows.Count > 0)
            {
                //flip our flag to true so we return a value that lets them know they're logged in
                Session["id"] = sqlDt.Rows[0]["empid"];
                Session["isAdmin"] = sqlDt.Rows[0]["is_admin"];
				success = true;
            }
            //return the result!
            return success;


        }

        [WebMethod(EnableSession = true)]
        public bool LogOff()
        {
            //if they log off, then we remove the session.  That way, if they access
            //again later they have to log back on in order for their ID to be back
            //in the session!
            Session.Abandon();
            return true;
        }

        // This method returns the current login information of the user
        [WebMethod(EnableSession = true)]
        public string LoginInfo()
        {
            // Check if the username is stored in the session, indicating a logged-in user
            if (Session["id"] != null)
            {
                string username = Session["id"].ToString();
                string userInfo = $"Logged in as: {username}";

                // Check if admin status is stored and if the user is an admin
                if (Session["isAdmin"] != null && (bool)Session["isAdmin"] == true)
                {
                    userInfo += " (Admin)";
                }
                else
                {
                    userInfo += " (User)"; // Explicitly state "User" for non admins
                }
                return userInfo;
            }
            else
            {
                return "No user is currently logged in.";
            }
        }

        // This method allows a user to clock in
        [WebMethod(EnableSession = true)]
        public string ClockIn()
        {
            //Check if user is logged in
            if (Session["id"] == null)
            {
                return "Error: You must be logged in to clock in.";
            }

            //Check if user is already clocked in
            if (Session["isClockedIn"] != null && (bool)Session["isClockedIn"] == true)
            {
                return "Error: You are already clocked in.";
            }

            int empId = Convert.ToInt32(Session["id"]);
            DateTime clockInTime = DateTime.Now;

            string sqlConnectString = getConString();
            // SQL for inserting data
            string sqlInsert = "INSERT INTO timelogs (empid, clock_in, clock_out) VALUES (@empid, @clockInTime, NULL)";

            using (MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString))
            {
                sqlConnection.Open();
                using (MySqlCommand sqlCommand = new MySqlCommand(sqlInsert, sqlConnection))
                {
                    sqlCommand.Parameters.AddWithValue("@empid", empId);
                    sqlCommand.Parameters.AddWithValue("@clockInTime", clockInTime);

                    // Feedback if you are clocked in
                    int rowsAffected = sqlCommand.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        Session["isClockedIn"] = true;
                        return "Successfully clocked in at " + clockInTime.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    else
                    {
                        //Clock in rror message
                        return "Error: Failed to record clock in.";
                    }
                }
            }
        }
        // This method allows a user to clock out
        [WebMethod(EnableSession = true)]
        public string ClockOut()
        {
            //Check if user is logged in
            if (Session["id"] == null)
            {
                return "Error: You must be logged in to clock out.";
            }

            //Check if user is not clocked in
            if (Session["isClockedIn"] == null || (bool)Session["isClockedIn"] == false)
            {
                return "Error: You are not currently clocked in.";
            }

            int empId = Convert.ToInt32(Session["id"]);
            DateTime clockOutTime = DateTime.Now;

            string sqlConnectString = getConString();
            string sqlUpdate = "UPDATE timelogs SET clock_out = @clockOutTime " +
                               "WHERE empid = @empid AND clock_out IS NULL " +
                               "ORDER BY clock_in DESC LIMIT 1";

            using (MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString))
            {
                sqlConnection.Open();
                using (MySqlCommand sqlCommand = new MySqlCommand(sqlUpdate, sqlConnection))
                {
                    sqlCommand.Parameters.AddWithValue("@clockOutTime", clockOutTime);
                    sqlCommand.Parameters.AddWithValue("@empid", empId);

                    // Feedback if you are clocked out
                    int rowsAffected = sqlCommand.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        Session["isClockedIn"] = false;
                        return "Successfully clocked out at " + clockOutTime.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    else
                    {
                        //Clock out rror message
                        return "Error: Failed to record clock out.";
                    }
                }
            }
        }
    }
}

