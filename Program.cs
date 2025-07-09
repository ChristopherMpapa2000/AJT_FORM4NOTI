using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Nancy.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WOLF_START_MigrateDAR;
using WolfApprove.Model.CustomClass;

namespace Form4Noti
{
    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Program));

        private static string dbConnectionString
        {
            get
            {
                var ServarName = ConfigurationManager.AppSettings["ServarName"];
                var Database = ConfigurationManager.AppSettings["Database"];
                var Username_database = ConfigurationManager.AppSettings["Username_database"];
                var Password_database = ConfigurationManager.AppSettings["Password_database"];
                var dbConnectionString = $"data source={ServarName};initial catalog={Database};persist security info=True;user id={Username_database};password={Password_database};Connection Timeout=200";

                if (!string.IsNullOrEmpty(dbConnectionString))
                {
                    return dbConnectionString;
                }
                return "";
            }
        }
        private static string _BaseAPI
        {
            get
            {
                var BaseAPI = ConfigurationManager.AppSettings["BaseAPI"];
                if (!string.IsNullOrEmpty(BaseAPI))
                {
                    return BaseAPI;
                }
                return "";
            }
        }
        private static double iIntervalTime
        {
            get
            {
                var IntervalTime = ConfigurationManager.AppSettings["IntervalTimeMinute"];
                if (!string.IsNullOrEmpty(IntervalTime))
                {
                    return Convert.ToDouble(IntervalTime);
                }
                return -10;
            }
        }
        private static string EsmtpServer
        {
            get
            {
                var smtpServer = ConfigurationManager.AppSettings["smtpServer"];
                if (!string.IsNullOrEmpty(smtpServer))
                {
                    return (smtpServer);
                }
                return string.Empty;
            }
        }
        private static int EsmtpPort
        {
            get
            {
                var smtpPort = ConfigurationManager.AppSettings["smtpPort"];
                if (!string.IsNullOrEmpty(smtpPort))
                {
                    return Convert.ToInt32(smtpPort);
                }
                return 0;
            }
        }
        private static string EsmtpUsername
        {
            get
            {
                var smtpUsername = ConfigurationManager.AppSettings["smtpUsername"];
                if (!string.IsNullOrEmpty(smtpUsername))
                {
                    return (smtpUsername);
                }
                return string.Empty;
            }
        }
        private static string EsmtpPassword
        {
            get
            {
                var smtpPassword = ConfigurationManager.AppSettings["smtpPassword"];
                if (!string.IsNullOrEmpty(smtpPassword))
                {
                    return (smtpPassword);
                }
                return string.Empty;
            }
        }
        private static string EfromEmail
        {
            get
            {
                var fromEmail = ConfigurationManager.AppSettings["fromEmail"];
                if (!string.IsNullOrEmpty(fromEmail))
                {
                    return (fromEmail);
                }
                return string.Empty;
            }
        }
        private static string EtoEmail
        {
            get
            {
                var toEmail = ConfigurationManager.AppSettings["toEmail"];
                if (!string.IsNullOrEmpty(toEmail))
                {
                    return (toEmail);
                }
                return string.Empty;
            }
        }
        private static int memoid
        {
            get
            {
                var memoid = ConfigurationManager.AppSettings["memoid"];
                if (!string.IsNullOrEmpty(memoid))
                {
                    return Convert.ToInt32(memoid);
                }
                return 0;
            }
        }
        static void Main(string[] args)
        {
            try
            {
                log4net.Config.XmlConfigurator.Configure();
                log.Info("====== Start Process JobForm4Noti ====== : " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                log.Info(string.Format("Run batch as :{0}", System.Security.Principal.WindowsIdentity.GetCurrent().Name));

                DbwolfDataContext db = new DbwolfDataContext(dbConnectionString);
                if (db.Connection.State == ConnectionState.Open)
                {
                    db.Connection.Close();
                    db.Connection.Open();
                }
                db.Connection.Open();
                db.CommandTimeout = 0;

                GetData(db);
            }
            catch (Exception ex)
            {
                Console.WriteLine(":ERROR");
                Console.WriteLine("exit 1");

                log.Error(":ERROR");
                log.Error("message: " + ex.Message);
                log.Error("Exit ERROR");
            }
            finally
            {
                log.Info("====== End Process Process JobForm4Noti ====== : " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));

            }
        }
        public static void GetData(DbwolfDataContext db)
        {
            List<TRNMemo> lstmemo = new List<TRNMemo>();
            if (memoid != 0)
            {
                lstmemo = db.TRNMemos.Where(x => x.MemoId == memoid).ToList();
            }
            else
            {
                var DocumentCode = ConfigurationManager.AppSettings["DocumentCode"];
                var documentCodes = DocumentCode.Split('|');
                var template = db.MSTTemplates.Where(x => documentCodes.Contains(x.DocumentCode) && x.IsActive == true).Select(s => s.TemplateId).ToList();
                lstmemo = db.TRNMemos.Where(m => m.TemplateId.HasValue && template.Contains(m.TemplateId.Value) && m.StatusName == "Completed").ToList();
            }
           
            string Enddate = string.Empty;
            string startdate = string.Empty;
            string ContractNo = string.Empty;
            if (lstmemo.Count > 0)
            {
                foreach (var itemmemo in lstmemo)
                {
                    JObject jsonAdvanceForm = JsonUtils.createJsonObject(itemmemo.MAdvancveForm);
                    JArray itemsArray = (JArray)jsonAdvanceForm["items"];
                    foreach (JObject jItems in itemsArray)
                    {
                        JArray jLayoutArray = (JArray)jItems["layout"];
                        if (jLayoutArray.Count >= 1)
                        {
                            JObject jTemplateL = (JObject)jLayoutArray[0]["template"];
                            JObject jData = (JObject)jLayoutArray[0]["data"];
                            if ((String)jTemplateL["label"] == "วันที่เริ่มต้นสัญญา")
                            {
                                startdate = jData["value"].ToString();
                            }
                            if (jLayoutArray.Count > 1)
                            {
                                JObject jTemplateR = (JObject)jLayoutArray[1]["template"];
                                JObject jData2 = (JObject)jLayoutArray[1]["data"];
                                if ((String)jTemplateR["label"] == "วันที่สิ้นสุดสัญญา")
                                {
                                    Enddate = jData2["value"].ToString();
                                }
                                if ((String)jTemplateR["label"] == "Reference Contract no. Edit")
                                {
                                    ContractNo = jData2["value"].ToString();
                                }
                            }
                        }
                        
                    }
                    if (!string.IsNullOrEmpty(Enddate))
                    {
                        DateTime Sdate = DateTime.ParseExact(startdate, "dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
                        DateTime Edate = DateTime.ParseExact(Enddate, "dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
                        DateTime alertDate = Edate.AddMonths(11);
                        DateTime today = DateTime.Now;

                        if (today.Day == alertDate.Day && today.Month == alertDate.Month)
                        {
                            log.Info("Start alert Email memoid : " + itemmemo.MemoId + " : " + itemmemo.DocumentNo);
                            var emailRequesterId = db.ViewEmployees.Where(x => x.EmployeeId == itemmemo.RequesterId).FirstOrDefault();
                            SendEmail(emailRequesterId.Email, db, itemmemo, emailRequesterId.NameEn, Sdate.ToString("dd/MM/yyyy"), Edate.ToString("dd/MM/yyyy"), ContractNo);
                        }
                    }
                    log.Info("----------------------------------------------------------------------------");
                }
            }
            else
            {
                Console.WriteLine("Not have DocumentCode | Form-4noti : " + lstmemo.Count);
                log.Info("Not have DocumentCode | Form-4noti : " + lstmemo.Count);
            }
        }
        public static void SendEmail(string email, DbwolfDataContext db, TRNMemo itemmemo, string nameEN, string startdate, string Enddate,string ContractNo)
        {
            string URL = ConfigurationManager.AppSettings["URLWeb"];
            string URLMobile = ConfigurationManager.AppSettings["URLMobile"];
            MSTEmailTemplate emailtem = db.MSTEmailTemplates.Where(e => e.TemplateId == 9999).FirstOrDefault();
            TRNActionHistory Actionhis = db.TRNActionHistories.Where(h => h.MemoId == itemmemo.MemoId).OrderByDescending(z => z.ActionId).FirstOrDefault();
            string subject = "";
            string toEmail = "";
            string body = "";
            string smtpServer = EsmtpServer;
            int smtpPort = EsmtpPort;
            string smtpUsername = EsmtpUsername;
            string smtpPassword = EsmtpPassword;
            string fromEmail = EfromEmail;

            if (!string.IsNullOrEmpty(EtoEmail))
            {
                toEmail = EtoEmail;
            }
            else
            {
                toEmail = email;
            }
            if (emailtem != null)
            {
                subject = emailtem.EmailSubject;
                body = emailtem.EmailBody;
            }
            subject = subject.Replace("[DocumentNo]", itemmemo.DocumentNo)
            .Replace("[TemplateSubject]", itemmemo.TemplateSubject);
            subject = subject.Replace("\n", "").Replace("\r", "");

            body = body.Replace("[DearName]", nameEN)
           .Replace("[DocumentNo]", itemmemo.DocumentNo)
           .Replace("[Contractname]", itemmemo.MemoSubject)
           .Replace("[StartingDate]", startdate)
           .Replace("[Expirydate]", Enddate)
           .Replace("[URLToRequest]", $"<a href=\"{URL + itemmemo.MemoId}\">Click</a>");

            MailMessage mail = new MailMessage(fromEmail, toEmail, subject, body);
            mail.IsBodyHtml = true;
            SmtpClient smtpClient = new SmtpClient(smtpServer, smtpPort);
            smtpClient.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
            smtpClient.EnableSsl = true;
            try
            {
                // ส่งอีเมล์
                smtpClient.Send(mail);
                Console.WriteLine("Email sent successfully." + "|| DocumentNo : " + itemmemo.DocumentNo);
                log.Info("Email sent successfully." + "|| DocumentNo : " + itemmemo.DocumentNo);
            }
            catch (Exception ex)
            {
                Console.WriteLine("!! Email sent error: " + ex.Message + "|| DocumentNo : " + itemmemo.DocumentNo);
                log.Info("!! Email sent error" + ex.Message + "|| DocumentNo : " + itemmemo.DocumentNo);
            }
        }
    }
}
