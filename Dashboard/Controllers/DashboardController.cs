using Dashboard.Models;
using Dashboard.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.IdentityModel.Tokens;
using Dashboard.Models;
using Dashboard.Components;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using System.ComponentModel;
using System.Net;

namespace Dashboard.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string employeeNik = null, string startDate = null, string endDate = null)
        {
            //var visittoday = LoadDataVisitToday();
            // Ambil daftar NIK karyawan
            var employeeNiks = _context.TABLE_MR
                                   .Select(q => q.NIK)
                                   .Distinct()
                                   .ToList();

            // Kirim daftar NIK ke ViewBag untuk dropdown
            ViewBag.EmployeeNiks = employeeNiks;

            // Jika employeeNik tidak null atau kosong, ambil hasil kuis
            if (!string.IsNullOrEmpty(employeeNik))
            {
                var sqlGetQuizData = @"
                        SELECT s.SCHEDULE_ID, 
                            n.ANAME, 
                            s.EMPLOYEE_NIK, 
                            CONVERT(DATE, n.CREATED_DATE) AS CREATED_DATE, 
                            MAX(CONVERT(DATE, s.ANSWER_DATE)) AS ANSWER_DATE,
                            SUM(s.CORRECT_COUNT) AS TOTAL_CORRECT_COUNT,
                            COUNT(s.IS_ANSWER) - SUM(s.CORRECT_COUNT) AS TOTAL_FALSE_COUNT,
                            SUM(s.IS_ANSWER) AS TOTAL_IS_ANSWER,
                            COUNT(s.IS_ANSWER) AS QUESTIONS,
                            CASE 
                                WHEN COUNT(s.IS_ANSWER) = 0 THEN 0
                                ELSE CAST((SUM(s.CORRECT_COUNT) * 100.0 / COUNT(s.IS_ANSWER)) AS DECIMAL(18,0))
                            END AS SCORE,
                            CASE 
                                WHEN COUNT(s.IS_ANSWER) = 0 THEN 'TIDAK LULUS'
                                ELSE CASE
                                    WHEN (SUM(s.CORRECT_COUNT) * 100.0 / COUNT(s.IS_ANSWER)) >= 85 THEN 'Lulus'
                                    ELSE 'Tidak Lulus'
                                END
                            END AS KETERANGAN
                    FROM TRAINING_TEST_SCHEDULE n
                    JOIN TRAINING_EXAM_QUESTION s ON n.SCHEDULE_ID = s.SCHEDULE_ID
                    WHERE s.EMPLOYEE_NIK = @EmployeeNik
                        AND YEAR(CONVERT(DATE, n.CREATED_DATE)) = YEAR(GETDATE())
                    GROUP BY s.SCHEDULE_ID, 
                                n.ANAME, 
                                s.EMPLOYEE_NIK, 
                                CONVERT(DATE, n.CREATED_DATE);               
                ";

                var sqlGetVisitPlanToday = @"
                    SELECT 
                    NOTES_ID_MOBILE, 
                    DATE_VISIT, 
                    DOCTOR_CODE, 
                    DOCTOR_NAME, 
                    PRACTICE_NAME, 
                    PROD_ID,
                    CASE 
                        WHEN STATUS_VISIT = 1 THEN 'Done' 
                        ELSE 'Planned' 
                    END AS VISIT_STATUS
                FROM 
                    VISITING_JUKUDO_NOTES_MOBILE
                WHERE 
                    PLAN_VISIT = '1' AND MR_NIK = @EmployeeNik AND CAST(DATE_VISIT AS DATE) = CAST(GETDATE() AS DATE)
                ORDER BY 
                    CASE 
                        WHEN STATUS_VISIT = 1 THEN 2 
                        ELSE 1 
                    END, 
                    DATE_VISIT;
                ";

                var sql3 = @"
                    SELECT 
                        NOTES_ID_MOBILE, 
                        DATE_VISIT, 
                        DOCTOR_CODE, 
                        DOCTOR_NAME, 
                        PRACTICE_NAME, 
                        PROD_ID,
                        CASE 
                            WHEN STATUS_VISIT = 1 THEN 'DONE' 
                            ELSE 'PLANNED' 
                        END AS VISIT_STATUS
                    FROM 
                        VISITING_JUKUDO_NOTES_MOBILE
                    WHERE 
                        PLAN_VISIT = '1' 
                        AND MR_NIK = @EmployeeNik 
                        AND CAST(DATE_VISIT AS DATE) > CAST(GETDATE() AS DATE)
                    ORDER BY
                        ABS(DATEDIFF(DAY, GETDATE(), DATE_VISIT)) ASC;
                ";

                var sqlGetVisitData = @"
                WITH LatestVisits AS (
                SELECT 
                    MR_NIK,
                    CONVERT(DATE, ADATE, 101) AS TANGGAL,
                    COUNT(CASE WHEN DOCTOR_CLASS = 'A' THEN 1 END) AS TOTAL_VISIT_GRADE_A,
                    COUNT(CASE WHEN DOCTOR_CLASS = 'B' THEN 1 END) AS TOTAL_VISIT_GRADE_B,
                    COUNT(CASE WHEN DOCTOR_CLASS = 'C' THEN 1 END) AS TOTAL_VISIT_GRADE_C,
                    ROW_NUMBER() OVER (ORDER BY CONVERT(DATE, ADATE, 101) DESC) AS rn
                FROM 
                    VISITING_JUKUDO_NOTES
                WHERE 
                    VISIT = '1' 
                    AND MR_NIK = @EmployeeNik
                    AND DOCTOR_CLASS IN ('A', 'B', 'C') 
                GROUP BY 
                    MR_NIK, 
                    CONVERT(DATE, ADATE, 101)
            )

            SELECT 
                MR_NIK,
                TANGGAL,
                TOTAL_VISIT_GRADE_A,
                TOTAL_VISIT_GRADE_B,
                TOTAL_VISIT_GRADE_C
            FROM 
                LatestVisits
            WHERE 
                rn <= 10
            ORDER BY 
                TANGGAL ASC;
                ";

                var sqlGetVisitPlanLater = @"
                SELECT 
                    NOTES_ID_MOBILE as notes_id_mobile, 
                    DATE_VISIT, 
                    DOCTOR_CODE, 
                    DOCTOR_NAME, 
                    PRACTICE_NAME, 
                    PROD_ID,
                CASE 
                    WHEN STATUS_VISIT = 1 THEN 'DONE' 
                    ELSE 'PLANNED' 
                END AS VISIT_STATUS
                FROM 
                    VISITING_JUKUDO_NOTES_MOBILE
                WHERE 
                    PLAN_VISIT = '1' 
                    AND MR_NIK = @EmployeeNik
                    AND CAST(DATE_VISIT AS DATE) BETWEEN @StartDate AND @EndDate
                ORDER BY
                    ABS(DATEDIFF(DAY, GETDATE(), DATE_VISIT)) ASC;
                ";

                var sqlGetVisitTarget = @"
                DECLARE @CurrentMonth NVARCHAR(20) = FORMAT(GETDATE(), 'MMMM', 'id-ID');
                DECLARE @MR_NIK NVARCHAR(20) = @EmployeeNik; -- Variabel untuk MR_NIK

                SELECT
                    b.MR_NIK,
                    a.MONTH,
                    a.WORKDAY,
                    ((a.WORKDAY - 2) * 10) AS VISIT_TARGET,
                    (SELECT COUNT(b2.NOTES_ID) 
                     FROM VISITING_JUKUDO_NOTES b2
                     WHERE b2.MR_NIK = @MR_NIK -- Menggunakan variabel
                       AND MONTH(b2.ADATE) = MONTH(GETDATE())
                       AND b2.VISIT = '1') AS VISIT_COUNT, -- Menambahkan filter VISIT = '1'
                    -- Menghitung Achievement Rate
                    CASE 
                        WHEN ((a.WORKDAY - 2) * 10) > 0 THEN
                            CAST((SELECT COUNT(b3.NOTES_ID) 
                                  FROM VISITING_JUKUDO_NOTES b3
                                  WHERE b3.MR_NIK = @MR_NIK -- Menggunakan variabel
                                    AND MONTH(b3.ADATE) = MONTH(GETDATE())
                                    AND b3.VISIT = '1') AS FLOAT)  -- Menambahkan filter VISIT = '1'
                            / ((a.WORKDAY - 2) * 10) * 100
                        ELSE 0
                    END AS ACHIEVEMENT_RATE,
                    -- Menambahkan kolom NEED_TO_VISIT
                    CASE 
                        WHEN ((a.WORKDAY - 2) * 10) - 
                             (SELECT COUNT(b2.NOTES_ID) 
                              FROM VISITING_JUKUDO_NOTES b2
                              WHERE b2.MR_NIK = @MR_NIK 
                                AND MONTH(b2.ADATE) = MONTH(GETDATE())
                                AND b2.VISIT = '1') < 0  -- Menambahkan filter VISIT = '1'
                        THEN 0
                        ELSE ((a.WORKDAY - 2) * 10) - 
                             (SELECT COUNT(b2.NOTES_ID) 
                              FROM VISITING_JUKUDO_NOTES b2
                              WHERE b2.MR_NIK = @MR_NIK 
                                AND MONTH(b2.ADATE) = MONTH(GETDATE())
                                AND b2.VISIT = '1')  -- Menambahkan filter VISIT = '1'
                    END AS NEED_TO_VISIT
                FROM 
                    TABLE_WORKDAY a
                LEFT JOIN 
                    VISITING_JUKUDO_NOTES b ON b.MR_NIK = @MR_NIK -- Menggunakan variabel
                WHERE 
                    a.MONTH = @CurrentMonth
                GROUP BY 
                    a.MONTH, 
                    a.WORKDAY,
                    b.MR_NIK; -- Masukkan b.MR_NIK ke dalam GROUP BY
                ";

                //var filter = "WHERE PLAN_VISIT = '1' ";
                //if (blabla) 
                //{
                //    filter = filter + "AND MR_NIK = @EmployeeNik ";
                //}
                //if ()
                //{

                //}
                //var sqlcontoh = @"
                //    SELECT 
                //        NOTES_ID_MOBILE, 
                //        DATE_VISIT, 
                //        DOCTOR_CODE, 
                //        DOCTOR_NAME, 
                //        PRACTICE_NAME, 
                //        PROD_ID,
                //        CASE 
                //            WHEN STATUS_VISIT = 1 THEN 'DONE' 
                //            ELSE 'PLANNED' 
                //        END AS VISIT_STATUS
                //    FROM 
                //        VISITING_JUKUDO_NOTES_MOBILE
                //    " + filter + " ";
                //    //WHERE 
                    //    PLAN_VISIT = '1' 
                    //    AND MR_NIK = @EmployeeNik 
                    //    AND CAST(DATE_VISIT AS DATE) > CAST(GETDATE() AS DATE)
                    //ORDER BY
                    //    ABS(DATEDIFF(DAY, GETDATE(), DATE_VISIT)) ASC;
                

                // Membuka koneksi dan mengeksekusi perintah
                try
                {
                    //var list = StoredProcedureExecutor.ExecuteQueryList<VisitTodayReponse>(_context, sql5);
                    var visitTarget = new List<dynamic>();
                    var tests = new List<dynamic>();
                    var visitToday = new List<dynamic>();
                    var visitLater = new List<dynamic>();
                    var actualVisit = new List<dynamic>();

                    await using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = sqlGetQuizData;
                        command.CommandType = System.Data.CommandType.Text;
                        command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));

                        _context.Database.OpenConnection();

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var testResult = new
                                {
                                    Name = reader["ANAME"].ToString(),
                                    CreatedDate = Convert.ToDateTime(reader["CREATED_DATE"]).ToString("yyyy-MM-dd"),
                                    AnswerDate = reader["ANSWER_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["ANSWER_DATE"]).ToString("yyyy-MM-dd") : null,
                                    CorrectAnswers = Convert.ToInt32(reader["TOTAL_CORRECT_COUNT"]),
                                    FalseAnswers = Convert.ToInt32(reader["TOTAL_FALSE_COUNT"]),
                                    TotalQuestions = Convert.ToInt32(reader["QUESTIONS"]),
                                    Score = Convert.ToInt32(reader["SCORE"]),
                                    Remarks = reader["KETERANGAN"].ToString()
                                };

                                tests.Add(testResult);
                            }
                        }

                        command.Parameters.Clear();
                        command.CommandText = sqlGetVisitPlanToday;
                        command.CommandType = System.Data.CommandType.Text;
                        command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var visittoday = new
                                {
                                    NotesIdMobile = Convert.ToInt32(reader["NOTES_ID_MOBILE"]),
                                    DateVisit = Convert.ToDateTime(reader["DATE_VISIT"]).ToString("yyyy-MM-dd"),
                                    DoctorCode = reader["DOCTOR_CODE"].ToString(),
                                    DoctorName = reader["DOCTOR_NAME"].ToString(),
                                    PracticeName = reader["PRACTICE_NAME"].ToString(),
                                    ProdID = reader["PROD_ID"].ToString(),
                                    VisitStatus = reader["VISIT_STATUS"].ToString(),
                                };

                                visitToday.Add(visittoday);
                            }
                        }

                        if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
                        {
                            command.Parameters.Clear();
                            command.CommandText = sqlGetVisitPlanLater;
                            command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));
                            command.Parameters.Add(new SqlParameter("@StartDate", startDate));
                            command.Parameters.Add(new SqlParameter("@EndDate", endDate));
                        }
                        else
                        {

                            DateTime defaultStartDate = DateTime.Now.AddDays(2);
                            DateTime defaultEndDate = defaultStartDate.AddMonths(1);

                            string startDateString = defaultStartDate.ToString("yyyy-MM-dd");
                            string endDateString = defaultEndDate.ToString("yyyy-MM-dd");

                            command.Parameters.Clear();
                            command.CommandText = sqlGetVisitPlanLater;
                            command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));
                            command.Parameters.Add(new SqlParameter("@StartDate", startDateString));
                            command.Parameters.Add(new SqlParameter("@EndDate", endDateString));
                        }


                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var visitlater = new
                                {
                                    NotesIdMobile = Convert.ToInt32(reader["NOTES_ID_MOBILE"]),
                                    DateVisit = Convert.ToDateTime(reader["DATE_VISIT"]).ToString("yyyy-MM-dd"),
                                    DoctorCode = reader["DOCTOR_CODE"].ToString(),
                                    DoctorName = reader["DOCTOR_NAME"].ToString(),
                                    PracticeName = reader["PRACTICE_NAME"].ToString(),
                                    ProdID = reader["PROD_ID"].ToString(),
                                    VisitStatus = reader["VISIT_STATUS"].ToString(),
                                };

                                visitLater.Add(visitlater);
                            }
                        }

                        command.Parameters.Clear();
                        command.CommandText = sqlGetVisitData;
                        command.CommandType = System.Data.CommandType.Text;
                        command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var actualvisit = new
                                {
                                    MrNik = Convert.ToInt32(reader["MR_NIK"]),
                                    TanggalVisit = Convert.ToDateTime(reader["TANGGAL"]).ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("id-ID")),
                                    TotalVisitA = Convert.ToInt32(reader["TOTAL_VISIT_GRADE_A"]),
                                    TotalVisitB = Convert.ToInt32(reader["TOTAL_VISIT_GRADE_B"]),
                                    TotalVisitC = Convert.ToInt32(reader["TOTAL_VISIT_GRADE_C"]),
                                };

                                actualVisit.Add(actualvisit);
                            }
                        }

                        command.Parameters.Clear();
                        command.CommandText = sqlGetVisitTarget;
                        command.CommandType = System.Data.CommandType.Text;
                        command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var visittarget = new
                                {
                                    VisitTarget = Convert.ToInt32(reader["VISIT_TARGET"]),
                                    VisitCount = Convert.ToInt32(reader["VISIT_COUNT"]),
                                    AchievementRate = Convert.ToDecimal(reader["ACHIEVEMENT_RATE"]),
                                    NeedToVisit = Convert.ToInt32(reader["NEED_TO_VISIT"])
                                };


                                visitTarget.Add(visittarget);
                            }
                        }

                        return Json
                            (
                                new 
                                { 
                                    TestResults = tests,
                                    VisitToday = visitToday,
                                    VisitLater = visitLater,
                                    ActualVisit = actualVisit,
                                    VisitTarget = visitTarget,
                                }
                            );
                    }
                }
                catch (Exception ex)
                {
                    // Log exception (misalnya dengan logging framework) dan tampilkan error page
                    // LogError(ex); 
                    return StatusCode(500, "Internal server error. Please try again later.");
                }
                finally
                {
                    _context.Database.CloseConnection();
                }
            }

            // Jika employeeNik tidak diset, hanya tampilkan daftar NIK
            return View();
        }

        public async Task<IActionResult> DashboardQuiz(string employeeNik = null, string startDate = null, string endDate = null)
        {
            // Ambil daftar NIK karyawan
            var employeeNiks = _context.TABLE_MR
                                   .Select(q => q.NIK)
                                   .Distinct()
                                   .ToList();

            // Kirim daftar NIK ke ViewBag untuk dropdown
            ViewBag.EmployeeNiks = employeeNiks;

            if (!string.IsNullOrEmpty(employeeNik))
            {
                var sql = @"
                SELECT TOP 12 
                       s.SCHEDULE_ID, 
                       n.ANAME, 
                       s.EMPLOYEE_NIK, 
                       CONVERT(DATE, n.CREATED_DATE) AS CREATED_DATE, 
                       MAX(CONVERT(DATE, s.ANSWER_DATE)) AS ANSWER_DATE,
                       SUM(s.CORRECT_COUNT) AS TOTAL_CORRECT_COUNT,
                       COUNT(s.IS_ANSWER) - SUM(s.CORRECT_COUNT) AS TOTAL_FALSE_COUNT,
                       SUM(s.IS_ANSWER) AS TOTAL_IS_ANSWER,
                       COUNT(s.IS_ANSWER) AS QUESTIONS,
                       CASE 
                           WHEN COUNT(s.IS_ANSWER) = 0 THEN 0
                           ELSE CAST((SUM(s.CORRECT_COUNT) * 100.0 / COUNT(s.IS_ANSWER)) AS DECIMAL(18,0))
                       END AS SCORE,
                       CASE 
                           WHEN COUNT(s.IS_ANSWER) = 0 THEN 'TIDAK LULUS'
                           ELSE CASE
                               WHEN (SUM(s.CORRECT_COUNT) * 100.0 / COUNT(s.IS_ANSWER)) >= 85 THEN 'Lulus'
                               ELSE 'Tidak Lulus'
                           END
                       END AS KETERANGAN
                FROM TRAINING_TEST_SCHEDULE n
                JOIN TRAINING_EXAM_QUESTION s ON n.SCHEDULE_ID = s.SCHEDULE_ID
                WHERE s.EMPLOYEE_NIK = '1003027'
                AND CONVERT(DATE, n.CREATED_DATE) BETWEEN '2024-01-01' AND '2024-12-31'
                GROUP BY s.SCHEDULE_ID, 
                         n.ANAME, 
                         s.EMPLOYEE_NIK, 
                         CONVERT(DATE, n.CREATED_DATE)
                ORDER BY CREATED_DATE ASC;
                ";

                try
                {
                    var tests = new List<dynamic>();

                    // Using a raw SQL command to query the database
                    await using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {

                        if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
                        {
                            command.CommandText = sql;
                            command.CommandType = System.Data.CommandType.Text;
                            command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));
                            command.Parameters.Add(new SqlParameter("@StartDate", startDate));
                            command.Parameters.Add(new SqlParameter("@EndDate", endDate));
                        }
                        else
                        {

                            int year = DateTime.Now.Year;

                            DateTime defaultStartDate = new DateTime(year, 1, 1);
                            DateTime defaultEndDate = new DateTime(year, 12, 31);

                            string startDateString = defaultStartDate.ToString("yyyy-MM-dd");
                            string endDateString = defaultEndDate.ToString("yyyy-MM-dd");

                            command.CommandText = sql;
                            command.CommandType = System.Data.CommandType.Text;
                            command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));
                            command.Parameters.Add(new SqlParameter("@StartDate", startDateString));
                            command.Parameters.Add(new SqlParameter("@EndDate", endDateString));
                        }

                        _context.Database.OpenConnection();

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var testResult = new
                                {
                                    Name = reader["ANAME"].ToString(),
                                    CreatedDate = Convert.ToDateTime(reader["CREATED_DATE"]).ToString("yyyy-MM-dd"),
                                    AnswerDate = reader["ANSWER_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["ANSWER_DATE"]).ToString("yyyy-MM-dd") : null,
                                    CorrectAnswers = Convert.ToInt32(reader["TOTAL_CORRECT_COUNT"]),
                                    FalseAnswers = Convert.ToInt32(reader["TOTAL_FALSE_COUNT"]),
                                    TotalQuestions = Convert.ToInt32(reader["QUESTIONS"]),
                                    Score = Convert.ToInt32(reader["SCORE"]),
                                    Remarks = reader["KETERANGAN"].ToString()
                                };

                                tests.Add(testResult);
                            }
                        }
                    }

                    return Json(tests);
                }
                catch (Exception ex)
                {
                    // Log the exception (you can use a logging framework here)
                    // LogError(ex);
                    return StatusCode(500, "Internal server error. Please try again later.");
                }
                finally
                {
                    if (_context.Database.GetDbConnection().State == System.Data.ConnectionState.Open)
                    {
                        _context.Database.GetDbConnection().Close();
                    }
                }
            }
            // Jika employeeNik tidak diset, hanya tampilkan daftar NIK
            return View();
        }

        public async Task<IActionResult> DashboardVisit(string employeeNik = null)
        {
            // Ambil daftar NIK karyawan
            var employeeNiks = _context.TABLE_MR
                                   .Select(q => q.NIK)
                                   .Distinct()
                                   .ToList();

            // Kirim daftar NIK ke ViewBag untuk dropdown
            ViewBag.EmployeeNiks = employeeNiks;

            if (!string.IsNullOrEmpty(employeeNik))
            {
                var sqlVisitTarget = @"
                DECLARE @CurrentMonth NVARCHAR(20) = FORMAT(GETDATE(), 'MMMM', 'id-ID');
                DECLARE @CurrentYear INT = 2024 -- Variabel untuk tahun saat ini
                DECLARE @MR_NIK NVARCHAR(20) = @EmployeeNik; -- Variabel untuk MR_NIK

                -- Variabel untuk bulan dan tahun yang diinginkan
                DECLARE @TargetMonth INT = 9; -- Ganti dengan bulan yang diinginkan
                DECLARE @TargetYear INT = @CurrentYear; -- Ganti dengan tahun yang diinginkan

                SELECT
                    b.MR_NIK,
                    a.MONTH,
                    a.WORKDAY,
                    ((a.WORKDAY - 2) * 10) AS VISIT_TARGET,
                    (SELECT COUNT(b2.NOTES_ID) 
                     FROM VISITING_JUKUDO_NOTES b2
                     WHERE b2.MR_NIK = @MR_NIK -- Menggunakan variabel
                       AND MONTH(b2.ADATE) = @TargetMonth
                       AND YEAR(b2.ADATE) = @TargetYear
                       AND b2.VISIT = '1') AS VISIT_COUNT, -- Menambahkan filter VISIT = '1'
                    -- Menghitung Achievement Rate
                    CASE 
                        WHEN ((a.WORKDAY - 2) * 10) > 0 THEN
                            CAST((SELECT COUNT(b3.NOTES_ID) 
                                  FROM VISITING_JUKUDO_NOTES b3
                                  WHERE b3.MR_NIK = @MR_NIK -- Menggunakan variabel
                                    AND MONTH(b3.ADATE) = @TargetMonth
                                    AND YEAR(b3.ADATE) = @TargetYear
                                    AND b3.VISIT = '1') AS FLOAT)  -- Menambahkan filter VISIT = '1'
                            / ((a.WORKDAY - 2) * 10) * 100
                        ELSE 0
                    END AS ACHIEVEMENT_RATE,
                    -- Menambahkan kolom NEED_TO_VISIT
                    CASE 
                        WHEN ((a.WORKDAY - 2) * 10) - 
                             (SELECT COUNT(b2.NOTES_ID) 
                              FROM VISITING_JUKUDO_NOTES b2
                              WHERE b2.MR_NIK = @MR_NIK 
                                AND MONTH(b2.ADATE) = @TargetMonth
                                AND YEAR(b2.ADATE) = @TargetYear
                                AND b2.VISIT = '1') < 0  -- Menambahkan filter VISIT = '1'
                        THEN 0
                        ELSE ((a.WORKDAY - 2) * 10) - 
                             (SELECT COUNT(b2.NOTES_ID) 
                              FROM VISITING_JUKUDO_NOTES b2
                              WHERE b2.MR_NIK = @MR_NIK 
                                AND MONTH(b2.ADATE) = @TargetMonth
                                AND YEAR(b2.ADATE) = @TargetYear
                                AND b2.VISIT = '1')  -- Menambahkan filter VISIT = '1'
                    END AS NEED_TO_VISIT
                FROM 
                    TABLE_WORKDAY a
                LEFT JOIN 
                    VISITING_JUKUDO_NOTES b ON b.MR_NIK = @MR_NIK -- Menggunakan variabel
                WHERE 
                    a.MONTH = 'September'
                GROUP BY 
                    a.MONTH, 
                    a.WORKDAY,
                    b.MR_NIK; -- Masukkan b.MR_NIK ke dalam GROUP BY
                ";

                var sqlVisitCoverage = @"
                
                DECLARE @MR_NIK VARCHAR(50) = @EmployeeNik; -- Ganti dengan NIK yang diinginkan
                DECLARE @Month INT = 9; -- Ganti dengan bulan yang diinginkan
                DECLARE @Year INT = 2024; -- Ganti dengan tahun yang diinginkan

                SELECT 
                    MR.NIK, 
                    MR.MR_RESPOSIBLE, 
                    COUNT(DISTINCT MCL.ID) AS MCL, 
                    (SELECT COUNT(DISTINCT VJN.DOCTOR_CODE) 
                     FROM VISITING_JUKUDO_NOTES VJN 
                     WHERE VJN.MR_NIK = @MR_NIK 
                     AND MONTH(VJN.ADATE) = @Month 
                     AND YEAR(VJN.ADATE) = @Year 
                     AND VJN.VISIT = '1') AS VISITED,  
                    CAST(
                        (SELECT COUNT(DISTINCT VJN.DOCTOR_CODE) 
                         FROM VISITING_JUKUDO_NOTES VJN 
                         WHERE VJN.MR_NIK = @MR_NIK 
                         AND MONTH(VJN.ADATE) = @Month 
                         AND YEAR(VJN.ADATE) = @Year 
                         AND VJN.VISIT = '1') AS FLOAT
                    ) / NULLIF(COUNT(DISTINCT MCL.ID), 0) * 100 AS COVERAGE,
                    NULLIF(COUNT(DISTINCT MCL.ID), 0) - CAST(
                        (SELECT COUNT(DISTINCT VJN.DOCTOR_CODE) 
                         FROM VISITING_JUKUDO_NOTES VJN 
                         WHERE VJN.MR_NIK = @MR_NIK 
                         AND MONTH(VJN.ADATE) = @Month 
                         AND YEAR(VJN.ADATE) = @Year 
                         AND VJN.VISIT = '1') AS FLOAT
                    ) AS NEED_TO_VISIT
                FROM 
                    TABLE_MR MR
                JOIN 
                    TABLE_MCL MCL 
                ON 
                    MR.MR_RESPOSIBLE = MCL.Fieldforce
                LEFT JOIN 
                    VISITING_JUKUDO_NOTES VJN
                ON 
                    MCL.ID = VJN.DOCTOR_CODE 
                    AND MONTH(VJN.ADATE) = @Month
                    AND YEAR(VJN.ADATE) = @Year
                    AND VJN.VISIT = '1'
                WHERE 
                    MR.NIK = @MR_NIK
                GROUP BY 
                    MR.NIK, 
                    MR.MR_RESPOSIBLE;

                ";

                var sqlVisitTargetByClass = @"

                DECLARE @MR_NIK NVARCHAR(50);
                DECLARE @Month INT;
                DECLARE @Year INT;

                -- Set nilai variabel
                SET @MR_NIK = @EmployeeNik;
                SET @Month = 9;
                SET @Year = 2024;

                SELECT 
                    MR.NIK, 
                    MR.MR_RESPOSIBLE, 
                    MCL.CLASS, 
                    COUNT(DISTINCT MCL.ID) AS DOCTOR_COUNT, 
                    CASE 
                        WHEN MCL.CLASS = 'A' THEN COUNT(DISTINCT MCL.ID) * 3
                        WHEN MCL.CLASS = 'B' THEN COUNT(DISTINCT MCL.ID) * 2
                        WHEN MCL.CLASS = 'C' THEN COUNT(DISTINCT MCL.ID) * 1
                    END AS TARGET_VISIT,
                    (SELECT COUNT(DOCTOR_CLASS) 
                     FROM VISITING_JUKUDO_NOTES 
                     WHERE MR_NIK = @MR_NIK 
                       AND MONTH(ADATE) = @Month 
                       AND YEAR(ADATE) = @Year 
                       AND VISIT = '1' 
                       AND DOCTOR_CLASS = MCL.CLASS) AS VISITED,
                    -- Perhitungan Progress dengan pembagian float
                    ROUND(
                        CASE 
                            -- Jika TARGET_VISIT adalah 0, return 0 untuk menghindari pembagian dengan nol
                            WHEN (CASE 
                                     WHEN MCL.CLASS = 'A' THEN COUNT(DISTINCT MCL.ID) * 3
                                     WHEN MCL.CLASS = 'B' THEN COUNT(DISTINCT MCL.ID) * 2
                                     WHEN MCL.CLASS = 'C' THEN COUNT(DISTINCT MCL.ID) * 1
                                 END) = 0 
                            THEN 0 
                            ELSE 
                                -- Pastikan salah satu nilai dalam pembagian adalah decimal/float
                                CAST((SELECT COUNT(DOCTOR_CLASS) 
                                      FROM VISITING_JUKUDO_NOTES 
                                      WHERE MR_NIK = @MR_NIK 
                                        AND MONTH(ADATE) = @Month 
                                        AND YEAR(ADATE) = @Year 
                                        AND VISIT = '1' 
                                        AND DOCTOR_CLASS = MCL.CLASS) AS FLOAT) 
                                * 100.0 / 
                                CAST((CASE 
                                         WHEN MCL.CLASS = 'A' THEN COUNT(DISTINCT MCL.ID) * 3
                                         WHEN MCL.CLASS = 'B' THEN COUNT(DISTINCT MCL.ID) * 2
                                         WHEN MCL.CLASS = 'C' THEN COUNT(DISTINCT MCL.ID) * 1
                                     END) AS FLOAT)
                        END, 2) AS PROGRESS,
                    CASE 
                        WHEN MCL.CLASS = 'A' THEN 
                            CASE WHEN COUNT(DISTINCT MCL.ID) * 3 - (SELECT COUNT(DOCTOR_CLASS) 
                                                                       FROM VISITING_JUKUDO_NOTES 
                                                                       WHERE MR_NIK = @MR_NIK 
                                                                         AND MONTH(ADATE) = @Month 
                                                                         AND YEAR(ADATE) = @Year 
                                                                         AND VISIT = '1' 
                                                                         AND DOCTOR_CLASS = 'A') < 0 
                                THEN 0 
                                ELSE COUNT(DISTINCT MCL.ID) * 3 - (SELECT COUNT(DOCTOR_CLASS) 
                                                                     FROM VISITING_JUKUDO_NOTES 
                                                                     WHERE MR_NIK = @MR_NIK 
                                                                       AND MONTH(ADATE) = @Month 
                                                                       AND YEAR(ADATE) = @Year 
                                                                       AND VISIT = '1' 
                                                                       AND DOCTOR_CLASS = 'A') 
                            END
                        WHEN MCL.CLASS = 'B' THEN 
                            CASE WHEN COUNT(DISTINCT MCL.ID) * 2 - (SELECT COUNT(DOCTOR_CLASS) 
                                                                       FROM VISITING_JUKUDO_NOTES 
                                                                       WHERE MR_NIK = @MR_NIK 
                                                                         AND MONTH(ADATE) = @Month 
                                                                         AND YEAR(ADATE) = @Year 
                                                                         AND VISIT = '1' 
                                                                         AND DOCTOR_CLASS = 'B') < 0 
                                THEN 0 
                                ELSE COUNT(DISTINCT MCL.ID) * 2 - (SELECT COUNT(DOCTOR_CLASS) 
                                                                     FROM VISITING_JUKUDO_NOTES 
                                                                     WHERE MR_NIK = @MR_NIK 
                                                                       AND MONTH(ADATE) = @Month 
                                                                       AND YEAR(ADATE) = @Year 
                                                                       AND VISIT = '1' 
                                                                       AND DOCTOR_CLASS = 'B') 
                            END
                        WHEN MCL.CLASS = 'C' THEN 
                            CASE WHEN COUNT(DISTINCT MCL.ID) * 1 - (SELECT COUNT(DOCTOR_CLASS) 
                                                                       FROM VISITING_JUKUDO_NOTES 
                                                                       WHERE MR_NIK = @MR_NIK 
                                                                         AND MONTH(ADATE) = @Month 
                                                                         AND YEAR(ADATE) = @Year 
                                                                         AND VISIT = '1' 
                                                                         AND DOCTOR_CLASS = 'C') < 0 
                                THEN 0 
                                ELSE COUNT(DISTINCT MCL.ID) * 1 - (SELECT COUNT(DOCTOR_CLASS) 
                                                                     FROM VISITING_JUKUDO_NOTES 
                                                                     WHERE MR_NIK = @MR_NIK 
                                                                       AND MONTH(ADATE) = @Month 
                                                                       AND YEAR(ADATE) = @Year 
                                                                       AND VISIT = '1' 
                                                                       AND DOCTOR_CLASS = 'C') 
                            END
                    END AS NEED_TO_VISIT
                FROM 
                    TABLE_MR MR
                JOIN 
                    TABLE_MCL MCL 
                ON 
                    MR.MR_RESPOSIBLE = MCL.Fieldforce
                LEFT JOIN 
                    VISITING_JUKUDO_NOTES VJN
                ON 
                    MCL.ID = VJN.DOCTOR_CODE 
                    AND MONTH(VJN.ADATE) = @Month
                    AND YEAR(VJN.ADATE) = @Year
                    AND VJN.VISIT = '1'
                WHERE 
                    MR.NIK = @MR_NIK
                GROUP BY 
                    MR.NIK, 
                    MR.MR_RESPOSIBLE,
                    MCL.CLASS;


                ";

                var sqlDataChartTableVisit = @"
                SELECT 
                    a.NOTES_ID, 
                    MIN(a.NOTES_ID_MOBILE) AS NOTES_ID_MOBILE, 
                    MIN(a.MR_NIK) AS MR_NIK, 
                    MIN(b.MR_RESPOSIBLE) AS MR_RESPOSIBLE,
                    MIN(a.USER_ID) AS USER_ID,
                    MIN(a.VISIT) AS VISIT,
                    MIN(a.ADATE) AS ADATE,
                    MIN(a.TIME_CALL) AS TIME_CALL,
                    MIN(a.DOCTOR_CODE) AS DOCTOR_CODE,
                    MIN(CONCAT(c.FirstName, ' ', c.LastName)) AS DOCTOR_NAME,
                    MIN(a.DOCTOR_CLASS) AS DOCTOR_CLASS,
                    MIN(a.JUKUDO_STEP) AS JUKUDO_STEP,
                    MIN(a.PROD_ID) AS PROD_ID,
                    MIN(d.DESCRIPTION) AS PROD_DESC,
                    MIN(e.PRACTICE_NAME) AS PRACTICE_NAME,
                    MIN(c.Address) AS Address,
                    MIN(a.PLAN_VISIT) AS PLAN_VISIT,
                    MIN(a.VISIT_QUALITY) AS VISIT_QUALITY,
                    MIN(e.VISIT_QUALITY) AS VISIT_QUALITY_DESC,
                    MIN(a.ANOTES) AS ANOTES,
                    MIN(a.VISIT_COUNT) AS VISIT_COUNT,
                    MIN(a.ASM_NIK) AS ASM_NIK,
                    MIN(a.TEAM_ID) AS TEAM_ID,
                    MIN(a.MRO_ID) AS MRO_ID
                FROM 
                    VISITING_JUKUDO_NOTES a
                LEFT JOIN 
                    TABLE_MR b ON a.MR_NIK = b.NIK
                LEFT JOIN
                    TABLE_MCL c ON a.DOCTOR_CODE = c.ID
                LEFT JOIN
                    TABLE_PRODUCT d ON a.PROD_ID = d.PROD_ID
                LEFT JOIN
                    VISITING_JUKUDO_NOTES_MOBILE e ON a.NOTES_ID_MOBILE = e.NOTES_ID_MOBILE
                WHERE 
                    a.VISIT = '1' 
                    AND a.MR_NIK = @EmployeeNik 
                    AND MONTH(a.ADATE) = 9 
                    AND YEAR(a.ADATE) = 2024
                GROUP BY 
                    a.NOTES_ID;
                ";

                var sqlDataMcl = @"
                DECLARE @MR_NIK NVARCHAR(50) = @EmployeeNik;
                DECLARE @MONTH_ADATE INT = 9;
                DECLARE @YEAR_ADATE INT = 2024;

                SELECT 
                    MR.NIK,
                    MCL.Fieldforce,
                    MCL.ID,
                    CONCAT(MIN(MCL.FirstName), ' ', MIN(MCL.LastName)) AS DOCTOR_NAME,
                    MIN(MCL.Class) AS Class,
                    (CASE 
                        WHEN MIN(MCL.Class) = 'A' THEN 3
                        WHEN MIN(MCL.Class) = 'B' THEN 2
                        WHEN MIN(MCL.Class) = 'C' THEN 1
                        ELSE 0 -- jika ada Class selain A, B, atau C
                     END) AS TARGET_VISIT,
                    (SELECT COALESCE(COUNT(DISTINCT VJ.NOTES_ID), 0)
                     FROM VISITING_JUKUDO_NOTES VJ
                     WHERE VJ.DOCTOR_CODE = MCL.ID
                       AND VJ.PLAN_VISIT = '1'
                       AND MONTH(VJ.ADATE) = @MONTH_ADATE
                       AND YEAR(VJ.ADATE) = @YEAR_ADATE
                       AND VJ.MR_NIK = @MR_NIK
                       AND VJ.VISIT = '1'
                    ) AS PLANNED_VISIT,
                    (SELECT COALESCE(COUNT(DISTINCT VJ.NOTES_ID), 0)
                     FROM VISITING_JUKUDO_NOTES VJ
                     WHERE VJ.DOCTOR_CODE = MCL.ID
                       AND VJ.PLAN_VISIT = '0'
                       AND MONTH(VJ.ADATE) = @MONTH_ADATE
                       AND YEAR(VJ.ADATE) = @YEAR_ADATE
                       AND VJ.MR_NIK = @MR_NIK
                       AND VJ.VISIT = '1'
                    ) AS UNPLANNED_VISIT,
                    (SELECT COALESCE(COUNT(DISTINCT VJ.NOTES_ID), 0)
                     FROM VISITING_JUKUDO_NOTES VJ
                     WHERE VJ.DOCTOR_CODE = MCL.ID
                       AND VJ.VISIT = '1'
                       AND MONTH(VJ.ADATE) = @MONTH_ADATE
                       AND YEAR(VJ.ADATE) = @YEAR_ADATE
                       AND VJ.MR_NIK = @MR_NIK
                    ) AS VISITED,
                    -- Kalkulasi Progress
                    CASE 
                        WHEN (CASE 
                                WHEN MIN(MCL.Class) = 'A' THEN 3
                                WHEN MIN(MCL.Class) = 'B' THEN 2
                                WHEN MIN(MCL.Class) = 'C' THEN 1
                                ELSE 0 
                              END) = 0 THEN '0' -- Jika Target 0, progress 0%
                        ELSE 
                            CASE 
                                WHEN (CAST((SELECT COALESCE(COUNT(DISTINCT VJ.NOTES_ID), 0)
                                           FROM VISITING_JUKUDO_NOTES VJ
                                           WHERE VJ.DOCTOR_CODE = MCL.ID
                                             AND VJ.VISIT = '1'
                                             AND MONTH(VJ.ADATE) = @MONTH_ADATE
                                             AND YEAR(VJ.ADATE) = @YEAR_ADATE
                                             AND VJ.MR_NIK = @MR_NIK
                                        ) AS FLOAT) / 
                                     CAST((CASE 
                                            WHEN MIN(MCL.Class) = 'A' THEN 3
                                            WHEN MIN(MCL.Class) = 'B' THEN 2
                                            WHEN MIN(MCL.Class) = 'C' THEN 1
                                            ELSE 0 
                                          END) AS FLOAT)) > 1 THEN '100' -- Membatasi hasil di atas 100%
                                ELSE 
                                    CAST(ROUND(CAST((SELECT COALESCE(COUNT(DISTINCT VJ.NOTES_ID), 0)
                                           FROM VISITING_JUKUDO_NOTES VJ
                                           WHERE VJ.DOCTOR_CODE = MCL.ID
                                             AND VJ.VISIT = '1'
                                             AND MONTH(VJ.ADATE) = @MONTH_ADATE
                                             AND YEAR(VJ.ADATE) = @YEAR_ADATE
                                             AND VJ.MR_NIK = @MR_NIK
                                        ) AS FLOAT) * 100 / 
                                     CAST((CASE 
                                            WHEN MIN(MCL.Class) = 'A' THEN 3
                                            WHEN MIN(MCL.Class) = 'B' THEN 2
                                            WHEN MIN(MCL.Class) = 'C' THEN 1
                                            ELSE 0 
                                          END) AS FLOAT), 2) AS NVARCHAR) -- Membatasi 2 angka dibelakang koma
                            END 
                    END AS PROGRESS,
                    -- Kalkulasi Need to Visit
                    CASE 
                        WHEN (CASE 
                                WHEN MIN(MCL.Class) = 'A' THEN 3
                                WHEN MIN(MCL.Class) = 'B' THEN 2
                                WHEN MIN(MCL.Class) = 'C' THEN 1
                                ELSE 0 
                              END) - 
                             (SELECT COALESCE(COUNT(DISTINCT VJ.NOTES_ID), 0)
                              FROM VISITING_JUKUDO_NOTES VJ
                              WHERE VJ.DOCTOR_CODE = MCL.ID
                                AND VJ.VISIT = '1'
                                AND MONTH(VJ.ADATE) = @MONTH_ADATE
                                AND YEAR(VJ.ADATE) = @YEAR_ADATE
                                AND VJ.MR_NIK = @MR_NIK
                             ) < 0 THEN 0 -- Jika hasil negatif, set ke 0
                        ELSE 
                            (CASE 
                                WHEN MIN(MCL.Class) IN ('A', 'B', 'C') THEN 
                                    (CASE 
                                        WHEN MIN(MCL.Class) = 'A' THEN 3
                                        WHEN MIN(MCL.Class) = 'B' THEN 2
                                        WHEN MIN(MCL.Class) = 'C' THEN 1
                                     END) - 
                                    (SELECT COALESCE(COUNT(DISTINCT VJ.NOTES_ID), 0)
                                     FROM VISITING_JUKUDO_NOTES VJ
                                     WHERE VJ.DOCTOR_CODE = MCL.ID
                                       AND VJ.VISIT = '1'
                                       AND MONTH(VJ.ADATE) = @MONTH_ADATE
                                       AND YEAR(VJ.ADATE) = @YEAR_ADATE
                                       AND VJ.MR_NIK = @MR_NIK
                                    )
                                ELSE 0
                            END)
                    END AS NEED_TO_VISIT
                FROM 
                    TABLE_MR MR
                JOIN
                    TABLE_MCL MCL ON MR.MR_RESPOSIBLE = MCL.Fieldforce
                WHERE 
                    MR.NIK = @MR_NIK
                GROUP BY 
                    MR.NIK, MCL.Fieldforce, MCL.ID;
                ";

                //var filter = "WHERE PLAN_VISIT = '1' ";
                //if (blabla) 
                //{
                //    filter = filter + "AND MR_NIK = @EmployeeNik ";
                //}
                //if ()
                //{

                //}
                //var sqlcontoh = @"
                //    SELECT 
                //        NOTES_ID_MOBILE, 
                //        DATE_VISIT, 
                //        DOCTOR_CODE, 
                //        DOCTOR_NAME, 
                //        PRACTICE_NAME, 
                //        PROD_ID,
                //        CASE 
                //            WHEN STATUS_VISIT = 1 THEN 'DONE' 
                //            ELSE 'PLANNED' 
                //        END AS VISIT_STATUS
                //    FROM 
                //        VISITING_JUKUDO_NOTES_MOBILE
                //    " + filter + " ";
                //    //WHERE 
                //    PLAN_VISIT = '1' 
                //    AND MR_NIK = @EmployeeNik 
                //    AND CAST(DATE_VISIT AS DATE) > CAST(GETDATE() AS DATE)
                //ORDER BY
                //    ABS(DATEDIFF(DAY, GETDATE(), DATE_VISIT)) ASC;


                // Membuka koneksi dan mengeksekusi perintah
                try
                {
                    //var list = StoredProcedureExecutor.ExecuteQueryList<VisitTodayReponse>(_context, sql5);
                    var visitTarget = new List<dynamic>();
                    var visitCoverage = new List<dynamic>();
                    var visitTargetByClass = new List<dynamic>();
                    var visitChartTableData = new List<dynamic>();
                    var mclTableData = new List<dynamic>();

                    await using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = sqlVisitTarget;
                        command.CommandType = System.Data.CommandType.Text;
                        command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));

                        _context.Database.OpenConnection();

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var visittarget = new
                                {
                                    VisitTarget = Convert.ToInt32(reader["VISIT_TARGET"]),
                                    VisitCount = Convert.ToInt32(reader["VISIT_COUNT"]),
                                    AchievementRate = Convert.ToDecimal(reader["ACHIEVEMENT_RATE"]),
                                    NeedToVisit = Convert.ToInt32(reader["NEED_TO_VISIT"])
                                };

                                visitTarget.Add(visittarget);
                            }
                        }

                        command.Parameters.Clear();
                        command.CommandText = sqlVisitCoverage;
                        command.CommandType = System.Data.CommandType.Text;
                        command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var visitcoverage = new
                                {
                                    Mcl = Convert.ToInt32(reader["MCL"]),
                                    Visited = Convert.ToInt32(reader["VISITED"]),
                                    Coverage = Convert.ToInt32(reader["COVERAGE"]),
                                    NeedToVisit = Convert.ToInt32(reader["NEED_TO_VISIT"])
                                };

                                visitCoverage.Add(visitcoverage);
                            }
                        }

                        command.Parameters.Clear();
                        command.CommandText = sqlVisitTargetByClass;
                        command.CommandType = System.Data.CommandType.Text;
                        command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var visittargetbyclass = new
                                {
                                    Class = reader["CLASS"].ToString(),
                                    DoctorCount = Convert.ToInt32(reader["DOCTOR_COUNT"]),
                                    TargetVisit = Convert.ToInt32(reader["TARGET_VISIT"]),
                                    Visited = Convert.ToInt32(reader["VISITED"]),
                                    NeedToVisit = Convert.ToInt32(reader["NEED_TO_VISIT"]),
                                    Progress = Math.Round(Convert.ToDecimal(reader["PROGRESS"]), 2),
                                };

                                visitTargetByClass.Add(visittargetbyclass);
                            }
                        }

                        command.Parameters.Clear();
                        command.CommandText = sqlDataChartTableVisit;
                        command.CommandType = System.Data.CommandType.Text;
                        command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var visitcharttabledata = new
                                {
                                    NotesId = reader["NOTES_ID"].ToString(),
                                    NotesIdMobile = reader["NOTES_ID_MOBILE"].ToString(),
                                    MrNik = reader["MR_NIK"].ToString(),
                                    MrResponsible = reader["MR_RESPOSIBLE"].ToString(),
                                    UserId = reader["USER_ID"].ToString(),
                                    Visit = reader["VISIT"].ToString(),
                                    Adate = Convert.ToDateTime(reader["ADATE"]).ToString("yyyy-MM-dd"),
                                    TimeCall = reader["TIME_CALL"].ToString(),
                                    DoctorCode = reader["DOCTOR_CODE"].ToString(),
                                    DoctorName = reader["DOCTOR_NAME"].ToString(),
                                    DoctorClass = reader["DOCTOR_CLASS"].ToString(),
                                    JukudoStep = reader["JUKUDO_STEP"].ToString(),
                                    ProdId = reader["PROD_ID"].ToString(),
                                    ProdDesc = reader["PROD_DESC"].ToString(),
                                    PracticeName = reader["PRACTICE_NAME"].ToString(),
                                    Address = reader["Address"].ToString(),
                                    PlanVisit = reader["PLAN_VISIT"].ToString(),
                                    VisitQuality = reader["VISIT_QUALITY"].ToString(),
                                    VisitQualityDesc = reader["VISIT_QUALITY_DESC"].ToString(),
                                    Anotes = reader["ANOTES"].ToString(),
                                    VisitCount = Convert.ToInt32(reader["VISIT_COUNT"]),
                                    AsmNik = reader["ASM_NIK"].ToString(),
                                    TeamId = reader["TEAM_ID"].ToString(),
                                    MroId = reader["MRO_ID"].ToString(),
                            };

                                visitChartTableData.Add(visitcharttabledata);
                            }
                        }

                        command.Parameters.Clear();
                        command.CommandText = sqlDataMcl;
                        command.CommandType = System.Data.CommandType.Text;
                        command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var mcltabledata = new
                                {
                                    Nik = reader["NIK"].ToString(),
                                    Fieldforce = reader["Fieldforce"].ToString(),
                                    Id = reader["ID"].ToString(),
                                    DoctorName = reader["DOCTOR_NAME"].ToString(),
                                    Class = reader["Class"].ToString(),
                                    TargetVisit = Convert.ToInt32(reader["TARGET_VISIT"]),
                                    PlannedVisit = Convert.ToInt32(reader["PLANNED_VISIT"]),
                                    UnplannedVisit = Convert.ToInt32(reader["UNPLANNED_VISIT"]),
                                    Visited = Convert.ToInt32(reader["VISITED"]),
                                    Progress = reader["PROGRESS"].ToString(),
                                    NeedToVisit = Convert.ToInt32(reader["NEED_TO_VISIT"]),
                                };

                                mclTableData.Add(mcltabledata);
                            }
                        }

                        return Json
                            (
                                new
                                {
                                    VisitTarget = visitTarget,
                                    VisitCoverage = visitCoverage,
                                    VisitTargetByClass = visitTargetByClass,
                                    VisitChartTableData = visitChartTableData,
                                    MclTableData = mclTableData,
                                }
                            );
                    }
                }
                catch (Exception ex)
                {
                    // Log exception (misalnya dengan logging framework) dan tampilkan error page
                    // LogError(ex); 
                    return StatusCode(500, "Internal server error. Please try again later.");
                }
                finally
                {
                    _context.Database.CloseConnection();
                }
            }

            return View();
        }

        //public async LoadDataVisitToday() 
        //{

        //}
        //public async LoadDataVisitToday { }

    }
}
