using System;
using System.Collections.Generic;

namespace Dashboard.Models;

public partial class TrainingTestSchedule
{
    public double? SCHEDULE_ID { get; set; }

    public string? ANAME { get; set; }

    public DateTime? DATE_START { get; set; }

    public DateTime? DATE_END { get; set; }

    public string? TIME_START { get; set; }

    public string? TIME_END { get; set; }

    public string? TEST_CREATED_BY { get; set; }

    public DateTime? CREATED_DATE { get; set; }

    public string? UPDATE_DATE { get; set; }

    public double? TARGET_AUDIENCE { get; set; }

    public double? ADURATION { get; set; }

    public double? ANSWER_TIME_LIMIT { get; set; }

    public double? QUESTION_NUMBER { get; set; }

    public double? ANSWER_OPTION { get; set; }

    public double? UPDATE_SCHEDULE { get; set; }

    public string? UPDATE_SCHEDULE_DATE  { get; set; }

    public string? UPDATE_SCHEDULE_BY { get; set; }

    public double? MAX_QUESTION_PASS { get; set; }

    public double? MAX_EXAM_REOPEN { get; set; }

    public double? MONTH_NUM { get; set; }

    public double? YEAR_NUM { get; set; }

    public string? CLUSTER_ID { get; set; }

    public double? ANSWER_TIME_LIMIT_Q { get; set; }

    public string? TIMER_BY_QUESTION { get; set; }
}
