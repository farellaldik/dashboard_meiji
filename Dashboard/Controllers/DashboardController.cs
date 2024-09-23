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

namespace Dashboard.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string employeeNik = null)
        {
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
                    WITH Results AS (
                    SELECT s.SCHEDULE_ID, 
                           n.ANAME, 
                           s.EMPLOYEE_NIK, 
                           CONVERT(DATE, n.CREATED_DATE) AS CREATED_DATE, 
                           MAX(CONVERT(DATE, s.ANSWER_DATE)) AS ANSWER_DATE,
                           SUM(s.CORRECT_COUNT) AS TOTAL_CORRECT_COUNT,
                           SUM(s.IS_ANSWER) AS TOTAL_IS_ANSWER,
                           COUNT(s.IS_ANSWER) AS QUESTIONS,
                           CASE 
                               WHEN COUNT(s.IS_ANSWER) = 0 THEN 0
                               ELSE CAST((SUM(s.CORRECT_COUNT) * 100.0 / COUNT(s.IS_ANSWER)) AS DECIMAL (18,0))
                           END AS SCORE,
                           CASE 
                               WHEN COUNT(s.IS_ANSWER) = 0 THEN 'TIDAK LULUS'
                               ELSE CASE
                                   WHEN (SUM(s.CORRECT_COUNT) * 100.0 / COUNT(s.IS_ANSWER)) >= 80 THEN 'LULUS'
                                   ELSE 'TIDAK LULUS'
                               END
                           END AS KETERANGAN
                    FROM TRAINING_TEST_SCHEDULE n
                    JOIN TRAINING_EXAM_QUESTION s ON n.SCHEDULE_ID = s.SCHEDULE_ID
                    GROUP BY s.SCHEDULE_ID, 
                             n.ANAME, 
                             s.EMPLOYEE_NIK, 
                             CONVERT(DATE, n.CREATED_DATE)
                ),
                Grade_Count AS (
                    SELECT v.MR_NIK,
                           SUM(CASE WHEN v.DOCTOR_CLASS = 'A' AND v.VISIT = '1' THEN 1 ELSE 0 END) AS GRADE_A_COUNT,
                           SUM(CASE WHEN v.DOCTOR_CLASS = 'B' AND v.VISIT = '1' THEN 1 ELSE 0 END) AS GRADE_B_COUNT,
                           SUM(CASE WHEN v.DOCTOR_CLASS = 'C' AND v.VISIT = '1' THEN 1 ELSE 0 END) AS GRADE_C_COUNT
                    FROM VISITING_JUKUDO_NOTES v
                    WHERE v.VISIT = '1'
                    GROUP BY v.MR_NIK
                )
                SELECT r.EMPLOYEE_NIK,
                       SUM(CASE WHEN r.KETERANGAN = 'LULUS' THEN 1 ELSE 0 END) AS TOTAL_LULUS,
                       SUM(CASE WHEN r.KETERANGAN = 'TIDAK LULUS' THEN 1 ELSE 0 END) AS TOTAL_TIDAK_LULUS,
                       COALESCE(g.GRADE_A_COUNT, 0) AS GRADE_A_COUNT,
                       COALESCE(g.GRADE_B_COUNT, 0) AS GRADE_B_COUNT,
                       COALESCE(g.GRADE_C_COUNT, 0) AS GRADE_C_COUNT
                FROM Results r
                LEFT JOIN Grade_Count g ON r.EMPLOYEE_NIK = g.MR_NIK
                WHERE r.EMPLOYEE_NIK = @EmployeeNik
                GROUP BY r.EMPLOYEE_NIK, g.GRADE_A_COUNT, g.GRADE_B_COUNT, g.GRADE_C_COUNT;
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
                    AND MR_NIK = @EmployeeNIk 
                    AND CAST(DATE_VISIT AS DATE) > CAST(GETDATE() AS DATE)
                ORDER BY 
                    CASE 
                        WHEN STATUS_VISIT = 1 THEN 2 
                        ELSE 1 
                    END, 
                    DATE_VISIT;
                ";

                // Membuka koneksi dan mengeksekusi perintah
                try
                {
                    var testResult = new
                    {
                        TotalLulus = 0,
                        TotalTidakLulus = 0,
                        TotalGradeA = 0,
                        TotalGradeB = 0,
                        TotalGradeC = 0
                    };
                    var visitToday = new List<dynamic>();
                    var visitLater = new List<dynamic>();

                    await using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = sql;
                        command.CommandType = System.Data.CommandType.Text;
                        command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));

                        _context.Database.OpenConnection();

                        await using (var result = await command.ExecuteReaderAsync())
                        {
                            if (await result.ReadAsync())
                            {
                                testResult = new
                                {
                                    TotalLulus = result.GetInt32(result.GetOrdinal("TOTAL_LULUS")),
                                    TotalTidakLulus = result.GetInt32(result.GetOrdinal("TOTAL_TIDAK_LULUS")),
                                    TotalGradeA = result.GetInt32(result.GetOrdinal("GRADE_A_COUNT")),
                                    TotalGradeB = result.GetInt32(result.GetOrdinal("GRADE_B_COUNT")),
                                    TotalGradeC = result.GetInt32(result.GetOrdinal("GRADE_C_COUNT")),
                                };
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

                        command.Parameters.Clear();
                        command.CommandText = sql3;
                        command.CommandType = System.Data.CommandType.Text;
                        command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));

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

                        return Json
                            (
                                new 
                                { 
                                    TestResults = testResult,
                                    VisitToday = visitToday,
                                    VisitLater = visitLater,
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

    }
}
