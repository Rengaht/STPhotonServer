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

        Timer total_round_timer;

        bool play_in_game;

         public GameB(STGameApp app):base(app,1)
        {
            GAME_SPAN=60000;
            END_SPAN=5000;
            Client_Limit=1;

            WAIT_SPAN = 10000;
            ROUND_SPAN = 150000;

            play_in_game = false;
            mcur_player = 2;

        }
        override public void handleMessage(STServerPeer sender,STClientCode code,Dictionary<byte,object> event_params)
        {
            Dictionary<byte,object> response_params=new Dictionary<byte,object>();
            switch(code)
            {
                case STClientCode.APP_Join:
                    Log.Warn("Join Game B!!");
                    
                    

                    int join_success=checkJoinSuccess(event_params);
                    response_params.Add((byte)1,join_success);

                    //int color_id =-1;
                    if (join_success==1)
                    {

                        // check if the peer already has a waiting number??
                        int waiting_index = -1;
                        if (event_params[(byte)102] != null)
                        {
                            waiting_index = Convert.ToInt32(event_params[(byte)102]);
                            String round_token = event_params[(byte)103] as String;
                            if (round_token == this.game_stamp && checkValidWaitingIndex(waiting_index)){
                               // correct waiting index
                            }
                            else
                            {  // wrong waiting index
                               waiting_index = getNewWaitingIndex(sender);
                            }

                        }
            
           

                        if (waiting_index != icur_player || waiting_index != icur_player + 1) // is waiting
                        {
                            response_params[(byte)1] = 2;
                        }

                        response_params.Add((byte)102, waiting_index);
                        response_params.Add((byte)103, game_stamp);
                        //Log.Warn("Add user for color " + color_id);

                        online_client.Add(sender);

                    }
                    sender.sendOpResponseToPeer(STServerCode.CJoin_Success,response_params);

                    checkWaitingStatus();

                    break;

                case STClientCode.APP_Rotate:
                    game_app.SendNotifyLED(STServerCode.LSet_Rotate,event_params);
                    break;


                case STClientCode.LED_Score:
                    sender.sendOpResponseToPeer(STServerCode.LSend_Score_Success,response_params);
                    game_app.sendGameOverToAll(event_params);
                    EndGame();

                    break;
            }
        }
        override public void InitGame()
        {
            base.InitGame();

            if (waiting_list != null) waiting_list.Clear();
            else waiting_list = new List<STServerPeer>();


            // setup total game round time
            game_stamp = "game_"+DateTime.Now.ToString("yyyyMMdd_HH_mm_ss");
            icur_player = 0;
           

            total_round_timer = new Timer(ROUND_SPAN);
            total_round_timer.Elapsed += new ElapsedEventHandler(EndRound);
            total_round_timer.AutoReset = false;
            total_round_timer.Enabled = true;


            round_start_time = DateTime.Now;
            Log.Debug("Game B Start at " + round_start_time.ToString());

        }

        
        override public void reallyEndGame(object sender, ElapsedEventArgs e)
        {
            
            game_app.SendNotifyLED(STServerCode.LSend_GG, new Dictionary<byte, object>());

            play_in_game = false;
            // check if anyone on waiting list
            icur_player += mcur_player;
            mcur_player = 2; // reset to 2

            checkWaitingStatus();
            
           
        }


        override public int checkJoinSuccess(Dictionary<byte, object> event_params)
        {
            
            //check id
            bool correct_id = game_app.checkValidId((String)event_params[(byte)100]);
            if (!correct_id) return 0;

            // check correct_game
            bool correct_game = ((int)event_params[(byte)1] == 1);
            if (!correct_game) return 0;


            // check time available !!!
            
            int mpair_waiting = (waiting_list.Count - (icur_player + mcur_player)) / 2;
            bool time_available = checkTimeAvailable(mpair_waiting);

            if(!time_available) return 0;

            return 1;
            
        }
        private bool checkValidWaitingIndex(int waiting_index)
        {

            if (waiting_index>=icur_player) return true;
            else return false;

        }
        private int getNewWaitingIndex(STServerPeer peer)
        {
            // put in wait list
            waiting_list.Add(peer);
            return waiting_list.Count - 1;
        }
        override public int InsertToSql(String[] cmd_values)
        {
            if (sql_command == null) return 0;

            sql_command.Parameters.Clear();
            sql_command.Parameters.AddWithValue("@Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sql_command.Parameters.AddWithValue("@Uid", cmd_values[0]);

            return sql_command.ExecuteNonQuery();
        }

        #region WAITING_LIST



        private void readyToStart()
        {
            Log.Debug("Start game with " + mcur_player + " player");

            Dictionary<byte, object> start_params = new Dictionary<byte, object>();
                
            int notified_user = 0;
            int i = 0;
            while(notified_user< mcur_player)
            {   
                STServerPeer peer=waiting_list[icur_player + i];
                
                // check peer.Connected?
                if (!peer.Connected)
                {
                    i++;
                    continue;
                }

                peer.sendEventToPeer(STServerCode.CGameB_Start, new Dictionary<byte, object>() {{(byte)101,notified_user}});

                i++;
                notified_user++;


                // Send Notify to LED
                String peer_id = peer.client_id;
                start_params.Add((byte)(100*(1+notified_user)), peer_id);
            
            }
            start_params.Add((byte)201, mcur_player);
            
            game_app.SendNotifyLED(STServerCode.LGameB_Start, start_params);

            play_in_game = true;

            this.StartGame();

        }

        private void checkWaitingStatus()
        {
            if (waiting_list.Count - 1 >= icur_player + (mcur_player-1))
            {
                // start new game
                if(wait_timer!=null) wait_timer.Close();

                if (checkTimeAvailable(1))
                {
                    //check is in game?
                    if(!play_in_game) readyToStart();
                }
                else
                {
                    Log.Debug("Not enough time Left!");
                }

            }
            else if (waiting_list.Count - 1 == icur_player)
            {
                // wait for second player
                // start wait_timer
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
        private void playByOne(object sender, ElapsedEventArgs e)
        {
            
            Log.Debug("No second player, play by one!!");

            mcur_player = 1;
            checkWaitingStatus();
        }
        /* Check if there's enough time for one game!
         */
        private bool checkTimeAvailable(int mgame_to_play)
        {

            TimeSpan t = new TimeSpan(DateTime.Now.Ticks);
            TimeSpan t2=new TimeSpan(round_start_time.Ticks);
            
            TimeSpan due = t.Subtract(t2).Duration();

            double remain_time = due.TotalMilliseconds+ROUND_SPAN;

            return remain_time>GAME_SPAN*mgame_to_play;

        }

        private void EndRound(object sender, ElapsedEventArgs e)
        {

            if (wait_timer != null) wait_timer.Close();
            if (stage_timer != null) stage_timer.Close();
            if (ending_timer != null) ending_timer.Close();


            Log.Info("End Total Round - Game B");
            game_app.goNextGame();
        }

        #endregion
    }
}
