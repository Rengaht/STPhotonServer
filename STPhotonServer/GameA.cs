using Photon.SocketServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;


namespace STPhotonServer
{
    class GameA :GameScene
    {
        Hashtable right_online_client, left_online_client;
        int INTERACTIVE_SPAN;
        int TIMEOUT_SPAN;

        public GameA(STGameApp app):base(app,0)
        {
             GAME_SPAN = 600000; // 10min
             ROUND_SPAN = 595000; // 9:55

             INTERACTIVE_SPAN = 180000;

             TIMEOUT_SPAN = 300000;
            
             END_SPAN=5000;
             Client_Limit=20;

             right_online_client = new Hashtable();
             left_online_client = new Hashtable();

            
        }
        override public void InitGame()
        {
            base.InitGame();
           // this.StartGame();

            right_online_client.Clear();
            left_online_client.Clear();

        }
        
        override public void handleMessage(STServerPeer sender,STClientCode code,Dictionary<byte,object> event_params)
        {

            String sid=(event_params.ContainsKey((byte)100))?(String)event_params[(byte)100]:"";

            Dictionary<byte,object> response_params=new Dictionary<byte,object>();

            switch(code){
                case STClientCode.APP_Join:
                    Log.Warn("Join Game A!!");
                    //bool enough_time = checkEnoughTimeForPlayer();
                    //if (!enough_time)
                    //{
                    //    Log.Debug("Not Enough Time!!");
                    //    response_params.Add(1, 0);
                    //}else{

                    int join_success = checkJoinSuccess(event_params);
                    response_params.Add(1, join_success);

                    if (join_success == 1)
                    {
                        online_client.Add(sender);
                        addIdInGame(sid);

                        InsertToSql(new String[] { sid, "Join Game"});
                    }
                    else
                    {
                       // sender.delayDisconnect(5);
                    }
                        // if is first one joining
                        //if(isWaiting()) StartRound();
                    //}
                    sender.sendOpResponseToPeer(STServerCode.CJoin_Success,response_params);
                    return;

                case STClientCode.LED_StartRun:
                    bool enough_time_round = checkEnoughTimeForRound();
                    if (isWaiting() && enough_time_round)
                    {
                        response_params.Add(1, 1);
                        response_params.Add(2, getGameRemainTime());
                        //sender.sendOpResponseToPeer(STServerCode.LGameB_Start, response_params);
                        game_app.SendNotifyLED(STServerCode.LGameB_Start, response_params);
                        StartRound();
                        return;                    
                    }
                    
                    Log.Debug("Not Enough Time!!");
                    response_params.Add(1, 0);

                    game_app.SendNotifyLED(STServerCode.LGameB_Start, response_params);


                    return;

                case STClientCode.LED_Score:
                    sender.sendOpResponseToPeer(STServerCode.LSend_Score_Success,response_params);
                    game_app.sendGameOverToAll(event_params);
                    EndRound();

                    return;
                
              
            }
                
            if(!isIdInGame(sid)){
                Log.Error("!! Not in-game ID: "+sid+" ! Kill it!!");
                //sender.delayDisconnect(3);
                return;
            }

            switch(code)
            {                  
                case STClientCode.APP_Set_Side:
                   
                    bool has_vacancy = false;
                    int side_index=-1;
                    if ((int)event_params[(byte)101] == 1)
                    {
                        has_vacancy = (left_online_client.Count < Client_Limit / 2);
                        if (has_vacancy){
                            if(!left_online_client.ContainsKey((String)event_params[(byte)100]))
                                left_online_client.Add((String)event_params[(byte)100], sender);
                            side_index=1;
                        }
                    }
                    else if ((int)event_params[(byte)101] == 0)
                    {
                        has_vacancy = (right_online_client.Count < Client_Limit / 2);
                        if (has_vacancy){
                            if (!right_online_client.ContainsKey((String)event_params[(byte)100])) 
                                right_online_client.Add((String)event_params[(byte)100], sender);
                            side_index=0;
                        }
                    }
                    response_params.Add(1,has_vacancy?1:0);
                    response_params.Add(101, side_index);
                    
                    sender.sendOpResponseToPeer(STServerCode.CSet_Side_Success,response_params);
                    
                    game_app.SendNotifyLED(STServerCode.LAdd_House, event_params);

                    break;


                case STClientCode.APP_Set_Name:

                    //TODO:check id & side??
                    
                    String _name = (String)event_params[(byte)1];
                    //check word                    
                    bool isgood=game_app.checkGoodName(_name);
                    
                    response_params.Add((byte)1, isgood?1:2);
                                        
                    if (isgood)
                    {
                        byte[] _bname = System.Text.Encoding.UTF8.GetBytes(_name);
                        event_params[(byte)1] = _bname;

                        game_app.SendNotifyLED(STServerCode.LSet_Name, event_params);

                        InsertToSql(new String[]{sid,"Name: "+_name});
                    }
                   

                    sender.sendOpResponseToPeer(STServerCode.CSet_Name_Success,response_params);

                    break;
                case STClientCode.APP_Set_House:
                    
                    //TODO: check id & side
                    
                    response_params.Add((byte)1, 1);
                    game_app.SendNotifyLED(STServerCode.LSet_House, event_params);
                    
                    sender.sendOpResponseToPeer(STServerCode.CSet_House_Success, response_params);
                    
                    break;

                case STClientCode.APP_Blow:
                    game_app.SendNotifyLED(STServerCode.LSet_Blow,event_params);
                    break;
                case STClientCode.APP_Light:
                    game_app.SendNotifyLED(STServerCode.LSet_Light, event_params);
                    break;
                case STClientCode.APP_Shake:
                    game_app.SendNotifyLED(STServerCode.LSet_Shake, event_params);

                    break;

                case STClientCode.APP_Leave:
                    
                    game_app.SendNotifyLED(STServerCode.LSet_User_Leave, event_params);
                    
                    response_params.Add((byte)1, 1);
                    sender.sendOpResponseToPeer(STServerCode.CSet_Leave_Success, response_params);
                    
                    /* disconnect finished player */
                    //sender.delayDisconnect();

                    removeIdInGame(sid);
                    InsertToSql(new String[] { sid, "Leave"});

                    break;
                
            }
        }

        override public int checkJoinSuccess(Dictionary<byte, object> event_params)
        {
            //TODO: check id, check vacancy
            
            bool correct_id = game_app.checkValidId((String)event_params[(byte)100]);
            if (!correct_id)
            {
                Log.Debug("Illegal ID!");
                return 0;
            }
            bool has_vacancy = online_client.Count<Client_Limit;
            if (!has_vacancy)
            {
                Log.Debug("No Vacancy");
                return 0;
            }
            
            bool correct_game = ((int)event_params[(byte)1] == 0);
            if(!correct_game)
            {
                Log.Debug("Illegal Game!");
                return 0;
            }
            bool enough_time = checkEnoughTimeForPlayer();
            if (!enough_time)
            {
                Log.Debug("Not Enough Time for Player!");
                return 0;
            }
           

            return 1;

        }

        override public int InsertToSql(String[] cmd_values)
        {
            if (sql_command == null) return 0;

            game_app.checkSqlConnection();

            sql_command.Parameters.Clear();
            sql_command.Parameters.AddWithValue("@Timestamp",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sql_command.Parameters.AddWithValue("@Uid", cmd_values[0]);
            sql_command.Parameters.AddWithValue("@Status", cmd_values[1]);

            return sql_command.ExecuteNonQuery();
        }

        public bool checkEnoughTimeForPlayer()
        {

            if (isWaiting())
            {
                Log.Warn("Check Time: No Game");
                return false;
            }
            TimeSpan t = new TimeSpan(DateTime.Now.Ticks);
            TimeSpan t2 = new TimeSpan(round_start_time.Ticks);

            TimeSpan due = t.Subtract(t2).Duration();

            double remain_time = ROUND_SPAN - due.TotalMilliseconds;

            Log.Warn("Remain Round Time: " + remain_time);

            bool is_enough = remain_time > INTERACTIVE_SPAN; 

            return is_enough;
        }
    }
}
