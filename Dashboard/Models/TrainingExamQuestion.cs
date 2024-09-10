using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;

namespace Dashboard.Models;

public partial class TrainingExamQuestion
{

    public double? SCHEDULE_ID { get; set; }

    public string? EMPLOYEE_NIK { get; set; }

    public double? Q_ID { get; set; }

    public double? ATIMER { get; set; }

    public double? ANSWER_ID { get; set; }

    public double? CORRECT_ANSWER { get; set; }

    public double? ASCORE { get; set; }

    public DateTime? ANSWER_DATE { get; set; }

    public double? PASS_TIMES { get; set; }

    public string? CORRECTION { get; set; }

    public double? CORRECT_COUNT { get; set; }

    public DateTime? CREATED_DATE { get; set; }

    public double? AREVIEW { get; set; }

    public double? IS_ANSWER { get; set; }

    public string? USER_ID { get; set; }

    public string? ESSAY_TXT { get; set; }

    public string? MRO_ID { get; set; }

    public double? FIELDFORCE_POS { get; set; }

    public double? POSITION_LEVEL { get; set; }

    public string? REVIEW_DATE { get; set; }

    public string? LAST_SHOW { get; set; }

    public double? AORDER { get; set; }
}
