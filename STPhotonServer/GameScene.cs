using ExitGames.Logging;
using Photon.SocketServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Collections;
using MySql.Data.MySqlClient;
using System.Collections.Concurrent;


namespace STPhotonServer
{
    class GameScene
    {
        static String[] Insert_Command ={
            "INSERT INTO a_game_log_table(start_time,user_id,status) values(@Timestamp,@Uid,@Status)",
            "INSERT INTO b_game_log_table(start_time,user_id,status) values(@Timestamp,@Uid,@Status)",
            "INSERT INTO c_game_log_table(start_time,user_id,face_image_path) values(@Timestamp,@Uid,@Imagepath)"};


        public STGameApp game_app;

        public int GAME_SPAN;
        public int ROUND_SPAN;
        public int END_SPAN;
        public int Client_Limit;


        public Timer total_game_timer,end_delay_timer;
        public DateTime round_start_time,game_start_time;

        public double this_game_span;

        protected static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        enum Game_State { Waiting, Playing, Ending };
        Game_State cur_state { get; set; }


        public List<STServerPeer> online_client; // peer
       //public BlockingCollection<STServerPeer> online_client;
       // public SynchronizedCollection<STServerPeer> online_client;

        List<String> ingame_id; // id
        
        MySqlConnection sql_connection;
        public MySqlCommand sql_command;
        
        

        public GameScene()
        {
        }
        public GameScene(STGameApp app,int game_type)
        {
            game_app=app;
           online_client = new List<STServerPeer>();
           // online_client = new SynchronizedCollection<STServerPeer>();

            ingame_id=new List<String>();

            // open db

            if (app.enable_db)
            {
                sql_connection = app.getSqlConnection();
                setupSqlCommand(Insert_Command[game_type]);
            }
        }

        virtual public void handleMessage(STServerPeer sender,STClientCode code,Dictionary<byte,object> ev_params)
        {
        }
        virtual public void removeClient(STServerPeer peer_to_remove)
        {

        }
        virtual public void InitGame()
        {
            Log.Warn(">>>> Game Scene Init");
            cur_state=Game_State.Waiting;

            lock (online_client)
            {
                online_client.Clear();
            }
            game_start_time = DateTime.Now;
            
            setupGameTimer();

        }
        virtual public void StartRound()
        {
            //if (!checkEnoughTimeForRound())
            //{
            //    Log.Warn(">>>> Not Enough Time For A Round");
            //    return;
            //}

            Log.Warn(">>>> Game Round Start");
            cur_state=Game_State.Playing;


            round_start_time = DateTime.Now;
        }

        virtual public void EndRound()
        {
            Log.Warn(">>>> Game Round End");

            /* disconnect all peer */
            foreach (STServerPeer peer in online_client)
            {
                //peer.delayDisconnect();
            }
            
            ingame_id.Clear();

            cur_state=Game_State.Waiting;

        }
        public void ForceEndGame()
        {
            if (total_game_timer != null) total_game_timer.Close();
            game_app.goNextGame();
        }

        void setupGameTimer()
        {
            if(total_game_timer!=null) total_game_timer.Close();
            if(end_delay_timer!=null) end_delay_timer.Close();

            this_game_span = getGameRemainTime();
            total_game_timer = new Timer(this_game_span);
            total_game_timer.Elapsed += new ElapsedEventHandler(reallyEndGame);
            total_game_timer.AutoReset=false;
            total_game_timer.Enabled=true;
            //total_game_timer.Start();

            Log.Info("Start Game Timer");

        }
        public double getGameRemainTime()
        {
            /* compute the remaining time to next "10 min" */
            DateTime now = DateTime.Now;
            int dest_ten = (int)Math.Floor(now.Minute / 10.0) + 1;
            int dest_hour = now.Hour + ((dest_ten == 6) ? 1 : 0);
            dest_ten = (dest_ten == 6) ? 0 : dest_ten * 10;
            DateTime dten = new DateTime(now.Year, now.Month, now.Day, dest_hour, dest_ten, 0, 0, now.Kind);

            TimeSpan t = new TimeSpan(now.Ticks);
            TimeSpan t2 = new TimeSpan(dten.Ticks);

            TimeSpan due = t.Subtract(t2).Duration();
            double time_to_ten = due.TotalMilliseconds;

            Log.Info("Get Remain Time: " + Math.Floor(due.TotalMinutes) + ":" + (due.TotalSeconds % 60));

            return time_to_ten;
        }

        //private void prepareToEndGame(object sender, ElapsedEventArgs e)
        //{
        //    if(end_delay_timer!=null) end_delay_timer.Close();

        //    end_delay_timer=new Timer(END_SPAN);
        //    end_delay_timer.Elapsed+=new ElapsedEventHandler(reallyEndGame);
        //    end_delay_timer.AutoReset=false;
        //    end_delay_timer.Enabled=true;
        //    //end_delay_timer.Start();
        //}

       
        virtual public void reallyEndGame(object sender,ElapsedEventArgs e)
        {

            Log.Warn(">>>> Really End");
            game_app.SendNotifyLED(STServerCode.LSend_GG,new Dictionary<byte,object>());
            game_app.goNextGame();

        }

        /* Ask Display for game scores
         * 
         */
        virtual public void requestToEndGame(object sender,ElapsedEventArgs e)
        {
            game_app.requestGameScore();
        }

        /*  Check 1) ID is legal, 2) has vacancy, 3) register for correct game
         *  when user joins the game
         */
        virtual public int checkJoinSuccess(Dictionary<byte, object> event_params)
        {
            return 0;
        }

        public bool checkEnoughTimeForRound()
        {
            return checkEnoughTimeForRound(ROUND_SPAN);
        }
        
        public bool checkEnoughTimeForRound(int time_to_check)
        {
            TimeSpan t = new TimeSpan(DateTime.Now.Ticks);
            TimeSpan t2 = new TimeSpan(game_start_time.Ticks);

            TimeSpan due = t.Subtract(t2).Duration();

            double remain_time = this_game_span - due.TotalMilliseconds;

            //double remain_time = getGameRemainTime();            
            Log.Warn("Remain Game Time: "+remain_time+" = "+Math.Floor(remain_time/60000)+":"+Math.Floor(remain_time/1000)%60+" -> "+time_to_check);
           
            bool is_enough=remain_time > time_to_check;

            return is_enough;
        }

        public bool isWaiting()
        {
            return cur_state == Game_State.Waiting;
        }

        public bool isIdInGame(String sid)
        {
            return ingame_id.Contains(sid);
        }
        public void addIdInGame(String sid)
        {
            if (!ingame_id.Contains(sid)) ingame_id.Add(sid);
        }
        public void removeIdInGame(String sid)
        {
            lock (ingame_id)
            {
                ingame_id.Remove(sid);
            }
        }

        #region Handle mySql

        private void setupSqlCommand(String prepare_cmd)
        {
            sql_command = new MySqlCommand();
            sql_command.Connection = sql_connection;
            sql_command.CommandText = prepare_cmd;
            sql_command.Prepare();
        }

        virtual public int InsertToSql(String[] cmd_values)
        {
            return 0;
        }

        #endregion

    }
}
