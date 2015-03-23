using Photon.SocketServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STPhotonServer
{
    class GameA :GameScene
    {
        public GameA(STGameApp app):base(app)
        {
             GAME_SPAN=20000;
             END_SPAN=5000;
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
                    Log.Warn("Join Game A!!");

                    response_params.Add(1,game_app.createNewClientId());
                    response_params.Add(2,game_app.getClientIndex(sender));

                    sender.sendOpResponseToPeer(STServerCode.CSend_Id,response_params);
                    break;

                case STClientCode.APP_Set_Name:
                    sender.sendOpResponseToPeer(STServerCode.CSet_Name_Success,response_params);
                    game_app.SendNotifyLED(STServerCode.LSet_Name,event_params);
                    break;

                case STClientCode.APP_Blow:
                    game_app.SendNotifyLED(STServerCode.LSet_Blow,event_params);
                    break;

                case STClientCode.LED_Score:
                    sender.sendOpResponseToPeer(STServerCode.LSend_Score_Success,response_params);
                    game_app.sendGameOverToAll(event_params);

                    break;
            }
        }
    }
}
