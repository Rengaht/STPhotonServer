using ExitGames.Logging;
using Photon.SocketServer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using MySql.Data.MySqlClient;
using System.Xml;
using System.Data.SqlClient;
using System.Data.Common;

namespace STPhotonServer
{
    public class STGameApp
    {
        public bool enable_db = true;

        bool debug_mode = false;
        int debug_game = 0;

        protected static readonly ILogger Log=LogManager.GetCurrentClassLogger();

        GameScene[] agame_scene;
        int cur_game;// { get; set; }
        
        
        List<PeerBase> aclient_peer;
        Boolean LED_Ready;
        public Boolean led_ready
        {
            get { return LED_Ready; }
        }
        //List<String> aclient_id;

        private string conn_str_log, conn_str_dword, conn_str_schedule;

        MySqlConnection sql_connection,dword_sql_connection,schedule_sql_connection;
        MySqlCommand sql_command,dword_cmd,schedule_cmd,checkid_cmd;

        STServerPeer led_peer;
        public STServerPeer LED_Peer {
            set { led_peer=value; }
        }

        private bool switchable_mode = true;
        private int switch_next_game = -1;

        private Timer timer_cleaner;
        private int CLEAN_SPAN = 5000;

        private string Ver_Ios;
        private string Ver_Android;


        public Timer sleep_game_timer;

        public STGameApp(List<PeerBase> lpeer_)
        {
            aclient_peer=lpeer_;
            LED_Ready=false;

            XmlDocument doc = new XmlDocument();
            doc.Load("../server_params.xml");            
            String sql_address = doc.DocumentElement.SelectSingleNode("/PARAM/SQL_DATA/ADDRESS").InnerText;           
            String sql_uid = doc.DocumentElement.SelectSingleNode("/PARAM/SQL_DATA/UID").InnerText;
            String sql_pass = doc.DocumentElement.SelectSingleNode("/PARAM/SQL_DATA/PASSWORD").InnerText;

            String sql_str = "server=" + sql_address + ";uid=" + sql_uid + ";pwd=" + sql_pass+";";
            Log.Info("SQL INFO: "+sql_str);

            Ver_Ios = doc.DocumentElement.SelectSingleNode("/PARAM/APP_VERSION/IOS").InnerText;
            Ver_Android = doc.DocumentElement.SelectSingleNode("/PARAM/APP_VERSION/ANDROID").InnerText;
            
            Log.Info("APP VERSION: iOS- " +Ver_Ios+"  Android- "+Ver_Android);

            // open db
            if (enable_db)
            {
                // database for id and log
                conn_str_log = sql_str+"database=stapp_logdb;CharSet=utf8";
                sql_connection = new MySqlConnection(conn_str_log);
                sql_command = sql_connection.CreateCommand();
                sql_connection.Open();               
                

                // setup check id command
                checkid_cmd = new MySqlCommand();
                checkid_cmd.Connection = sql_connection;
                checkid_cmd.CommandText = "find_existing_id";
                checkid_cmd.CommandType = System.Data.CommandType.StoredProcedure;
                checkid_cmd.Parameters.AddWithValue("@pid", "ppid");
                checkid_cmd.Parameters["@pid"].Direction = System.Data.ParameterDirection.Input;

                checkid_cmd.Parameters.AddWithValue("@pcount", MySqlDbType.Int32);
                checkid_cmd.Parameters["@pcount"].Direction = System.Data.ParameterDirection.Output;




                conn_str_dword = sql_str + "database=dirtydb;CharSet=utf8";
                dword_sql_connection = new MySqlConnection(conn_str_dword);
                dword_sql_connection.Open();

                if (dword_sql_connection != null)
                {
                    dword_cmd = new MySqlCommand();
                    dword_cmd.Connection = dword_sql_connection;
                    dword_cmd.CommandText = "find_word";
                    dword_cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    dword_cmd.Parameters.AddWithValue("@pword", "name");
                    dword_cmd.Parameters["@pword"].Direction = System.Data.ParameterDirection.Input;


                    dword_cmd.Parameters.AddWithValue("@pcount", MySqlDbType.Int32);
                    dword_cmd.Parameters["@pcount"].Direction = System.Data.ParameterDirection.Output;
                }

                conn_str_schedule = sql_str + "database=stapp_schedule;";
                schedule_sql_connection = new MySqlConnection(conn_str_schedule);
                schedule_cmd = schedule_sql_connection.CreateCommand();
                schedule_sql_connection.Open();

                //getHourlyGameSchedule();
                //getGameSchedule();

                //checkGoodName("aaa");
                //checkGoodName("animalsex");

            }

            //aclient_id = new List<String>();
            // load existing IDs from past games
            //getExistingUserID();


            agame_scene = new GameScene[3];
            agame_scene[0] = new GameA(this);
            agame_scene[1] = new GameB(this);
            agame_scene[2] = new GameC(this);

        }


        public void addNewClientPeer(PeerBase peer)
        {
            //clearClientList();

            aclient_peer.Add(peer);
        }
        public void removeClientPeer(PeerBase peer)
        {
            aclient_peer.Remove(peer);

            // remove from game's player list??
            //agame_scene[cur_game].online_client.Remove((STServerPeer)peer);

        }

        private void initGame(int game_index)
        {

            Log.Debug("APP Init Game "+game_index);
            
            cur_game=game_index;

            if (cur_game>=0 && cur_game<=2)
            {
                agame_scene[cur_game].InitGame();
            }
            else if(cur_game==-1)
            {
                //start sleep timer for another 10min
                if (sleep_game_timer != null) sleep_game_timer.Close();

                sleep_game_timer = new Timer(600000);
                sleep_game_timer.Elapsed += new ElapsedEventHandler(goNextGameAfterSleep);
                sleep_game_timer.AutoReset = false;
                sleep_game_timer.Enabled = true;
                //total_game_timer.Start();

                Log.Info("Start Sleep Timer");
            }
            else
            {
                Log.Info("Get Illegal Game: "+cur_game);
            }

            SendNotifyLED(STServerCode.Id_And_Game_Info,new Dictionary<byte,object>() { { (byte)1,cur_game } });

            EventData event_data = new EventData((byte)STServerCode.CChange_Game,
                                               new Dictionary<byte,object>() { { (byte)1,cur_game}});
            foreach(STServerPeer peer in aclient_peer)
            {
                peer.sendEventToPeer(event_data);
            }

            switch_next_game = -1;
        }

        private void goNextGameAfterSleep(object sender, ElapsedEventArgs e)
        {
            goNextGame();
        }
       
        public void goNextGame()
        {
            Log.Warn(">>>> Go Next Game");
            //if (debug_mode) {
            //    if (switchable_mode)
            //    {
            //        Log.Warn("Switch to Game: "+switch_next_game);
            //        if (switch_next_game > -1)
            //        {
            //            initGame(switch_next_game);
                   
            //        }
            //        else initGame(cur_game);

            //        return;
            //    }
            //    else initGame(debug_game);
            //}
            //else{
            //    initGame(getGameSchedule()-1);
            //}
            initGame(getGameSchedule() - 1);
        }

        public void SendNotifyLED(STServerCode event_code,Dictionary<byte,Object> event_param)
        {
            if(led_peer!=null && led_peer.Connected) led_peer.sendEventToPeer(new EventData((byte)event_code,event_param));
            else Log.Warn("There's no LED connected !!!");
            
        }
        public string createNewClientId()
        {
            String new_id = Guid.NewGuid().ToString();
            //aclient_id.Add(new_id);

            addToUserDatabase(new_id);


            return new_id;
        }
        
        public void setupLedPeer(STServerPeer peer)
        {
            //if(led_peer!=null) return;


            aclient_peer.Remove(peer);

            // disconnect all other clients
            foreach(STServerPeer rest_peer in aclient_peer)
            {
                rest_peer.delayDisconnect();
            }
            //aclient_peer.Clear();

            
            this.led_peer=peer;
            LED_Ready=true;

            if (debug_mode) initGame(debug_game);
            else
            {
                initGame(getGameSchedule()-1);
            }
        }
        
        public void requestGameScore()
        {
            
            SendNotifyLED(STServerCode.LRequest_Score,new Dictionary<byte,object>());

        }
        public void sendGameOverToAll(Dictionary<byte,Object> event_params)
        {
            
            //agame_scene[cur_game].EndGame();

            EventData event_data=new EventData((byte)STServerCode.CSend_GG,
                                                new Dictionary<byte,object>() );
            foreach(STServerPeer peer in aclient_peer)
            {
                peer.sendEventToPeer(event_data);
            }

            
        }

        public int getClientCount()
        {
            return aclient_peer.Count;
        }

        public int getClientIndex(PeerBase peer)
        {
            int index=aclient_peer.IndexOf(peer);
            return index;

        }
        public bool checkAvailable()
        {
            return aclient_peer.Count<agame_scene[cur_game].Client_Limit;
        }

        public void handleMessage(STServerPeer sender,STClientCode code,Dictionary<byte,object> ev_params)
        {
            agame_scene[cur_game].handleMessage(sender,code,ev_params);
        }
       
      
        public STServerPeer getClientPeer(int index)
        {
            return (STServerPeer)aclient_peer[index];
        }

        /* check the ID given by user
         * 
         */
        public bool getValidId(ref String p)
        {

            if (checkValidId(p)) return true;
            else
            {   
                p = createNewClientId();
                Log.Debug("Create New ID: " + p); 
                return false;
            }

        }
        public bool checkValidId(String id_string)
        {
            //if (id_string == null) return false;
            //return aclient_id.Contains(id_string);


            if (!enable_db) return true;
            checkSqlConnection();


            checkid_cmd.Parameters["@pid"].Value = id_string;
            checkid_cmd.ExecuteNonQuery();

            int pcount = (int)checkid_cmd.Parameters["@pcount"].Value;
            Log.Warn("Check Valid ID In db: " + pcount);

            return (pcount > 0);


        }
        public bool checkLed()
        {
            if (led_peer==null || !led_peer.Connected)
            {
                LED_Ready = false;
                
                Log.Warn("Check LED NOT Ready!");
                
            }
            else
            {
                Log.Warn("Check LED Ready!");
                LED_Ready = true;
            }
            return LED_Ready;
        }
        public void killAllPeer()
        {
            foreach (STServerPeer rest_peer in aclient_peer)
            {
                rest_peer.delayDisconnect();
            }
        }
        public void killPeer(STServerPeer peer)
        {
            Log.Debug("Kill Peer: "+peer.client_id);
            peer.Disconnect();

            setupCleaner();
        }
        private void clearClientList(object sender, ElapsedEventArgs e)
        {            
            aclient_peer.RemoveAll(s=> !s.Connected);
            agame_scene[cur_game].online_client.RemoveAll(s => !s.Connected);
           
            Log.Debug("Clear List, rest client= " + aclient_peer.Count);
        }
        private void setupCleaner()
        {
            if(timer_cleaner!= null) timer_cleaner.Close();            

            timer_cleaner = new Timer(CLEAN_SPAN);
            timer_cleaner.Elapsed += new ElapsedEventHandler(clearClientList);
            timer_cleaner.AutoReset = false;
            timer_cleaner.Enabled = true;
   
        }

        public int getCurGame()
        {
            checkLed();
            if(LED_Ready) return cur_game;
            return -1;
        }

        public void switchGame(int switch_to_game)
        {
            if (cur_game == switch_to_game) return;

            switch_next_game = switch_to_game;


           agame_scene[cur_game].ForceEndGame();

        }
        public String getIosVersion()
        {
            return Ver_Ios;
        }
        public String getAndroidVersion(){
            return Ver_Android;
        }

#region Handle Sql

        private void addToUserDatabase(String id_str)
        {
            if (!enable_db) return;

            checkSqlConnection();
            
            String time_str = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            sql_command.CommandText = "Insert into user_id_table(time_create,user_id) values('" + time_str + "','" + id_str + "')";
            sql_command.ExecuteNonQuery();
        }

        public void closeDatabase()
        {
            if (enable_db)
            {
                sql_connection.Close();
                dword_sql_connection.Close();
                schedule_sql_connection.Close();
            }
        }
        public void checkSqlConnection()
        {
            bool logsql=checkSqlConnection(sql_connection);
            if (!logsql)
            {
                sql_connection = new MySqlConnection(conn_str_log);
                sql_connection.Open();
                sql_command.Connection = sql_connection;
                checkid_cmd.Connection = sql_connection;
            }

            bool dwordsql = checkSqlConnection(dword_sql_connection);
            if (!dwordsql)
            {
                dword_sql_connection = new MySqlConnection(conn_str_dword);
                dword_sql_connection.Open();
                dword_cmd.Connection=dword_sql_connection;
            }


            bool schedulesql = checkSqlConnection(schedule_sql_connection);
            if (!schedulesql)
            {
                schedule_sql_connection = new MySqlConnection(conn_str_schedule);
                schedule_sql_connection.Open();
                schedule_cmd.Connection = schedule_sql_connection;
            }


        }
        private bool checkSqlConnection(MySqlConnection conn)
        {
            //if (!conn.Ping())
            //{
            //    Log.Info("CheckSqlConnection: Fail!");
            //    return false;
            //}
            if (!(conn.State == System.Data.ConnectionState.Open))
            {
                Log.Info("CheckSqlConnection: Fail!");
                return false;
                //conn.Close();
                //conn.Open();
            }
            Log.Info("CheckSqlConnection: Good!");
            return true;
        }

        //private void getExistingUserID()
        //{   
        //    // TODO: decide how long ids should be kept
        //    // String select_str = "Select user_id FROM user_id_table WHERE time_create like '2015-04%'";

        //    if (!enable_db) return;

        //    String select_str = "Select user_id FROM user_id_table";
        //    MySqlCommand select_command = new MySqlCommand(select_str, sql_connection);
        //    MySqlDataReader reader = select_command.ExecuteReader();
        //    while (reader.Read())
        //    {
        //        Log.Debug(reader.GetString(0));
        //        aclient_id.Add(reader.GetString(0));
        //    }
        //    reader.Close();

        //}
        public MySqlConnection getSqlConnection()
        {
            if (!enable_db) return null;
            return sql_connection;
        }

        public bool checkGoodName(String name)
        {

            if (!enable_db) return true;

            checkSqlConnection();

            dword_cmd.Parameters["@pword"].Value = name;
            dword_cmd.ExecuteNonQuery();

            int pcount = (int)dword_cmd.Parameters["@pcount"].Value;
            Log.Warn("Check Good Word In db: "+pcount);

            return !(pcount>0);
        }

        //public int[] getHourlyGameSchedule()
        //{
        //    int mhour=24;
        //    int mseg=6;

        //    int[] arr_game = new int[mhour*mseg];
        //    for (int x = 0; x < mhour; ++x)
        //    {
        //        String str_hour = String.Format("{0:00}", x);
        //        String p = "";
        //        for (int i = 0; i < mseg; ++i)
        //        {
        //            String str_min = String.Format("{0:00}",i*10);
        //            String sql_str = "SELECT * FROM timetab WHERE start_time='" + str_hour +str_min +"'";

        //            schedule_cmd.CommandText = sql_str;
        //            MySqlDataReader reader = schedule_cmd.ExecuteReader();
                    
        //            if(reader.Read()){
                    
        //                    arr_game[i+x*mseg] = reader.GetInt32(1);
        //                    p += (arr_game[i]-1);
        //            }
        //            reader.Close();
        //        }

        //        Log.Info("Hour Schedule: " + str_hour + "  -> " + p);
            
        //    }
            

        //    return arr_game;
        //}
        public int getGameSchedule()
        {
            var date = DateTime.Now;
            
            return getGameSchedule(date.Hour,date.Minute);
        }
        

        public int getGameSchedule(int ihour,int iminute)
        {

            int imin = (int)Math.Floor(iminute / 10.0); 
            
            int[] arr_game = new int[6];
            String str_hour = String.Format("{0:00}", ihour);
            String str_min = String.Format("{0:00}", imin * 10);
            String sql_str = "SELECT * FROM timetab WHERE start_time='" + str_hour + str_min + "'";

            int igame = 0;

            schedule_cmd.CommandText = sql_str;

            checkSqlConnection(schedule_sql_connection);

            MySqlDataReader reader = schedule_cmd.ExecuteReader();
            if(reader.Read())
            {
                object i=reader.GetValue(1);
              
                if(i!=null) igame=(Int32)i;
                    
            }
            reader.Close();

            Log.Info("--------  Read Game Schedule: " + str_hour + str_min + " -> " + igame + " --------");

            return igame;
        }
#endregion


     
    }
}
