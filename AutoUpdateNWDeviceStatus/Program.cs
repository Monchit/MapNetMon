using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using WebCommonFunction;

namespace AutoUpdateNWDeviceStatus
{
    class Program
    {
        MapNetMonEntities db = new MapNetMonEntities();
        MailCenterEntities mail = new MailCenterEntities();
        TNCUtility util = new TNCUtility();

        static void Main(string[] args)
        {
            var prog = new Program();
            var adminEmail = prog.GetAdminEmail();
            var sender = "TNCAutoMail-MapNetwork@nok.co.th";
            var body = string.Empty;
            var deadCount = 0;

            try
            {
                foreach (var item in prog.db.TD_Device)
                {
                    var deviceStatus = prog.LocalPing(item.IP);
                    Console.WriteLine("Ping => " + item.IP + " : " + deviceStatus);

                    if (!item.Status && deviceStatus)
                    {
                        item.LastOn = DateTime.Now;
                        var recoveryDevice = item.TD_DeadTransaction.Where(w => w.IP == item.IP && w.RebornDate == null).LastOrDefault();
                        if (recoveryDevice != null)
                        {
                            recoveryDevice.RebornDate = DateTime.Now;
                        }
                    }

                    if (item.Status && !deviceStatus)
                    {
                        deadCount++;
                        body += "<div><strong>Device Type:</strong> " + item.TM_Type.Name + "<br /><strong>Device Name:</strong> " + item.Name + "<br /><strong>Device IP:</strong> " + item.IP + "<br /><strong>Location:</strong> " + item.TM_Factory.Name + "<hr /></div>";

                        var dead = new TD_DeadTransaction();
                        dead.IP = item.IP;
                        dead.DeadDate = DateTime.Now;

                        prog.db.TD_DeadTransaction.Add(dead);
                    }

                    item.Status = deviceStatus;
                    item.LastUpdateDate = DateTime.Now;
                }
                Console.ReadLine();
                prog.db.SaveChanges();

                if (deadCount > 0)
                {
                    body = "Hi, Network Admin Professional<br /><br /><br />There are " + deadCount + " Network Device(s) are DEAD. You need to check it!!<br /><br /><hr />" + body + "<br /><br /><strong>Good Luck</strong>";
                    var subject = deadCount + " TNC Network Device(s) DEAD. Details in mail.";
                    prog.util.SendMail(25, sender, adminEmail, subject, body);
                }
            }
            catch (DbEntityValidationException dbEx)
            {
                foreach (var validationErrors in dbEx.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        Trace.TraceInformation("Class: {0}, Property: {1}, Error: {2}",
                            validationErrors.Entry.Entity.GetType().FullName,
                            validationError.PropertyName,
                            validationError.ErrorMessage);
                    }
                }

                throw new Exception(dbEx.Message);
            }
            catch (Exception ex)
            {
                // Program abnormal then mail to Network Admin Professional
            }
            
        }

        public bool LocalPing(string ip)
        {
            var pingStatus = false;
            // Ping's the local machine.
            Ping pingSender = new Ping();
            IPAddress address = IPAddress.Parse(ip);

            for (int i = 0; i < 10; i++)
            {
                PingReply reply = pingSender.Send(address);
                if (reply.Status == IPStatus.Success)
                {
                    pingStatus = true;
                }
            }

            return pingStatus;
        }

        public string GetAdminEmail()
        {
            var admin1 = db.VW_Emp_Admin.Select(s => s.email_ci_ai);
            var admin2 = db.VW_Emp_Admin.Where(w => w.AdditionalEmail != null).Select(s => s.AdditionalEmail);

            var admin = admin1.Union(admin2);

            return string.Join(",", admin);
        }

    }
}
