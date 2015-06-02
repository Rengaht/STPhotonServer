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

        public GameA(STGameApp app):base(app,0)
        {
             GAME_SPAN=20000;
             END_SPAN=5000;
             Client_Limit=20;

             right_online_client = new Hashtable();
             left_online_client = new Hashtable();

            
        }
        override public void InitGame()
        {
            base.InitGame();
            this.StartGame();

            right_online_client.Clear();
            left_online_client.Clear();

        }
        override public void handleMessage(STServerPeer sender,STClientCode code,Dictionary<byte,object> event_params)
        {
            Dictionary<byte,object> response_params=new Dictionary<byte,object>();
            switch(code)
            {
                case STClientCode.APP_Join:
                    
                    Log.Warn("Join Game A!!");

                    int join_success=checkJoinSuccess(event_params);                    
                    response_params.Add(1,join_success);
                    
                    sender.sendOpResponseToPeer(STServerCode.CJoin_Success,response_params);

                    online_client.Add(sender);
                    break;
                case STClientCode.APP_Set_Side:

                    bool has_vacancy = false;
                    int side_index=-1;
                    if ((int)event_params[(byte)101] == 1)
                    {
                        has_vacancy = (left_online_client.Count < Client_Limit / 2);
                        if (has_vacancy){
                            left_online_client.Add((String)event_params[(byte)100], sender);
                            side_index=1;
                        }
                    }
                    else if ((int)event_params[(byte)101] == 0)
                    {
                        has_vacancy = (right_online_client.Count < Client_Limit / 2);
                        if (has_vacancy){
                            right_online_client.Add((String)event_params[(byte)100], sender);
                            side_index=0;
                        }
                    }
                    response_params.Add(1,has_vacancy?1:0);
                    response_params.Add(101, side_index);
                    sender.sendOpResponseToPeer(STServerCode.CSet_Side_Success,response_params);

                    break;


                case STClientCode.APP_Set_Name:
                    response_params.Add((byte)1, 1);
                    sender.sendOpResponseToPeer(STServerCode.CSet_Name_Success,response_params);
                    game_app.SendNotifyLED(STServerCode.LSet_Name,event_params);
                    break;
                case STClientCode.APP_Set_House:
                    response_params.Add((byte)1, 1);
                    sender.sendOpResponseToPeer(STServerCode.CSet_House_Success, response_params);
                    game_app.SendNotifyLED(STServerCode.LSet_House, event_params);
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
                
                case STClientCode.LED_Score:
                    sender.sendOpResponseToPeer(STServerCode.LSend_Score_Success,response_params);
                    game_app.sendGameOverToAll(event_params);
                    EndGame();

                    break;
            }
        }

        override public int checkJoinSuccess(Dictionary<byte, object> event_params)
        {
            //TODO: check id, check vacancy
            
            bool correct_id = game_app.checkValidId((String)event_params[(byte)100]);
            
            bool has_vacancy = online_client.Count<Client_Limit;

            
            bool correct_game = ((int)event_params[(byte)1] == 0);


            if(correct_id && has_vacancy && correct_game) return 1;
            return 0;

        }

        override public int InsertToSql(String[] cmd_values)
        {
            if (sql_command == null) return 0;

            sql_command.Parameters.Clear();
            sql_command.Parameters.AddWithValue("@Timestamp",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sql_command.Parameters.AddWithValue("@Uid", cmd_values[0]);

            return sql_command.ExecuteNonQuery();
        }

    }
}
