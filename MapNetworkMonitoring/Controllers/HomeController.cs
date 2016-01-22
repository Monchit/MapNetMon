using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MapNetworkMonitoring.Models;
using System.Transactions;
using System.Net.Sockets;
using System.Text;
using System.Net;
using System.Net.NetworkInformation;
using WebCommonFunction;

namespace MapNetworkMonitoring.Controllers
{
    public class HomeController : Controller
    {
        private MapNetMonEntities db = new MapNetMonEntities();
        private TNCFileDirectory dir = new TNCFileDirectory();
        private byte[] bytes;
        private NetworkStream netStream;
        private TcpClient tcpClient;
        private const string configFilePath = "Uploads/Config/";
        public  string root = "~/";

        public ActionResult Index()
        {
            ViewBag.Predefined = db.TM_Predefined.FirstOrDefault();
            ViewBag.Summary = db.VW_Device_Summary.OrderBy(o => o.Id);
            var plant = db.TM_Plant;

            return View(plant);
        }

        public ActionResult GetMap(byte plantId = 0, byte facId = 0, string mode = "user")
        {
            var devices = db.TD_Device.AsQueryable();

            if (plantId > 0)
            {
                devices = devices.Where(w => w.TM_Factory.PlantId == plantId && w.MainDevice == true).OrderBy(o => o.Type).ThenBy(o => o.IP);
                var deadDevices = db.TD_Device.Where(w => w.TM_Factory.PlantId == plantId && w.MainDevice == false && w.Status == false).GroupBy(g => g.FactoryId).Select(s => s.Key);

                ViewBag.WarnFactory = deadDevices.ToList();
                ViewBag.BG = db.TM_Plant.Find(plantId).Img;
                ViewBag.PlantId = plantId;
            }
            else
            {
                devices = devices.Where(w => w.FactoryId == facId);
                ViewBag.BG = db.TM_Factory.Find(facId).Img;
                ViewBag.FacId = facId;
            }

            ViewBag.Location = db.TM_Factory.OrderBy(o => o.TM_Plant.Name).ThenBy(o => o.Name);
            ViewBag.DeviceType = db.TM_Type.OrderBy(o => o.Name);

            ViewBag.predefined = db.TM_Predefined.First();
            ViewBag.Level = plantId == 0 ? "Factory" : "Plant";
            ViewBag.Ret = plantId == 0 ? "facId" : "plantId";
            ViewBag.RetId = plantId == 0 ? facId : plantId;
            ViewBag.Mode = mode;
            
            return View(devices);
        }

        public ActionResult SetPosition(string ip, int X, int Y, string level)
        {
            var device = db.TD_Device.Find(ip);

            if (level == "Plant")
            {
                device.XMain = X;
                device.YMain = Y;
            }
            else
            {
                device.X = X;
                device.Y = Y;
            }
            

            try
            {
                db.SaveChanges();
                return Json(1);
            }
            catch (Exception ex)
            {
                return Json(ex.Message);
            }
        }

        public ActionResult UpdateStatus(byte plantId = 0, byte facId = 0)
        {
            try
            {
                var devices = db.TD_Device.AsQueryable();

                if (plantId > 0)
                {
                    devices = devices.Where(w => w.TM_Factory.PlantId == plantId && w.MainDevice == true);
                }
                else
                {
                    devices = devices.Where(w => w.FactoryId == facId);
                }

                var deadDevices = db.TD_Device.Where(w => w.TM_Factory.PlantId == plantId && w.MainDevice == false && w.Status == false).GroupBy(g => g.FactoryId).Select(s => s.Key);
                return Json(devices.ToList().Select(s => new {
                    id = "status-" + s.IP.Replace(".", "-"),
                    status = s.Status,
                    img = plantId == 0 ? Url.Content(root + s.TM_Status.Img) : (!deadDevices.Contains(s.FactoryId) ? Url.Content(root + s.TM_Status.Img) : Url.Content("~/Image/Status/warn.png"))
                }));
            }
            catch (Exception)
            {
                return Json(0);
            }
            
        }

        public ActionResult AddDevice(string txtName, short selType, string txtIP, byte selLocation, string selUplinkIP, string txtUplinkPort, HttpPostedFileBase uplConfig, bool mainDevice, byte returnId, string[] NIP, string[] A, string[] B)
        {
            try
            {
                using (var scope = new TransactionScope())
                {
                    var device = new TD_Device();
                    device.IP = txtIP;
                    device.Type = selType;
                    device.FactoryId = selLocation;
                    device.Name = txtName;
                    device.UplinkIP = selUplinkIP;
                    device.UplinkPort = txtUplinkPort;
                    device.MainDevice = mainDevice;
                    device.EntryDate = DateTime.Now;
                    if (mainDevice == true)
                    {
                        device.XMain = 0;
                        device.YMain = 0;
                    }

                    if (uplConfig != null && uplConfig.ContentLength > 0)
                    {
                        var serverPath = dir.SaveFile(uplConfig, configFilePath, txtIP + "-" + uplConfig.FileName);
                        device.ConfigFile = serverPath;
                    }

                    db.TD_Device.Add(device);

                    if (NIP != null)
                    {
                        for (int i = 0; i < NIP.Length; i++)
                        {
                            var neighbor = new TD_Neighbor();
                            neighbor.IP = txtIP;
                            neighbor.NIP = NIP[i];
                            neighbor.AnchorA = A[i];
                            neighbor.AnchorB = B[i];

                            db.TD_Neighbor.Add(neighbor);
                        }
                    }

                    db.SaveChanges();
                    scope.Complete();
                }

                TempData["Msg"] = "Add Device " + txtIP + " - " + txtName + " Success";
            }
            catch (Exception ex)
            {

                TempData["Msg"] = "Add Device Error: " + ex.Message;
            }

            if (mainDevice)
                return RedirectToAction("GetMap", "Home", new { plantid = returnId, mode = "admin" });
            else
                return RedirectToAction("GetMap", "Home", new { facid = returnId, mode = "admin" });
        }

        public ActionResult EditDevice(string txtName, short selType, string txtIP, byte selLocation, string selUplinkIP, string txtUplinkPort, bool mainDevice, HttpPostedFileBase uplConfig, byte returnId, string[] NIP, string[] A, string[] B)
        {
            try
            {
                using (var scope = new TransactionScope())
                {
                    // Device
                    var device = db.TD_Device.Find(txtIP);
                    device.Type = selType;
                    device.Name = txtName;
                    device.UplinkIP = selUplinkIP;
                    device.UplinkPort = txtUplinkPort;
                    device.EntryDate = DateTime.Now;

                    if (uplConfig != null && uplConfig.ContentLength > 0)
                    {
                        var serverPath = dir.SaveFile(uplConfig, configFilePath, txtIP + "-" + uplConfig.FileName);
                        if (device.ConfigFile != null && System.IO.File.Exists(device.ConfigFile))
                        {
                            System.IO.File.Delete(Server.MapPath(root + device.ConfigFile));
                        }
                        
                        device.ConfigFile = serverPath;
                    }

                    // Nieghbors
                    var oldNieghbor = db.TD_Neighbor.Where(w => w.IP == txtIP || w.NIP == txtIP);
                    foreach (var item in oldNieghbor)
                    {
                        db.TD_Neighbor.Remove(item);
                    }

                    if (NIP != null)
                    {
                        for (int i = 0; i < NIP.Length; i++)
                        {
                            var neighbor = new TD_Neighbor();
                            neighbor.IP = txtIP;
                            neighbor.NIP = NIP[i];
                            neighbor.AnchorA = A[i];
                            neighbor.AnchorB = B[i];

                            db.TD_Neighbor.Add(neighbor);
                        }
                    }

                    db.SaveChanges();
                    scope.Complete();
                }

                TempData["Msg"] = "Update Device " + txtIP + " - " + txtName + " Success";
            }
            catch (Exception ex)
            {

                TempData["Msg"] = "Update Device Error: " + ex.Message;
            }

            if (mainDevice)
                return RedirectToAction("GetMap", "Home", new { plantid = returnId, mode = "admin" });
            else
                return RedirectToAction("GetMap", "Home", new { facid = returnId, mode = "admin" });
        }

        public ActionResult GetPlantId(byte facid)
        {
            var fac = db.TM_Factory.Find(facid);

            return Json(fac.PlantId, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetDeviceByPlant(bool main, byte facId)
        {
            var fac = db.TM_Factory.Find(facId);
            var devices = db.TD_Device.Where(w => w.TM_Factory.PlantId == fac.PlantId && (main ? w.MainDevice == true : w.MainDevice == true || w.MainDevice == false)).Select(s => new { Type = s.TM_Type.Name, IP = s.IP, Name = s.Name }).OrderByDescending(o => o.Type).ThenBy(o => o.IP).ThenBy(o => o.Name);

            return Json(devices, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetDeviceByFactory(byte facId)
        {
            var devices = db.TD_Device.Where(w => w.FactoryId == facId).Select(s => new { Type = s.TM_Type.Name, IP = s.IP, Name = s.Name }).OrderByDescending(o => o.Type).ThenBy(o => o.IP).ThenBy(o => o.Name);

            return Json(devices, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetDeviceUpTime(string ip)
        {
            var device = db.TD_Device.Find(ip);
            var upTime = DateTime.Now.Subtract(device.LastOn.Value);

            return Json(string.Format( @"{0:dd} Day {0:hh} Hour {0:mm} Minute {0:ss} Second", upTime), JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetDeviceName(string ip)
        {
            var device = db.TD_Device.Find(ip);

            return Json(device == null ? "-" : device.Name, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetInterfaceStatus(string ip)
        {
            var device = db.TD_Device.Find(ip);

            
            bytes = new byte[10000];
            string outStr = string.Empty;

            // Please notice that \r\n is executed by telnet as a carriage return, so you can put all telnet commands you want!
            // Login, Try from login list TM_Authentication
            var loginStatus = false;

            try
            {

            

            foreach (var item in db.TM_Authentication)
            {

                tcpClient = new TcpClient();
                tcpClient.Connect(ip, 23);
                netStream = tcpClient.GetStream();

                // ######## Login ########
                byte[] login = Encoding.UTF8.GetBytes(item.Username + "\r\n" + item.Password + "\r\nterminal width 512\r\nterminal length 0\r\nenable\r\n");
                netStream.Write(login, 0, login.Length);

                // I tried to use some async methods but all of them fail, so I just wait for some seconds to telnet device to finish parsing commands and returning output
                System.Threading.Thread.Sleep(1000);

                netStream.Read(bytes, 0, 10000);

                // Here it is the telnet device output
                outStr = Encoding.UTF8.GetString(bytes);

                if (outStr.Contains("Authentication failed") || outStr.Contains("Login invalid") || outStr.Contains("Bad passwords") || !outStr.Contains("enable"))
                {
                    tcpClient.Close();
                    continue;
                }

                // ######## Enable ########
                var privelegeStr = outStr.Substring(outStr.IndexOf("enable"));
                if (privelegeStr.Contains("Password"))
                {
                    byte[] enable = Encoding.UTF8.GetBytes(item.Password + "\r\n");
                    netStream.Write(enable, 0, enable.Length);
                }

                // ######## CLI Command ########
                var ciscoCmd = device.TM_Type.TM_Command.Where(w => w.Name.Contains("Interfaces Status")).FirstOrDefault();
                byte[] command = Encoding.UTF8.GetBytes(ciscoCmd.Command + "\r\n");
                netStream.Write(command, 0, command.Length);

                // I tried to use some async methods but all of them fail, so I just wait for some seconds to telnet device to finish parsing commands and returning output
                System.Threading.Thread.Sleep(2000);

                netStream.Read(bytes, 0, 10000);
                
                // Here it is the telnet device output
                outStr = Encoding.UTF8.GetString(bytes);

                if ((!outStr.Contains("Port") && !outStr.Contains("Interface")) || outStr.Contains("Authentication failed") || outStr.Contains("Login invalid") || outStr.Contains("Bad passwords"))
                {
                    tcpClient.Close();
                    continue;
                }

                loginStatus = true;
                netStream.Close();
                tcpClient.Close();
                break;
            }

            if (!loginStatus)
            {
                return Json("Read data failed" , JsonRequestBehavior.AllowGet);
            }

            var retStr = SetFormatIntStatus(device.TM_Type.Id, outStr);

            return Json(retStr, JsonRequestBehavior.AllowGet);

            }
            catch (Exception ex)
            {
                return Json("<strong>Error:</strong> " + ex.Message, JsonRequestBehavior.AllowGet);
            }
        }

        public string SetFormatIntStatus(short deviceType, string statusStr)
        {
            var retStr = string.Empty;

            try
            {
                // Switch
                if (deviceType == 1)
                {
                    statusStr = statusStr.Replace("\0", "").Replace("\r\n", root);
                    var firstIndex = statusStr.IndexOf("Port");
                    statusStr = statusStr.Substring(firstIndex);
                    var lastIndex = statusStr.LastIndexOf(root);
                    statusStr = statusStr.Substring(0, lastIndex);
                    statusStr = statusStr.Replace(" ", "&nbsp;");

                    var collection = statusStr.Split('~');

                    retStr = "<div class='div-int-status'>";
                    foreach (var items in collection)
                    {
                        retStr += "<div>" + items + "</div>";
                    }
                    retStr += "</div>";

                    retStr = retStr.Replace("connected", "<img title='Link Up' src='" + Url.Content("~/Image/Misc/Circle_Green.png") + "' /> Link Up");
                    retStr = retStr.Replace("notconnect", "<img title='No Link' src='" + Url.Content("~/Image/Misc/Circle_Empty.png") + "' /> No Link");
                    retStr = retStr.Replace("disabled", "<img title='Link Shutdown' src='" + Url.Content("~/Image/Misc/Circle_Red.png") + "' /> Link Shutdown");
                }
                // Access Point
                else if (deviceType == 2)
                {
                    statusStr = statusStr.Replace("\0", "").Replace("\r\n", root);
                    var firstIndex = statusStr.IndexOf("Interface");
                    statusStr = statusStr.Substring(firstIndex);
                    var lastIndex = statusStr.LastIndexOf(root);
                    statusStr = statusStr.Substring(0, lastIndex);
                    statusStr = statusStr.Replace(" ", "&nbsp;");

                    var collection = statusStr.Split('~');

                    retStr = "<div class='div-int-status'>";
                    foreach (var items in collection)
                    {
                        retStr += "<div>" + items + "</div>";
                    }
                    retStr += "</div>";

                    retStr = retStr.Replace("up", "<img title='up' src='" + Url.Content("~/Image/Misc/Circle_Green.png") + "' /> up");
                    retStr = retStr.Replace("down", "<img title='down' src='" + Url.Content("~/Image/Misc/Circle_Red.png") + "' /> down");
                }

                return retStr;
            }
            catch (Exception)
            {

                return "Can't Run Command: " + statusStr;
            }
            
        }

        public ActionResult DeleteDevice(string ip)
        {
            try
            {
                var device = db.TD_Device.Find(ip);
                db.TD_Device.Remove(device);
                db.SaveChanges();

                return Json(true);
            }
            catch (Exception)
            {
                return Json(false);
            }
        }
        
        public ActionResult GetDevice(string ip)
        {
            var device = db.TD_Device.Where(w => w.IP == ip).ToList().Select(s => new
            {
                s.Name,
                s.Type,
                s.IP,
                s.UplinkIP,
                s.UplinkPort,
                ConfigFile = !string.IsNullOrEmpty(s.ConfigFile) ? Url.Content(root + s.ConfigFile) : string.Empty,
                Neighbor = db.TD_Neighbor.Where(w => w.IP == ip || w.NIP == ip
).ToList().Select(n => new { Type = n.TD_Device.TM_Type.Name, NIP = n.IP == ip ? n.NIP : n.IP, Name = n.IP == ip ? db.TD_Device.Find(n.NIP).Name : n.TD_Device.Name, AnchorA = n.IP == ip ? n.AnchorA : n.AnchorB, AnchorB = n.IP == ip ? n.AnchorB : n.AnchorA })
            }).First();

            return Json(device, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Ping(string ip)
        {
            return Json(LocalPing(ip));
        }

        public string LocalPing(string ip)
        {
            // Ping's the local machine.
            Ping pingSender = new Ping();
            IPAddress address = IPAddress.Parse(ip);

            PingReply reply = pingSender.Send(address);

            if (reply.Status == IPStatus.Success)
            {
                return "<div>" + string.Format("Reply from {0}: bytes={1} time={2}ms TTL={3}", reply.Address.ToString(), reply.Buffer.Length, reply.RoundtripTime, reply.Options.Ttl) + "</div>";
            }
            else
            {
                return "<div>" + string.Format("Reply from {0}: Destination host unreachable.", reply.Address.ToString()) + "</div>";
            }
        }

    }
}
