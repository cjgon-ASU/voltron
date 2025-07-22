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
            string sqlSelect = "SELECT empid, username, is_admin FROM users WHERE username=@idValue and pass=@passValue";

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
                int empId = Convert.ToInt32(sqlDt.Rows[0]["empid"]);
                Session["id"] = empId;
                Session["loggedInUsername"] = sqlDt.Rows[0]["username"].ToString();
                Session["isAdmin"] = sqlDt.Rows[0]["is_admin"];

                //Check if the user is clocked in, (to store in session)
                string checkClockInSql = "SELECT COUNT(*) FROM timelogs WHERE empid = @empid AND clock_out IS NULL";
                sqlConnection.Open();
                MySqlCommand checkCmd = new MySqlCommand(checkClockInSql, sqlConnection);
                
                checkCmd.Parameters.AddWithValue("@empid", empId);
                int openClockIn = Convert.ToInt32(checkCmd.ExecuteScalar());

                //Set session status based on whether there's an open clock in record in the DB
                Session["isClockedIn"] = (openClockIn > 0);
                
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

        //This method returns the current login information of the user
        [WebMethod(EnableSession = true)]
        public string LoginInfo()
        {
            //Check if the username is stored in the session, indicating a logged in user
            if (Session["loggedInUsername"] != null)
            {
                string username = Session["loggedInUsername"].ToString();
                string userInfo = $"Logged in as: {username}";

                //Check if admin status is stored and if the user is an admin
                if (Session["isAdmin"] != null && (bool)Session["isAdmin"] == true)
                {
                    userInfo += " (Admin)";
                }
                else
                {
                    userInfo += " (User)";
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

            //Check if user is already clocked in (from session)
            if (Session["isClockedIn"] != null && (bool)Session["isClockedIn"] == true)
            {
                return "Error: You are already clocked in.";
            }

            int empId = Convert.ToInt32(Session["id"]);
            DateTime clockInTime = DateTime.Now;

            string sqlConnectString = getConString();

            //SQL for inserting data into timelogs and setting clock in status in DB
            string sqlInsertTimelog = "INSERT INTO timelogs (empid, clock_in, clock_out) VALUES (@empid, @clockInTime, NULL)";
            string sqlUpdateUserClockedIn = "UPDATE users SET is_clocked_in = 1 WHERE empid = @empid";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand timelogCmd = new MySqlCommand(sqlInsertTimelog, sqlConnection);
            MySqlCommand userUpdateCmd = new MySqlCommand(sqlUpdateUserClockedIn, sqlConnection);

            sqlConnection.Open();

            //INSERT into timelogs
            timelogCmd.Parameters.AddWithValue("@empid", empId);
            timelogCmd.Parameters.AddWithValue("@clockInTime", clockInTime);
            int timelogRowsAffected = timelogCmd.ExecuteNonQuery();

            if (timelogRowsAffected > 0)
            {
                //UPDATE on users table
                userUpdateCmd.Parameters.AddWithValue("@empid", empId);
                int userRowsAffected = userUpdateCmd.ExecuteNonQuery();

                if (userRowsAffected > 0)
                {
                    Session["isClockedIn"] = true;
                    return "Successfully clocked in at " + clockInTime.ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    return "Error: Clock in recorded, but user status not updated.";
                }
            }
            else
            {
                return "Error: Failed to record timelogs table).";
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

            //Check if user is not clocked in (from session)
            if (Session["isClockedIn"] == null || (bool)Session["isClockedIn"] == false)
            {
                return "Error: You are not currently clocked in.";
            }

            int empId = Convert.ToInt32(Session["id"]);
            DateTime clockOutTime = DateTime.Now;

            string sqlConnectString = getConString();

            //SQL for updating data into timelogs and setting clock out status in DB
            string sqlUpdateTimelog = "UPDATE timelogs SET clock_out = @clockOutTime " +
                                      "WHERE empid = @empid AND clock_out IS NULL " +
                                      "ORDER BY clock_in DESC LIMIT 1";
            
            string sqlUpdateUserClockedOut = "UPDATE users SET is_clocked_in = 0 WHERE empid = @empid";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand timelogCmd = new MySqlCommand(sqlUpdateTimelog, sqlConnection);
            MySqlCommand userUpdateCmd = new MySqlCommand(sqlUpdateUserClockedOut, sqlConnection);

            sqlConnection.Open();
            int timelogRowsAffected = 0;
            int userRowsAffected = 0;

            //UPDATE on timelogs
            timelogCmd.Parameters.AddWithValue("@clockOutTime", clockOutTime);
            timelogCmd.Parameters.AddWithValue("@empid", empId);
            timelogRowsAffected = timelogCmd.ExecuteNonQuery();

            //Update user status if timelog update was successful
            if (timelogRowsAffected > 0)
            {
                userUpdateCmd.Parameters.AddWithValue("@empid", empId);
                userRowsAffected = userUpdateCmd.ExecuteNonQuery();

                if (userRowsAffected > 0)
                {
                    Session["isClockedIn"] = false; // Update session status
                    return "Successfully clocked out at " + clockOutTime.ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    return "Error: Clock out recorded, but user status not updated.";
                }
            }
            else
            {
                return "Error: No clock in record found to clock out from.";
            }
        }
        [WebMethod(EnableSession = true)]
        public List<string> ClockedInUsers()
        {
            //admin session check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
               
                return new List<string> { "Error: You must be an administrator to view this list." };
            }

            List<string> clockedInUsers = new List<string>();
            string sqlConnectString = getConString();

            string sqlQuery = "SELECT users.username, timelogs.clock_in " +
                              "FROM users " +
                              "INNER JOIN timelogs ON users.empid = timelogs.empid " +
                              "WHERE users.is_clocked_in = 1 AND timelogs.clock_out IS NULL " +
                              "ORDER BY timelogs.clock_in ASC";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlQuery, sqlConnection);

            try
            {
                sqlConnection.Open();
                MySqlDataAdapter sqlDa = new MySqlDataAdapter(sqlCommand);
                DataTable sqlDt = new DataTable();
                sqlDa.Fill(sqlDt);

                //loop to add usernames to the list
                foreach (DataRow row in sqlDt.Rows)
                {
                    string username = row["username"].ToString();
                    string clockInTime = Convert.ToDateTime(row["clock_in"]).ToString("yyyy-MM-dd HH:mm:ss");
                    clockedInUsers.Add($"{username} (Clocked in at {clockInTime})");
                }

            }
            catch (Exception ex)
            {
                return new List<string> { "An error occurred while fetching the list of clocked in users." };
            }

            if (clockedInUsers.Count == 0)
            {
                clockedInUsers.Add("No users currently clocked in.");
            }

            return clockedInUsers;
        }
    }

}

