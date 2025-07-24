using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ProjectTemplate
{
    public class Feedback
    {
        public int feedback_id;
        public int question_id;
        public int? empid;
        public int department;
        public string category;
        public int? score;
        public string feedback_text;
        public DateTime feedback_time;
        
    }
}