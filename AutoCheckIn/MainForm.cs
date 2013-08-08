using System;
using System.Data;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Diagnostics;
using System.Security.AccessControl;

namespace AutoCheckIn
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// The local time on computer sometimes is different from the actual time.
        /// </summary>
        TimeSpan t_offset;

        public MainForm()
        {
            InitializeComponent();
        }

        private void lv_Confirmation_SelectedIndexChanged(object sender, EventArgs e)
        {
            btn_Delete.Enabled = btn_Edit.Enabled = (lv_Confirmation.SelectedIndices.Count > 0);
        }

        private void btn_Add_Click(object sender, EventArgs e)
        {
            EditForm frm = new EditForm();
            if (frm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ListViewItem lvi = new ListViewItem(frm.tb_Confirmation.Text);
                lvi.SubItems.Add(frm.tb_FirstName.Text);
                lvi.SubItems.Add(frm.tb_LastName.Text);
                lvi.SubItems.Add(frm.tb_Times.Text);
                lvi.Selected = true;
                lv_Confirmation.Items.Add(lvi);
            }
        }

        private void btn_Edit_Click(object sender, EventArgs e)
        {
            if (lv_Confirmation.SelectedItems.Count == 1)
            {
                EditForm frm = new EditForm();
                frm.lvi_Edit = lv_Confirmation.SelectedItems[0];
                if (frm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    lv_Confirmation.SelectedItems[0].SubItems[0].Text = frm.tb_Confirmation.Text;
                    lv_Confirmation.SelectedItems[0].SubItems[1].Text = frm.tb_FirstName.Text;
                    lv_Confirmation.SelectedItems[0].SubItems[2].Text = frm.tb_LastName.Text;
                    lv_Confirmation.SelectedItems[0].SubItems[3].Text = frm.tb_Times.Text;
                }
            }
        }

        private void btn_Delete_Click(object sender, EventArgs e)
        {
            if (lv_Confirmation.SelectedItems.Count == 1)
            {
                lv_Confirmation.SelectedItems[0].Remove();
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

            // load saved list
            if (File.Exists("saved.xml"))
            {
                DataSet ds = new DataSet();
                ds.ReadXml("saved.xml");
                DataTable dt = ds.Tables[0];
                foreach (DataRow dr in dt.Rows)
                {
                    ListViewItem lvi = new ListViewItem(dr[0].ToString());
                    lvi.SubItems.Add(dr[1].ToString());
                    lvi.SubItems.Add(dr[2].ToString());
                    lvi.SubItems.Add(dr[3].ToString());
                    lv_Confirmation.Items.Add(lvi);
                }
            }

            // get time offset
            DateTime now = GetInternetTime(t_offset);
            t_offset = now - DateTime.Now;

            // set timer
            if (now.Second >= 2) timer1.Interval = (60 - now.Second + 2) * 1000;
            else timer1.Interval = (2 - now.Second) * 1000;

            timer1.Enabled = true;

            // do check in first
            doCheckIn(now);

            // show current time
            this.Text = "Auto Check In : " + now.ToLongTimeString();

            this.WindowState = FormWindowState.Minimized;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();

            if (lv_Confirmation.Items.Count > 0)
            {
                DataTable dt = new DataTable("MyData");
                foreach (ColumnHeader col in lv_Confirmation.Columns)
                {
                    dt.Columns.Add(col.Text, typeof(string));
                }
                foreach (ListViewItem lvi in lv_Confirmation.Items)
                {
                    DataRow dr = dt.NewRow();
                    for (int i = 0; i < dt.Columns.Count; i++)
                        dr[i] = lvi.SubItems[i].Text;
                    dt.Rows.Add(dr);
                }
                if (args.Length <= 1)
                    dt.WriteXml("saved.xml");
                else
                    dt.WriteXml(args[1]);
            }
            else
            {
                if (args.Length <= 1)
                    File.Delete("saved.xml");
                else
                    File.Decrypt(args[1]);
            }

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now + t_offset;
            if (sender == timer1)
            {
                this.Text = "Auto Check In : " + now.ToLongTimeString();
                if (now.Second >= 2) timer1.Interval = (60 - now.Second + 2) * 1000;
                else timer1.Interval = (2 - now.Second) * 1000;
            }

            doCheckIn(now);

            if (now.Hour == 2) this.Close();
        }

        private void btn_CheckIn_Click(object sender, EventArgs e)
        {
            if (lv_Confirmation.SelectedItems.Count == 1)
            {
                if (!checkIn(lv_Confirmation.SelectedItems[0].SubItems[0].Text,
                    lv_Confirmation.SelectedItems[0].SubItems[1].Text,
                    lv_Confirmation.SelectedItems[0].SubItems[2].Text))
                {
                    string html = webBrowser1.DocumentText;
                }
            }
        }

        private void btn_CheckTime_Click(object sender, EventArgs e)
        {
            DateTime now = GetInternetTime(t_offset);
            t_offset = now - DateTime.Now;
            this.Text = "Auto Check In : " + now.ToLongTimeString();
        }

        #region Support functions

        public DateTime GetInternetTime(TimeSpan offset)
        {
            DateTime date = DateTime.Now + offset;
            string[] timeServers = new string[]{
                "time-a.timefreq.bldrdoc.gov",
                "time-b.timefreq.bldrdoc.gov",
                "time-c.timefreq.bldrdoc.gov",
                //"time-a.nist.gov",
                //"time-b.nist.gov",
                "utcnist.colorado.edu",
                "time-nw.nist.gov",
                "nist1.datum.com",
                "nist1.dc.certifiedtime.com",
                "nist1.nyc.certifiedtime.com",
                "nist1.sjc.certifiedtime.com"
            };
            for (int i = 0; i < timeServers.Length; i++)
                try
                {
                    if (!timeServers[i].Contains(".")) continue;

                    StreamReader reader = new StreamReader(new System.Net.Sockets.TcpClient(timeServers[i], 13).GetStream());
                    string serverResponse = reader.ReadToEnd();
                    reader.Close();

                    // Check to see that the signiture is there
                    if (serverResponse.Length > 47 && serverResponse.Substring(38, 9).Equals("UTC(NIST)"))
                    {
                        // Parse the date
                        int jd = int.Parse(serverResponse.Substring(1, 5));
                        int yr = int.Parse(serverResponse.Substring(7, 2));
                        int mo = int.Parse(serverResponse.Substring(10, 2));
                        int dy = int.Parse(serverResponse.Substring(13, 2));
                        int hr = int.Parse(serverResponse.Substring(16, 2));
                        int mm = int.Parse(serverResponse.Substring(19, 2));
                        int sc = int.Parse(serverResponse.Substring(22, 2));

                        if (jd > 51544)
                            yr += 2000;
                        else
                            yr += 1999;

                        date = new DateTime(yr, mo, dy, hr, mm, sc);

                        // Convert it to the current timezone if desired
                        date = date.ToLocalTime();
                        return date;
                    }
                }
                catch (Exception ex)
                {
                    webBrowser1.DocumentText += ex.ToString();
                }
            return date;
        }

        private bool checkIn(string confirmation, string firstname, string lastname)
        {
            String postdata = "formToken=&" +
                "confirmationNumber=" + confirmation + "&" +
                "firstName=" + firstname + "&" +
                "lastName=" + lastname + "&" +
                "submitButton=Check+In";

            System.Text.Encoding a = System.Text.Encoding.UTF8;

            byte[] byte1 = a.GetBytes(postdata);

            webBrowser1.Navigate("http://www.southwest.com/flight/retrieveCheckinDoc.html", "", byte1, "Content-Type: application/x-www-form-urlencoded\r\n");

            //wait 2 seconds
            DateTime tickle = DateTime.Now;
            while ((DateTime.Now - tickle).TotalSeconds < 2)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(100);
            }
            // wait till page complete
            tickle = DateTime.Now;
            while (webBrowser1.ReadyState != WebBrowserReadyState.Complete)
            {
                System.Threading.Thread.Sleep(100);
                Application.DoEvents();
                if ((DateTime.Now - tickle).TotalMinutes > 1) break;
            }
            string html = webBrowser1.DocumentText;

            HtmlElement frmCheckIn = webBrowser1.Document.Forms["checkinOptions"];
            if (frmCheckIn != null)
            {
                Application.DoEvents();
                webBrowser1.Document.GetElementById("printDocumentsButton").ScrollIntoView(false);
                Application.DoEvents();
                webBrowser1.Document.GetElementById("printDocumentsButton").InvokeMember("click");
                //wait 30 seconds
                tickle = DateTime.Now;
                while ((DateTime.Now - tickle).TotalSeconds < 30)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(100);
                }
                tickle = DateTime.Now;
                while (webBrowser1.ReadyState != WebBrowserReadyState.Complete)
                {
                    System.Threading.Thread.Sleep(100);
                    Application.DoEvents();
                    if ((DateTime.Now - tickle).TotalMinutes > 1) break;
                }
                html = webBrowser1.DocumentText;
                int pos1 = html.IndexOf("<div class=\"boardingPosition\">");
                if (pos1 > 0)
                {
                    try
                    {
                        pos1 = html.IndexOf("alt=\"", pos1);
                        int pos2 = html.IndexOf("\"", pos1 + 6);
                        string position = html.Substring(pos1 + 5, pos2 - pos1 - 5);
                        pos1 = html.IndexOf("alt=\"", pos1 + 6);
                        pos2 = html.IndexOf("\"", pos1 + 6);
                        position += html.Substring(pos1 + 5, pos2 - pos1 - 5);
                        pos1 = html.IndexOf("alt=\"", pos1 + 6);
                        pos2 = html.IndexOf("\"", pos1 + 6);
                        position += html.Substring(pos1 + 5, pos2 - pos1 - 5);
                        while ((pos1 = html.IndexOf("<div class=\"boardingPosition\">", pos1)) > 0)
                        {
                            pos1 = html.IndexOf("alt=\"", pos1);
                            pos2 = html.IndexOf("\"", pos1 + 6);
                            position += "/" + html.Substring(pos1 + 5, pos2 - pos1 - 5);
                            pos1 = html.IndexOf("alt=\"", pos1 + 6);
                            pos2 = html.IndexOf("\"", pos1 + 6);
                            position += html.Substring(pos1 + 5, pos2 - pos1 - 5);
                            pos1 = html.IndexOf("alt=\"", pos1 + 6);
                            pos2 = html.IndexOf("\"", pos1 + 6);
                            position += html.Substring(pos1 + 5, pos2 - pos1 - 5);
                        }
                        string subject = "Flight " + confirmation + " Position " + position;
                        tickle = DateTime.Now;
                        string content = subject + "\r\nActual Time: " + (tickle + t_offset).ToLongTimeString() + "\r\n" + "Computer System Time:" + (tickle).ToLongTimeString();
                        /*
                        MailMessage msg = new MailMessage();
                        msg.To.Add(Properties.Settings.Default.email);
                        msg.Subject = subject;
                        msg.Body = content;
                        msg.From = new MailAddress(Properties.Settings.Default.fromEmail, "SW Flight");
                        SmtpClient client = new SmtpClient(Properties.Settings.Default.smtpServer);
                        client.Credentials = CredentialCache.DefaultNetworkCredentials;
                        client.Send(msg);
                         */
                    }
                    catch { }
                    return true;
                }
            }
            else
            {
            }

            return false;
        }

        private void doCheckIn(DateTime now)
        {
            //DateTime now = DateTime.Now + t_offset;

            foreach (ListViewItem lvi in lv_Confirmation.Items)
            {
                string s_times = lvi.SubItems[3].Text;
                string[] t_times = s_times.Split(new char[] { ';', ',', '|' });
                bool dirty = false;
                for (int i = 0; i < t_times.Length; i++)
                {
                    DateTime dt = now;
                    if (DateTime.TryParse(t_times[i], out dt))
                    {
                        dt = dt.AddDays(-1);
                        if (dt >= now.AddDays(-1) && dt <= now) //with in a day
                        {
                            //log("Flight time: " + dt.AddDays(1).ToShortDateString() + " " + dt.ToLongTimeString());
                            //log("Current time: " + now.ToShortDateString() + " " + now.ToLongTimeString());
                            //log("Try to checkin " + lvi.SubItems[0].Text);
                            if (checkIn(lvi.SubItems[0].Text,
                                lvi.SubItems[1].Text,
                                lvi.SubItems[2].Text))
                            {
                                //remove the checked-in one
                                t_times[i] = "";
                                dirty = true;
                            }
                        }
                    }
                }
                if (dirty)
                {
                    string tmp = "";
                    for (int i = 0; i < t_times.Length; i++)
                    {
                        if (t_times[i] != "") tmp += t_times[i] + ";";
                    }
                    if (tmp.Length > 0)
                    {
                        tmp = tmp.Substring(0, tmp.Length - 1);
                    }
                    lvi.SubItems[3].Text = tmp;
                }
            }

            foreach (ListViewItem lvi in lv_Confirmation.Items)
            {
                if (lvi.SubItems[3].Text.Trim().Length < 6)
                    lv_Confirmation.Items.Remove(lvi);
            }

            // check time offset again during mid-night
            if (now.Minute == 0 && now.Hour == 0 && now.Second < 30)
            {
                btn_CheckTime_Click(null, null);
            }
        }

        #endregion
    }
}
