using Dashboard.Models;
using Dashboard.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var employeeNiks = _context.TRAINING_EXAM_QUESTION
                                   .Select(q => q.EMPLOYEE_NIK)
                                   .Distinct()
                                   .ToList();

            ViewBag.EmployeeNiks = employeeNiks;

            return View();
        }

        //[HttpGet("dashboard/index/results")]
        //public async Task<IActionResult> IndexResults(string employeeNik)
        //{
        //    if (string.IsNullOrEmpty(employeeNik))
        //    {
        //        return BadRequest("Employee Nik is required.");
        //    }

        //    var sql = @"
        //        WITH Results AS (
        //            SELECT s.SCHEDULE_ID, 
        //                   n.ANAME, 
        //                   s.EMPLOYEE_NIK, 
        //                   CONVERT(DATE, n.CREATED_DATE) AS CREATED_DATE, 
        //                   MAX(CONVERT(DATE, s.ANSWER_DATE)) AS ANSWER_DATE,
        //                   SUM(s.CORRECT_COUNT) AS TOTAL_CORRECT_COUNT,
        //                   SUM(s.IS_ANSWER) AS TOTAL_IS_ANSWER,
        //                   COUNT(s.IS_ANSWER) AS QUESTIONS,
        //                   CASE 
        //                       WHEN COUNT(s.IS_ANSWER) = 0 THEN 0
        //                       ELSE CAST((SUM(s.CORRECT_COUNT) * 100.0 / COUNT(s.IS_ANSWER)) AS DECIMAL (18,0))
        //                   END AS SCORE,
        //                   CASE 
        //                       WHEN COUNT(s.IS_ANSWER) = 0 THEN 'TIDAK LULUS'
        //                       ELSE CASE
        //                           WHEN (SUM(s.CORRECT_COUNT) * 100.0 / COUNT(s.IS_ANSWER)) >= 80 THEN 'LULUS'
        //                           ELSE 'TIDAK LULUS'
        //                       END
        //                   END AS KETERANGAN
        //            FROM TRAINING_TEST_SCHEDULE n
        //            JOIN TRAINING_EXAM_QUESTION s ON n.SCHEDULE_ID = s.SCHEDULE_ID
        //            GROUP BY s.SCHEDULE_ID, 
        //                     n.ANAME, 
        //                     s.EMPLOYEE_NIK, 
        //                     CONVERT(DATE, n.CREATED_DATE)
        //        )
        //        SELECT EMPLOYEE_NIK,
        //               SUM(CASE WHEN KETERANGAN = 'LULUS' THEN 1 ELSE 0 END) AS TOTAL_LULUS,
        //               SUM(CASE WHEN KETERANGAN = 'TIDAK LULUS' THEN 1 ELSE 0 END) AS TOTAL_TIDAK_LULUS
        //        FROM Results
        //        WHERE EMPLOYEE_NIK = {0}
        //        GROUP BY EMPLOYEE_NIK;
        //    ";

        //    // Jalankan query dan dapatkan hasilnya
        //    var results = await _context.Set<TestResult>()
        //        .FromSqlRaw(sql, employeeNik)
        //        .AsNoTracking()
        //        .FirstOrDefaultAsync();

        //    if (results == null)
        //    {
        //        return NotFound();
        //    }

        //    return Json(new
        //    {
        //        totalLulus = results.TOTAL_LULUS,
        //        totalTidakLulus = results.TOTAL_TIDAK_LULUS
        //    });
        //}

    }
}
