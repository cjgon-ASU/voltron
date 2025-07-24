using MySql.Data;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Services;

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

        // This method allows a user to log on
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
        
        // This method allows a user to log off
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
        
        // This method retrieves all users currently clocked in
        [WebMethod(EnableSession = true)]
        public User[] GetClockedInUsers()
        {
            //check out the return type.  It's an array of Account objects.  You can look at our custom Account class in this solution to see that it's 
            //just a container for public class-level variables.  It's a simple container that asp.net will have no trouble converting into json.  When we return
            //sets of information, it's a good idea to create a custom container class to represent instances (or rows) of that information, and then return an array of those objects.  
            //Keeps everything simple.

            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //if not an admin, return an empty array of User objects.
                return new User[0];
            }
          


            //LOGIC: get all the active users and return them!
            DataTable sqlDt = new DataTable("users");

            string sqlConnectString = getConString();
            string sqlSelect = "select empid, username, fname, lname, department from users where is_clocked_in = 1 order by lname";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);

            //gonna use this to fill a data table
            MySqlDataAdapter sqlDa = new MySqlDataAdapter(sqlCommand);
            //filling the data table
            sqlConnection.Open();
            sqlDa.Fill(sqlDt);

            //loop through each row in the dataset, creating instances
            //of our container class Account.  Fill each acciount with
            //data from the rows, then dump them in a list.
            List<User> accounts = new List<User>();
            for (int i = 0; i < sqlDt.Rows.Count; i++)
            {
                accounts.Add(new User
                {
                    empid = Convert.ToInt32(sqlDt.Rows[i]["empid"]),
                    username = sqlDt.Rows[i]["username"].ToString(),
                    fname = sqlDt.Rows[i]["fname"].ToString(),
                    lname = sqlDt.Rows[i]["lname"].ToString(),
                    department = sqlDt.Rows[i]["department"].ToString()
                });
            }
            //convert the list of accounts to an array and return!
            return accounts.ToArray();
        }
       
        // This method allows an admin to remove a user
        [WebMethod(EnableSession = true)]
        public string RemoveUser(string username)
        {
            string sqlConnectString = getConString();

            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                return "Error: You must be an administrator to remove users.";
            }

            //this deletes user from database table
            string sqlDelete = "DELETE FROM users WHERE username = @username";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlDelete, sqlConnection);

            try
            {
                sqlCommand.Parameters.AddWithValue("@username", HttpUtility.UrlDecode(username));

                sqlConnection.Open();
                int rowsAffected = sqlCommand.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    return $"User '{username}' removed successfully.";
                }
                else
                {
                    return $"Error: User '{username}' not found or could not be removed.";
                }
            }
            catch (Exception e)
            {
                return "Error: Cound not remove user.";
            }

        }
       
        // This method allows admin to retrieve all users in the system        
        [WebMethod(EnableSession = true)]
        public User[] GetUsers()
        {
            //check out the return type.  It's an array of Account objects.  You can look at our custom Account class in this solution to see that it's 
            //just a container for public class-level variables.  It's a simple container that asp.net will have no trouble converting into json.  When we return
            //sets of information, it's a good idea to create a custom container class to represent instances (or rows) of that information, and then return an array of those objects.  
            //Keeps everything simple.

            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //if not an admin, return an empty array of User objects.
                return new User[0];
           }

            //LOGIC: get all the active users and return them!
            DataTable sqlDt = new DataTable("users");

            string sqlConnectString = getConString();
            string sqlSelect = "select empid, username, fname, lname, department from users  order by lname";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);

            //gonna use this to fill a data table
            MySqlDataAdapter sqlDa = new MySqlDataAdapter(sqlCommand);
            //filling the data table
            sqlConnection.Open();
            sqlDa.Fill(sqlDt);

            //loop through each row in the dataset, creating instances
            //of our container class Account.  Fill each acciount with
            //data from the rows, then dump them in a list.
            List<User> accounts = new List<User>();
            for (int i = 0; i < sqlDt.Rows.Count; i++)
            {
                accounts.Add(new User
                {
                    empid = Convert.ToInt32(sqlDt.Rows[i]["empid"]),
                    username = sqlDt.Rows[i]["username"].ToString(),
                    fname = sqlDt.Rows[i]["fname"].ToString(),
                    lname = sqlDt.Rows[i]["lname"].ToString(),
                    department = sqlDt.Rows[i]["department"].ToString()
                });
            }
            //convert the list of accounts to an array and return!
            return accounts.ToArray();
        }
       
        // This method allows an admin to add a new user
        [WebMethod(EnableSession = true)]
        public string AddUser(string uid, string pass, string fname, string lname, string dept)
        {
            //admin check
             if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
             {
                return "Error: You must be an administrator to remove users.";
             }

            string sqlConnectString = getConString();
            //the only thing fancy about this query is SELECT LAST_INSERT_ID() at the end.  All that
            //does is tell mySql server to return the primary key of the last inserted row.
            string sqlInsert = "insert into users (username, pass, fname, lname, department) " +
                "values(@usernameValue, @passValue, @fnameValue, @lnameValue, @departmentValue); SELECT LAST_INSERT_ID();";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlInsert, sqlConnection);

            sqlCommand.Parameters.AddWithValue("@usernameValue", HttpUtility.UrlDecode(uid));
            sqlCommand.Parameters.AddWithValue("@passValue", HttpUtility.UrlDecode(pass));
            sqlCommand.Parameters.AddWithValue("@fnameValue", HttpUtility.UrlDecode(fname));
            sqlCommand.Parameters.AddWithValue("@lnameValue", HttpUtility.UrlDecode(lname));
            sqlCommand.Parameters.AddWithValue("@departmentValue", HttpUtility.UrlDecode(dept));

            //this time, we're not using a data adapter to fill a data table.  We're just
            //opening the connection, telling our command to "executescalar" which says basically
            //execute the query and just hand me back the number the query returns (the ID, remember?).
            //don't forget to close the connection!
            sqlConnection.Open();
            //we're using a try/catch so that if the query errors out we can handle it gracefully
            //by closing the connection and moving on
            try
            {
                // Use ExecuteNonQuery() for INSERT, UPDATE, DELETE operations
                // ExecuteScalar() is for when you expect a single value back (like LAST_INSERT_ID)
                int rowsAffected = sqlCommand.ExecuteNonQuery();
                sqlConnection.Close();
                if (rowsAffected > 0)
                {
                    //success message
                    return $"User '{uid}' added successfully.";
                }
                else
                {
                    //failed message
                    return $"Error: Failed to add user '{uid}'.";
                }
            }
            catch (Exception e)
            { 
                
                return "Error: Unable to add user.";
            }

            
        }
    }

}

