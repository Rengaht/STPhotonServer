using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Timers;



namespace STPhotonServer
{

    

    class GameC:GameScene
    {
        static String SERVER_PATH = "C://Users/Administrator/Desktop/KerkerPhoto/";


        public GameC(STGameApp app):base(app,2)
        {
             GAME_SPAN=600000;
             END_SPAN=3000;
             Client_Limit=20;
        }
        override public void InitGame()
        {
            base.InitGame();
            this.StartGame();
        }
        override public void handleMessage(STServerPeer sender,STClientCode code,Dictionary<byte,object> event_params)
        {
            Dictionary<byte,object> response_params=new Dictionary<byte,object>();
            switch(code)
            {
                case STClientCode.APP_Join:
                    Log.Warn("Join Game C!!");
                    String client_id=(String)event_params[(byte)100];
                    if(!online_client.Contains(sender)) online_client.Add(sender);

                    response_params.Add(1,checkJoinSuccess(event_params));
                    //response_params.Add(2,game_app.getClientIndex(sender));

                    sender.sendOpResponseToPeer(STServerCode.CJoin_Success,response_params);
                    break;

              
                case STClientCode.APP_Face:
                    sender.sendOpResponseToPeer(STServerCode.CSet_Face_Success, response_params);

                    // TODO: save to server
                    String uid=(String)event_params[(byte)100];
                    String file_path = saveImage(uid,(String)event_params[(byte)2]);

                    //event_params[(byte)2] = file_path;
                    game_app.SendNotifyLED(STServerCode.LSet_Face,event_params);

                    InsertToSql(new String[]{uid,file_path});
                    
                    break;

                case STClientCode.LED_Score:
                    sender.sendOpResponseToPeer(STServerCode.LSend_Score_Success,response_params);
                    game_app.sendGameOverToAll(event_params);
                    EndGame();

                    break;
            }
        }

        override public void requestToEndGame(object sender, ElapsedEventArgs e)
        {

            this.EndGame();
        }

        override public int checkJoinSuccess(Dictionary<byte, object> event_params)
        {
            //TODO: check id, check vacancy
            bool correct_id = game_app.checkValidId((String)event_params[(byte)100]);

            bool has_vacancy = online_client.Count < Client_Limit;

            bool correct_game = ((int)event_params[(byte)1] == 2);

            if(correct_id && has_vacancy && correct_game) return 1;
            return 0;
        }

        override public int InsertToSql(String[] cmd_values)
        {
            if (sql_command == null) return 0;

            sql_command.Parameters.Clear();
            sql_command.Parameters.AddWithValue("@Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sql_command.Parameters.AddWithValue("@Uid", cmd_values[0]);
            sql_command.Parameters.AddWithValue("@ImagePath", cmd_values[1]);

            return sql_command.ExecuteNonQuery();
        }


        private String saveImage(String user_id, String encoded_image)
        {
            byte[] image_byte = Convert.FromBase64String(encoded_image);
            String file_path = SERVER_PATH + "user_" + DateTime.Now.ToString("yyyyMMDD_HHmm")+"_" + user_id + ".png";


            Task.Factory.StartNew(() => File.WriteAllBytes(file_path, image_byte));
            return file_path;

        }
        

    }
}
