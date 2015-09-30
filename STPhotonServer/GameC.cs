using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
//using System.Threading;
using System.Timers;
//using System.Timers.Timer;

using System.Drawing;

namespace STPhotonServer
{

    

    class GameC:GameScene
    {

        static String SERVER_PATH = "C://Users/Administrator/Desktop/KerkerPhoto/";
        static int CLOCK_SPAN = 60000;

        
        bool clock_mode = false;
        Timer clock_mode_timer;

        public GameC(STGameApp app):base(app,2)
        {
             GAME_SPAN=600000; // 10min
             ROUND_SPAN = GAME_SPAN;
             END_SPAN=3000;
             Client_Limit=20;
        }
        override public void InitGame()
        {
            base.InitGame();
            this.StartRound();

            // set clock_mode_timer for last 1 min
            clock_mode = false;
            double time_to_clock = getGameRemainTime() - CLOCK_SPAN;
            if (time_to_clock > 0)
            {
                clock_mode_timer = new Timer(time_to_clock);
                clock_mode_timer.Elapsed += new ElapsedEventHandler(startClockMode);
                clock_mode_timer.AutoReset = false;
                clock_mode_timer.Enabled = true;
            }

        }
        override public void handleMessage(STServerPeer sender,STClientCode code,Dictionary<byte,object> event_params)
        {
            String sid = (event_params.ContainsKey((byte)100)) ? (String)event_params[(byte)100] : "";

            Dictionary<byte,object> response_params=new Dictionary<byte,object>();
            switch(code)
            {
                case STClientCode.APP_Join:
                    Log.Warn("Join Game C!!");
                    
                    int success=checkJoinSuccess(event_params);
                    response_params.Add(1,success);
                    //response_params.Add(2,game_app.getClientIndex(sender));
                    if (success == 1)
                    {
                        lock (online_client)
                        {
                            if (!online_client.Contains(sender)) online_client.Add(sender);
                        }
                        addIdInGame(sid);
                    }
                    else
                    {
                       // sender.delayDisconnect(5);
                    }
                    sender.sendOpResponseToPeer(STServerCode.CJoin_Success,response_params);


                    break;

              
                case STClientCode.APP_Face:
                   
                    if (!clock_mode && isIdInGame(sid))
                    {
                        // TODO: save to server
                        String uid = (String)event_params[(byte)100];
                        byte[] image_byte = Convert.FromBase64String((String)event_params[(byte)2]);

                        bool img_good = checkImage(image_byte);
                        if (img_good)
                        {
                            String file_path = saveImage(uid, image_byte);
                            InsertToSql(new String[] { uid, file_path });
                            //event_params[(byte)2] = file_path;
                            game_app.SendNotifyLED(STServerCode.LSet_Face, event_params);
                            response_params.Add((byte)1, 1);
                        }
                        else
                        {
                            response_params.Add((byte)1, 0);
                        }
                        
                        sender.sendOpResponseToPeer(STServerCode.CSet_Face_Success, response_params);
                        
                        
                    }
                    else
                    {
                        Log.Error("!! Not in-game ID: " + sid + " ! Kill it!!");
                        response_params.Add((byte)1, 0);
                        sender.sendOpResponseToPeer(STServerCode.CSet_Face_Success, response_params);
                    }
                    /* disconnect finished player */
                    //sender.delayDisconnect();
                    removeIdInGame(sid);
                    lock (online_client)
                    {
                        online_client.Remove(sender);
                    }
                    break;

                //case STClientCode.LED_Score:
                //    sender.sendOpResponseToPeer(STServerCode.LSend_Score_Success,response_params);
                //    game_app.sendGameOverToAll(event_params);
                //    EndRound();

                //    break;
            }
        }
        private void startClockMode(object sender, ElapsedEventArgs e)
        {
            clock_mode = true;
            game_app.SendNotifyLED(STServerCode.LSet_ClockMode, new Dictionary<byte, object>());

            //send switch game to mobile clients
            game_app.sendSwitchGame();

        }

        override public void reallyEndGame(object sender, ElapsedEventArgs e)
        {
            EndRound();
            base.reallyEndGame(sender, e);
            
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
            bool has_vacancy = online_client.Count < Client_Limit;
            if (!has_vacancy)
            {
                Log.Debug("No Vacancy!");
                return 0;
            }
            bool correct_game = ((int)event_params[(byte)1] == 2);
            if (!correct_game)
            {
                Log.Debug("Incorrect Game: "+(int)event_params[(byte)1]+"!");
                return 0;
            }

            if (!checkEnoughTimeForRound(120000))
            {
                Log.Debug("Not Enough Time: " + (int)event_params[(byte)1] + "!");
                return 0;
            }

            if (clock_mode)
            {
                Log.Debug("Now Clock Mode!");
                return 0;
            }

            return 1;
        }

        override public int InsertToSql(String[] cmd_values)
        {
            if (sql_command == null) return 0;

            game_app.checkSqlConnection();

            sql_command.Parameters.Clear();
            sql_command.Parameters.AddWithValue("@Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sql_command.Parameters.AddWithValue("@Uid", cmd_values[0]);
            sql_command.Parameters.AddWithValue("@ImagePath", cmd_values[1]);

            return sql_command.ExecuteNonQuery();
        }


        private String saveImage(String user_id, byte[] abyte)
        {
           
            String file_path = SERVER_PATH + "user_" + DateTime.Now.ToString("yyyyMMDD_HHmm")+"_" + user_id + ".png";
            

            Task task_save=Task.Factory.StartNew(() => File.WriteAllBytes(file_path, abyte));
            
            return file_path;

        }
        private bool checkImage(byte[] abyte)
        {
            MemoryStream ms = new MemoryStream(abyte);
            Image img = Image.FromStream(ms);

            Log.Debug("Check Image Size: "+img.Size.Width+" x "+img.Size.Height);

            if (img.Size.Width>= 104 && img.Size.Height>=104) return true;
            return false;
        }

        /* when client disconnect */
        override public void removeClient(STServerPeer peer_to_remove)
        {

            string sid = peer_to_remove.client_id;
           
            lock (online_client)
            {
                bool rm = online_client.Remove(peer_to_remove);
                if (rm) Log.Debug("     Remove Disconnect Client Success!!");
                else Log.Debug("     Remove Disconnect Client Fail!!");
            }

            if (sid != null)
            {
                removeIdInGame(sid);
                InsertToSql(new String[] { sid, "Disconnect" });
            }
        }
    }
}
