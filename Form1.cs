using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace webbrowser_test
{
    public partial class Form1 : Form
    {
        private int step = 0;
        private string useragent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.66 Safari/537.36\r\n";
        private DateTime updateinprogress = System.DateTime.Now.AddHours(-10);

        System.IO.StreamWriter logWriter;

        private string lastip = "";

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                logWriter = new System.IO.StreamWriter("app.log", true);
            }
            catch (Exception ex)
            {
                DoLog("WARN", "can't write app.log file (" + ex.Message + ")");
                logWriter = null;
            }
            DoLog("INFO", "app started");
            DoLog("INFO", "start folder: " + Application.StartupPath);
            LoadConfig();
            BrowserNavigate("https://www.noip.com/login");
            timer1.Enabled = true;
        }

        private void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (webBrowser1.Url.ToString().Contains("https://www.noip.com/login") && step == 0)
            {
                DoLog("INFO", "inserting username/password");
                HtmlElementCollection inputs = webBrowser1.Document.GetElementsByTagName("input");
                for (int i = 0; i < inputs.Count; i++)
                {
                    if (inputs[i].Name == "username")
                    {
                        inputs[i].SetAttribute("value", textBox1.Text);
                    }

                    if (inputs[i].Name == "password")
                    {
                        inputs[i].SetAttribute("value", textBox2.Text);
                    }
                }

                DoLog("INFO", "logging in...");
                HtmlElementCollection buttons = webBrowser1.Document.GetElementsByTagName("button");
                for (int i = 0; i < buttons.Count; i++)
                {
                    if (buttons[i].Name == "Login")
                    {
                        buttons[i].InvokeMember("Click");
                        break;
                    }
                }

                step = 1;
            }
            else if (webBrowser1.Url.ToString().Contains("my.noip") && step == 1)
            {
                DoLog("INFO", "logged in");
                BrowserNavigate("https://www.noip.com/members/dns/dyn-groups.php");
                step = 2;
            }
            else if (webBrowser1.Url.ToString().Contains("dns/dyn-groups.php") && step == 2)
            {
                string actip = GetIP();
                DoLog("INFO", "inserting IP address");
                HtmlElementCollection inputs = webBrowser1.Document.GetElementsByTagName("input");
                for (int i = 0; i < inputs.Count; i++)
                {
                    if (inputs[i].Name == "IP")
                    {
                        inputs[i].SetAttribute("value", actip);
                        break;
                    }
                }

                DoLog("INFO", "updating IP address");
                for (int i = 0; i < inputs.Count; i++)
                {
                    if (inputs[i].Name == "Update IP")
                    {
                        inputs[i].InvokeMember("Click");
                        break;
                    }
                }

                step = -1;
                DoLog("INFO", "IP updated (" + lastip + " -> " + actip + ")");
                lastip = actip;
                PushoverNotification(String.Format("IP updated {0}", lastip));
                BrowserNavigate("about: blank");
                GC.Collect(0, GCCollectionMode.Forced);
            }
        }

        private string GetIP()
        {
            DoLog("INFO", "getting public IP address");
            string externalip = "";
            try
            {
                externalip = new System.Net.WebClient().DownloadString("http://ipinfo.io/ip");
                DoLog("INFO", "got IP (" + externalip + ")");
            }
            catch (Exception ex)
            {
                DoLog("ERROR", "can't get IP (" + ex.Message + ")");
            }

            return externalip;
        }

        private bool CheckIfLoggedIn()
        {
            return true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            step = 0;
            BrowserNavigate("https://www.noip.com/login");
        }

        private void BrowserNavigate(string url)
        {
            DoLog("INFO", "navigating to: " + url);
            webBrowser1.Navigate(url, null, null, "User-Agent: " + useragent);
        }

        private void SaveConfig(string user, string pw, string interval, string po_token, string po_user)
        {
            DoLog("INFO", "saving config file");
            try
            {
                System.IO.StreamWriter sw = new System.IO.StreamWriter("app.cfg");
                sw.WriteLine(user);
                sw.WriteLine(pw);
                sw.WriteLine(interval);
                sw.WriteLine(po_token);
                sw.WriteLine(po_user);
                sw.Close();
            }
            catch (Exception ex)
            {
                DoLog("WARN", "can't save config file (" + ex.Message + ")");
            }
            timer1.Interval = Convert.ToInt32(numericUpDown1.Value) * 1000;
        }

        private void LoadConfig()
        {
            DoLog("INFO", "loading config");
            if (System.IO.File.Exists("app.cfg"))
            {
                DoLog("INFO", "app.cfg found");
                try
                {
                    System.IO.StreamReader sr = new System.IO.StreamReader("app.cfg");
                    textBox1.Text = sr.ReadLine();
                    textBox2.Text = sr.ReadLine();
                    numericUpDown1.Value = Convert.ToDecimal(sr.ReadLine());
                    timer1.Interval = Convert.ToInt32(numericUpDown1.Value) * 1000;
                    textBox3.Text = sr.ReadLine();
                    textBox4.Text = sr.ReadLine();
                    sr.Close();

                    DoLog("INFO", "app.cfg loaded");
                }
                catch (Exception ex)
                {
                    DoLog("ERROR", "app.cfg not valid (" + ex.Message + ")");
                    if (MessageBox.Show(String.Format("Hibás a config fájl. Kérlek ellenőrizd!\r\n\r\n({0})\r\nSzeretnéd megnyitni?", ex.Message), "app.cfg", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start("notepad.exe", "app.cfg");
                    }
                    DoLog("INFO", "exiting...");
                    Application.Exit();
                }
            }
            else
            {
                DoLog("WARN", "app.cfg not found");
                DoLog("INFO", "saving default app.cfg");
                SaveConfig("username", "password", "[interval (in s)]", "pushover token (leave empty to not use it)", "pushover user (leave empty to not use it)");
                if (MessageBox.Show("Nem volt config fájl, így ki lett írva.\r\nAz alkalmazás most kilép, kérlek szerkeszd a fájlt, majd indítsd újra!\r\n\r\nSzeretnéd a fájlt megnyitni szerkesztésre?", "app.cfg", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start("notepad.exe", "app.cfg");
                }
                DoLog("INFO", "exiting...");
                Application.Exit();
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            string newip = GetIP();
            if (newip != lastip && step == -1)
            {
                DoLog("INFO", "Check - IP not matching");
                step = 0;
                BrowserNavigate("https://www.noip.com/login");
                PushoverNotification(String.Format("IP mismatch ({0} {1})", lastip, newip));
            }
            else
            {
                DoLog("INFO", "Check - IP matching");
            }
            notifyIcon1.BalloonTipText = "IP: " + lastip;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SaveConfig(textBox1.Text, textBox2.Text, numericUpDown1.ToString(), textBox3.Text, textBox4.Text);
        }

        private void DoLog(string loglevel, string message)
        {
            logTextBox.AppendText("\r\n" + System.DateTime.Now.ToString() + " - " + loglevel.ToUpper() + " " + message);
            if (logWriter != null)
            {
                logWriter.WriteLine(System.DateTime.Now.ToString() + " - " + loglevel.ToUpper() + " " + message);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.ApplicationExitCall || e.CloseReason == CloseReason.TaskManagerClosing || e.CloseReason == CloseReason.WindowsShutDown)
            {
                DoLog("INFO", "exiting...");
                logWriter.Close();
                logWriter = null;
            }
            else
            {
                e.Cancel = true;
                this.Hide();
                notifyIcon1.Visible = true;
                //notifyIcon1.ShowBalloonTip(10000, "No-IP updater", "The app is still running.", ToolTipIcon.Info);
            }
        }

        private void PushoverNotification(string message)
        {
            if (textBox3.Text != "" && textBox4.Text != "")
            {
                DoLog("INFO", "Sending Pushover message");
                try
                {
                    string resp = "";
                    var webRequest = System.Net.WebRequest.CreateHttp("https://api.pushover.net/1/messages.json");
                    if (webRequest != null)
                    {
                        webRequest.Method = "POST";
                        webRequest.Timeout = 5000;
                        webRequest.ContentType = "application/x-www-form-urlencoded";

                        string data_str = String.Format("token={0}&user={1}&message={2}", Uri.EscapeDataString(textBox3.Text), Uri.EscapeDataString(textBox4.Text), Uri.EscapeDataString(message));
                        var data = Encoding.ASCII.GetBytes(data_str);

                        webRequest.ContentLength = data.Length;

                        using (var stream = webRequest.GetRequestStream())
                        {
                            stream.Write(data, 0, data.Length);
                        }

                        var response = (System.Net.HttpWebResponse)webRequest.GetResponse();
                        resp = new System.IO.StreamReader(response.GetResponseStream()).ReadToEnd();
                    }
                    DoLog("INFO", "Pushover sending finished");
                }
                catch (Exception ex)
                {
                    DoLog("ERROR", "Pushover sending failed (" + ex.Message + ")");
                }
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void maximizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
        }

        private void showIPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            notifyIcon1.ShowBalloonTip(10000, "No-IP updater", "IP: " + lastip, ToolTipIcon.Info);
            //MessageBox.Show(lastip, "Public IP address", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void restartApplicationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoLog("INFO", "restarting...");
            logWriter.Close();
            logWriter = null;

            Application.Restart();
        }

        private void fRefreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            step = 0;
            BrowserNavigate("https://www.noip.com/login");
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
        }
    }
}
