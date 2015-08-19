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
        List<String> aclient_id;
        
        MySqlConnection sql_connection,dword_sql_connection,schedule_sql_connection;
        MySqlCommand sql_command,dword_cmd,schedule_cmd;

        STServerPeer led_peer;
        public STServerPeer LED_Peer {
            set { led_peer=value; }
        }

        private bool switchable_mode = true;
        private int switch_next_game = -1;

        private Timer timer_cleaner;
        private int CLEAN_SPAN = 5000;

        public STGameApp(List<PeerBase> lpeer_)
        {
            aclient_peer=lpeer_;
            LED_Ready=false;

            XmlDocument doc = new XmlDocument();
            doc.Load("../server_params.xml");            
            String sql_address = doc.DocumentElement.SelectSingleNode("/SQL_DATA/ADDRESS").InnerText;           
            String sql_uid = doc.DocumentElement.SelectSingleNode("/SQL_DATA/UID").InnerText;
            String sql_pass = doc.DocumentElement.SelectSingleNode("/SQL_DATA/PASSWORD").InnerText;

            String sql_str = "server=" + sql_address + ";uid=" + sql_uid + ";pwd=" + sql_pass+";";
            Log.Info("SQL INFO: "+sql_str);

            // open db
            if (enable_db)
            {
                string connString = sql_str+"database=stapp_logdb;CharSet=utf8";
                sql_connection = new MySqlConnection(connString);
                sql_command = sql_connection.CreateCommand();
                sql_connection.Open();

                string dconnString = sql_str + "database=dirtydb;CharSet=utf8";
                dword_sql_connection = new MySqlConnection(dconnString);
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

                string sconnString = sql_str + "database=stapp_schedule;";
                schedule_sql_connection = new MySqlConnection(sconnString);
                schedule_cmd = schedule_sql_connection.CreateCommand();
                schedule_sql_connection.Open();

                getHourlyGameSchedule();
                //getGameSchedule();

                //checkGoodName("aaa");
                //checkGoodName("animalsex");

            }

            aclient_id = new List<String>();
            // load existing IDs from past games
            getExistingUserID();


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

            if (cur_game < 0) return;

            agame_scene[cur_game].InitGame();
            

            SendNotifyLED(STServerCode.Id_And_Game_Info,new Dictionary<byte,object>() { { (byte)1,cur_game } });

            EventData event_data = new EventData((byte)STServerCode.Id_And_Game_Info,
                                               new Dictionary<byte,object>() { { (byte)1,cur_game}});
            foreach(STServerPeer peer in aclient_peer)
            {
                peer.sendEventToPeer(event_data);
            }

            switch_next_game = -1;
        }
       
        public void goNextGame()
        {
            Log.Warn(">>>> Go Next Game");
            if (debug_mode) {
                if (switchable_mode)
                {
                    Log.Warn("Switch to Game: "+switch_next_game);
                    if (switch_next_game > -1)
                    {
                        initGame(switch_next_game);
                   
                    }
                    else initGame(cur_game);

                    return;
                }
                else initGame(debug_game);
            }
            else{
                initGame(getGameSchedule()-1);
            }
        }

        public void SendNotifyLED(STServerCode event_code,Dictionary<byte,Object> event_param)
        {
            if(led_peer!=null && led_peer.Connected) led_peer.sendEventToPeer(new EventData((byte)event_code,event_param));
            else Log.Warn("There's no LED connected !!!");
            
        }
        public string createNewClientId()
        {
            String new_id = Guid.NewGuid().ToString();
            aclient_id.Add(new_id);

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
                rest_peer.Disconnect();
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
            if (id_string == null) return false;
            return aclient_id.Contains(id_string);
        }
        public void checkLed()
        {
            if (led_peer==null || !led_peer.Connected)
            {
                LED_Ready = false;
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
       

#region Handle Sql

        private void addToUserDatabase(String id_str)
        {
            if (!enable_db) return;

            String time_str = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            sql_command.CommandText = "Insert into user_id_table(time_create,user_id) values('" + time_str + "','" + id_str + "')";
            sql_command.ExecuteNonQuery();
        }

        public void closeDatabase()
        {
            if (enable_db) sql_connection.Close();
        }

        private void getExistingUserID()
        {   
            // TODO: decide how long ids should be kept
            // String select_str = "Select user_id FROM user_id_table WHERE time_create like '2015-04%'";

            if (!enable_db) return;

            String select_str = "Select user_id FROM user_id_table";
            MySqlCommand select_command = new MySqlCommand(select_str, sql_connection);
            MySqlDataReader reader = select_command.ExecuteReader();
            while (reader.Read())
            {
                Log.Debug(reader.GetString(0));
                aclient_id.Add(reader.GetString(0));
            }
            reader.Close();

        }
        public MySqlConnection getSqlConnection()
        {
            if (!enable_db) return null;
            return sql_connection;
        }

        public bool checkGoodName(String name)
        {

            if (!enable_db) return true;

            dword_cmd.Parameters["@pword"].Value = name;
            dword_cmd.ExecuteNonQuery();

            int pcount = (int)dword_cmd.Parameters["@pcount"].Value;
            Log.Warn("Check Good Word In db: "+pcount);

            return !(pcount>0);
        }

        public int[] getHourlyGameSchedule()
        {
            int mhour=24;
            int mseg=6;

            int[] arr_game = new int[mhour*mseg];
            for (int x = 0; x < mhour; ++x)
            {
                String str_hour = String.Format("{0:00}", x);
                String p = "";
                for (int i = 0; i < mseg; ++i)
                {
                    String str_min = String.Format("{0:00}",i*10);
                    String sql_str = "SELECT * FROM timetab WHERE start_time='" + str_hour +str_min +"'";

                    schedule_cmd.CommandText = sql_str;
                    MySqlDataReader reader = schedule_cmd.ExecuteReader();
                    
                    if(reader.Read()){
                    
                            arr_game[i+x*mseg] = reader.GetInt32(1);
                            p += (arr_game[i]-1);
                    }
                    reader.Close();
                }

                Log.Info("Hour Schedule: " + str_hour + "  -> " + p);
            
            }
            

            return arr_game;
        }
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
