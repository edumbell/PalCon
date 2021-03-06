﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Palcon.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View("Classic", Palcon.Models.Game.Colours);
        }

        public ActionResult Cowards()
        {
            return View("index", Palcon.Models.Game.Colours);
        }   

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}