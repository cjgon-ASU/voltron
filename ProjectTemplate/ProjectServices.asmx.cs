using MySql.Data;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Web;
using System.Web.Services;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

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
        private string getConString()
        {
            return "SERVER=107.180.1.16; PORT=3306; DATABASE=" + dbName + "; UID=" + dbID + "; PASSWORD=" + dbPass;
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
                return "Something went wrong, please check your credentials and db name and try again.  Error: " + e.Message;
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
            string sqlSelect = "SELECT empid, username, department, is_admin FROM users WHERE username=@idValue and pass=@passValue";

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
                Session["department"] = sqlDt.Rows[0]["department"].ToString();

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
                string empId = Session["id"] != null ? Session["id"].ToString() : "Unknown ID";
                string department = Session["department"] != null ? Session["department"].ToString() : "Unknown Department";
                
                string userInfo = $"Logged in as: {username} {empId} {department}";

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

        //This method checks if logged in user is an admin or not
        [WebMethod(EnableSession = true)]
        public bool AdminCheck()
        {
            //Check if the username is stored in the session, indicating a logged in user
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                
                return false;
            }
            else
            {
                return true;
            }
        }

        // This method allows a user to clocked on
        [WebMethod(EnableSession = true)]
        public bool IsUserClockedIn()
        {
           
            //we return this flag to tell them if they clocked in or not
            bool clockedin = false;

            //Check if user is logged in
            if (Session["id"] == null)
            {
                //return "Error: You must be logged in to clock in.";
                return false;
            }

            //get the empid from the session
            int empId = Convert.ToInt32(Session["id"]);

            //our connection string comes from our web.config file like we talked about earlier
            string sqlConnectString = getConString();
            //here's our query.  A basic select with nothing fancy.  Note the parameters that begin with @
            string sqlSelect = "select count(*) from timelogs where empid = @empid AND clock_out IS NULL";

            //set up our connection object to be ready to use our connection string
            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            //set up our command object to use our connection, and our query
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);

            // get empid from session to 
            sqlCommand.Parameters.AddWithValue("@empid", empId);
            sqlConnection.Open();
            
            //get the single value from the COUNT(*) query to see if the user is clocked in
            object result = sqlCommand.ExecuteScalar();
            int ClockCount = 0;

            if (result != null && result != DBNull.Value)
            {
                ClockCount = Convert.ToInt32(result);
            }

            // If the count is greater than 0, the user is clocked in.
            if (ClockCount > 0)
            {
                clockedin = true;
            }
            sqlConnection.Close();
            //return the result!
            return clockedin;


        }

        // This method allows a user to clock in
        [WebMethod(EnableSession = true)]
        public bool ClockIn()
        {
            //Check if user is logged in
            if (Session["id"] == null)
            {
                //return "Error: You must be logged in to clock in.";
                return false;
            }

            //Check if user is already clocked in (from session)
            if (Session["isClockedIn"] != null && (bool)Session["isClockedIn"] == true)
            {
                //return "Error: You are already clocked in.";
                return false;
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
                    //return "Successfully clocked in at " + clockInTime.ToString("yyyy-MM-dd HH:mm:ss");
                    return true;
                }
                else
                {
                    //return "Error: Clock in recorded, but user status not updated.";
                    return false;
                }
            }
            else
            {
                //return "Error: Failed to record timelogs table).";
                return false;
            }
        }

        // This method allows a user to clock out
        [WebMethod(EnableSession = true)]
        public bool ClockOut()
        {
            //Check if user is logged in
            if (Session["id"] == null)
            {
                //return "Error: You must be logged in to clock out.";
                return false;
            }

            //Check if user is not clocked in (from session)
            if (Session["isClockedIn"] == null || (bool)Session["isClockedIn"] == false)
            {
                //return "Error: You are not currently clocked in.";
                return false;
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
                    //return "Successfully clocked out at " + clockOutTime.ToString("yyyy-MM-dd HH:mm:ss");
                    return true;
                }
                else
                {
                    //return "Error: Clock out recorded, but user status not updated.";
                    return false;
                }
            }
            else
            {
                //return "Error: No clock in record found to clock out from.";
                return false;
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
            List<User> users = new List<User>();
            for (int i = 0; i < sqlDt.Rows.Count; i++)
            {
                users.Add(new User
                {
                    empid = Convert.ToInt32(sqlDt.Rows[i]["empid"]),
                    username = sqlDt.Rows[i]["username"].ToString(),
                    fname = sqlDt.Rows[i]["fname"].ToString(),
                    lname = sqlDt.Rows[i]["lname"].ToString(),
                    department = sqlDt.Rows[i]["department"].ToString()
                });
            }
            //convert the list of accounts to an array and return!
            return users.ToArray();
        }

        // This method allows an admin to remove a user
        [WebMethod(EnableSession = true)]
        public bool RemoveUser(string username)
        {
            string sqlConnectString = getConString();

            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //return "Error: You must be an administrator to remove users.";
                return false;
            }

            //this deletes user from database table
            string sqlDelete = "DELETE FROM users WHERE username=@usernameValue";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlDelete, sqlConnection);

            try
            {
                sqlCommand.Parameters.AddWithValue("@usernameValue", HttpUtility.UrlDecode(username));

                sqlConnection.Open();
                int rowsAffected = sqlCommand.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    //return $"User '{username}' removed successfully.";
                    return true;
                }
                else
                {
                    //return $"Error: User '{username}' not found or could not be removed.";
                    return false;
                }
            }
            catch (Exception e)
            {
                //return "Error: Cound not remove user.";
                return false;
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
            List<User> users = new List<User>();
            for (int i = 0; i < sqlDt.Rows.Count; i++)
            {
                users.Add(new User
                {
                    empid = Convert.ToInt32(sqlDt.Rows[i]["empid"]),
                    username = sqlDt.Rows[i]["username"].ToString(),
                    fname = sqlDt.Rows[i]["fname"].ToString(),
                    lname = sqlDt.Rows[i]["lname"].ToString(),
                    department = sqlDt.Rows[i]["department"].ToString()
                });
            }
            //convert the list of accounts to an array and return!
            return users.ToArray();
        }

        // This method allows an admin to add a new user
        [WebMethod(EnableSession = true)]
        public bool AddUser(string uid, string pass, string fname, string lname, string dept)
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //return "Error: You must be an administrator to remove users.";
                return false;
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
                    //return $"User '{uid}' added successfully.";
                    return true;
                }
                else
                {
                    //failed message
                    //return $"Error: Failed to add user '{uid}'.";
                    return false;
                }
            }
            catch (Exception e)
            {

                //return "Error: Unable to add user.";
                return false;
            }


        }

        [WebMethod(EnableSession = true)]
        public bool UpdateUser(string empid, string userid, string pass, string fname, string lname, string dept)
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //return "Error: You must be an administrator to update users.";
                return false;
            }

            string sqlConnectString = getConString();
            //this is a simple update, with parameters to pass in values

            string sqlSelect = @"update users set username = IF(@usernameValue = '', username, @usernameValue), pass = IF(@passValue = '', pass, @passValue), fname = IF(@fnameValue = '', fname, @fnameValue), " +
                   "lname = IF(@lnameValue = '', lname, @lnameValue), department = IF(@departmentValue = '', department, @departmentValue) where empid = @empidValue";


            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);

            sqlCommand.Parameters.AddWithValue("@empidValue", HttpUtility.UrlDecode(empid));
            sqlCommand.Parameters.AddWithValue("@usernameValue", HttpUtility.UrlDecode(userid));
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
                    //return $"User '{uid}' updated successfully.";
                    return true;
                }
                else
                {
                    //failed message
                    //return $"Error: Failed to update user '{uid}'.";
                    return false;
                }
            }
            catch (Exception e)
            {

                //return "Error: Unable to update user.";
                return false;
            }
        }

        // This method allows user to see a question   
        [WebMethod(EnableSession = true)]
        public Question[] GetQuestion()
        {
            //check out the return type.  It's an array of Question objects.  You can look at our custom Account class in this solution to see that it's 
            //just a container for public class-level variables.  It's a simple container that asp.net will have no trouble converting into json.  When we return
            //sets of information, it's a good idea to create a custom container class to represent instances (or rows) of that information, and then return an array of those objects.  
            //Keeps everything simple.


            //LOGIC: get one question and return it
            DataTable sqlDt = new DataTable("questions");

            string sqlConnectString = getConString();
            string sqlSelect = "select question_id, category, question_text from questions where is_active = true order by last_shown_at asc, created_at asc limit 1";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);

            //gonna use this to fill a data table
            MySqlDataAdapter sqlDa = new MySqlDataAdapter(sqlCommand);
            //filling the data table
            sqlConnection.Open();
            sqlDa.Fill(sqlDt);

            // Get the first (and only) row
            DataRow row = sqlDt.Rows[0]; 
            int qid = Convert.ToInt32(row["question_id"]);

            // Update the last_shown_at timestamp to the current time
            string sqlUpdate = "UPDATE questions SET last_shown_at = NOW() WHERE question_id = @question_id";
            MySqlCommand updateCommand = new MySqlCommand(sqlUpdate, sqlConnection);
            updateCommand.Parameters.AddWithValue("@question_id", qid);
            updateCommand.ExecuteNonQuery();

            // Create the Question object
            Question singleQuestion = new Question
            {
                question_id = qid,
                category = row["category"].ToString(),
                question_text = row["question_text"].ToString(),
            };
            sqlConnection.Close();

            // Return an array containing just this one question
            return new Question[] { singleQuestion };

        }

        // This method allows a user to submit feedback
        [WebMethod(EnableSession = true)]
        public bool SubmitFeedback(string qid, string cat, string score, string feedback)
        {

            // Get empid and dept from session instead of method parameter
            string empid = Session["id"].ToString();
            string dept = Session["department"] != null ? Session["department"].ToString() : string.Empty;


            string sqlConnectString = getConString();
            //the only thing fancy about this query is SELECT LAST_INSERT_ID() at the end.  All that
            //does is tell mySql server to return the primary key of the last inserted row.
            string sqlInsert = "insert into feedback (question_id, empid, department, category, score, feedback_text) " +
                "values(@question_id, @empid, @department, @category, @score, @feedback_text);";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlInsert, sqlConnection);

            sqlCommand.Parameters.AddWithValue("@question_id", string.IsNullOrWhiteSpace(qid) ? (object)DBNull.Value : (object)HttpUtility.UrlDecode(qid));
            sqlCommand.Parameters.AddWithValue("@empid", string.IsNullOrWhiteSpace(empid) ? (object)DBNull.Value : (object)HttpUtility.UrlDecode(empid));
            sqlCommand.Parameters.AddWithValue("@department", string.IsNullOrWhiteSpace(dept) ? (object)DBNull.Value : (object)HttpUtility.UrlDecode(dept));
            sqlCommand.Parameters.AddWithValue("@category", string.IsNullOrWhiteSpace(cat) ? (object)DBNull.Value : (object)HttpUtility.UrlDecode(cat));
            sqlCommand.Parameters.AddWithValue("@score", string.IsNullOrWhiteSpace(score) ? (object)DBNull.Value : (object)HttpUtility.UrlDecode(score));
            sqlCommand.Parameters.AddWithValue("@feedback_text", string.IsNullOrWhiteSpace(feedback) ? (object)DBNull.Value : (object)HttpUtility.UrlDecode(feedback));

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
                    //return $"Feedback '{qid}' added successfully.";
                    return true;
                }
                else
                {
                    //failed message
                    //return $"Error: Failed to add feedback for '{qid}'.";
                    return false;
                }
            }
            catch (Exception e)
            {

                //return "Error: Unable to add user.";
                return false;
            }


        }

        // This method allows admin to retrieve all questions in the system        
        [WebMethod(EnableSession = true)]
        public Question[] GetAllQuestions()
        {
            //check out the return type.  It's an array of Question objects.  You can look at our custom Account class in this solution to see that it's 
            //just a container for public class-level variables.  It's a simple container that asp.net will have no trouble converting into json.  When we return
            //sets of information, it's a good idea to create a custom container class to represent instances (or rows) of that information, and then return an array of those objects.  
            //Keeps everything simple.

            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //if not an admin, return an empty array of User objects.
                return new Question[0];
            }

            //LOGIC: get all the questions and return them!
            DataTable sqlDt = new DataTable("users");

            string sqlConnectString = getConString();
            string sqlSelect = "select question_id, category, question_text, is_active from questions order by question_id asc";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);

            //gonna use this to fill a data table
            MySqlDataAdapter sqlDa = new MySqlDataAdapter(sqlCommand);
            //filling the data table
            sqlConnection.Open();
            sqlDa.Fill(sqlDt);

            //loop through each row in the dataset, creating instances
            //of our container class Question.  Fill each question with
            //data from the rows, then dump them in a list.
            List<Question> questions = new List<Question>();
            for (int i = 0; i < sqlDt.Rows.Count; i++)
            {
                questions.Add(new Question
                {
                    question_id = Convert.ToInt32(sqlDt.Rows[i]["question_id"]),
                    category = sqlDt.Rows[i]["category"].ToString(),
                    question_text = sqlDt.Rows[i]["question_text"].ToString(),
                    is_active = Convert.ToInt32(sqlDt.Rows[i]["is_active"])
                });
            }
            //convert the list of accounts to an array and return!
            return questions.ToArray();
        }

        // This method allows an admin to add a new question
        [WebMethod(EnableSession = true)]
        public bool AddQuestion(string cat, string text, string active)
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //return "Error: You must be an administrator to remove questions.";
                return false;
            }

            string sqlConnectString = getConString();
            //the only thing fancy about this query is SELECT LAST_INSERT_ID() at the end.  All that
            //does is tell mySql server to return the primary key of the last inserted row.
            //!!! activeValue is expected to be a 1 for true and 0 for false !!!
            string sqlInsert = "insert into questions (category, question_text, is_active) " +
                "values(@catValue, @textValue, @activeValue); SELECT LAST_INSERT_ID();";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlInsert, sqlConnection);

            sqlCommand.Parameters.AddWithValue("@catValue", HttpUtility.UrlDecode(cat));
            sqlCommand.Parameters.AddWithValue("@textValue", HttpUtility.UrlDecode(text));
            //activeValue is expected to be a 1 for true and 0 for false
            sqlCommand.Parameters.AddWithValue("@activeValue", HttpUtility.UrlDecode(active));

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
                    //return $"Question added successfully.";
                    return true;
                }
                else
                {
                    //failed message
                    //return $"Error: Failed to add question.";
                    return false;
                }
            }
            catch (Exception e)
            {

                //return "Error: Unable to add question.";
                return false;
            }


        }

        // This method allows an admin to update a question
        [WebMethod(EnableSession = true)]
        public bool UpdateQuestion(string qid, string cat, string text, string active)
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //return "Error: You must be an administrator to update questions.";
                return false;
            }

            string sqlConnectString = getConString();
            //this is a simple update, with parameters to pass in values
            string sqlSelect = "update questions set category = IF(@catValue = '', category, @catValue)," +
                               "question_text = IF(@textValue = '', question_text, @textValue), is_active = IF(@activeValue = '', is_active, @activeValue)" +
                                "where question_id=@questionValue";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);

            sqlCommand.Parameters.AddWithValue("@questionValue", HttpUtility.UrlDecode(qid));
            sqlCommand.Parameters.AddWithValue("@catValue", HttpUtility.UrlDecode(cat));
            sqlCommand.Parameters.AddWithValue("@textValue", HttpUtility.UrlDecode(text));
            sqlCommand.Parameters.AddWithValue("@activeValue", HttpUtility.UrlDecode(active));


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
                    //return $"Question '{qid}' updated successfully.";
                    return true;
                }
                else
                {
                    //failed message
                    //return $"Error: Failed to update question '{qid}'.";
                    return false;
                }
            }
            catch (Exception e)
            {

                //return "Error: Unable to update question.";
                return false;
            }
        }

        // This method allows an admin to remove a question
        [WebMethod(EnableSession = true)]
        public bool RemoveQuestion(string qid)
        {
            string sqlConnectString = getConString();

            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //return "Error: You must be an administrator to remove users.";
                return false;
            }

            //this deletes question from database table
            string sqlDelete = "DELETE FROM questions WHERE question_id=@questionValue";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlDelete, sqlConnection);

            try
            {
                sqlCommand.Parameters.AddWithValue("@questionValue", HttpUtility.UrlDecode(qid));

                sqlConnection.Open();
                int rowsAffected = sqlCommand.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    //return $"Question '{qid}' removed successfully.";
                    return true;
                }
                else
                {
                    //return $"Error: Question '{qid}' not found or could not be removed.";
                    return false;
                }
            }
            catch (Exception e)
            {
                //return "Error: Cound not remove question.";
                return false;
            }

        }

        // This method allows admin to turns off a question
        [WebMethod(EnableSession = true)]
        public bool TurnOffQuestion(string qid)
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //return "Error: You must be an administrator to update questions.";
                return false;
            }

            string sqlConnectString = getConString();
            //this is a simple update, with parameters to pass in values
            string sqlSelect = "update questions set is_active=0 " +
                "where question_id=@questionValue";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);

            sqlCommand.Parameters.AddWithValue("@questionValue", HttpUtility.UrlDecode(qid));


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
                    //return $"Question '{qid}' updated successfully.";
                    return true;
                }
                else
                {
                    //failed message
                    //return $"Error: Failed to update question '{qid}'.";
                    return false;
                }
            }
            catch (Exception e)
            {

                //return "Error: Unable to update question.";
                return false;
            }
        }

        // This method allows admin to turns on a question
        [WebMethod(EnableSession = true)]
        public bool TurnOnQuestion(string qid)
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //return "Error: You must be an administrator to update questions.";
                return false;
            }

            string sqlConnectString = getConString();
            //this is a simple update, with parameters to pass in values
            string sqlSelect = "update questions set is_active=1 " +
                "where question_id=@questionValue";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);

            sqlCommand.Parameters.AddWithValue("@questionValue", HttpUtility.UrlDecode(qid));


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
                    //return $"Question '{qid}' updated successfully.";
                    return true;
                }
                else
                {
                    //failed message
                    //return $"Error: Failed to update question '{qid}'.";
                    return false;
                }
            }
            catch (Exception e)
            {

                //return "Error: Unable to update question.";
                return false;
            }
        }

        // This method allows admin to add a news announcement
        [WebMethod(EnableSession = true)]
        public bool AddNews(string title, string content)
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //return "Error: You must be an administrator to update questions.";
                return false;
            }

            string sqlConnectString = getConString();
            //SQL for inserting data into the 'news' table
            string sqlInsert = "insert into news (title, content, posted_at, is_active) " +
                               "values (@titleValue, @contentValue, @postedAtValue, @isActiveValue)";

            MySqlConnection sqlConnection = null;
            MySqlCommand sqlCommand = null;

            try
            {
                DateTime postedAt = DateTime.Now;
                int isActive = 1;

                sqlConnection = new MySqlConnection(sqlConnectString);
                sqlConnection.Open();

                sqlCommand = new MySqlCommand(sqlInsert, sqlConnection);
                sqlCommand.Parameters.AddWithValue("@titleValue", title);
                sqlCommand.Parameters.AddWithValue("@contentValue", content);
                sqlCommand.Parameters.AddWithValue("@postedAtValue", postedAt);
                sqlCommand.Parameters.AddWithValue("@isActiveValue", isActive);

                int rowsAffected = sqlCommand.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    //return "News announcement added successfully.";
                    return true;
                }
                else
                {
                    //return "Error: Failed to add news announcement.";
                    return false;
                }
            }
            catch (Exception e)
            {
                //return "An error occurred while adding the news announcement.";
                return false;
            }

        }

        // This method allows admin to update news announcements
        [WebMethod(EnableSession = true)]
        public bool UpdateNews(string nid, string title, string content)
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //return "Error: You must be an administrator to update news.";
                return false;
            }

            string sqlConnectString = getConString();
            //this is a simple update, with parameters to pass in values
            string sqlUpdate = "update news set title = IF(@newTitleValue = '', title, @newTitleValue), content = IF(@newContentValue = '', content, @newContentValue) where news_id = @newsIdValue";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlUpdate, sqlConnection);

            sqlCommand.Parameters.AddWithValue("@newsIdValue", nid);
            sqlCommand.Parameters.AddWithValue("@newTitleValue", title);
            sqlCommand.Parameters.AddWithValue("@newContentValue", content);


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
                    //return $"News '{nid}' updated successfully.";
                    return true;
                }
                else
                {
                    //failed message
                    //return $"Error: Failed to update News '{nid}'.";
                    return false;
                }
            }
            catch (Exception e)
            {

                //return "Error: Unable to update News.";
                return false;
            }

        }

        // This method allows admin to remove a news announcement
        [WebMethod(EnableSession = true)]
        public bool RemoveNews(string nid)
        {
            string sqlConnectString = getConString();

            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //return "Error: You must be an administrator to delete news.";
                return false;
            }

            //this deletes news from database table
            string sqlDelete = "DELETE FROM news WHERE news_id=@newsValue";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlDelete, sqlConnection);

            try
            {
                sqlCommand.Parameters.AddWithValue("@newsValue", HttpUtility.UrlDecode(nid));

                sqlConnection.Open();
                int rowsAffected = sqlCommand.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    //return $"Question '{qid}' removed successfully.";
                    return true;
                }
                else
                {
                    //return $"Error: Question '{qid}' not found or could not be removed.";
                    return false;
                }
            }
            catch (Exception e)
            {
                //return "Error: Cound not remove question.";
                return false;
            }

        }

        // This method allows admin to turn off a news announcement
        [WebMethod(EnableSession = true)]
        public bool TurnOffNews(string nid)
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //return "Error: You must be an administrator to turn off news post.";
                return false;
            }

            string sqlConnectString = getConString();
            //this is a simple update, with parameters to pass in values
            string sqlSelect = "update news set is_active=0 " +
                "where news_id=@newsValue";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);

            sqlCommand.Parameters.AddWithValue("@newsValue", HttpUtility.UrlDecode(nid));


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
                    //return $"News post '{nid}' removed successfully.";
                    return true;
                }
                else
                {
                    //failed message
                    //return $"Error: Failed to remove news post '{nid}'.";
                    return false;
                }
            }
            catch (Exception e)
            {

                //return "Error: Unable to remove news post.";
                return false;
            }
        }

        // This method allows admin to turn on a news announcement
        [WebMethod(EnableSession = true)]
        public bool TurnOnNews(string nid)
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //return "Error: You must be an administrator to turn on news post.";
                return false;
            }

            string sqlConnectString = getConString();
            //this is a simple update, with parameters to pass in values
            string sqlSelect = "update news set is_active=1 " +
                "where news_id=@newsValue";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);

            sqlCommand.Parameters.AddWithValue("@newsValue", HttpUtility.UrlDecode(nid));


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
                    //return $"News post '{nid}' activated successfully.";
                    return true;
                }
                else
                {
                    //failed message
                    //return $"Error: Failed to activate news post '{nid}'.";
                    return false;
                }
            }
            catch (Exception e)
            {

                //return "Error: Unable to activate news post.";
                return false;
            }
        }

        // Fix the return type of GetAllNews to match the actual return type (News[] instead of Question[])
        [WebMethod(EnableSession = true)]
        public News[] GetAllNews()
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //if not an admin, return an empty array of News objects.
                return new News[0];
            }

            //LOGIC: get all the news and return them!
            DataTable sqlDt = new DataTable("news");

            string sqlConnectString = getConString();
            string sqlSelect = "select news_id, title, content, posted_at from news order by news_id asc";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);

            //gonna use this to fill a data table
            MySqlDataAdapter sqlDa = new MySqlDataAdapter(sqlCommand);
            //filling the data table
            sqlConnection.Open();
            sqlDa.Fill(sqlDt);

            //loop through each row in the dataset, creating instances
            //of our container class News.  Fill each news with
            //data from the rows, then dump them in a list.
            List<News> news = new List<News>();
            for (int i = 0; i < sqlDt.Rows.Count; i++)
            {
                news.Add(new News
                {
                    news_id = Convert.ToInt32(sqlDt.Rows[i]["news_id"]),
                    title = sqlDt.Rows[i]["title"].ToString(),
                    content = sqlDt.Rows[i]["content"].ToString(),
                    posted_at = Convert.ToDateTime(sqlDt.Rows[i]["posted_at"]),
                });
            }
            //convert the list of news to an array and return!
            return news.ToArray();
        }

        [WebMethod(EnableSession = true)]
        public News[] UserNews()
        {

            //LOGIC: get all the news for users and return them!
            DataTable sqlDt = new DataTable("news");

            string sqlConnectString = getConString();
            string sqlSelect = "select title, content, posted_at from news order by news_id asc";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);

            //gonna use this to fill a data table
            MySqlDataAdapter sqlDa = new MySqlDataAdapter(sqlCommand);
            //filling the data table
            sqlConnection.Open();
            sqlDa.Fill(sqlDt);

            //loop through each row in the dataset, creating instances
            //of our container class News.  Fill each news with
            //data from the rows, then dump them in a list.
            List<News> news = new List<News>();
            for (int i = 0; i < sqlDt.Rows.Count; i++)
            {
                news.Add(new News
                {
                    //news_id = Convert.ToInt32(sqlDt.Rows[i]["news_id"]),
                    title = sqlDt.Rows[i]["title"].ToString(),
                    content = sqlDt.Rows[i]["content"].ToString(),
                    posted_at = Convert.ToDateTime(sqlDt.Rows[i]["posted_at"]),
                });
            }
            //convert the list of news to an array and return!
            return news.ToArray();
        }

        // This method weekly hours worked for the logged in user
        [WebMethod(EnableSession = true)]
        public double WeeklyHours()
        {
            //check if the user is logged in
            if (Session["id"] == null)
            {
                return 0.0;
            }
            //get employee ID from session
            string empid = Session["id"].ToString();

            //get the total seconds worked this week by subtracting clock_in from clock_out
            string sqlConnectString = getConString();
            string sqlSelect = "SELECT SUM(TIMESTAMPDIFF(SECOND, clock_in, clock_out)) AS total_seconds FROM timelogs " +
            "WHERE empid = @empid AND YEARWEEK(clock_in) = YEARWEEK(CURDATE()) AND clock_out IS NOT NULL;";
            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);
            sqlCommand.Parameters.AddWithValue("@empid", HttpUtility.UrlDecode(empid));
            
                sqlConnection.Open();
                object seconds = sqlCommand.ExecuteScalar();
                double totalHours = 0.0;
                sqlConnection.Close();

                //convert seconds worked to hours worked
                double totalSeconds = Convert.ToDouble(seconds);
                totalHours = totalSeconds / 3600.0;
                return totalHours;
                          
                     
        }

        // This method looks up the logged in users department and stores it in the session
        [WebMethod(EnableSession = true)]
        public string GetUserDept()
        {
            //check if the user is logged in
            if (Session["id"] == null)
            {
                return "Error: User not logged in.";
            }
            //get employee ID from session
            string empid = Session["id"].ToString();
            //get the department of the user
            string sqlConnectString = getConString();
            string sqlSelect = "SELECT department FROM users WHERE empid = @empid";
            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);
            sqlCommand.Parameters.AddWithValue("@empid", HttpUtility.UrlDecode(empid));
            try
            {
                sqlConnection.Open();
                object department = sqlCommand.ExecuteScalar();
                sqlConnection.Close();
                if (department != null)
                {
                    Session["department"] = department.ToString();
                    return department.ToString();
                }
                else
                {
                    return "Error: Department not found.";
                }
            }
            catch (Exception e)
            {
                return "Error: Unable to retrieve department.";
            }
        }

        // This method retrieves all the feedback that exists in the database (MINUS the empid)
        [WebMethod(EnableSession = true)]
        public Feedback[] GetAllFeedback()
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //if not an admin, return an empty array of Feedback objects.
                return new Feedback[0];
            }
            //LOGIC: get all the feedback and return them!
            DataTable sqlDt = new DataTable("feedback");
            string sqlConnectString = getConString();
            string sqlSelect = "select feedback_id, question_id, department, category, score, feedback_text, feedback_time, is_resolved from feedback order by feedback_id asc";
            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);
            //gonna use this to fill a data table
            MySqlDataAdapter sqlDa = new MySqlDataAdapter(sqlCommand);
            //filling the data table
            sqlConnection.Open();
            sqlDa.Fill(sqlDt);
            //loop through each row in the dataset, creating instances
            //of our container class Feedback.  Fill each feedback with
            //data from the rows, then dump them in a list.
            List<Feedback> feedbackList = new List<Feedback>();
            foreach (DataRow row in sqlDt.Rows)
            {
                feedbackList.Add(new Feedback
                {
                    feedback_id = row["feedback_id"] != DBNull.Value ? Convert.ToInt32(row["feedback_id"]) : 0,
                    question_id = row["question_id"] != DBNull.Value ? Convert.ToInt32(row["question_id"]) : 0,
                    department = row["department"] != DBNull.Value ? row["department"].ToString() : null,
                    category = row["category"] != DBNull.Value ? row["category"].ToString() : null,
                    score = row["score"] != DBNull.Value ? Convert.ToInt32(row["score"]) : (int?)null,
                    feedback_text = row["feedback_text"] != DBNull.Value ? row["feedback_text"].ToString() : null,
                    feedback_time = (DateTime)(row["feedback_time"] != DBNull.Value ? Convert.ToDateTime(row["feedback_time"]) : (DateTime?)null),
                    is_resolved = row["is_resolved"] != DBNull.Value ? Convert.ToBoolean(row["is_resolved"]) : false
                });
            }
            //convert the list of feedback to an array and return!
            return feedbackList.ToArray();
        }

        // This method allows the logged in user to view their own feedback
        [WebMethod(EnableSession = true)]
        public Feedback[] GetUserFeedback()
        {
            //check if the user is logged in
            if (Session["id"] == null)
            {
                return new Feedback[0];
            }
            //get employee ID from session
            string empid = Session["id"].ToString();
            //LOGIC: get all the feedback for the logged in user and return them!
            DataTable sqlDt = new DataTable("feedback");
            string sqlConnectString = getConString();
            string sqlSelect = "select feedback_id, question_id, empid, department, category, score, feedback_text, feedback_time, is_resolved " +
                               "from feedback where category != 'Stress' order by feedback_id ASC";
            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);
            sqlCommand.Parameters.AddWithValue("@empid", HttpUtility.UrlDecode(empid));
            //gonna use this to fill a data table
            MySqlDataAdapter sqlDa = new MySqlDataAdapter(sqlCommand);
            //filling the data table
            sqlConnection.Open();
            sqlDa.Fill(sqlDt);
            //loop through each row in the dataset, creating instances
            //of our container class Feedback.  Fill each feedback with
            //data from the rows, then dump them in a list.
            List<Feedback> feedbackList = new List<Feedback>();
            foreach (DataRow row in sqlDt.Rows)
            {
                feedbackList.Add(new Feedback
                {
                    feedback_id = row["feedback_id"] != DBNull.Value ? Convert.ToInt32(row["feedback_id"]) : 0,
                    question_id = row["question_id"] != DBNull.Value ? Convert.ToInt32(row["question_id"]) : 0,
                    empid = row["empid"] != DBNull.Value ? Convert.ToInt32(row["empid"]) : (int?)null,
                    department = row["department"] != DBNull.Value ? row["department"].ToString() : null,
                    category = row["category"] != DBNull.Value ? row["category"].ToString() : null,
                    score = row["score"] != DBNull.Value ? Convert.ToInt32(row["score"]) : (int?)null,
                    feedback_text = row["feedback_text"] != DBNull.Value ? row["feedback_text"].ToString() : null,
                    feedback_time = (DateTime)(row["feedback_time"] != DBNull.Value ? Convert.ToDateTime(row["feedback_time"]) : (DateTime?)null),
                    is_resolved = row["is_resolved"] != DBNull.Value ? Convert.ToBoolean(row["is_resolved"]) : false
                });
            }
            //convert the list of feedback to an array and return!
            return feedbackList.ToArray();
        }
        // This method allows admin to resolve feedback
        [WebMethod(EnableSession = true)]
        public bool ResolveFeedback(string fid)
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //return "Error: You must be an administrator to resolve feedback.";
                return false;
            }
            string sqlConnectString = getConString();
            
            //this is a simple update, with parameters to pass in values
            string sqlUpdate = "update feedback set is_resolved=1 " +
                "where feedback_id=@feedbackValue";

            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlUpdate, sqlConnection);

            try
            {
                sqlCommand.Parameters.AddWithValue("@feedbackValue", HttpUtility.UrlDecode(fid));

                sqlConnection.Open();
                int rowsAffected = sqlCommand.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    //return $"Feedback '{fid}' resolved successfully.";
                    return true;
                }
                else
                {
                    //return $"Error: Feedback '{fid}' not found or could not be resolved.";
                    return false;
                }
            }
            catch (Exception e)
            {
                //return "Error: Cound not resolve feedback.";
                return false;
            }
        }

        // This method allow admin to get the average stress score for a department for the current week
        [WebMethod(EnableSession = true)]
        public double GetAvgStressScore(string dept)
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //if not an admin, return 0.0
                return 0.0;
            }

            // with user provided department, get the average stress score for that department for the current week
            string sqlConnectString = getConString();
            string sqlSelect = "select avg(score) as avg_stress_score from feedback " +
                               "where category = 'Stress' AND department = @department " +
                               "and YEARWEEK(feedback_time, 1) = YEARWEEK(CURDATE(), 1);";
            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);
            sqlCommand.Parameters.AddWithValue("@department", HttpUtility.UrlDecode(dept));
            
            sqlConnection.Open();
            object stress = sqlCommand.ExecuteScalar();
            sqlConnection.Close();
            if (stress != DBNull.Value && stress != null)
            {
                return Convert.ToDouble(stress);
            }
            else
            {
                return 0.0;
            }
            
            
        }

        // This method allows admin to get the average stress score for all departments for the current week
        [WebMethod(EnableSession = true)]
        public Stress[] GetDeptStressAveragesForCurrentWeek()
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //if not an admin return an empty array of Stress objects.
                return new Stress[0];
               
            }
            //get employee ID from session
            string empid = Session["id"].ToString();
            //LOGIC: get all the average stress scores for the current week/department and return them!
            DataTable sqlDt = new DataTable("feedback");
            string sqlConnectString = getConString();
            string sqlSelect = "select department, AVG(score) as avg_stress_score from feedback " +
                       "where category = 'Stress' AND YEARWEEK(feedback_time, 1) = YEARWEEK(CURDATE(), 1) " +
                       "group by department;";
            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);
            
            //gonna use this to fill a data table
            MySqlDataAdapter sqlDa = new MySqlDataAdapter(sqlCommand);
            //filling the data table
            sqlConnection.Open();
            sqlDa.Fill(sqlDt);
            //loop through each row in the dataset, creating instances
            //of our container class Stress.  Fill each Stress with
            //data from the rows, then dump them in a list.
            List<Stress> stressList = new List<Stress>();
            foreach (DataRow row in sqlDt.Rows)
            {
                stressList.Add(new Stress
                {
                    department = row["department"] != DBNull.Value ? row["department"].ToString() : null,
                    stress = row["avg_stress_score"] != DBNull.Value ? Convert.ToDouble(row["avg_stress_score"]) : (double?)null,
                    
                });
            }
            //convert the list of Stress to an array and return!
            return stressList.ToArray();
        }

        // This method allows admin to get the average stress score for all departments within the start and end dates
        [WebMethod(EnableSession = true)]
        public Stress[] GetDeptStressAveragesForDateRange(string startDate, string endDate)
        {
            //admin check
            if (Session["isAdmin"] == null || (bool)Session["isAdmin"] == false)
            {
                //if not an admin return an empty array of Stress objects.
                return new Stress[0];

            }

            //get employee ID from session
            string empid = Session["id"].ToString();
            //LOGIC: get all the department average stress scores for the specfied dates and return them!
            DataTable sqlDt = new DataTable("feedback");
            string sqlConnectString = getConString();
            string sqlSelect = "select department, AVG(score) as avg_stress_score from feedback " +
                               "where category = 'Stress' AND feedback_time between @startDate AND @endDate " +
                               "group by department;";
            MySqlConnection sqlConnection = new MySqlConnection(sqlConnectString);
            MySqlCommand sqlCommand = new MySqlCommand(sqlSelect, sqlConnection);
            sqlCommand.Parameters.AddWithValue("@startDate", HttpUtility.UrlDecode(startDate));
            sqlCommand.Parameters.AddWithValue("@endDate", HttpUtility.UrlDecode(endDate));

            //gonna use this to fill a data table
            MySqlDataAdapter sqlDa = new MySqlDataAdapter(sqlCommand);
            //filling the data table
            sqlConnection.Open();
            sqlDa.Fill(sqlDt);
            //loop through each row in the dataset, creating instances
            //of our container class Stress.  Fill each Stress with
            //data from the rows, then dump them in a list.
            List<Stress> stressList = new List<Stress>();
            foreach (DataRow row in sqlDt.Rows)
            {
                stressList.Add(new Stress
                {
                    department = row["department"] != DBNull.Value ? row["department"].ToString() : null,
                    stress = row["avg_stress_score"] != DBNull.Value ? Convert.ToDouble(row["avg_stress_score"]) : (double?)null,
                });
            }
            //convert the list of Stress to an array and return!
            return stressList.ToArray();
        }

    }

}

