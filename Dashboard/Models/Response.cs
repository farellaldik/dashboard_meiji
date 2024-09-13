namespace Dashboard.Models
{
    public class DataDropdown
    {
        public string id { get; set; }
        public string text { get; set; }
    }

    public class ReponseDashboardQuiz
    {
        public int SCHEDULE_ID { get; set; }
        public string ANAME { get; set; }
        public int EMPLOYEE_NIK { get; set; }
        public DateTime CREATED_DATE { get; set; }
        public DateTime ANSWER_DATE { get; set; }
        public int TOTAL_CORRECT_COUNT { get; set; }
        public int QUESTIONS { get; set; }
        public int SCORE { get; set; }
        public string KETERANGAN { get; set; }

    }


    }
