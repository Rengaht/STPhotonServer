using ExitGames.Logging;
using Photon.SocketServer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace STPhotonServer
{
    public class STGameApp
    {


        protected static readonly ILogger Log=LogManager.GetCurrentClassLogger();

        GameScene[] agame_scene;
        public int cur_game { get; set; }
        
        
        List<PeerBase> aclientpeer;
        Boolean LED_Ready;
      


        STServerPeer led_peer;
        public STServerPeer LED_Peer {
            set { led_peer=value; }
        }
        
       

        public STGameApp(List<PeerBase> lpeer_)
        {
            aclientpeer=lpeer_;
            LED_Ready=false;

            agame_scene=new GameScene[3];
            agame_scene[0]=new GameA(this);
            agame_scene[1]=new GameB(this);
            agame_scene[2]=new GameC(this);


        }


        public void addNewClientPeer(PeerBase peer)
        {  
            aclientpeer.Add(peer);
        }
        public void removeClientPeer(PeerBase peer)
        {
            aclientpeer.Remove(peer);
        }

        private void initGame(int game_index)
        {

            Log.Debug("APP Init Game "+game_index);
            
            cur_game=game_index;
            agame_scene[cur_game].InitGame();
            

            SendNotifyLED(STServerCode.Game_Info,new Dictionary<byte,object>() { { (byte)1,cur_game } });

            EventData event_data=new EventData((byte)STServerCode.Game_Info,
                                               new Dictionary<byte,object>() { { (byte)1,cur_game}});
            foreach(STServerPeer peer in aclientpeer)
            {
                peer.sendEventToPeer(event_data);
            }


        }
       
        public void goNextGame()
        {
            initGame((cur_game+1)%3);
        }

        public void SendNotifyLED(STServerCode event_code,Dictionary<byte,Object> event_param)
        {
            if(led_peer!=null) led_peer.sendEventToPeer(new EventData((byte)event_code,event_param));
            else Log.Warn("There's no LED connected !!!");
            
        }
        public string createNewClientId()
        {
            return Guid.NewGuid().ToString();
        }



        public void setupLedPeer(STServerPeer peer)
        {
            //if(led_peer!=null) return;


            aclientpeer.Remove(peer);

            foreach(STServerPeer rest_peer in aclientpeer)
            {
                rest_peer.Disconnect();
            }
            
            this.led_peer=peer;
            LED_Ready=true;

            initGame(2);
        }
        
        public void requestGameScore()
        {
            
            SendNotifyLED(STServerCode.LRequest_Score,new Dictionary<byte,object>());

        }
        public void sendGameOverToAll(Dictionary<byte,Object> event_params)
        {
            
            agame_scene[cur_game].EndGame();

            EventData event_data=new EventData((byte)STServerCode.CSend_GG,
                                                new Dictionary<byte,object>() { { (byte)1,1 },{(byte)2,100} });
            foreach(STServerPeer peer in aclientpeer)
            {
                peer.sendEventToPeer(event_data);
            }

            
        }

        public int getClientCount()
        {
            return aclientpeer.Count;
        }

        public int getClientIndex(PeerBase peer)
        {
            int index=aclientpeer.IndexOf(peer);
            return index;

        }
        public bool checkAvailable()
        {
            return aclientpeer.Count<agame_scene[cur_game].Client_Limit;
        }

        public void handleMessage(STServerPeer sender,STClientCode code,Dictionary<byte,object> ev_params)
        {
            agame_scene[cur_game].handleMessage(sender,code,ev_params);
        }
       
      
        public STServerPeer getClientPeer(int index)
        {
            return (STServerPeer)aclientpeer[index];
        }

    }
}
