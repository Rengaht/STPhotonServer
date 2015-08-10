using ExitGames.Logging;
using Photon.SocketServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Collections;
using MySql.Data.MySqlClient;

namespace STPhotonServer
{
    class GameScene
    {
        static String[] Insert_Command ={
            "INSERT INTO a_game_log_table(start_time,user_id) values(@Timestamp,@Uid)",
            "INSERT INTO b_game_log_table(start_time,user_id) values(@Timestamp,@Uid)",
            "INSERT INTO c_game_log_table(start_time,user_id,face_image_path) values(@Timestamp,@Uid,@Imagepath)"};


        public STGameApp game_app;

        public int GAME_SPAN;
        public int ROUND_SPAN;
        public int END_SPAN;
        public int Client_Limit;


        public Timer total_game_timer,end_delay_timer;
        public DateTime round_start_time,game_start_time;


        protected static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        enum Game_State { Waiting, Playing, Ending };
        Game_State cur_state { get; set; }


        public List<STServerPeer> online_client; // id string -> peer

        
        MySqlConnection sql_connection;
        public MySqlCommand sql_command;

        

        public GameScene()
        {
        }
        public GameScene(STGameApp app,int game_type)
        {
            game_app=app;
            online_client = new List<STServerPeer>();

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

        virtual public void InitGame()
        {
            Log.Warn(">>>> Game Scene Init");
            cur_state=Game_State.Waiting;
            
            online_client.Clear();

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

           //
            
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


            total_game_timer=new Timer(GAME_SPAN);
            total_game_timer.Elapsed += new ElapsedEventHandler(reallyEndGame);
            total_game_timer.AutoReset=false;
            total_game_timer.Enabled=true;

        }

        private void prepareToEndGame(object sender, ElapsedEventArgs e)
        {
            if(end_delay_timer!=null) end_delay_timer.Close();

            end_delay_timer=new Timer(END_SPAN);
            end_delay_timer.Elapsed+=new ElapsedEventHandler(reallyEndGame);
            end_delay_timer.AutoReset=false;
            end_delay_timer.Enabled=true;

        }

       
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
            TimeSpan t = new TimeSpan(DateTime.Now.Ticks);
            TimeSpan t2 = new TimeSpan(game_start_time.Ticks);

            TimeSpan due = t.Subtract(t2).Duration();

            double remain_time = GAME_SPAN-due.TotalMilliseconds;
            
            Log.Warn("Remain Game Time: "+remain_time);
            
            bool is_enough=remain_time > ROUND_SPAN;

            return is_enough;
        }

        public bool isWaiting()
        {
            return cur_state == Game_State.Waiting;
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
