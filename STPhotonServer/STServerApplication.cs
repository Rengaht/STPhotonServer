using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Photon.SocketServer;
using ExitGames.Logging;
using ExitGames.Logging.Log4Net;
using log4net;
using log4net.Config;
using LogManager = ExitGames.Logging.LogManager;
using System.IO;
using System.Timers;


namespace STPhotonServer
{
    class STServerApplication:ApplicationBase
    {
        
        protected static readonly ILogger Log=LogManager.GetCurrentClassLogger();

        List<PeerBase> lconnected_peer;
        //int message_count=0;

        public STGameApp game_app;

        protected override PeerBase CreatePeer(InitRequest initRequest)
        {
            STServerPeer new_connected=new STServerPeer(initRequest.Protocol, initRequest.PhotonPeer,game_app);
           
            //lconnected_peer.Add(new_connected);
            //Log.Debug("mpeer="+lconnected_peer.Count);
            game_app.addNewClientPeer(new_connected);


            return new_connected;
        }

        protected override void Setup()
        {
            //server init
            LogManager.SetLoggerFactory(Log4NetLoggerFactory.Instance);
            GlobalContext.Properties["LogFileName"]=ApplicationName+System.DateTime.Now.ToString("yyyy-MM-dd-hh-mm");
            XmlConfigurator.ConfigureAndWatch(new FileInfo(Path.Combine(BinaryPath,"log4net.config")));

            Log.Debug("!!! STPhotonServer Start !!!");

            lconnected_peer=new List<PeerBase>();
            game_app=new STGameApp(lconnected_peer);
            
            
            //Timer timer=new Timer(2000);
            //timer.Elapsed+=new ElapsedEventHandler(sendToAll);
            //timer.AutoReset=true;
            //timer.Enabled=true;

        }

        private void sendToAll(object sender, ElapsedEventArgs e)
        {
            Log.Debug("--- send message to all ---");
            Log.Debug("mpeer="+lconnected_peer.Count);

            foreach(STServerPeer peer in lconnected_peer)
            {
                Log.Debug("peer: "+peer.ToString());
                //peer.sendEventToPeer();
            }

        }

        protected override void TearDown()
        {
            // server close
            game_app.closeDatabase();
        }

        
    }
}
