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
        public int END_SPAN;
        public int Client_Limit;

        protected static readonly ILogger Log=LogManager.GetCurrentClassLogger();

        enum Game_State { Waiting, Playing, Ending };
        Game_State cur_state { get; set; }

        public Timer stage_timer,ending_timer;

        public List<STServerPeer> online_client; // id string -> peer

        
        MySqlConnection sql_connection;
        public MySqlCommand sql_command;

        public int ROUND_SPAN;
        public DateTime round_start_time;


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

            round_start_time = DateTime.Now;

        }
        virtual public void StartGame()
        {
            Log.Warn(">>>> Game Scene Start");
            cur_state=Game_State.Playing;
            
            setupStageTimer();

        }

        virtual public void EndGame()
        {
            Log.Warn(">>>> Game Scene End");

            if(stage_timer!=null) stage_timer.Close();
            
            cur_state=Game_State.Ending;
            prepareToEndGame();
        }

        void setupStageTimer()
        {
            if(stage_timer!=null) stage_timer.Close();
            if(ending_timer!=null) ending_timer.Close();


            stage_timer=new Timer(GAME_SPAN);
            stage_timer.Elapsed+=new ElapsedEventHandler(requestToEndGame);
            stage_timer.AutoReset=false;
            stage_timer.Enabled=true;

        }

        private void prepareToEndGame()
        {
            if(ending_timer!=null) ending_timer.Close();

            ending_timer=new Timer(END_SPAN);
            ending_timer.Elapsed+=new ElapsedEventHandler(reallyEndGame);
            ending_timer.AutoReset=false;
            ending_timer.Enabled=true;

        }

       
        virtual public void reallyEndGame(object sender,ElapsedEventArgs e)
        {
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
