using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

class dht_logger
{
    static SynchronizedCollection<string> logs = new SynchronizedCollection<string>();
    static string[] latest = new string[2];
    static HttpListener dht_lts;

    static void Main(string[] args)
    {
        //Taskで全ての処理を別スレッドにて行う
        Task.Run(() =>
        {
            udplistener();
        });
        Task.Run(() =>
        {
            logger();
        });
        Task.Run(() =>
        {
            cam_sock();
        });
        Task.Run(() =>
        {
            chart_sock();
        });

        //Mainスレッド君はここで悠久の時を過ごす
        using (ManualResetEvent mre = new ManualResetEvent(false))
        {
            mre.WaitOne();
        }
    }

    //
    /*tcp for chart*/
    //
    static void chart_sock()
    {

    }
    //
    /*tcp for chart*/
    //


    //
    /*websokcet for cam*/
    // 参考(ほぼ丸パクリ)にさせていただいたサイト様 : https://qiita.com/Zumwalt/items/53797b0156ebbdcdbfb1  https://qiita.com/washikawau/items/bfcd8babcffab30e6d26
    //上記サイトのやり方に udp & tcp 非同期通信のやり方をトッピングする
    static void cam_sock()
    {
        Console.WriteLine("cam_ws server started");

        dht_lts = new HttpListener();
        dht_lts.Prefixes.Add("http://*:60005/ws/");
        dht_lts.Start();

        dht_lts.BeginGetContext(OnRequested, dht_lts);
    }

    static void OnRequested(IAsyncResult ar)
    {
        HttpListener listener = (HttpListener)ar.AsyncState;

        //IAsyncResultが接続情報を握ってる？からwebsock()外部でGetContextする
        var hc = listener.EndGetContext(ar);

        Console.WriteLine("connected");

        if (!hc.Request.IsWebSocketRequest)
        {
            //クライアント側にエラー(400)を返却し接続を閉じる
            hc.Response.StatusCode = 400;
            hc.Response.Close();
            listener.BeginGetContext(OnRequested, listener);
            return;
        }

        //wsで接続できた時のみ別スレッドで受信・送信処理を回す
        Task.Run(() =>
        {
            websock(hc);
        });

        listener.BeginGetContext(OnRequested, listener);
    }

    static async void websock(HttpListenerContext hc)
    {
        var wsc = await hc.AcceptWebSocketAsync(null);
        var ws = wsc.WebSocket;

        while (true)
        {
            try
            {
                var buffer = Encoding.UTF8.GetBytes(latest[0] + "℃ " + latest[1] + "%");
                var segment = new ArraySegment<byte>(buffer);

                await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                try
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
                }
                catch (Exception exet) { Console.WriteLine(exet.Message); }
                break;
            }

            Thread.Sleep(1000);
        }

        hc.Response.StatusCode = 400;
        hc.Response.Close();
    }
    //
    /*websocket for cam*/
    //


    //
    /*udplistener*/
    //センサ　→ センサ取得プログラム　→　このプログラム
    static void udplistener()
    {
        IPEndPoint ipend = new IPEndPoint(IPAddress.Any, 15622);
        UdpClient udpClient = new UdpClient(ipend);

        udpClient.BeginReceive(ReceiveCallback, udpClient);
    }

    static void ReceiveCallback(IAsyncResult ar)
    {
        UdpClient udp = (UdpClient)ar.AsyncState;

        IPEndPoint remoteEP = null;
        byte[] buffer = null;

        try
        {
            buffer = udp.EndReceive(ar, ref remoteEP);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        string rcvMSG = Encoding.UTF8.GetString(buffer);

        logs.Add(DateTime.Now.ToString("HH=mm-ss") + ":" + rcvMSG);
        //Console.WriteLine(rcvMSG);
        latest = rcvMSG.Split('@');

        udp.BeginReceive(ReceiveCallback, udp);
    }
    //
    /*udplistener*/
    //


    //ログファイルを作成する
    //突貫工事につき要修正
    //
    /*logger*/
    //ルートディレクトリ直下に専用のディレクトリを作成
    static void logger()
    {
        while (true)
        {
            bool flag = false;

            //ディレクトリ構造
            // /dhtlogs/[Every Date (yyyy-MM-dd)]/[Every HOUR].log

            string[] dater = DateTime.Now.ToString("yyyy-MM-dd").Split('-');
            string hour = DateTime.Now.ToString("HH");

            string dir = "/dhtlogs/" + dater[0] + "/" + dater[1] + "/" + dater[2] + "/";
            string file = hour + ".log";

            Console.WriteLine("start");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (!File.Exists(dir + file))
            {
                File.Create(dir + file).Close();
            }

            Console.WriteLine(dir + file);

            try
            {
                while (true)
                {
                    if (logs.Count > 0)
                    {
                        List<string> log = new List<string>(logs);
                        logs.Clear();

                        using (FileStream fs = new FileStream(dir + file, FileMode.Append, FileAccess.Write))
                        {
                            using (StreamWriter sw = new StreamWriter(fs))
                            {
                                int i = 0;

                                foreach (string content in log)
                                {
                                    if (content.Split('=')[0] != hour)
                                    {
                                        Console.WriteLine("change time");
                                        flag = true;
                                        break;
                                    }
                                    Console.WriteLine(content.Split('=')[1]);
                                    sw.WriteLine(content.Split('=')[1], true);
                                    i++;
                                }

                                if (flag)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    Thread.Sleep(900);
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

        }
    }
    //
    /*logger*/
    //


    //
    /*log reader*/
    //
    static List<string> fl_reader(int cmd)
    {
        //ディレクトリ構造
        // /dhtlogs/[Every Date (yyyy-MM-dd)]/[Every HOUR].log

        DateTime date = DateTime.Now;
        DateTime target_date = DateTime.Now.AddHours(-1 * cmd);
        bool target_reach_flag = false;
        List<string> str = null;
        List<string> old_data = null;


        string dater = date.ToString("yyyy-MM-dd");
        string hour = date.ToString("HH");

        string dir = "/dhtlogs/" + dater + "/";
        string file = hour + ".log";

        Console.WriteLine("start");

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (!File.Exists(dir + file))
        {
            File.Create(dir + file).Close();
        }


        using (FileStream fs = new FileStream(dir + file, FileMode.Open, FileAccess.Read))
        {
            using (StreamReader sr = new StreamReader(fs))
            {
                try
                {
                    str = new(sr.ReadToEnd().Split('\n'));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
        if (str == null)
        {
            str = new List<string>();
        }
        str.Remove("");
        str.Reverse();


        while (!target_reach_flag)
        {
            date = date.AddHours(-1);

            dater = date.ToString("yyyy-MM-dd");
            hour = date.ToString("HH");

            dir = "/dhtlogs/" + dater + "/";
            file = hour + ".log";

            Console.WriteLine("start");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (!File.Exists(dir + file))
            {
                File.Create(dir + file).Close();
            }


            using (FileStream fs = new FileStream(dir + file, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader sr = new StreamReader(fs))
                {
                    try
                    {
                        old_data = new(sr.ReadToEnd().Split('\n'));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            if (old_data == null)
            {
                old_data = new();
            }
            old_data.Remove("");
            old_data.Reverse();

            foreach (string content in old_data)
            {
                str.Add(content.Split(':')[1]);

                if (DateTime.Parse(content.Split(':')[0]) <= target_date)
                {
                    target_reach_flag = true;
                    break;
                }
            }
        }

        return str;
    }
    //
    /*log reader*/
    //
}