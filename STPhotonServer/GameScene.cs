using ExitGames.Logging;
using Photon.SocketServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;



namespace STPhotonServer
{
    class GameScene
    {

        public STGameApp game_app;

        public int GAME_SPAN;
        public int END_SPAN;
        public int Client_Limit;

        protected static readonly ILogger Log=LogManager.GetCurrentClassLogger();

        enum Game_State { Waiting, Playing, Ending };
        Game_State cur_state { get; set; }

        Timer stage_timer,ending_timer;

        public GameScene()
        {
        }
        public GameScene(STGameApp app)
        {
            game_app=app;
        }

        virtual public void handleMessage(STServerPeer sender,STClientCode code,Dictionary<byte,object> ev_params)
        {
        }

        virtual public void InitGame()
        {
            Log.Warn(">>>> Game Scene Init");
            cur_state=Game_State.Waiting;
        }
        virtual public void StartGame()
        {
            Log.Warn(">>>> Game Scene Start");
            cur_state=Game_State.Playing;
            
            setupStageTimer();

        }

        virtual public void EndGame()
        {
            Log.Warn(">>>> Game Scene End");

            if(stage_timer!=null) stage_timer.Close();
            
            cur_state=Game_State.Ending;
            prepareToEndGame();
        }

        void setupStageTimer()
        {
            if(stage_timer!=null) stage_timer.Close();
            if(ending_timer!=null) ending_timer.Close();


            stage_timer=new Timer(GAME_SPAN);
            stage_timer.Elapsed+=new ElapsedEventHandler(requestToEndGame);
            stage_timer.AutoReset=false;
            stage_timer.Enabled=true;

        }

        private void prepareToEndGame()
        {
            if(ending_timer!=null) ending_timer.Close();

            ending_timer=new Timer(END_SPAN);
            ending_timer.Elapsed+=new ElapsedEventHandler(reallyEndGame);
            ending_timer.AutoReset=false;
            ending_timer.Enabled=true;

        }

       
        private void reallyEndGame(object sender,ElapsedEventArgs e)
        {
            game_app.SendNotifyLED(STServerCode.LSend_GG,new Dictionary<byte,object>());
            game_app.goNextGame();
        }
        virtual public void requestToEndGame(object sender,ElapsedEventArgs e)
        {
            game_app.requestGameScore();
        }

       
    }
}
