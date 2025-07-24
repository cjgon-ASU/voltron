using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ProjectTemplate
{
    public class Question
    {
        public int qid;
        public string question_text;
        public string answer_text;
        public float scale;
        public int is_active;
        public DateTime created_at;
        public DateTime updated_at;
    }
}  