using Dashboard.Models;
using Dashboard.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Dashboard.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private readonly ApplicationDbContext _context;


        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
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

        //[HttpPost]
        //public async Task<JsonResult> GetAllEmpNik(string q, string page, int rowPerPage)
        //{
        //    List<DataDropdown> dataDropdown = new List<DataDropdown>();

        //    try
        //    {
        //        dataDropdown = _context.TRAINING_EXAM_QUESTION
        //                           .Select(q => new DataDropdown { id = q.EMPLOYEE_NIK, text = q.EMPLOYEE_NIK})
        //                           .Distinct()
        //                           .ToList();
        //        dataDropdown = dataDropdown.Skip(rowPerPage * (int.Parse(page) - 1)).Take(rowPerPage).ToList();

        //        return Json(dataDropdown);
        //    }
        //    catch (Exception ex)
        //    {
        //        throw;
        //    }
        //}

        public IActionResult Privacy()
        {
            var employeeNiks = _context.TRAINING_EXAM_QUESTION
                                   .Select(q => q.EMPLOYEE_NIK)
                                   .Distinct()
                                   .ToList();

            ViewBag.EmployeeNiks = employeeNiks;

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

    }
}
