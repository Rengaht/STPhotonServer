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
using System.Timers;

namespace STPhotonServer
{
    public class STServerPeer:PeerBase
    {

        public STGameApp game_app;

        protected static readonly ILogger Log=LogManager.GetCurrentClassLogger();
        protected int message_count=0;

        private Boolean is_led=false;
        private readonly IFiber fiber;
        public String client_id { get; set; }
        public int client_side { get; set; }

        private Timer timer_disconnect;
        private int DELAY_SPAN = 7000;


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

            if (is_led)
            {
                Log.Warn("----------------- LED Disconnect! -----------------");
                game_app.checkLed();
                game_app.killAllPeer();
            }
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
                    //var info_param=new Dictionary<byte,object>{{1,game_app.cur_game},{2,game_app.checkAvailable()}};
                    var info_param=new Dictionary<byte,object>();
                    OperationResponse info_response=new OperationResponse((byte)STServerCode.CLogin_Success,info_param){
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
                            try
                            {
                                Log.Debug(entry.Key + " " + entry.Key.GetType() + " - " + entry.Value + " " + entry.Value.GetType());
                            }
                            catch (Exception e)
                            {
                                Log.Error(e.ToString());
                            }
                            byte kbyte;
                            try{
                                kbyte=(byte)entry.Key;
                            }catch(InvalidCastException e){
                                
                                byte[] bkey = BitConverter.GetBytes((int)entry.Key);
                                Log.Debug("Unable to cast: "+bkey);
                                kbyte = bkey[0];
                            }
                            
                            event_params.Add((byte)kbyte,entry.Value);
                        }
                    }
                    Log.Warn("--------------------------------- ");        
                    switch(event_code){
                       

                        case STClientCode.LED_Join:

                            game_app.setupLedPeer(this);

                            event_params.Add((byte)1, game_app.getCurGame());
                            OperationResponse led_connected_response=new OperationResponse((byte)STServerCode.LConnected,event_params)
                            {
                                ReturnCode=(short)0,
                                DebugMessage="server feedback"
                            };
                            this.fiber.Enqueue(()=>SendOperationResponse(led_connected_response, new SendParameters()));
                            is_led = true;
                            
                            break;
                        
                        case STClientCode.APP_Check_Id:
                           
                            
                            Dictionary<byte, Object> id_params = new Dictionary<byte, Object>();
                            id_params.Add((byte)1, game_app.getCurGame());

                            String get_id = (String)event_params[(byte)100];
                            
                            if (game_app.led_ready)
                            {
                                id_params.Add((byte)2, game_app.checkAvailable());

                                bool valid_id = game_app.getValidId(ref get_id);
                                Log.Debug("id: " + valid_id + " - " + get_id);

                                id_params.Add((byte)3, valid_id ? 1 : 0);
                                id_params.Add((byte)100, get_id);

                                id_params.Add((byte)200,game_app.getIosVersion());
                                id_params.Add((byte)201, game_app.getAndroidVersion());

                                this.client_id = get_id;
                                
                            }

                            OperationResponse id_response=new OperationResponse((byte)STServerCode.Id_And_Game_Info,id_params)
                            {
                                ReturnCode=(short)0,
                                DebugMessage="server feedback"
                            };
                            this.fiber.Enqueue(()=>SendOperationResponse(id_response, new SendParameters()));
                            
                            
                            this.client_id = get_id;

                            break;
                        case STClientCode.LED_SwitchGame:
                            int switch_to_game = (int)event_params[(byte)1];
                            game_app.switchGame(switch_to_game);

                            break;
                        default:
                            //Log.Warn("Undefined event code= "+event_code.ToString());
                           
                           if (game_app.checkLed())
                            {
                                game_app.handleMessage(this, event_code, event_params);
                            }
                            else
                            {   // if no led available, kick them off!
                                Dictionary<byte, Object> _params = new Dictionary<byte, Object>();
                                _params.Add((byte)1, game_app.getCurGame());
                                OperationResponse _response = new OperationResponse((byte)STServerCode.Id_And_Game_Info, _params)
                                {
                                    ReturnCode = (short)0,
                                    DebugMessage = "server feedback"
                                };
                                this.fiber.Enqueue(() => SendOperationResponse(_response, new SendParameters()));

                            }
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

        public void delayDisconnect()
        {
            Log.Info("Prepare to disconnect peer!");
            delayDisconnect(DELAY_SPAN);
        }

        public void delayDisconnect(float dtime)
        {
            if (timer_disconnect != null) timer_disconnect.Close();

            timer_disconnect = new Timer(dtime);
            timer_disconnect.Elapsed += new ElapsedEventHandler(doDisconnect);
            timer_disconnect.AutoReset = false;
            timer_disconnect.Enabled = true;
        }

        public void doDisconnect(object sender, ElapsedEventArgs e)
        {

            Log.Info("Disconnect peer!"+client_id);
            this.Disconnect();
            game_app.killPeer(this);
        }
    }
}
