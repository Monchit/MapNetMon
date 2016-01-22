using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DotNet.Highcharts;
using DotNet.Highcharts.Enums;
using DotNet.Highcharts.Helpers;
using DotNet.Highcharts.Options;
using MapNetworkMonitoring.Models;
using System.Drawing;

namespace MapNetworkMonitoring.Controllers
{
    public class ReportController : Controller
    {
        //
        // GET: /Report/
        private MapNetMonEntities db = new MapNetMonEntities();

        public ActionResult GraphDeviceDown()
        {
            ViewBag.SelectPlant = db.TM_Plant;
            return View();
        }

        public ActionResult _GetGraphDeviceDown(DateTime? dtFrom, DateTime? dtTo, byte plant, byte? fac)
        {
            Highcharts chart = new Highcharts("chart")
            .InitChart(new Chart
            {
                Height = 600
                //Options3d = new ChartOptions3d
                //{
                //    Enabled = true,
                //    Alpha = 10,
                //    Beta = 25,
                //    Depth = 70
                //}
                //ZoomType = ZoomTypes.Xy 
            })
            .SetTitle(new Title { Text = "Device Down" })
                //.SetSubtitle(new Subtitle { Text = "By Plant" })
                //.SetXAxis(new XAxis
                //{
                //    Categories = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" },
                //    Title = new XAxisTitle { Text = "Device Name" }
                //})
            .SetYAxis(new YAxis
            {
                Min = 0,//Start at
                Title = new YAxisTitle { Text = "Count" }
            })
            .SetTooltip(new Tooltip { Formatter = @"function() { return ''+ this.x +' Down: '+ this.y +' times.'; }" })
            .SetPlotOptions(new PlotOptions
            {
                Column = new PlotOptionsColumn
                {
                    PointPadding = 0,
                    BorderWidth = 0,
                    //Depth = 25,
                    DataLabels = new PlotOptionsColumnDataLabels
                    {
                        Enabled = true,
                        Color = Color.BlueViolet
                    }
                }
            })
            .SetLegend(new Legend
            {
                Enabled = false//Show Legend
                //1
                //Layout = Layouts.Horizontal,
                //Align = HorizontalAligns.Center,
                //VerticalAlign = VerticalAligns.Top,
                //X = -100,
                //Y = 40,
                //Floating = true,
                //BorderWidth = 1,
                //Shadow = false
                //2
                //Align = HorizontalAligns.Left,
                //Floating = true,
                //Layout = Layouts.Vertical,
                //Shadow = true,
                //VerticalAlign = VerticalAligns.Top
            })
            .SetCredits(new Credits { Enabled = false });

            //var get_deadTran = from a in db.TD_DeadTransaction
            //                   group a by a.IP into g
            //                   select new { Device = g.Key, DeadCount = g.Count() };
            var get_deadTran = from a in db.TD_DeadTransaction
                               select a;

            if (dtFrom != null && dtTo != null)
            {
                var dateTo = dtTo.Value.AddDays(1);
                get_deadTran = get_deadTran.Where(w => w.DeadDate >= dtFrom.Value && w.DeadDate <= dateTo);
            }
            if (fac != null)
            {
                get_deadTran = get_deadTran.Where(w => w.TD_Device.FactoryId == fac);
            }
            else if (plant != 0)
            {
                var fac_in_plant = db.TM_Factory.Where(w => w.PlantId == plant).Select(s => s.Id);
                get_deadTran = get_deadTran.Where(w => fac_in_plant.Contains(w.TD_Device.FactoryId));
            }

            var get_device = get_deadTran.Select(s => s.TD_Device.Name).Distinct().ToArray();
            object[] device_count = new object[get_device.Count()];

            chart.SetXAxis(new XAxis
            {
                Categories = get_device,
                Title = new XAxisTitle { Text = "Device Name" },
                Labels = new XAxisLabels
                {
                    Rotation = -45//Angle of XAxis Labels
                    //Align = HorizontalAligns.Right
                    //Style = "font: 'normal 13px Verdana, sans-serif'"
                }
            });

            int i = 0;
            foreach (var item in get_device)
            {
                device_count[i] = get_deadTran.Where(w => w.TD_Device.Name == item).Count();
                i++;
            }

            chart.SetSeries(new[]
            {
                new Series { Type = ChartTypes.Column, Name = "Device Down", Data = new Data(device_count) }
            });

            TempData["DeadDevice"] = i + 1;

            return PartialView(chart);
        }
        
        public ActionResult GetPlant()
        {
            var get_plant = db.TM_Plant.Select(s => new { s.Id, s.Name });
            return Json(get_plant, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetFac(byte plant)
        {
            var get_fac = db.TM_Factory
                .Where(w => w.PlantId == plant)
                .Select(s => new { s.Id, s.Name });
            return Json(get_fac, JsonRequestBehavior.AllowGet);
        }

    }
}
