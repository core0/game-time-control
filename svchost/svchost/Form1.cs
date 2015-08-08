using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;

using System.Text;
using System.Windows.Forms;
//
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.Media;

namespace svchost
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        class Telnet
        {
            public Boolean access = true;
            
            private String USER = "admin\n";
            private String PASS = "admin\n";

            private String IP = Dns.GetHostByName(Dns.GetHostName()).AddressList[0].ToString();
            private int numbRule;

            private String VIEWRULE = "iptables -L --line-numbers\n";
            private String ADDRULE = "iptables -A FORWARD -p all -s ~ -j DROP\n";
            private String DELETERULE = "iptables -D FORWARD ~\n";

            private TcpClient tcpc = new TcpClient();

            //поиск запрещающего правила 
            private bool NetState(byte[] bt)
            {
                String[] rules = Encoding.ASCII.GetString(bt).Split('\n');

                foreach (String rl in rules)
                    if (rl.IndexOf("DROP") > -1 && rl.IndexOf(IP) > -1)
                    {
                        //определение нормера запрещаюшего правила
                        Int32.TryParse(rl.Substring(0, 2), out this.numbRule);
                        return false;
                    }

                return true;
            }

            //получение таблицы правил iptables
            private byte[] ViewRule()
            {
                byte[] bt = new byte[4000];

                this.tcpc.Client.Send(Encoding.ASCII.GetBytes(this.VIEWRULE));
                Thread.Sleep(200);
                this.tcpc.Client.Receive(bt);

                return bt;
            }


            //открытие доступа в сеть
            public void OpenAccess()
            {
                this.access = this.NetState(this.ViewRule());

                if (!this.access)
                {
                    String req = this.DELETERULE.Replace("~", this.numbRule.ToString());
                    Thread.Sleep(300);
                    this.tcpc.Client.Send(Encoding.ASCII.GetBytes(req));

                    this.access = true;
                }
            }

            //закрытие доступа в сеть
            public void CloseAcces()
            {
                if (this.access)
                {
                    String req = this.ADDRULE.Replace("~", this.IP);
                    Thread.Sleep(300);
                    this.tcpc.Client.Send(Encoding.ASCII.GetBytes(req));

                    this.access = false;
                }
            }

            // закрываем подключение к телнет
            public void CloseConn()
            {
                try
                {
                    Thread.Sleep(300);
                    this.tcpc.Client.Send(Encoding.ASCII.GetBytes("exit\n"));
                    this.tcpc.Close();
                }
                catch
                { }
            }

            //подключение к телнет
            public bool autorization()
            {
                byte[] bt = new byte[5000];
                this.tcpc = new TcpClient();
                this.tcpc.Connect("192.168.1.1", 23);

                this.tcpc.Client.Send(Encoding.ASCII.GetBytes("admin\n"));
                Thread.Sleep(200);

                this.tcpc.Client.Send(Encoding.ASCII.GetBytes("admin\n"));
                Thread.Sleep(200);
                this.tcpc.Client.Receive(bt);

                //валидация имени и пароля
                if (Encoding.ASCII.GetString(bt).IndexOf("BusyBox") == -1)
                {
                    return false;
                }
                else
                {
                    //определяем this.access
                    this.access = this.NetState(this.ViewRule());
                }

                return true;
            }
        }


        class day
        {
            public String templateFileName = "pir_temp.log";
            public String mainLogs = "pir_log.log";
            public String DetectProc = "Game";

            static Int32 MAXTIME = 180;
            static Int32 TIMETOSOUND = MAXTIME - 5;

            private String id;
            private Byte counter;
            private Telnet tl = new Telnet();

            public string getCounters()
            {
                return this.counter.ToString();
            }

            public day()
            {
                //подключение к телнет 
                this.tl.autorization();

                //генерация id = день + месяц + год
                this.id = DateTime.Now.Day.ToString() + DateTime.Now.Month + DateTime.Now.Year.ToString();

                FileInfo fif = new FileInfo("C:\\windows\\" + templateFileName);
                if (!fif.Exists)
                    File.WriteAllText(fif.FullName, " ");

                String state = File.ReadAllText("C:\\windows\\" + templateFileName);

                if (state.Length > 1)
                {
                    //извлекаем данные из временного файла
                    //если программа была выключена, или перезагружен компьютер
                    //отсчет продолжится 
                    String tmp_id = state.Substring(0, state.IndexOf('='));
                    String tmp_counters = state.Substring(state.IndexOf('=') + 1, state.Length - state.IndexOf('=') - 1);

                    if (this.id == tmp_id)
                    {
                        this.counter = Convert.ToByte(tmp_counters);
                    }
                    else
                    {
                        //открытие доступа
                        this.tl.OpenAccess();
                        this.counter = 0;
                    }
                }
                else
                {
                    this.tl.OpenAccess();
                    this.counter = 0;
                }

                this.tl.CloseConn();
            }

            // counter++ и сохранение резулятата
            public void inrc()
            {
                if (this.counter <= MAXTIME)
                    this.counter++;

                this.save();
            }

            // сохранение времени работы процесса и звуковой сигнал
            bool flagSound = true;
            private bool save()
            {
                if (this.counter < MAXTIME)
                {
                    if (this.counter == TIMETOSOUND && flagSound)
                    {
                        //проиграть извещение об окончании времени игры
                        flagSound = false;
                        SoundPlayer player = new SoundPlayer();
                        player.SoundLocation = "C:\\windows\\aoogah.wav";
                        player.Play();
                    }

                    if (this.counter <= MAXTIME && !tl.access)
                    {
                        tl.autorization();
                        tl.OpenAccess();
                        tl.CloseConn();
                    }

                    // сохранение прогресса времени
                    File.WriteAllText("C:\\windows\\" + templateFileName, this.id.ToString() + "=" + this.counter.ToString());
                }
                else
                    this.end();

                return true;
            }


            private void end()
            {
                FileInfo fif = new FileInfo("C:\\windows\\" + this.mainLogs);
                if (!fif.Exists)
                    File.WriteAllText(fif.FullName, " ");
                String tresh = File.ReadAllText("C:\\windows\\" + this.mainLogs);

                //запись, в главный лог файл, информации о достигшем лимите за день
                if (tresh.IndexOf(this.id.ToString()) == -1)
                {
                    File.AppendAllText("C:\\windows\\" + this.mainLogs, this.id.ToString() + "=" + this.counter.ToString() + "\n");
                }

                this.runDrop();
            }

            //drop internet
            private void runDrop()
            {
                if (tl.access)
                {   //подключение к телнет 
                    this.tl.autorization();
                    this.tl.CloseAcces();
                    this.tl.CloseConn();
                }

            }

        }//end class day

        public bool flg_on_day = true;

        public void RunDetectProc()
        {
            day d = new day();

            while (flg_on_day)
            {

                //получить список процессов
                Process[] pr = Process.GetProcesses();

                foreach (Process t in pr)
                {
                    try
                    {
                        if (t.MainModule.FileName == "D:\\Games\\Пиратия Online\\system\\game.exe")
                        {
                            d.inrc();
                            //label1.Invoke(new MethodInvoker(delegate() { label1.Text = "Проработал = " + d.getCounters(); }));
                            //label1.Text = "Проработал = " + d.getCounters();
                            break;
                        }
                    }
                    catch
                    {}
                }

                //проверка каждую минуту
                Thread.Sleep(1000 * 60);

                if (d.getCounters() != DateTime.Now.Day.ToString() + DateTime.Now.Month + DateTime.Now.Year.ToString())
                {
                    d = new day();
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;

            Thread dt = new Thread(RunDetectProc);
            dt.Name = "detect";
            dt.IsBackground = true;
            dt.Start();
        }
    }
}
