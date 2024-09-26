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
                var sql = @"
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

                var sql2 = @"
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

                var sql4 = @"
                WITH LatestVisits AS (
                    SELECT 
                        MR_NIK,
                        CONVERT(DATE, DATE_VISIT, 101) AS TANGGAL,
                        COUNT(CASE WHEN DOCTOR_CLASS = 'A' THEN 1 END) AS TOTAL_VISIT_GRADE_A,
                        COUNT(CASE WHEN DOCTOR_CLASS = 'B' THEN 1 END) AS TOTAL_VISIT_GRADE_B,
                        COUNT(CASE WHEN DOCTOR_CLASS = 'C' THEN 1 END) AS TOTAL_VISIT_GRADE_C,
                        ROW_NUMBER() OVER (ORDER BY CONVERT(DATE, DATE_VISIT, 101) DESC) AS rn
                    FROM 
                        VISITING_JUKUDO_NOTES_MOBILE 
                    WHERE 
                        STATUS_VISIT = '1' 
                        AND MR_NIK = @EmployeeNik
                        AND DOCTOR_CLASS IN ('A', 'B', 'C') 
                    GROUP BY 
                        MR_NIK, 
                        CONVERT(DATE, DATE_VISIT, 101)
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

                var sql5 = @"
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

                    var tests = new List<dynamic>();
                    var visitToday = new List<dynamic>();
                    var visitLater = new List<dynamic>();
                    var actualVisit = new List<dynamic>();

                    await using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = sql;
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
                        command.CommandText = sql2;
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
                            command.CommandText = sql5;
                            command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));
                            command.Parameters.Add(new SqlParameter("@StartDate", startDate));
                            command.Parameters.Add(new SqlParameter("@EndDate", endDate));
                        }
                        else
                        {
                            command.Parameters.Clear();
                            command.CommandText = sql3;
                            command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));
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
                        command.CommandText = sql4;
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

                        return Json
                            (
                                new 
                                { 
                                    TestResults = tests,
                                    VisitToday = visitToday,
                                    VisitLater = visitLater,
                                    ActualVisit = actualVisit,
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

        public async Task<IActionResult> DashboardQuiz(string employeeNik = null)
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
                GROUP BY s.SCHEDULE_ID, 
                         n.ANAME, 
                         s.EMPLOYEE_NIK, 
                         CONVERT(DATE, n.CREATED_DATE)
                ";

                try
                {
                    var tests = new List<dynamic>();

                    // Using a raw SQL command to query the database
                    await using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = sql;
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

        //public async LoadDataVisitToday() 
        //{

        //}
        //public async LoadDataVisitToday { }

    }
}
