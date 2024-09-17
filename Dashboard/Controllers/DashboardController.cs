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
            var employeeNiks = _context.TRAINING_EXAM_QUESTION
                                   .Select(q => q.EMPLOYEE_NIK)
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
                    )
                    SELECT EMPLOYEE_NIK,
                           SUM(CASE WHEN KETERANGAN = 'LULUS' THEN 1 ELSE 0 END) AS TOTAL_LULUS,
                           SUM(CASE WHEN KETERANGAN = 'TIDAK LULUS' THEN 1 ELSE 0 END) AS TOTAL_TIDAK_LULUS
                    FROM Results
                    WHERE EMPLOYEE_NIK = @EmployeeNik
                    GROUP BY EMPLOYEE_NIK;
                ";

                // Membuka koneksi dan mengeksekusi perintah
                try
                {
                    using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = sql;
                        command.CommandType = System.Data.CommandType.Text;
                        command.Parameters.Add(new SqlParameter("@EmployeeNik", employeeNik));

                        _context.Database.OpenConnection();

                        using (var result = await command.ExecuteReaderAsync())
                        {
                            if (await result.ReadAsync())
                            {
                                var testResult = new
                                {
                                    TotalLulus = result.GetInt32(result.GetOrdinal("TOTAL_LULUS")),
                                    TotalTidakLulus = result.GetInt32(result.GetOrdinal("TOTAL_TIDAK_LULUS"))
                                };

                                return Json(testResult);
                            }
                            else
                            {
                                // Jika tidak ada hasil ditemukan, kembalikan NotFound
                                return Json(new { TotalLulus = 0, TotalTidakLulus = 0 });
                            }
                        }
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
            var employeeNiks = _context.TRAINING_EXAM_QUESTION
                                   .Select(q => q.EMPLOYEE_NIK)
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
                       SUM(s.IS_ANSWER) AS TOTAL_IS_ANSWER,
                       COUNT(s.IS_ANSWER) AS QUESTIONS,
                       CASE 
                           WHEN COUNT(s.IS_ANSWER) = 0 THEN 0
                           ELSE CAST((SUM(s.CORRECT_COUNT) * 100.0 / COUNT(s.IS_ANSWER)) AS DECIMAL (18,0))
                       END AS SCORE,
                       CASE 
                           WHEN COUNT(s.IS_ANSWER) = 0 THEN 'TIDAK LULUS'
                           ELSE CASE
                               WHEN (SUM(s.CORRECT_COUNT) * 100.0 / COUNT(s.IS_ANSWER)) >= 80 THEN 'Lulus'
                               ELSE 'Tidak Lulus'
                           END
                       END AS KETERANGAN
                FROM TRAINING_TEST_SCHEDULE n
                JOIN TRAINING_EXAM_QUESTION s ON n.SCHEDULE_ID = s.SCHEDULE_ID
                WHERE s.EMPLOYEE_NIK = @EmployeeNik
                GROUP BY s.SCHEDULE_ID, 
                         n.ANAME, 
                         s.EMPLOYEE_NIK, 
                         CONVERT(DATE, n.CREATED_DATE);";

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
