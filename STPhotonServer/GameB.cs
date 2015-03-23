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
         public GameB(STGameApp app):base(app)
        {
            GAME_SPAN=20000;
            END_SPAN=5000;
            Client_Limit=1;
        }
        override public void handleMessage(STServerPeer sender,STClientCode code,Dictionary<byte,object> event_params)
        {
            Dictionary<byte,object> response_params=new Dictionary<byte,object>();
            switch(code)
            {
                case STClientCode.APP_Join:
                    Log.Warn("Join Game B!!");
                    
                    String user_id=game_app.createNewClientId();
                    response_params.Add(1,user_id);

                    int client_id=game_app.getClientIndex(sender);
                    response_params.Add(2,client_id);
                    
                    sender.sendOpResponseToPeer(STServerCode.CSend_Id,response_params);
                    Log.Warn("Add user for color "+client_id);
                    
                    if(client_id>-1)
                    {
                        Dictionary<byte,object> user_color_params=new Dictionary<byte,object>();
                        user_color_params.Add(100,user_id);
                        user_color_params.Add(1,client_id);
                        game_app.SendNotifyLED(STServerCode.LUser_Color,user_color_params);
                    }

                    if(client_id==Client_Limit-1)
                        readyToStart();

                    break;

                case STClientCode.APP_Rotate:
                    game_app.SendNotifyLED(STServerCode.LSet_Rotate,event_params);
                    break;


                case STClientCode.LED_Score:
                    sender.sendOpResponseToPeer(STServerCode.LSend_Score_Success,response_params);
                    game_app.sendGameOverToAll(event_params);

                    break;
            }
        }
        override public void EndGame()
        {
            base.EndGame();

        }
        private void readyToStart()
        {
            
            for(int i=0;i<Math.Min(2,game_app.getClientCount());++i)
            {
                game_app.getClientPeer(i).sendEventToPeer(STServerCode.CGameB_Start,new Dictionary<byte,object>());
            }
            game_app.SendNotifyLED(STServerCode.LGameB_Start,new Dictionary<byte,object>());

            this.StartGame();
        }
        override public void requestToEndGame(object sender,ElapsedEventArgs e)
        {

            this.EndGame();
        }
    }
}
