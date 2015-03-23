using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Photon.SocketServer;
using PhotonHostRuntimeInterfaces;
using ExitGames.Logging;
using System.Collections;
using ExitGames.Concurrency.Fibers;

namespace STPhotonServer
{
    public class STServerPeer:PeerBase
    {

        public STGameApp game_app;

        protected static readonly ILogger Log=LogManager.GetCurrentClassLogger();
        protected int message_count=0;


        private readonly IFiber fiber;


        public STServerPeer(IRpcProtocol rpcProtocol, IPhotonPeer nativePeer,STGameApp ga)
            : base(rpcProtocol, nativePeer)
        {
            game_app=ga;

            this.fiber=new PoolFiber();
            this.fiber.Start();


        }

        protected override void OnDisconnect(PhotonHostRuntimeInterfaces.DisconnectReason reasonCode, string reasonDetail)
        {
            game_app.removeClientPeer(this);
            
        }

        protected override void OnOperationRequest(OperationRequest operationRequest, SendParameters sendParameters)
        {   
            

            Log.Debug("!!! STPhotonServer Recv Operation: "+operationRequest.OperationCode);
            //foreach(KeyValuePair<byte,Object> keypair in operationRequest.Parameters)
            //{
            //    Log.Debug(keypair.Key.ToString()+" - "+keypair.Value.ToString());
            //}
            
            //Console.WriteLine("STPhotonServer Recv Request: " + operationRequest.OperationCode);

            switch (operationRequest.OperationCode){
                case 230: // user log in

                    Log.Warn("------------------- User LogIn !");
                    var info_param=new Dictionary<byte,object>{{1,game_app.cur_game},{2,game_app.checkAvailable()}};
                    OperationResponse info_response=new OperationResponse((byte)STServerCode.Game_Info,info_param){
                        ReturnCode=(short)0,
                        DebugMessage="server feedback"
                    };
                    this.fiber.Enqueue(()=>SendOperationResponse(info_response, new SendParameters()));
                    
                    break;

                case 253: //raise event:

                    int ievent=Convert.ToInt32(operationRequest.Parameters[244]);
                    
                    //Log.Warn("Get Event: "+ievent);
                    
                    STClientCode event_code=(STClientCode)(ievent&0xFF);
                    Log.Warn("---------Get Event: "+event_code.ToString()+"---------- ");

                    Object oparams=operationRequest.Parameters[245];
                    //Log.Warn("params type: "+oparams.GetType());

                    Hashtable eparams=(Hashtable)(operationRequest.Parameters[245]);
                    
                    Dictionary<byte,Object> event_params=new Dictionary<byte,Object>();
                    if(eparams!=null)
                    {
                        foreach(DictionaryEntry entry in eparams)
                        {
                            Log.Debug(entry.Key+" - "+entry.Value);
                            event_params.Add((byte)entry.Key,entry.Value);
                        }
                    }
                    Log.Warn("--------------------------------- ");        
                    switch(event_code){
                       

                        case STClientCode.LED_Join:
                            OperationResponse led_connected_response=new OperationResponse((byte)STServerCode.LConnected,event_params)
                            {
                                ReturnCode=(short)0,
                                DebugMessage="server feedback"
                            };
                            this.fiber.Enqueue(()=>SendOperationResponse(led_connected_response, new SendParameters()));
                            
                            game_app.setupLedPeer(this);
                            break;
                       

                        default:
                            //Log.Warn("Undefined event code= "+event_code.ToString());
                            game_app.handleMessage(this,event_code,event_params);
                            break;

                    }
                    break;
            }
        }
        public void sendEventToPeer(EventData event_data)
        {
            this.fiber.Enqueue(() => SendEvent(event_data,new SendParameters()));
            //SendResult result=SendEvent(event_data,new SendParameters());
            //Log.Debug("Send to peer: "+result.ToString());

        }
        public void sendEventToPeer(STServerCode code,Dictionary<byte,object> event_params)
        {
            EventData event_data=new EventData((byte)code,event_params);
            sendEventToPeer(event_data);

        }
        public void sendOpResponseToPeer(STServerCode code,Dictionary<byte,object> event_params){
            
            OperationResponse led_score_response=new OperationResponse((byte)code,event_params)
            {
                ReturnCode=(short)0,
                DebugMessage="server feedback"
            };
            this.fiber.Enqueue(() => SendOperationResponse(led_score_response,new SendParameters()));
                            
        }


    }
}
