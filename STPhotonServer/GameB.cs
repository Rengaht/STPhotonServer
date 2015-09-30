using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace STPhotonServer
{
    class GameB :GameScene
    {

        private String game_stamp;
        
        private List<STServerPeer> waiting_list;
        private int icur_player; // index in waiting_list 
        private int mcur_player; // 1 or 2
        
        Timer wait_timer;
        private int WAIT_SPAN;
        private int RUN_SPAN;

        Timer total_round_timer;

        Timer delay_timer;

        bool play_in_game;

         public GameB(STGameApp app):base(app,1)
        {
            WAIT_SPAN = 60000;
            RUN_SPAN = 100000;
             
            GAME_SPAN = 600000; // whole game_b time, 10 min
            ROUND_SPAN= WAIT_SPAN+RUN_SPAN; //each round 1:40 + 1:00(wait)

            END_SPAN=2000;
            Client_Limit=2;

           

            resetWaitingList();

        }
        override public void handleMessage(STServerPeer sender,STClientCode code,Dictionary<byte,object> event_params)
        {

            String sid = (event_params.ContainsKey((byte)100)) ? (String)event_params[(byte)100] : "";

            Dictionary<byte,object> response_params=new Dictionary<byte,object>();
            switch(code)
            {
                case STClientCode.APP_Join:
                    Log.Warn("Join Game B!!");

                    
                    int join_success=checkJoinSuccess(event_params);
                    response_params.Add((byte)1,join_success);
                    
                    
                    if (join_success == 1)
                    {
                        int iwait=getNewWaitingIndex(sender);
                        Log.Debug("New in Waiting List: "+iwait);
                        response_params.Add((byte)101, iwait);
                        addIdInGame(sid);

                        InsertToSql(new String[]{sid,"Join Game"});
                        lock (online_client)
                        {
                            online_client.Add(sender);
                        }
                        checkWaitingStatus();
                    }
                    else
                    {
                        //sender.delayDisconnect(5);
                    }
                    
                    sender.sendOpResponseToPeer(STServerCode.CJoin_Success,response_params);

                    
                    break;

                case STClientCode.APP_Rotate:
                    if (isIdInGame(sid))
                        game_app.SendNotifyLED(STServerCode.LSet_Rotate, event_params);
                    else
                    {
                        Log.Error("!! Not in-game ID: " + sid + " ! Kill it!!");
                        //sender.delayDisconnect(3);
                    }
                    break;

                case STClientCode.LED_StartRun:
                    if(waiting_list.Count>=mcur_player) sendStartRun();
                    break;
                case STClientCode.LED_EatIcon:

                    int ipeer =(int)event_params[(byte)101];
                    STServerPeer peer =null;
                    if (ipeer == 1 && waiting_list.Count > 0) peer = waiting_list[icur_player];
                    if(ipeer==0 && waiting_list.Count>1) peer = waiting_list[icur_player + 1];

                    if(peer!=null) peer.sendEventToPeer(STServerCode.CGameB_Eat,event_params);
                        
                    break;

                case STClientCode.LED_Score:
                    sender.sendOpResponseToPeer(STServerCode.LSend_Score_Success,response_params);
                    //game_app.sendGameOverToAll(event_params);
                    sendScoreToPeer(event_params);
                    EndRound();

                    InsertToSql(new String[] { "game", "End Round" });

                    break;
            }

          
        }
        override public void InitGame()
        {
            base.InitGame();

            


            // setup total game round time
            game_stamp = "game_"+DateTime.Now.ToString("yyyyMMdd_HH_mm_ss");
            icur_player = 0;


            resetWaitingList();

            //if (total_round_timer != null) total_round_timer.Close();
            //total_round_timer = new Timer(ROUND_SPAN);
            //total_round_timer.Elapsed += new ElapsedEventHandler(EndTotalGame);
            //total_round_timer.AutoReset = false;
            //total_round_timer.Enabled = true;

            

            //round_start_time = DateTime.Now;
            //Log.Debug("Game B Start at " + round_start_time.ToString());

           

        }

        
        //override public void reallyEndGame(object sender, ElapsedEventArgs e)
        //{
            
        //    game_app.SendNotifyLED(STServerCode.LSend_GG, new Dictionary<byte, object>());


        //    resetWaitingList();
        //    //checkWaitingStatus();
            
           
        //}


        override public int checkJoinSuccess(Dictionary<byte, object> event_params)
        {
            
            //check id
            bool correct_id = game_app.checkValidId((String)event_params[(byte)100]);
            if (!correct_id) return 0;

            // check correct_game
            bool correct_game = ((int)event_params[(byte)1] == 1);
            if (!correct_game) return 0;


            // check is playing
            if(play_in_game) return 2;


            // check time available if there's no one in wait list !!!
            lock (waiting_list)
            {
                if (waiting_list.Count == 0)
                {
                    if (!checkEnoughTimeForRound())
                    {
                        Log.Debug("Not Enough Time!");
                        return 0;
                    }
                }
                if (waiting_list.Count > 1)
                {
                    Log.Debug("Already someone in waiting list: " + waiting_list.Count);
                    return 0;
                }
            }
            return 1;
            
        }
        private bool checkValidWaitingIndex(int waiting_index)
        {

            if (waiting_index>=icur_player) return true;
            else return false;

        }
        private int getNewWaitingIndex(STServerPeer peer)
        {
            lock (waiting_list)
            {
                // put in wait list
                waiting_list.Add(peer);
                return waiting_list.Count - 1;
            }
        }
        override public int InsertToSql(String[] cmd_values)
        {
            if (sql_command == null) return 0;

            game_app.checkSqlConnection();

            sql_command.Parameters.Clear();
            sql_command.Parameters.AddWithValue("@Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sql_command.Parameters.AddWithValue("@Uid", cmd_values[0]);
            sql_command.Parameters.AddWithValue("@Status", cmd_values[1]);

            return sql_command.ExecuteNonQuery();
        }

        private void sendScoreToPeer(Dictionary<byte, object> led_score_params)
        {
            int[] score = new int[2];
            int[] icar = new int[2];

            score[0] = (int)led_score_params[(byte)1];
            score[1] = (int)led_score_params[(byte)2];

            icar[0] = (int)led_score_params[(byte)3];
            icar[1] = (int)led_score_params[(byte)4];

            //int iwinner = (score1<score2) ? 1 : 0;

            for(int i = 0; i < mcur_player;++i){
                int index = icur_player + i;
                if (index < 0 || index >= waiting_list.Count) continue;
                STServerPeer peer = waiting_list[icur_player + i];
                peer.sendEventToPeer(STServerCode.CSend_GG,
                    new Dictionary<byte, object>() { { (byte)1, (score[i]>=score[(i+1)%2])?1:0},{(byte)2,score[i]},{(byte)3,icar[i]}});

                InsertToSql(new String[]{peer.client_id,"score: "+score[i]});
            }



        }
        #region WAITING_LIST

        private void resetWaitingList()
        {
            if (waiting_list != null) waiting_list.Clear();
            else waiting_list = new List<STServerPeer>();

            

            if (wait_timer != null) wait_timer.Close();
            wait_timer = null;

            play_in_game = false;
            mcur_player = 2;
            icur_player = 0;
        }


        private void readyToStart()
        {
            Log.Debug("----- Start game with " + mcur_player + " player -----");

            InsertToSql(new String[] { "game", "Start Round, player "+mcur_player });

            Dictionary<byte, object> start_params = new Dictionary<byte, object>();
                
            int notified_user = 0;
            int i = 0;
            //while(notified_user< mcur_player)
            //{   
            //    STServerPeer peer=waiting_list[icur_player + i];
            foreach(STServerPeer peer in waiting_list)
            {
                // check peer.Connected?
                

                peer.sendEventToPeer(STServerCode.CGameB_Ready, new Dictionary<byte, object>() {{(byte)101,notified_user}});
                peer.client_side = notified_user;

                i++;
                notified_user++;


                // Send Notify to LED
                String peer_id = peer.client_id;
                start_params.Add((byte)(100*(1+notified_user)), peer_id);
            
            }
            start_params.Add((byte)201, mcur_player);
            
            game_app.SendNotifyLED(STServerCode.LGameB_Ready, start_params);

            play_in_game = true;

            this.StartRound();

        }
        private void sendStartRun(){
            foreach (STServerPeer peer in waiting_list)
            {
                peer.sendEventToPeer(STServerCode.CGameB_Start, new Dictionary<byte, object>());
            }
            game_app.SendNotifyLED(STServerCode.LGameB_Start, new Dictionary<byte, object>());
        }

        private void checkWaitingStatus()
        {
            Log.Debug("----- Check waiting status! -----");

            if (waiting_list.Count >= mcur_player)
            {
                Log.Debug("----- Try to start a new game -----");

                // start new game
                if(wait_timer!=null) wait_timer.Close();

                if (checkEnoughTimeForRound(RUN_SPAN))
                {
                    //check is in game?
                    if (!play_in_game)
                    {
                        if (delay_timer != null) delay_timer.Close();
                        delay_timer = new Timer(2000);
                        delay_timer.Elapsed += new ElapsedEventHandler(readyToStart);
                        delay_timer.AutoReset = false;
                        delay_timer.Enabled = true;
                        //readyToStart();
                    }
                    else
                    {
                        Log.Debug("----- Fail: play in game -----");
                    }
                }
                else
                {
                    Log.Debug("----- Fail: Not enough time Left! -----");
                    //game_app.killAllPeer();
                }

            }
            else if (waiting_list.Count - 1 == icur_player)
            {
                // wait for second player
                // start wait_timer
                Log.Debug("----- Start waiting timer! -----");
                wait_timer = new Timer(WAIT_SPAN);
                wait_timer.Elapsed += new ElapsedEventHandler(playByOne);
                wait_timer.AutoReset = false;
                wait_timer.Enabled = true;
            }
            else
            {
                // wait for first player
                // reset led
                game_app.SendNotifyLED(STServerCode.Id_And_Game_Info, new Dictionary<byte, object>() { { (byte)1, 1 } });


            }

        }

        private void readyToStart(object sender, ElapsedEventArgs e)
        {
            readyToStart();
        }
        private void playByOne(object sender, ElapsedEventArgs e)
        {
            
            Log.Debug("----- No second player, play by one!! -----");

            mcur_player = 1;
            checkWaitingStatus();
        }
        /* Check if there's enough time for one game!
         */
        //private bool checkTimeAvailable(int mgame_to_play)
        //{

        //    TimeSpan t = new TimeSpan(DateTime.Now.Ticks);
        //    TimeSpan t2=new TimeSpan(round_start_time.Ticks);
            
        //    TimeSpan due = t.Subtract(t2).Duration();

        //    double remain_time = due.TotalMilliseconds+ROUND_SPAN;

        //    return remain_time>GAME_SPAN*mgame_to_play;

        //}


        public override void EndRound()
        {
            base.EndRound();

            resetWaitingList();

        }

        //private void EndTotalGame(object sender, ElapsedEventArgs e)
        //{
        //    EndTotalGame();
        //}

        //public void EndTotalGame(){
        //    if (wait_timer != null) wait_timer.Close();
        //    if (stage_timer != null) stage_timer.Close();
        //    if (ending_timer != null) ending_timer.Close();


        //    Log.Info(">>>>>  End Total Round - Game B <<<<<");
        //    game_app.goNextGame();
        //}

        #endregion
    }
}
