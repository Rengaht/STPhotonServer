using ExitGames.Logging;
using Photon.SocketServer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using MySql.Data.MySqlClient;

namespace STPhotonServer
{
    public class STGameApp
    {
        public bool enable_db = false;

        bool debug_mode = true;
        int debug_game = 2;

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
        
        MySqlConnection sql_connection;
        MySqlCommand sql_command;

        STServerPeer led_peer;
        public STServerPeer LED_Peer {
            set { led_peer=value; }
        }
        
       

        public STGameApp(List<PeerBase> lpeer_)
        {
            aclient_peer=lpeer_;
            LED_Ready=false;

           
            // open db
            if (enable_db)
            {
                string connString = "server=127.0.0.1;uid=root;pwd=reng;database=stapp_logdb";
                sql_connection = new MySqlConnection(connString);
                sql_command = sql_connection.CreateCommand();
                sql_connection.Open();
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
            agame_scene[cur_game].InitGame();
            

            SendNotifyLED(STServerCode.Id_And_Game_Info,new Dictionary<byte,object>() { { (byte)1,cur_game } });

            EventData event_data = new EventData((byte)STServerCode.Id_And_Game_Info,
                                               new Dictionary<byte,object>() { { (byte)1,cur_game}});
            foreach(STServerPeer peer in aclient_peer)
            {
                peer.sendEventToPeer(event_data);
            }

          
        }
       
        public void goNextGame()
        {
            if (debug_mode) initGame(debug_game);
            else initGame((cur_game + 1) % 3);
            //initGame(2);
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
            else initGame(0);
        }
        
        public void requestGameScore()
        {
            
            SendNotifyLED(STServerCode.LRequest_Score,new Dictionary<byte,object>());

        }
        public void sendGameOverToAll(Dictionary<byte,Object> event_params)
        {
            
            //agame_scene[cur_game].EndGame();

            EventData event_data=new EventData((byte)STServerCode.CSend_GG,
                                                new Dictionary<byte,object>() { { (byte)1,1 },{(byte)2,100} });
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
        private void clearClientList()
        {
            aclient_peer.RemoveAll(s=> !s.Connected);

        }
        public int getCurGame()
        {
            checkLed();
            if(LED_Ready) return cur_game;
            return -1;
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
#endregion

    }
}
